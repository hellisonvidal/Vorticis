-- =====================================================================
-- Escritório: juros e multa opcionais na baixa de honorários e
-- descrição padrão "Honorários" nas parcelas geradas pelo contrato
-- =====================================================================

ALTER TABLE escritorio.honorario_cobranca
    ADD COLUMN juros NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN multa NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD CONSTRAINT ck_honorario_cob_encargos
        CHECK (juros >= 0 AND multa >= 0 AND (situacao = 'RECEBIDA' OR (juros = 0 AND multa = 0)));

UPDATE escritorio.honorario_cobranca
   SET descricao = 'Honorários'
 WHERE contrato_id IS NOT NULL
   AND (descricao IS NULL OR descricao LIKE 'Contrato%');
