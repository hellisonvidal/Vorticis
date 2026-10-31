-- O escritório contábil (empresa só do módulo Escritório) não ocupa a numeração das empresas: recebe o código 9999 (9998, 9997...
-- se houver outro), e o primeiro cliente cadastrado nos módulos fica com o código 0001. A limpeza do banco também volta a numeração ao 1.
-- Empresas já cadastradas mantêm o código (o id é a chave de todas as tabelas).

CREATE FUNCTION core.fn_codigo_escritorio() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    v_seq TEXT := pg_get_serial_sequence('core.empresa', 'id');
    v_max BIGINT;
BEGIN
    IF NEW.modulo_escritorio AND NOT (NEW.modulo_contabil OR NEW.modulo_fiscal OR NEW.modulo_folha OR NEW.modulo_erp OR NEW.modulo_condominio OR NEW.modulo_frota) THEN
        PERFORM pg_advisory_xact_lock(hashtext('vorticis_codigo_escritorio'));
        NEW.id := COALESCE((SELECT min(id) FROM core.empresa WHERE id BETWEEN 9000 AND 9999), 10000) - 1;
        -- devolve o número que o valor padrão já tinha tirado da sequência
        SELECT max(id) INTO v_max FROM core.empresa WHERE id < 9000;
        IF v_max IS NULL THEN PERFORM setval(v_seq, 1, false); ELSE PERFORM setval(v_seq, v_max, true); END IF;
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_codigo_escritorio BEFORE INSERT ON core.empresa FOR EACH ROW EXECUTE FUNCTION core.fn_codigo_escritorio();

-- Limpar banco de dados: numeração das empresas recomeça do 1.
CREATE OR REPLACE FUNCTION core.fn_limpar_banco()
RETURNS INTEGER LANGUAGE plpgsql AS $$
DECLARE
    v_empresa BIGINT;
    v_qtd     INTEGER := 0;
BEGIN
    FOR v_empresa IN SELECT id FROM core.empresa ORDER BY id LOOP
        PERFORM core.fn_excluir_empresa(v_empresa);
        v_qtd := v_qtd + 1;
    END LOOP;
    PERFORM setval(pg_get_serial_sequence('core.empresa', 'id'), 1, false);
    DELETE FROM core.certificado_digital;
    DELETE FROM core.contador;
    DELETE FROM core.escritorio_contabil;
    DELETE FROM contabil.plano_referencial;
    DELETE FROM contabil.plano_referencial_importacao;
    DELETE FROM contabil.layout_importacao;
    DELETE FROM core.preferencia_menu;

    -- Todos os usuários saem. A regra "sempre um administrador ativo" é suspensa só durante esta exclusão.
    DELETE FROM core.registro_acesso;
    ALTER TABLE core.usuario DISABLE TRIGGER trg_protege_ultimo_admin;
    PERFORM core.fn_apagar_em_cascata('core.usuario', 'TRUE');
    ALTER TABLE core.usuario ENABLE TRIGGER trg_protege_ultimo_admin;

    DELETE FROM core.perfil WHERE NOT padrao;
    DELETE FROM core.configuracao_sistema WHERE chave <> 'pasta_backup';
    RETURN v_qtd;
END $$;
