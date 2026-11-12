-- =====================================================================
-- Escritório: parcelas do contrato no Financeiro
--
-- * escritorio.contrato: prazo em meses (padrão 12) e renovação automática
--   (ao vencer a última parcela, o contrato gera as do prazo seguinte).
-- * escritorio.honorario_cobranca.contrato_id: a cobrança gerada pelo
--   contrato (Gerar parcelas / Estender parcelas / renovação). Excluir o
--   contrato não apaga as cobranças: o vínculo fica vazio.
-- =====================================================================

ALTER TABLE escritorio.contrato
    ADD COLUMN prazo_meses          SMALLINT NOT NULL DEFAULT 12,
    ADD COLUMN renovacao_automatica BOOLEAN  NOT NULL DEFAULT FALSE,
    ADD CONSTRAINT ck_contrato_prazo CHECK (prazo_meses BETWEEN 1 AND 120);

ALTER TABLE escritorio.honorario_cobranca
    ADD COLUMN contrato_id BIGINT REFERENCES escritorio.contrato(id) ON DELETE SET NULL;
CREATE INDEX ix_honorario_cobranca_contrato ON escritorio.honorario_cobranca (contrato_id) WHERE contrato_id IS NOT NULL;
