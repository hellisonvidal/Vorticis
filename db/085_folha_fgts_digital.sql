-- Folha › Obrigações › FGTS Digital: demonstrativo do FGTS da competência (dos cálculos da folha) e registro da guia (GFD) emitida no
-- portal do FGTS Digital e do pagamento. Só com a competência da folha fechada. Vencimento: dia 20 do mês seguinte, antecipado para
-- o dia útil anterior (parâmetro legal FOLHA_FGTS_DIGITAL).

INSERT INTO core.parametro_legal (chave, vigencia_inicio, vigencia_fim, valor, fundamento_legal) VALUES
('FOLHA_FGTS_DIGITAL', '2024-03-01', NULL, '{"diaVencimento":20,"antecipaParaDiaUtil":true}',
 'Lei 8.036/1990, art. 15 (redação da Lei 14.438/2022): recolhimento até o dia 20 do mês seguinte, antecipado se não houver expediente bancário; FGTS Digital desde a competência 03/2024')
ON CONFLICT (chave, vigencia_inicio) DO NOTHING;

CREATE TABLE folha.fgts_guia (
    empresa_id     BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    competencia    DATE          NOT NULL CHECK (competencia = date_trunc('month', competencia)::date),
    identificador  VARCHAR(40)   NOT NULL CHECK (btrim(identificador) <> ''),
    valor          NUMERIC(18,2) NOT NULL CHECK (valor > 0),
    vencimento     DATE          NOT NULL,
    emitida_em     TIMESTAMPTZ   NOT NULL DEFAULT now(),
    emitida_por    BIGINT        REFERENCES core.usuario (id) ON DELETE SET NULL,
    pago_em        DATE,
    valor_pago     NUMERIC(18,2) CHECK (valor_pago > 0),
    PRIMARY KEY (empresa_id, competencia),
    CONSTRAINT ck_fgts_pagamento CHECK ((pago_em IS NULL) = (valor_pago IS NULL))
);

-- Guia só com a folha da competência fechada.
CREATE FUNCTION folha.fn_fgts_guia_competencia_fechada() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM folha.competencia c WHERE c.empresa_id = NEW.empresa_id AND c.competencia = NEW.competencia AND c.status = 'FECHADA') THEN
        RAISE EXCEPTION 'A folha de % não está fechada: feche a competência antes de registrar a guia do FGTS Digital', to_char(NEW.competencia, 'MM/YYYY')
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_fgts_guia_competencia BEFORE INSERT OR UPDATE OF valor, competencia ON folha.fgts_guia
    FOR EACH ROW EXECUTE FUNCTION folha.fn_fgts_guia_competencia_fechada();

CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON folha.fgts_guia FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
