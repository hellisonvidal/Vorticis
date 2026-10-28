-- =============================================================================
-- Vorticis — Migração 081: importação de arquivos SPED (ECD, ECF, EFD ICMS/IPI e EFD-Contribuições)
--   * core.importacao_sped: histórico de cada importação (arquivo, período, opção, resumo e relatório) e a situação
--     (IMPORTADA ou DESFEITA); "desfazer" guarda em `desfazer` o que a importação substituiu, para devolver.
--   * core.importacao_sped_item: tudo o que a importação criou (tabela e id), para a opção "Desfazer importação".
--   * fiscal.documento.origem passa a aceitar 'SPED' (nota que veio só do arquivo SPED, sem o XML).
--   * fiscal.apuracao_icms_historico: apuração do ICMS (E110) de cada competência importada, como histórico.
--   * fiscal.sped_consolidado: operações consolidadas da EFD-Contribuições (C180/C190), sem nota por nota.
-- =============================================================================

CREATE TABLE core.importacao_sped (
    id              BIGSERIAL    PRIMARY KEY,
    empresa_id      BIGINT       NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    tipo            VARCHAR(12)  NOT NULL CHECK (tipo IN ('ECD', 'ECF', 'EFD_ICMS', 'EFD_CONTRIB')),
    modo            VARCHAR(30)  NOT NULL,
    arquivo         VARCHAR(260) NOT NULL,
    sha256          CHAR(64)     NOT NULL,
    periodo_inicio  DATE,
    periodo_fim     DATE,
    situacao        VARCHAR(10)  NOT NULL DEFAULT 'IMPORTADA' CHECK (situacao IN ('IMPORTADA', 'DESFEITA')),
    importados      INTEGER      NOT NULL DEFAULT 0,
    relatorio       JSONB        NOT NULL DEFAULT '{}'::jsonb,
    desfazer        JSONB,
    importado_em    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    importado_por   BIGINT       REFERENCES core.usuario (id) ON DELETE SET NULL,
    desfeito_em     TIMESTAMPTZ,
    desfeito_por    BIGINT       REFERENCES core.usuario (id) ON DELETE SET NULL
);
CREATE INDEX ix_importacao_sped_empresa ON core.importacao_sped (empresa_id, tipo, importado_em DESC);

CREATE TABLE core.importacao_sped_item (
    importacao_id  BIGINT       NOT NULL REFERENCES core.importacao_sped (id) ON DELETE CASCADE,
    tabela         VARCHAR(40)  NOT NULL,
    registro_id    BIGINT       NOT NULL,
    PRIMARY KEY (importacao_id, tabela, registro_id)
);

ALTER TABLE fiscal.documento DROP CONSTRAINT IF EXISTS documento_origem_check;
ALTER TABLE fiscal.documento ADD CONSTRAINT documento_origem_check CHECK (origem IN ('XML', 'DIGITADO', 'SPED'));
CREATE UNIQUE INDEX uq_documento_sped
    ON fiscal.documento (empresa_id, tipo_operacao, modelo, serie, numero, COALESCE(participante_id, 0))
 WHERE origem = 'SPED' AND chave_acesso IS NULL;

CREATE TABLE fiscal.apuracao_icms_historico (
    id                      BIGSERIAL     PRIMARY KEY,
    empresa_id              BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    competencia             DATE          NOT NULL CHECK (EXTRACT(DAY FROM competencia) = 1),
    origem                  VARCHAR(10)   NOT NULL DEFAULT 'SPED',
    vl_tot_debitos          NUMERIC(15,2) NOT NULL DEFAULT 0,     -- E110.VL_TOT_DEBITOS
    vl_aj_debitos           NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_tot_aj_debitos       NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_estornos_cred        NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_tot_creditos         NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_aj_creditos          NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_tot_aj_creditos      NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_estornos_deb         NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_sld_credor_ant       NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_sld_apurado          NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_tot_ded              NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_icms_recolher        NUMERIC(15,2) NOT NULL DEFAULT 0,
    vl_sld_credor_transp    NUMERIC(15,2) NOT NULL DEFAULT 0,
    deb_esp                 NUMERIC(15,2) NOT NULL DEFAULT 0,
    criado_em               TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT uq_apuracao_icms_historico UNIQUE (empresa_id, competencia)
);

CREATE TABLE fiscal.sped_consolidado (
    id           BIGSERIAL     PRIMARY KEY,
    empresa_id   BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    competencia  DATE          NOT NULL CHECK (EXTRACT(DAY FROM competencia) = 1),
    registro     VARCHAR(4)    NOT NULL,                          -- C190
    modelo       VARCHAR(2),
    cfop         VARCHAR(4),
    ncm          VARCHAR(8),
    cst_pis      VARCHAR(2),
    cst_cofins   VARCHAR(2),
    valor        NUMERIC(15,2) NOT NULL DEFAULT 0,
    bc_pis       NUMERIC(15,2) NOT NULL DEFAULT 0,
    aliq_pis     NUMERIC(9,4)  NOT NULL DEFAULT 0,
    vl_pis       NUMERIC(15,2) NOT NULL DEFAULT 0,
    bc_cofins    NUMERIC(15,2) NOT NULL DEFAULT 0,
    aliq_cofins  NUMERIC(9,4)  NOT NULL DEFAULT 0,
    vl_cofins    NUMERIC(15,2) NOT NULL DEFAULT 0
);
CREATE INDEX ix_sped_consolidado ON fiscal.sped_consolidado (empresa_id, competencia);
