-- Testes da migração 093: a recodificação troca o código das contas guardado como texto nas configurações da empresa,
-- só da empresa informada e só onde o valor é exatamente o código antigo.

DO $$
DECLARE
    v_a BIGINT;
    v_b BIGINT;
    v_n INTEGER;
BEGIN
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ, conta_apuracao, conta_lucros, plano_nome)
    VALUES ('45723174000110', 'Recod A', '2062', '4711302', 'SP', '3550308', '2020-01-01', '3.1.1', '2.3.1', 'Plano da casa') RETURNING id INTO v_a;
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ, conta_apuracao)
    VALUES ('00360305000104', 'Recod B', '2062', '4711302', 'SP', '3550308', '2020-01-01', '3.1.1') RETURNING id INTO v_b;
    INSERT INTO fiscal.configuracao (empresa_id, conta_clientes, conta_fornecedores) VALUES (v_a, '1.1.1', '3.1.1');

    v_n := contabil.fn_recodificar_referencias(v_a, ARRAY['3.1.1', '1.1.1', '2.3.1'], ARRAY['3.01.001', '1.01.001', '2.03.001']);
    IF (SELECT conta_apuracao FROM core.empresa WHERE id = v_a) <> '3.01.001' THEN RAISE EXCEPTION 'conta de apuração não foi recodificada'; END IF;
    IF (SELECT conta_lucros FROM core.empresa WHERE id = v_a) <> '2.03.001' THEN RAISE EXCEPTION 'conta de lucros não foi recodificada'; END IF;
    IF (SELECT conta_apuracao FROM core.empresa WHERE id = v_b) <> '3.1.1' THEN RAISE EXCEPTION 'outra empresa não pode ser alterada'; END IF;
    IF (SELECT conta_prejuizos FROM core.empresa WHERE id = v_a) IS NOT NULL THEN RAISE EXCEPTION 'conta vazia deve continuar vazia'; END IF;
    IF NOT EXISTS (SELECT 1 FROM fiscal.configuracao WHERE empresa_id = v_a AND conta_clientes = '1.01.001' AND conta_fornecedores = '3.01.001') THEN
        RAISE EXCEPTION 'as contas da configuração fiscal não foram recodificadas';
    END IF;
    -- troca cruzada (a <-> b) não pode se perder no meio
    UPDATE core.empresa SET conta_apuracao = '1.1', conta_lucros = '1.01' WHERE id = v_a;
    PERFORM contabil.fn_recodificar_referencias(v_a, ARRAY['1.1', '1.01'], ARRAY['1.01', '1.1']);
    IF NOT EXISTS (SELECT 1 FROM core.empresa WHERE id = v_a AND conta_apuracao = '1.01' AND conta_lucros = '1.1') THEN RAISE EXCEPTION 'troca cruzada falhou'; END IF;
    BEGIN
        PERFORM contabil.fn_recodificar_referencias(v_a, ARRAY['1'], ARRAY['1', '2']);
        RAISE EXCEPTION 'listas diferentes deveriam falhar';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE 'Listas de códigos%' THEN RAISE; END IF;
    END;
    DELETE FROM fiscal.configuracao WHERE empresa_id IN (v_a, v_b);
    DELETE FROM core.empresa WHERE id IN (v_a, v_b);
    RAISE NOTICE 'OK: recodificação das referências de contas';
END $$;
