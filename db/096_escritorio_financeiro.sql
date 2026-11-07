-- =====================================================================
-- Escritório: honorário do cliente com periodicidade e menu Financeiro
--
-- * honorario_contrato (aba Contrato do cliente): periodicidade MENSAL
--   (cobrança todo mês), ANUAL (uma vez por ano, no mês do início) ou
--   ESPECIAL (sem cobrança automática: lançada no Financeiro).
-- * honorario_cobranca (Financeiro › Honorários): tipo HONORARIO (do
--   contrato ou lançado com parcelas) ou AVULSA (serviço extra), com
--   descrição, parcela n de N e observação da baixa. Só uma cobrança de
--   honorário (não cancelada) por cliente e competência; as avulsas
--   podem ser várias no mesmo mês.
-- =====================================================================

ALTER TABLE escritorio.honorario_contrato
    ADD COLUMN periodicidade VARCHAR(8) NOT NULL DEFAULT 'MENSAL',
    ADD CONSTRAINT ck_honorario_periodicidade CHECK (periodicidade IN ('MENSAL', 'ANUAL', 'ESPECIAL'));

ALTER TABLE escritorio.honorario_cobranca
    ADD COLUMN tipo             VARCHAR(9)   NOT NULL DEFAULT 'HONORARIO',
    ADD COLUMN descricao        VARCHAR(200),
    ADD COLUMN parcela          SMALLINT,
    ADD COLUMN parcelas         SMALLINT,
    ADD COLUMN observacao_baixa VARCHAR(200),
    ADD CONSTRAINT ck_honorario_cob_tipo CHECK (tipo IN ('HONORARIO', 'AVULSA')),
    ADD CONSTRAINT ck_honorario_cob_parcela CHECK (parcela IS NULL OR (parcela >= 1 AND parcelas >= parcela));

ALTER TABLE escritorio.honorario_cobranca DROP CONSTRAINT uq_honorario_cobranca_pessoa;
CREATE UNIQUE INDEX uq_honorario_cobranca_pessoa ON escritorio.honorario_cobranca (pessoa_id, competencia)
    WHERE tipo = 'HONORARIO' AND situacao <> 'CANCELADA';
CREATE INDEX ix_honorario_cobranca_venc ON escritorio.honorario_cobranca (empresa_id, vencimento);
