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
    RAISE NOTICE 'OK: limpeza do banco recomeça do zero';
END $$;

\echo 'TODOS OS TESTES PASSARAM'
