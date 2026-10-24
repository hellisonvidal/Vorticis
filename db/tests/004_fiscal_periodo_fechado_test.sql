-- Testes da migração 065: fechamento do período fiscal bloqueia incluir, alterar e excluir notas (e itens) do mês fechado,
-- mas deixa marcar a contabilização e excluir a empresa em cascata.

DO $$
DECLARE
    v_emp BIGINT;
    v_doc BIGINT;
BEGIN
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ)
    VALUES ('19131243000197', 'Empresa Fechamento LTDA', '2062', '6920601', 'SP', '3550308', '2020-01-01') RETURNING id INTO v_emp;
    INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, data_emissao, data_entrada_saida, valor_total)
    VALUES (v_emp, '55', repeat('5', 44), 'E', '501', '2026-04-10', '2026-04-10', 100) RETURNING id INTO v_doc;
    INSERT INTO fiscal.documento_item (documento_id, n_item, cfop) VALUES (v_doc, 1, '1102');

    INSERT INTO fiscal.periodo_fechado (empresa_id, competencia) VALUES (v_emp, '2026-04-01');
    PERFORM pg_temp.espera_erro_dup(format('UPDATE fiscal.documento SET valor_total = 200 WHERE id = %s', v_doc), 'está fechado');
    PERFORM pg_temp.espera_erro_dup(format('DELETE FROM fiscal.documento WHERE id = %s', v_doc), 'está fechado');
    PERFORM pg_temp.espera_erro_dup(format('UPDATE fiscal.documento_item SET cfop = ''1101'' WHERE documento_id = %s', v_doc), 'está fechado');
    PERFORM pg_temp.espera_erro_dup(format('INSERT INTO fiscal.documento (empresa_id, modelo, tipo_operacao, numero, data_emissao, data_entrada_saida) '
                                           'VALUES (%s, ''55'', ''S'', ''502'', ''2026-04-15'', ''2026-04-15'')', v_emp), 'está fechado');
    PERFORM pg_temp.espera_erro_dup(format('UPDATE fiscal.documento SET data_entrada_saida = ''2026-04-20'' WHERE id = %s', v_doc), 'está fechado');
    -- outro mês continua livre; a marcação de contabilização é permitida no mês fechado
    INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, data_emissao, data_entrada_saida) VALUES (v_emp, '55', repeat('6', 44), 'S', '503', '2026-05-02', '2026-05-02');
    UPDATE fiscal.documento SET contabilizado_em = now() WHERE id = v_doc;
    -- reaberto, volta a aceitar
    DELETE FROM fiscal.periodo_fechado WHERE empresa_id = v_emp;
    UPDATE fiscal.documento SET valor_total = 150 WHERE id = v_doc;
    -- fechado de novo, a exclusão da empresa (cascata) não é bloqueada
    INSERT INTO fiscal.periodo_fechado (empresa_id, competencia) VALUES (v_emp, '2026-04-01');
    PERFORM core.fn_excluir_empresa(v_emp);
    RAISE NOTICE 'OK: período fiscal fechado bloqueia notas e itens do mês e deixa contabilizar e excluir a empresa';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
