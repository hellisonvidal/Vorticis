-- =============================================================================
-- Vorticis Fiscal — Migração 073: regras fiscais (de-para de CFOP e créditos na importação de notas)
--   * fiscal.regra_fiscal: ENTRADA = CFOP do emitente (saída: 5/6/7) -> CFOP de entrada da empresa, com condições
--     opcionais (participante, NCM ou prefixo, produto, CST/CSOSN de origem) e campos de saída opcionais (CST de entrada,
--     crédito de ICMS/IPI/PIS/COFINS, contas e centro de custo). SAÍDA = sugestão de CFOP/CST por produto/NCM/destino.
--   * fiscal.documento_item: escrituração aplicada pelas regras (CST de escrituração, créditos e marca "sem regra"
--     para revisão). Os valores do XML não mudam: quando o item não credita, os valores de crédito são zerados na leitura.
-- =============================================================================

CREATE TABLE fiscal.regra_fiscal (
    id                BIGSERIAL    PRIMARY KEY,
    empresa_id        BIGINT       NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    tipo_operacao     CHAR(1)      NOT NULL CHECK (tipo_operacao IN ('E', 'S')),
    ordem             INTEGER      NOT NULL DEFAULT 100,            -- desempate manual (menor primeiro)
    ativo             BOOLEAN      NOT NULL DEFAULT TRUE,
    descricao         VARCHAR(120),
    -- condições
    cfop_origem       CHAR(4),                                       -- entrada: CFOP do emitente (obrigatório); saída: opcional
    participante_id   BIGINT       REFERENCES core.participante (id) ON DELETE CASCADE,
    ncm               VARCHAR(8),                                    -- NCM completo ou prefixo
    produto_id        BIGINT       REFERENCES fiscal.produto (id) ON DELETE CASCADE,
    cst_origem        VARCHAR(4),                                    -- CST/CSOSN do item no XML
    destino           CHAR(1)      CHECK (destino IN ('I', 'X')),    -- saída: I = dentro do estado, X = outro estado
    -- resultado
    cfop_destino      CHAR(4),
    cst_destino       VARCHAR(4),
    credita_icms      BOOLEAN      NOT NULL DEFAULT TRUE,
    credita_ipi       BOOLEAN      NOT NULL DEFAULT TRUE,
    credita_pis       BOOLEAN      NOT NULL DEFAULT TRUE,
    credita_cofins    BOOLEAN      NOT NULL DEFAULT TRUE,
    conta_debito      VARCHAR(30),
    conta_credito     VARCHAR(30),
    centro_custo_id   BIGINT       REFERENCES contabil.centro_custo (id) ON DELETE SET NULL,
    criado_em         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ck_regra_fiscal_cfop_origem CHECK (cfop_origem IS NULL OR cfop_origem ~ '^[0-9]{4}$'),
    CONSTRAINT ck_regra_fiscal_cfop_destino CHECK (cfop_destino IS NULL OR cfop_destino ~ '^[0-9]{4}$'),
    CONSTRAINT ck_regra_fiscal_entrada CHECK (tipo_operacao <> 'E' OR (cfop_origem IS NOT NULL AND cfop_destino IS NOT NULL))
);
CREATE INDEX ix_regra_fiscal_empresa ON fiscal.regra_fiscal (empresa_id, tipo_operacao, ativo);
CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON fiscal.regra_fiscal FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();

ALTER TABLE fiscal.documento_item
    ADD COLUMN cst_icms_escrituracao VARCHAR(4),                     -- nulo = vale o CST do XML (cst_icms)
    ADD COLUMN credita_icms          BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN credita_ipi           BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN credita_pis           BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN credita_cofins        BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN sem_regra             BOOLEAN NOT NULL DEFAULT FALSE,  -- entrada importada sem regra fiscal: conferir
    ADD COLUMN regra_fiscal_id       BIGINT  REFERENCES fiscal.regra_fiscal (id) ON DELETE SET NULL;
CREATE INDEX ix_documento_item_sem_regra ON fiscal.documento_item (documento_id) WHERE sem_regra;
