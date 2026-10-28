-- =============================================================================
-- Vorticis Fiscal — Migração 078: conhecimento de transporte eletrônico (CT-e, modelo 57)
--   * fiscal.documento passa a aceitar o modelo '57'. Emitente = transportadora; no destinatário fica o
--     tomador do serviço; o CFOP, o ICMS e o valor da prestação ficam no único item (fiscal.documento_item).
--     É entrada para o tomador e saída para o emitente; o XML original é guardado como nas demais notas.
--   * fiscal.documento_transporte: dados próprios do CT-e (partes, percurso, tipo, valor a receber e as
--     chaves das NF-e transportadas), usados na consulta de CT-e.
-- =============================================================================

ALTER TABLE fiscal.documento DROP CONSTRAINT IF EXISTS documento_modelo_check;
ALTER TABLE fiscal.documento ADD CONSTRAINT documento_modelo_check CHECK (modelo IN ('55', '65', 'NFSE', '57'));

CREATE TABLE fiscal.documento_transporte (
    documento_id           BIGINT        PRIMARY KEY REFERENCES fiscal.documento (id) ON DELETE CASCADE,
    tomador_documento      VARCHAR(14),
    tomador_nome           VARCHAR(150),
    tomador_uf             CHAR(2),
    remetente_documento    VARCHAR(14),
    remetente_nome         VARCHAR(150),
    remetente_uf           CHAR(2),
    destinatario_documento VARCHAR(14),
    destinatario_nome      VARCHAR(150),
    destinatario_uf        CHAR(2),
    modal                  VARCHAR(2),                              -- 01 rodoviário, 02 aéreo, 03 aquaviário, 04 ferroviário, 05 dutoviário, 06 multimodal
    tipo_cte               SMALLINT      NOT NULL DEFAULT 0,        -- 0 normal, 1 complemento, 2 anulação, 3 substituto
    municipio_inicio       VARCHAR(60),
    uf_inicio              CHAR(2),
    cod_municipio_inicio   VARCHAR(7),
    municipio_fim          VARCHAR(60),
    uf_fim                 CHAR(2),
    cod_municipio_fim      VARCHAR(7),
    valor_receber          NUMERIC(15,2) NOT NULL DEFAULT 0,
    chaves_nfe             TEXT[]        NOT NULL DEFAULT '{}'
);
