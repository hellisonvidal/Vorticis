-- 093: nome do plano de contas da empresa e recodificação dos códigos de conta guardados como texto.
-- Ao converter o plano para outra máscara, os códigos mudam; as telas de configuração (contas de encerramento, regras de
-- contabilização, duplicatas, bancos, inventário, folha, frota) guardam o código como texto e precisam acompanhar.

ALTER TABLE core.empresa ADD COLUMN IF NOT EXISTS plano_nome VARCHAR(80);
COMMENT ON COLUMN core.empresa.plano_nome IS 'Nome dado ao plano de contas criado pelo usuário (null = modelo embutido ou plano importado)';

-- Contas "título" (sintéticas do último nível) sob as quais nascem, automaticamente, as contas analíticas dos clientes e
-- dos fornecedores cadastrados com a opção "Criar conta no plano de contas".
ALTER TABLE core.empresa
    ADD COLUMN IF NOT EXISTS conta_titulo_clientes_id    BIGINT REFERENCES contabil.plano_contas (id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS conta_titulo_fornecedores_id BIGINT REFERENCES contabil.plano_contas (id) ON DELETE SET NULL;

-- Troca, nas colunas de texto "conta_*" de cada tabela com empresa_id (e nas contas de encerramento da própria empresa),
-- o código antigo pelo novo. Devolve quantas linhas mudaram. Os dois vetores têm o mesmo tamanho (antigo[i] -> novo[i]).
CREATE OR REPLACE FUNCTION contabil.fn_recodificar_referencias(p_empresa BIGINT, p_antigos TEXT[], p_novos TEXT[])
RETURNS INTEGER LANGUAGE plpgsql AS $$
DECLARE
    v_total INTEGER := 0;
    v_n     INTEGER;
    r       RECORD;
BEGIN
    IF coalesce(array_length(p_antigos, 1), 0) <> coalesce(array_length(p_novos, 1), 0) THEN
        RAISE EXCEPTION 'Listas de códigos com tamanhos diferentes';
    END IF;
    IF coalesce(array_length(p_antigos, 1), 0) = 0 THEN RETURN 0; END IF;

    UPDATE core.empresa e SET
        conta_apuracao  = coalesce((SELECT n.novo FROM unnest(p_antigos, p_novos) n(antigo, novo) WHERE n.antigo = e.conta_apuracao), e.conta_apuracao),
        conta_lucros    = coalesce((SELECT n.novo FROM unnest(p_antigos, p_novos) n(antigo, novo) WHERE n.antigo = e.conta_lucros), e.conta_lucros),
        conta_prejuizos = coalesce((SELECT n.novo FROM unnest(p_antigos, p_novos) n(antigo, novo) WHERE n.antigo = e.conta_prejuizos), e.conta_prejuizos)
     WHERE e.id = p_empresa;

    FOR r IN
        SELECT c.table_schema AS esquema, c.table_name AS tabela, c.column_name AS coluna
          FROM information_schema.columns c
          JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
         WHERE c.column_name LIKE 'conta\_%' AND c.data_type IN ('character varying', 'text')
           AND c.table_schema IN ('core', 'contabil', 'fiscal', 'folha', 'frota', 'imobilizado', 'erp', 'escritorio', 'condominio')
           AND EXISTS (SELECT 1 FROM information_schema.columns e
                        WHERE e.table_schema = c.table_schema AND e.table_name = c.table_name AND e.column_name = 'empresa_id')
    LOOP
        EXECUTE format(
            'UPDATE %1$I.%2$I t SET %3$I = n.novo FROM unnest($2, $3) n(antigo, novo) WHERE t.empresa_id = $1 AND t.%3$I = n.antigo AND n.antigo <> n.novo',
            r.esquema, r.tabela, r.coluna) USING p_empresa, p_antigos, p_novos;
        GET DIAGNOSTICS v_n = ROW_COUNT;
        v_total := v_total + v_n;
    END LOOP;
    RETURN v_total;
END $$;
