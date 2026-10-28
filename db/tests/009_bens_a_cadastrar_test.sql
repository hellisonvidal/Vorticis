-- Testes da migração 076: bens a cadastrar das notas de entrada (imobilizado).

DO $$
DECLARE
    v_emp BIGINT;
    v_doc BIGINT;
    v_doc2 BIGINT;
    v_prod BIGINT;
    v_grupo BIGINT;
    v_conta BIGINT;
    v_bem BIGINT;
BEGIN
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ)
    VALUES ('19131243000359', 'Empresa Bens LTDA', '2062', '6920601', 'SP', '3550308', '2020-01-01') RETURNING id INTO v_emp;

    INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, data_emissao, data_entrada_saida, valor_total)
    VALUES (v_emp, '55', repeat('8', 44), 'E', '801', '2026-04-10', '2026-04-12', 5000) RETURNING id INTO v_doc;
    INSERT INTO fiscal.documento_item (documento_id, n_item, descricao, cfop, valor_produto, valor_contabil) VALUES (v_doc, 1, 'Notebook', '1551', 4000, 4000);
    INSERT INTO fiscal.documento_item (documento_id, n_item, descricao, cfop, valor_produto, valor_contabil) VALUES (v_doc, 2, 'Papel A4', '1102', 100, 100);
    IF (SELECT count(*) FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc) <> 1 THEN RAISE EXCEPTION 'só o item de CFOP 1551 deveria virar pendência'; END IF;
    IF NOT EXISTS (SELECT 1 FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc AND n_item = 1 AND valor = 4000 AND motivo = 'CFOP'
                   AND data_aquisicao = '2026-04-10' AND data_inicio_uso = '2026-04-12' AND numero_nota = '801' AND situacao = 'PENDENTE')
    THEN RAISE EXCEPTION 'pendência do item 1 com dados errados'; END IF;

    -- regravar o item não duplica e atualiza a pendência aberta
    UPDATE fiscal.documento_item SET cfop = '2551' WHERE documento_id = v_doc AND n_item = 1;
    IF (SELECT count(*) FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc) <> 1 THEN RAISE EXCEPTION 'reprocessar duplicou a pendência'; END IF;

    -- produto com tipo do item 08 (ativo imobilizado)
    INSERT INTO fiscal.produto (empresa_id, codigo, descricao, unidade, tipo_item) VALUES (v_emp, 1, 'Servidor', 'UN', '08') RETURNING id INTO v_prod;
    INSERT INTO fiscal.documento_item (documento_id, n_item, descricao, cfop, produto_id, valor_produto, valor_contabil) VALUES (v_doc, 3, 'Servidor', '1102', v_prod, 900, 900);
    IF (SELECT motivo FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc AND n_item = 3) <> 'TIPO_ITEM' THEN RAISE EXCEPTION 'tipo do item 08 não detectado'; END IF;

    -- saída nunca gera pendência
    INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, data_emissao, data_entrada_saida, valor_total)
    VALUES (v_emp, '55', repeat('9', 44), 'S', '802', '2026-04-10', '2026-04-10', 10) RETURNING id INTO v_doc2;
    INSERT INTO fiscal.documento_item (documento_id, n_item, cfop, valor_produto) VALUES (v_doc2, 1, '5551', 10);
    IF EXISTS (SELECT 1 FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc2) THEN RAISE EXCEPTION 'nota de saída gerou pendência'; END IF;

    -- bem cadastrado e depois excluído volta a ser pendência
    SELECT id INTO v_grupo FROM imobilizado.grupo ORDER BY codigo LIMIT 1;
    INSERT INTO contabil.plano_contas (empresa_id, codigo, descricao, tipo, nivel, conta_pai_id, cod_nat, natureza_saldo, data_inclusao)
    VALUES (v_emp, '1', 'ATIVO', 'S', 1, NULL, '01', 'D', '2020-01-01') RETURNING id INTO v_conta;
    INSERT INTO imobilizado.bem (empresa_id, codigo, descricao, grupo_id, data_aquisicao, data_inicio_uso, valor_aquisicao, vida_util_meses, conta_bem_id)
    VALUES (v_emp, 1, 'Notebook', v_grupo, '2026-04-10', '2026-04-12', 4000, 60, v_conta) RETURNING id INTO v_bem;
    UPDATE imobilizado.bem_a_cadastrar SET situacao = 'CADASTRADO', bem_id = v_bem WHERE documento_id = v_doc AND n_item = 1;
    UPDATE fiscal.documento_item SET cfop = '1551' WHERE documento_id = v_doc AND n_item = 1;   -- reprocessar: cadastrado continua cadastrado
    IF (SELECT situacao FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc AND n_item = 1) <> 'CADASTRADO' THEN RAISE EXCEPTION 'reprocessar reabriu item cadastrado'; END IF;
    DELETE FROM imobilizado.bem WHERE id = v_bem;
    IF (SELECT situacao FROM imobilizado.bem_a_cadastrar WHERE documento_id = v_doc AND n_item = 1) <> 'PENDENTE' THEN RAISE EXCEPTION 'excluir o bem deveria reabrir a pendência'; END IF;

    PERFORM pg_temp.espera_erro_dup(format('UPDATE imobilizado.bem_a_cadastrar SET situacao = ''X'' WHERE empresa_id = %s', v_emp), 'ck_bem_a_cadastrar_situacao');

    PERFORM core.fn_excluir_empresa(v_emp);
    IF EXISTS (SELECT 1 FROM imobilizado.bem_a_cadastrar WHERE empresa_id = v_emp) THEN RAISE EXCEPTION 'pendências ficaram após excluir a empresa'; END IF;
    RAISE NOTICE 'OK: bens a cadastrar das notas de entrada';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
