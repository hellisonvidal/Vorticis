-- =============================================================================
-- Vorticis Fiscal — Migração 077: regras de contabilização por sentido (abas Entradas e Saídas)
--   A tela passa a ter uma aba para cada sentido e a prioridade deixa de ser digitada: a mais específica
--   vence (participante > CFOP completo > prefixo) e, no empate, vale a ordem da lista (coluna prioridade,
--   mantida e renumerada de 10 em 10). Regras antigas "qualquer sentido" são desdobradas em uma de entrada
--   e uma de saída, preservando contas, histórico e situação.
-- =============================================================================

INSERT INTO fiscal.regra_contabilizacao (empresa_id, prioridade, tipo_operacao, cfop, participante_id, conta_debito_id, conta_credito_id, historico, ativo)
SELECT empresa_id, prioridade, 'S', cfop, participante_id, conta_debito_id, conta_credito_id, historico, ativo
  FROM fiscal.regra_contabilizacao WHERE tipo_operacao IS NULL;

UPDATE fiscal.regra_contabilizacao SET tipo_operacao = 'E' WHERE tipo_operacao IS NULL;

UPDATE fiscal.regra_contabilizacao r SET prioridade = n.ordem * 10
  FROM (SELECT id, ROW_NUMBER() OVER (PARTITION BY empresa_id, tipo_operacao ORDER BY prioridade, id) AS ordem
          FROM fiscal.regra_contabilizacao) n
 WHERE r.id = n.id;
