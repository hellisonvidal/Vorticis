-- =============================================================================
-- 074 — Classificação das contas para a DFC e o EBITDA.
--   * contabil.classificacao_conta: classe escolhida pelo usuário para a conta (só ficam gravadas as que diferem da
--     sugestão automática ou que são itens não recorrentes). A sugestão sai do nome da conta, do COD_NAT e do plano referencial.
-- =============================================================================
CREATE TABLE contabil.classificacao_conta (
    empresa_id     BIGINT       NOT NULL REFERENCES core.empresa(id) ON DELETE CASCADE,
    conta_id       BIGINT       NOT NULL REFERENCES contabil.plano_contas(id) ON DELETE CASCADE,
    classe         VARCHAR(30)  NOT NULL,
    nao_recorrente BOOLEAN      NOT NULL DEFAULT FALSE,
    atualizado_em  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    PRIMARY KEY (empresa_id, conta_id),
    CONSTRAINT ck_classificacao_classe CHECK (classe IN ('CAIXA', 'OPERACIONAL', 'INVESTIMENTO', 'FINANCIAMENTO', 'CONTRAPARTIDAAJUSTE',
        'RECEITA', 'DEDUCAO', 'CUSTODESPESA', 'DEPRECIACAO', 'FINANCEIRO', 'IMPOSTOLUCRO'))
);
