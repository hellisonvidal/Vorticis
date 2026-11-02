-- Testes da migração 081: "Limpar banco de dados" apaga tudo o que os testes anteriores gravaram (notas com duplicata
-- baixada, pedidos concluídos, condomínio, escritório, folha...) e o banco fica como numa instalação nova.

DO $$
DECLARE
    r RECORD;
    v_n BIGINT;
BEGIN
    IF (SELECT count(*) FROM core.empresa) = 0 THEN RAISE EXCEPTION 'os testes anteriores deveriam ter deixado empresas'; END IF;
    INSERT INTO core.configuracao_sistema (chave, valor) VALUES ('pasta_backup', 'C:\Backup'), ('partida', 'SIMPLES')
        ON CONFLICT (chave) DO UPDATE SET valor = EXCLUDED.valor;
    PERFORM core.fn_limpar_banco();

    FOR r IN SELECT n.nspname AS s, c.relname AS t FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
              WHERE c.relkind = 'r' AND n.nspname IN ('core', 'contabil', 'fiscal', 'folha', 'imobilizado', 'erp', 'escritorio', 'condominio')
                AND (n.nspname, c.relname) NOT IN (('core', 'auditoria'), ('core', 'parametro_legal'), ('core', 'perfil'),
                                                   ('core', 'perfil_permissao'), ('imobilizado', 'grupo'), ('core', 'configuracao_sistema'))
    LOOP
        EXECUTE format('SELECT count(*) FROM %I.%I', r.s, r.t) INTO v_n;
        IF v_n > 0 THEN RAISE EXCEPTION 'a limpeza deixou % linha(s) em %.%', v_n, r.s, r.t; END IF;
    END LOOP;
    IF EXISTS (SELECT 1 FROM core.perfil WHERE NOT padrao) THEN RAISE EXCEPTION 'perfil personalizado ficou'; END IF;
    IF EXISTS (SELECT 1 FROM core.configuracao_sistema WHERE chave <> 'pasta_backup') THEN RAISE EXCEPTION 'configuração gravada ficou'; END IF;
    IF NOT EXISTS (SELECT 1 FROM core.configuracao_sistema WHERE chave = 'pasta_backup') THEN RAISE EXCEPTION 'a pasta de backup é da instalação e deve ficar'; END IF;
    -- numeração recomeça: o escritório fica com 0001 e já é o cliente 0001; as empresas seguintes, 0002...
    SET CONSTRAINTS ALL IMMEDIATE;
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ, modulo_contabil, modulo_fiscal, modulo_folha, modulo_escritorio)
    VALUES ('11222333000181', 'Escritório Teste', '2062', '6920601', 'SP', '3550308', '2020-01-01', FALSE, FALSE, FALSE, TRUE) RETURNING id INTO v_n;
    IF v_n <> 1 THEN RAISE EXCEPTION 'escritório deveria ficar com o código 1 (ficou %)', v_n; END IF;
    IF NOT EXISTS (SELECT 1 FROM escritorio.pessoa WHERE empresa_id = 1 AND codigo = 1 AND empresa_vinculada_id = 1 AND cliente) THEN
        RAISE EXCEPTION 'o escritório deveria entrar como o cliente 0001';
    END IF;
    INSERT INTO core.empresa (cnpj, razao_social, natureza_juridica, cnae_principal, uf, cod_municipio_ibge, data_inicio_ativ)
    VALUES ('19131243000197', 'Primeiro Cliente', '2062', '4711302', 'SP', '3550308', '2020-01-01') RETURNING id INTO v_n;
    IF v_n <> 2 THEN RAISE EXCEPTION 'a empresa seguinte deveria ficar com o código 2 (ficou %)', v_n; END IF;
    PERFORM core.fn_limpar_banco();
    RAISE NOTICE 'OK: limpeza do banco recomeça do zero';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
