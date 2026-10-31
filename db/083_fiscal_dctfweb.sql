-- Fiscal › Obrigações › DCTFWeb (MIT): a declaração do mês só é preparada com a apuração encerrada (período fiscal fechado em
-- Apuração › Fechamento do período fiscal). Guarda os débitos declarados (por código de receita), quem preparou e, depois de
-- transmitida na DCTFWeb (e-CAC), o número do recibo. Mês com declaração transmitida não pode ser reaberto sem desfazer o registro.

CREATE TABLE fiscal.dctfweb (
    id             BIGSERIAL PRIMARY KEY,
    empresa_id     BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    competencia    DATE          NOT NULL CHECK (competencia = date_trunc('month', competencia)::date),
    situacao       VARCHAR(12)   NOT NULL DEFAULT 'PREPARADA' CHECK (situacao IN ('PREPARADA', 'TRANSMITIDA')),
    debitos        JSONB         NOT NULL,
    total          NUMERIC(15,2) NOT NULL CHECK (total >= 0),
    preparada_em   TIMESTAMPTZ   NOT NULL DEFAULT now(),
    preparada_por  BIGINT        REFERENCES core.usuario (id) ON DELETE SET NULL,
    recibo         VARCHAR(40),
    transmitida_em DATE,
    CONSTRAINT uq_dctfweb UNIQUE (empresa_id, competencia),
    CONSTRAINT ck_dctfweb_transmissao CHECK ((situacao = 'TRANSMITIDA') = (recibo IS NOT NULL AND btrim(recibo) <> '' AND transmitida_em IS NOT NULL))
);

-- Só com a apuração do mês encerrada.
CREATE FUNCTION fiscal.fn_dctfweb_mes_fechado() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT fiscal.fn_mes_fechado(NEW.empresa_id, NEW.competencia) THEN
        RAISE EXCEPTION 'A apuração de % não está encerrada: feche o mês em Apuração › Fechamento do período fiscal antes de preparar a DCTFWeb',
            to_char(NEW.competencia, 'MM/YYYY') USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_dctfweb_mes_fechado BEFORE INSERT OR UPDATE OF debitos, total, competencia ON fiscal.dctfweb
    FOR EACH ROW EXECUTE FUNCTION fiscal.fn_dctfweb_mes_fechado();

-- Mês com DCTFWeb transmitida não é reaberto (a reabertura pede retificação: desfaça o registro da transmissão antes).
CREATE FUNCTION fiscal.fn_periodo_com_dctfweb() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF core.excluindo_empresa(OLD.empresa_id) THEN RETURN OLD; END IF;
    IF EXISTS (SELECT 1 FROM fiscal.dctfweb d WHERE d.empresa_id = OLD.empresa_id AND d.competencia = OLD.competencia AND d.situacao = 'TRANSMITIDA') THEN
        RAISE EXCEPTION 'A DCTFWeb de % já foi transmitida: desfaça o registro da transmissão em Obrigações › DCTFWeb antes de reabrir o mês (e transmita a retificadora depois)',
            to_char(OLD.competencia, 'MM/YYYY') USING ERRCODE = 'check_violation';
    END IF;
    RETURN OLD;
END $$;
CREATE TRIGGER trg_periodo_com_dctfweb BEFORE DELETE ON fiscal.periodo_fechado FOR EACH ROW EXECUTE FUNCTION fiscal.fn_periodo_com_dctfweb();

CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON fiscal.dctfweb FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
