-- Testes da migração 073: regras fiscais (entrada exige CFOP de origem e de destino) e escrituração dos itens.

DO $$
DECLARE
    v_emp BIGINT;
    v_doc BIGINT;
    v_regra BIGINT;
BEGIN
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ)
    VALUES ('19131243000278', 'Empresa Regras LTDA', '2062', '6920601', 'SP', '3550308', '2020-01-01') RETURNING id INTO v_emp;

    INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, cfop_origem, cfop_destino) VALUES (v_emp, 'E', '5102', '1102') RETURNING id INTO v_regra;
    PERFORM pg_temp.espera_erro_dup(format('INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, cfop_origem) VALUES (%s, ''E'', ''5102'')', v_emp), 'ck_regra_fiscal_entrada');
    PERFORM pg_temp.espera_erro_dup(format('INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, cfop_origem, cfop_destino) VALUES (%s, ''E'', ''51A2'', ''1102'')', v_emp), 'ck_regra_fiscal_cfop_origem');
    INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, destino, cst_destino) VALUES (v_emp, 'S', 'X', '00');

    INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, data_emissao, data_entrada_saida, valor_total)
    VALUES (v_emp, '55', repeat('7', 44), 'E', '701', '2026-04-10', '2026-04-10', 100) RETURNING id INTO v_doc;
    INSERT INTO fiscal.documento_item (documento_id, n_item, cfop, regra_fiscal_id, credita_pis) VALUES (v_doc, 1, '5102', v_regra, FALSE);
    IF NOT (SELECT credita_icms AND NOT credita_pis AND NOT sem_regra FROM fiscal.documento_item WHERE documento_id = v_doc) THEN
        RAISE EXCEPTION 'Padrões da escrituração do item incorretos';
    END IF;
    DELETE FROM fiscal.regra_fiscal WHERE id = v_regra;
    IF (SELECT regra_fiscal_id FROM fiscal.documento_item WHERE documento_id = v_doc) IS NOT NULL THEN
        RAISE EXCEPTION 'Excluir a regra deveria soltar o vínculo do item';
    END IF;
    PERFORM core.fn_excluir_empresa(v_emp);
END $$;

\echo 'TODOS OS TESTES PASSARAM'
