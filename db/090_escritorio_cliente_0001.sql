-- O escritório entra na numeração normal das empresas (a primeira empresa cadastrada é a 0001) e é, ao mesmo tempo, o
-- primeiro CLIENTE do cadastro de clientes do Escritório (código 1, ligado à própria empresa). É do cadastro de clientes que
-- o sistema todo é alimentado: os módulos marcados no cliente 0001 valem para a própria empresa do escritório (a contabilidade,
-- a folha ou o fiscal dele), sem criar outra empresa. Os clientes seguintes ficam com 0002, 0003...

-- o escritório pode usar os outros módulos (é a própria empresa do cliente 0001)
ALTER TABLE core.empresa DROP CONSTRAINT ck_empresa_escritorio_proprio;

-- sem o código 9999: só marca o registro do escritório (cadastrado só no módulo Escritório)
CREATE OR REPLACE FUNCTION core.fn_codigo_escritorio() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.modulo_escritorio AND NOT (NEW.modulo_contabil OR NEW.modulo_fiscal OR NEW.modulo_folha OR NEW.modulo_erp OR NEW.modulo_condominio OR NEW.modulo_frota) THEN
        NEW.escritorio_proprio := TRUE;
    END IF;
    RETURN NEW;
END $$;

-- cliente do próprio escritório: código 1 (ou o menor livre), com os dados do cadastro do escritório
CREATE FUNCTION escritorio.fn_cliente_do_escritorio(p_escritorio BIGINT) RETURNS BIGINT LANGUAGE plpgsql AS $$
DECLARE
    v_id     BIGINT;
    v_codigo INTEGER;
BEGIN
    SELECT id INTO v_id FROM escritorio.pessoa WHERE empresa_id = p_escritorio AND empresa_vinculada_id = p_escritorio;
    IF v_id IS NOT NULL THEN RETURN v_id; END IF;
    -- já cadastrado como cliente pelo CNPJ (versões anteriores): só liga à própria empresa
    UPDATE escritorio.pessoa p SET empresa_vinculada_id = p_escritorio
      FROM core.empresa e
     WHERE e.id = p_escritorio AND p.empresa_id = p_escritorio AND p.documento = e.cnpj AND p.empresa_vinculada_id IS NULL
    RETURNING p.id INTO v_id;
    IF v_id IS NOT NULL THEN RETURN v_id; END IF;
    IF EXISTS (SELECT 1 FROM escritorio.pessoa p JOIN core.empresa e ON e.id = p_escritorio WHERE p.empresa_id = p_escritorio AND p.documento = e.cnpj) THEN
        RETURN NULL;   -- já existe cliente com o CNPJ, ligado a outra empresa: deixa como está
    END IF;
    SELECT min(c) INTO v_codigo FROM generate_series(1, (SELECT COALESCE(max(codigo), 0) + 1 FROM escritorio.pessoa WHERE empresa_id = p_escritorio)) c
     WHERE NOT EXISTS (SELECT 1 FROM escritorio.pessoa WHERE empresa_id = p_escritorio AND codigo = c);
    INSERT INTO escritorio.pessoa (empresa_id, codigo, cliente, fornecedor, nome, documento, regime, email, telefone, endereco, municipio, uf,
                                   nome_fantasia, natureza_juridica, cnae, inscricao_estadual, data_inicio_atividades, cep, logradouro, numero,
                                   complemento, bairro, cod_municipio_ibge, empresa_vinculada_id, observacao)
    SELECT e.id, v_codigo, TRUE, FALSE, e.razao_social, CASE WHEN e.cnpj ~ '^[0-9]{14}$' THEN e.cnpj END,
           (SELECT CASE r.regime::text WHEN 'SIMPLES_NACIONAL' THEN 'Simples Nacional' WHEN 'LUCRO_PRESUMIDO' THEN 'Lucro Presumido'
                                       WHEN 'LUCRO_REAL' THEN 'Lucro Real' WHEN 'MEI' THEN 'MEI' WHEN 'LUCRO_ARBITRADO' THEN 'Lucro Arbitrado'
                                       ELSE 'Imune/isenta' END
              FROM core.empresa_regime r WHERE r.empresa_id = e.id ORDER BY r.vigencia_inicio DESC LIMIT 1),
           e.email, e.telefone,
           NULLIF(concat_ws(', ', e.logradouro, e.numero, e.complemento, e.bairro), ''), e.municipio, e.uf,
           e.nome_fantasia, e.natureza_juridica, e.cnae_principal, e.inscricao_estadual, e.data_inicio_ativ, e.cep, e.logradouro, e.numero,
           e.complemento, e.bairro, e.cod_municipio_ibge, e.id, 'O próprio escritório (cadastrado automaticamente)'
      FROM core.empresa e WHERE e.id = p_escritorio
    RETURNING id INTO v_id;
    RETURN v_id;
EXCEPTION WHEN others THEN
    -- o cadastro do escritório nunca falha por causa do cliente: ele pode ser incluído à mão em Cadastros › Clientes
    RAISE WARNING 'cliente do escritório % não foi criado: %', p_escritorio, SQLERRM;
    RETURN NULL;
END $$;

CREATE FUNCTION escritorio.fn_trg_cliente_do_escritorio() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM escritorio.fn_cliente_do_escritorio(NEW.id);
    RETURN NULL;
END $$;

-- depois do regime (gravado na mesma transação do cadastro): DEFERRABLE, roda no fim da transação
CREATE CONSTRAINT TRIGGER trg_cliente_do_escritorio AFTER INSERT ON core.empresa DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW WHEN (NEW.escritorio_proprio) EXECUTE FUNCTION escritorio.fn_trg_cliente_do_escritorio();

-- escritórios já cadastrados: ganham o cliente agora
SELECT escritorio.fn_cliente_do_escritorio(id) FROM core.empresa WHERE escritorio_proprio;
