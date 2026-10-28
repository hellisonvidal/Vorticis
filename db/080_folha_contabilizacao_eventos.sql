-- =============================================================================
-- Vorticis Folha — Migração 080: contabilização da folha por eventos
--   * folha.configuracao_empresa: modo de contabilização (RESUMIDA = um lançamento por competência, como sempre;
--     EVENTOS = um lançamento por evento contábil) e opção de detalhar por funcionário.
--   * folha.evento_contabil / folha.evento_contabil_rubrica: agrupamento de rubricas (ou códigos especiais ENC_*, PROV_*)
--     em "eventos contábeis". Rubrica fora de qualquer evento forma um evento próprio.
--   * folha.contabilizacao_evento: um lançamento por competência, tipo de cálculo e evento; desfazer estorna o lançamento.
-- =============================================================================

ALTER TABLE folha.configuracao_empresa
    ADD COLUMN contab_modo VARCHAR(10) NOT NULL DEFAULT 'RESUMIDA' CHECK (contab_modo IN ('RESUMIDA', 'EVENTOS')),
    ADD COLUMN contab_detalha_funcionario BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE folha.evento_contabil (
    id          BIGSERIAL PRIMARY KEY,
    empresa_id  BIGINT       NOT NULL REFERENCES core.empresa(id) ON DELETE CASCADE,
    codigo      VARCHAR(30)  NOT NULL,
    descricao   VARCHAR(100) NOT NULL,
    CONSTRAINT uq_folha_evento_contabil UNIQUE (empresa_id, codigo)
);

CREATE TABLE folha.evento_contabil_rubrica (
    evento_id   BIGINT       NOT NULL REFERENCES folha.evento_contabil(id) ON DELETE CASCADE,
    empresa_id  BIGINT       NOT NULL REFERENCES core.empresa(id) ON DELETE CASCADE,
    chave       VARCHAR(30)  NOT NULL,                  -- código da rubrica ou código especial da regra (ENC_*, PROV_*)
    PRIMARY KEY (evento_id, chave),
    CONSTRAINT uq_folha_evento_rubrica UNIQUE (empresa_id, chave)   -- uma rubrica soma em um único evento
);

CREATE TABLE folha.contabilizacao_evento (
    id                  BIGSERIAL PRIMARY KEY,
    empresa_id          BIGINT        NOT NULL REFERENCES core.empresa(id) ON DELETE CASCADE,
    competencia         DATE          NOT NULL,
    tipo_calculo        VARCHAR(10)   NOT NULL,          -- MENSAL | FERIAS | DECIMO | RESCISAO | PROVISAO
    evento              VARCHAR(30)   NOT NULL,          -- código do evento contábil (ou da rubrica avulsa)
    lancamento_id       BIGINT        NOT NULL,          -- contabil.lancamento (vínculo lógico)
    numero_lancamento   BIGINT,
    valor_total         NUMERIC(18,2) NOT NULL,
    criada_em           TIMESTAMPTZ   NOT NULL DEFAULT now(),
    criada_por          BIGINT        REFERENCES core.usuario(id),
    desfeita_em         TIMESTAMPTZ,
    desfeita_por        BIGINT        REFERENCES core.usuario(id),
    estorno_id          BIGINT,
    CONSTRAINT ck_folha_contab_ev_tipo CHECK (tipo_calculo IN ('MENSAL', 'FERIAS', 'DECIMO', 'RESCISAO', 'PROVISAO'))
);
CREATE UNIQUE INDEX uq_folha_contab_ev_ativa ON folha.contabilizacao_evento (empresa_id, competencia, tipo_calculo, evento) WHERE desfeita_em IS NULL;
CREATE TRIGGER trg_auditoria_contab_ev AFTER INSERT OR UPDATE OR DELETE ON folha.contabilizacao_evento
    FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
CREATE TRIGGER trg_auditoria_evento_contabil AFTER INSERT OR UPDATE OR DELETE ON folha.evento_contabil
    FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
