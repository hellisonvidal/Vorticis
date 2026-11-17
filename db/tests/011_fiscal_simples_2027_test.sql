-- Migração 102: Simples Nacional a partir de 2027 (opção do IBS/CBS por semestre, repartição com CBS/IBS e janelas em parâmetros legais).
DO $$
DECLARE v JSONB; e BIGINT; n INT; falhou BOOLEAN;
BEGIN
    SELECT valor INTO v FROM core.parametro_legal WHERE chave = 'SIMPLES_NACIONAL' AND vigencia_inicio = DATE '2027-01-01';
    IF v IS NULL THEN RAISE EXCEPTION 'SIMPLES_NACIONAL 2027 não semeado'; END IF;
    IF NOT (v->'anexos'->'I'->0->'reparticao' ? 'CBS') OR NOT (v->'anexos'->'I'->0->'reparticao' ? 'IBS') THEN RAISE EXCEPTION 'repartição 2027 sem CBS/IBS'; END IF;
    IF v->'anexos'->'I'->0->'reparticao' ? 'PIS' THEN RAISE EXCEPTION 'repartição 2027 não deve ter PIS'; END IF;
    -- todas as faixas fecham 100%
    SELECT count(*) INTO n FROM jsonb_each(v->'anexos') a, jsonb_array_elements(a.value) f,
        LATERAL (SELECT sum(x.value::numeric) s FROM jsonb_each_text(f->'reparticao') x) t WHERE t.s <> 100;
    IF n > 0 THEN RAISE EXCEPTION 'faixas do Simples 2027 que não fecham 100%%: %', n; END IF;
    IF (v->>'limite_exportacao')::numeric <> 3600000 OR (v->>'excesso_pct')::numeric <> 20 THEN RAISE EXCEPTION 'limites 2027'; END IF;
    SELECT valor INTO v FROM core.parametro_legal WHERE chave = 'SIMPLES_OPCAO_IBSCBS' AND vigencia_inicio = DATE '2026-01-01';
    IF v IS NULL OR jsonb_array_length(v->'regras') <> 2 THEN RAISE EXCEPTION 'SIMPLES_OPCAO_IBSCBS não semeado'; END IF;

    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ)
    VALUES ('11222333000181', 'Teste opção IBS/CBS', '2062', '4711302', 'SP', '3550308', DATE '2020-01-01') RETURNING id INTO e;
    INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, opcao) VALUES (e, DATE '2027-01-01', 'DENTRO_DO_DAS');
    INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, opcao, data_pedido) VALUES (e, DATE '2027-07-01', 'REGIME_REGULAR', DATE '2027-03-15');
    falhou := FALSE;
    BEGIN INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, opcao) VALUES (e, DATE '2027-03-01', 'REGIME_REGULAR');
    EXCEPTION WHEN check_violation THEN falhou := TRUE; END;
    IF NOT falhou THEN RAISE EXCEPTION 'vigência fora de 1º/1 e 1º/7 deve ser recusada'; END IF;
    falhou := FALSE;
    BEGIN INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, opcao) VALUES (e, DATE '2028-01-01', 'OUTRA');
    EXCEPTION WHEN check_violation THEN falhou := TRUE; END;
    IF NOT falhou THEN RAISE EXCEPTION 'opção inválida deve ser recusada'; END IF;
    falhou := FALSE;
    BEGIN INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, opcao) VALUES (e, DATE '2027-01-01', 'REGIME_REGULAR');
    EXCEPTION WHEN unique_violation THEN falhou := TRUE; END;
    IF NOT falhou THEN RAISE EXCEPTION 'duas opções na mesma vigência devem ser recusadas'; END IF;
    PERFORM 1 FROM fiscal.documento WHERE emitente_simples IS NULL LIMIT 1;
    DELETE FROM fiscal.opcao_ibscbs_simples WHERE empresa_id = e;
    DELETE FROM core.empresa WHERE id = e;
    RAISE NOTICE 'OK 011_fiscal_simples_2027';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
