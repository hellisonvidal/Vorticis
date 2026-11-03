-- =============================================================================
-- Vorticis — Migração 095: importação de eventos do eSocial (Folha › Integrações › Importar eSocial)
--   * core.importacao_sped passa a aceitar o tipo 'ESOCIAL': o histórico (arquivos, período, resumo, relatório) e a opção
--     "Desfazer importação" usam a mesma estrutura das importações SPED (core.importacao_sped e core.importacao_sped_item).
--   * Nada muda nas tabelas da folha: funcionários, rubricas, cálculos (histórico de holerites), férias e afastamentos
--     importados entram nas mesmas tabelas da folha e ficam registrados em core.importacao_sped_item.
-- =============================================================================

ALTER TABLE core.importacao_sped DROP CONSTRAINT IF EXISTS importacao_sped_tipo_check;
ALTER TABLE core.importacao_sped ADD CONSTRAINT importacao_sped_tipo_check CHECK (tipo IN ('ECD', 'ECF', 'EFD_ICMS', 'EFD_CONTRIB', 'ESOCIAL'));
