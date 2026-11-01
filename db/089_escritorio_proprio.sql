-- O cadastro do escritório (Escritório › Cadastros › Dados do escritório) deixa de ser o cadastro geral de empresas: o registro do
-- próprio escritório (dono das tarefas, protocolos, honorários e contratos) é marcado como escritorio_proprio e nunca fica ativo
-- nos outros módulos. Assim o escritório pode ser cadastrado também como CLIENTE (para fazer a própria contabilidade, folha...):
-- essa empresa é outra, com código da numeração normal (0001...), mesmo tendo o mesmo CNPJ do registro do escritório.

ALTER TABLE core.empresa ADD COLUMN escritorio_proprio BOOLEAN NOT NULL DEFAULT FALSE;

UPDATE core.empresa SET escritorio_proprio = TRUE
 WHERE modulo_escritorio AND NOT (modulo_contabil OR modulo_fiscal OR modulo_folha OR modulo_erp OR modulo_condominio OR modulo_frota);

-- o registro do escritório fica só no módulo Escritório
ALTER TABLE core.empresa ADD CONSTRAINT ck_empresa_escritorio_proprio
    CHECK (NOT escritorio_proprio OR (modulo_escritorio AND NOT (modulo_contabil OR modulo_fiscal OR modulo_folha OR modulo_erp OR modulo_condominio OR modulo_frota)));

-- CNPJ único entre as empresas dos módulos e entre os escritórios (o escritório como cliente é uma empresa à parte)
ALTER TABLE core.empresa DROP CONSTRAINT empresa_cnpj_key;
CREATE UNIQUE INDEX uq_empresa_cnpj ON core.empresa (cnpj) WHERE NOT escritorio_proprio;
CREATE UNIQUE INDEX uq_empresa_cnpj_escritorio ON core.empresa (cnpj) WHERE escritorio_proprio;

-- o escritório recebe o código 9999 (9998...) e a marca de escritório próprio na inclusão
CREATE OR REPLACE FUNCTION core.fn_codigo_escritorio() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    v_seq TEXT := pg_get_serial_sequence('core.empresa', 'id');
    v_max BIGINT;
BEGIN
    IF NEW.modulo_escritorio AND NOT (NEW.modulo_contabil OR NEW.modulo_fiscal OR NEW.modulo_folha OR NEW.modulo_erp OR NEW.modulo_condominio OR NEW.modulo_frota) THEN
        PERFORM pg_advisory_xact_lock(hashtext('vorticis_codigo_escritorio'));
        NEW.id := COALESCE((SELECT min(id) FROM core.empresa WHERE id BETWEEN 9000 AND 9999), 10000) - 1;
        NEW.escritorio_proprio := TRUE;
        -- devolve o número que o valor padrão já tinha tirado da sequência
        SELECT max(id) INTO v_max FROM core.empresa WHERE id < 9000;
        IF v_max IS NULL THEN PERFORM setval(v_seq, 1, false); ELSE PERFORM setval(v_seq, v_max, true); END IF;
    END IF;
    RETURN NEW;
END $$;

-- o estabelecimento (matriz) do registro do escritório também não conflita com o da empresa do escritório-cliente
ALTER TABLE core.estabelecimento ADD COLUMN escritorio_proprio BOOLEAN NOT NULL DEFAULT FALSE;
UPDATE core.estabelecimento s SET escritorio_proprio = TRUE FROM core.empresa e WHERE e.id = s.empresa_id AND e.escritorio_proprio;
ALTER TABLE core.estabelecimento DROP CONSTRAINT estabelecimento_cnpj_key;
CREATE UNIQUE INDEX uq_estabelecimento_cnpj ON core.estabelecimento (cnpj) WHERE NOT escritorio_proprio;
CREATE UNIQUE INDEX uq_estabelecimento_cnpj_escritorio ON core.estabelecimento (cnpj) WHERE escritorio_proprio;

CREATE FUNCTION core.fn_estabelecimento_escritorio() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.escritorio_proprio := COALESCE((SELECT escritorio_proprio FROM core.empresa WHERE id = NEW.empresa_id), FALSE);
    RETURN NEW;
END $$;

CREATE TRIGGER trg_estabelecimento_escritorio BEFORE INSERT OR UPDATE OF empresa_id ON core.estabelecimento
    FOR EACH ROW EXECUTE FUNCTION core.fn_estabelecimento_escritorio();
