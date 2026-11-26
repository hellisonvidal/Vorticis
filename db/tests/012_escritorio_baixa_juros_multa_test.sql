-- Migração 103: juros e multa opcionais na baixa de honorários do Escritório.
DO $$
DECLARE
    v_emp BIGINT;
    v_pes BIGINT;
    v_cob BIGINT;
    falhou BOOLEAN;
BEGIN
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ,
                              modulo_contabil, modulo_fiscal, modulo_folha, modulo_escritorio, escritorio_proprio)
    VALUES ('52938461000103', 'Escritório Teste Baixa LTDA', '2062', '6920601', 'SP', '3550308', DATE '2020-01-01',
            FALSE, FALSE, FALSE, TRUE, TRUE) RETURNING id INTO v_emp;

    INSERT INTO escritorio.pessoa (empresa_id, codigo, nome, documento)
    VALUES (v_emp, 2, 'Cliente Teste Encargos', '11444777000161') RETURNING id INTO v_pes;

    INSERT INTO escritorio.honorario_cobranca (empresa_id, pessoa_id, competencia, valor, vencimento, descricao)
    VALUES (v_emp, v_pes, DATE '2027-03-01', 1000.00, DATE '2027-04-10', 'Honorários') RETURNING id INTO v_cob;

    -- cobrança em aberto não pode ter juros nem multa
    falhou := FALSE;
    BEGIN
        UPDATE escritorio.honorario_cobranca SET juros = 10.00 WHERE id = v_cob;
    EXCEPTION WHEN check_violation THEN falhou := TRUE;
    END;
    IF NOT falhou THEN RAISE EXCEPTION 'juros em cobrança aberta deve ser recusado'; END IF;

    -- baixa com juros e multa válidos
    UPDATE escritorio.honorario_cobranca
       SET situacao = 'RECEBIDA', recebido_em = DATE '2027-04-20', valor_recebido = 1035.00, juros = 15.00, multa = 20.00
     WHERE id = v_cob;

    -- juros negativos recusados
    falhou := FALSE;
    BEGIN
        UPDATE escritorio.honorario_cobranca SET juros = -1.00 WHERE id = v_cob;
    EXCEPTION WHEN check_violation THEN falhou := TRUE;
    END;
    IF NOT falhou THEN RAISE EXCEPTION 'juros negativo deve ser recusado'; END IF;

    DELETE FROM escritorio.honorario_cobranca WHERE empresa_id = v_emp;
    DELETE FROM escritorio.pessoa WHERE empresa_id = v_emp;
    DELETE FROM core.empresa WHERE id = v_emp;
    RAISE NOTICE 'OK 012_escritorio_baixa_juros_multa';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
