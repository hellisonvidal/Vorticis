-- =============================================================================
-- 076 — Bens a cadastrar das notas fiscais de entrada (Imobilizado).
--   * imobilizado.bem_a_cadastrar: um registro por item de nota de ENTRADA que é bem do ativo imobilizado (CFOP 1551/2551/3551/
--     1406/2406, produto com tipo do item 08, regra fiscal ou regra de contabilização que debita conta de bem). Serve às duas
--     formas de contabilização: ON-LINE (a tela abre o cadastro de bens já preenchido) e EM LOTE (a pendência espera a
--     Contabilização em lote). O bem gravado fica vinculado ao item (bem_id); a chave (documento, item) impede duplicar
--     quando a nota é regravada ou reprocessada.
--   * Trigger em fiscal.documento_item: registra a pendência em qualquer caminho (digitação, XML, chave, lote).
--   * Excluir o bem devolve o item à situação PENDENTE.
-- =============================================================================
CREATE TABLE imobilizado.bem_a_cadastrar (
    id              BIGSERIAL      PRIMARY KEY,
    empresa_id      BIGINT         NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    documento_id    BIGINT         NOT NULL REFERENCES fiscal.documento (id) ON DELETE CASCADE,
    n_item          INTEGER        NOT NULL,
    descricao       VARCHAR(200)   NOT NULL,
    valor           NUMERIC(15,2)  NOT NULL DEFAULT 0,
    data_aquisicao  DATE           NOT NULL,
    data_inicio_uso DATE           NOT NULL,
    fornecedor_id   BIGINT         REFERENCES core.participante (id) ON DELETE SET NULL,
    numero_nota     VARCHAR(20)    NOT NULL,
    serie           VARCHAR(10)    NOT NULL DEFAULT '',
    chave_acesso    VARCHAR(60),
    cfop            VARCHAR(10),
    ncm             VARCHAR(8),
    conta_sugerida  VARCHAR(30),
    motivo          VARCHAR(12)    NOT NULL,
    situacao        VARCHAR(10)    NOT NULL DEFAULT 'PENDENTE',
    bem_id          BIGINT         REFERENCES imobilizado.bem (id) ON DELETE SET NULL,
    criado_em       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    resolvido_em    TIMESTAMPTZ,
    resolvido_por   BIGINT         REFERENCES core.usuario (id) ON DELETE SET NULL,
    CONSTRAINT uq_bem_a_cadastrar UNIQUE (documento_id, n_item),
    CONSTRAINT ck_bem_a_cadastrar_motivo CHECK (motivo IN ('CFOP', 'TIPO_ITEM', 'REGRA_FISCAL', 'CONTA')),
    CONSTRAINT ck_bem_a_cadastrar_situacao CHECK (situacao IN ('PENDENTE', 'CADASTRADO', 'DESCARTADO'))
);
CREATE INDEX ix_bem_a_cadastrar_empresa ON imobilizado.bem_a_cadastrar (empresa_id, situacao);
CREATE INDEX ix_bem_a_cadastrar_bem ON imobilizado.bem_a_cadastrar (bem_id);

CREATE FUNCTION imobilizado.fn_registrar_bem_a_cadastrar() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    d       fiscal.documento%ROWTYPE;
    v_cfops TEXT[] := ARRAY['1551', '2551', '3551', '1406', '2406'];
    v_motivo TEXT;
    v_conta TEXT;
    v_cfop  TEXT := COALESCE(NULLIF(NEW.cfop_escrituracao, ''), NEW.cfop);
BEGIN
    SELECT * INTO d FROM fiscal.documento WHERE id = NEW.documento_id;
    IF NOT FOUND OR d.tipo_operacao <> 'E' OR d.situacao <> 'AUTORIZADA' THEN RETURN NULL; END IF;

    IF replace(NEW.cfop, '.', '') = ANY (v_cfops) OR replace(v_cfop, '.', '') = ANY (v_cfops) THEN
        v_motivo := 'CFOP';
    ELSIF NEW.produto_id IS NOT NULL AND EXISTS (SELECT 1 FROM fiscal.produto p WHERE p.id = NEW.produto_id AND p.tipo_item = '08') THEN
        v_motivo := 'TIPO_ITEM';
    ELSE
        -- regra fiscal aplicada ao item que debita uma conta de bem de algum grupo
        SELECT r.conta_debito INTO v_conta FROM fiscal.regra_fiscal r
         WHERE r.id = NEW.regra_fiscal_id AND r.conta_debito IN (SELECT conta_bem FROM imobilizado.grupo WHERE conta_bem IS NOT NULL);
        IF v_conta IS NOT NULL THEN v_motivo := 'REGRA_FISCAL';
        ELSE
            -- regra de contabilização da empresa (CFOP específico) que debita uma conta de bem
            SELECT pc.codigo INTO v_conta
              FROM fiscal.regra_contabilizacao rc
              JOIN contabil.plano_contas pc ON pc.id = rc.conta_debito_id
             WHERE rc.empresa_id = d.empresa_id AND rc.ativo AND rc.cfop IS NOT NULL AND v_cfop LIKE rc.cfop || '%'
               AND (rc.tipo_operacao IS NULL OR rc.tipo_operacao = 'E')
               AND (rc.participante_id IS NULL OR rc.participante_id = d.participante_id)
               AND pc.codigo IN (SELECT conta_bem FROM imobilizado.grupo WHERE conta_bem IS NOT NULL)
             ORDER BY rc.prioridade LIMIT 1;
            IF v_conta IS NOT NULL THEN v_motivo := 'CONTA'; END IF;
        END IF;
    END IF;
    IF v_motivo IS NULL THEN RETURN NULL; END IF;

    INSERT INTO imobilizado.bem_a_cadastrar (empresa_id, documento_id, n_item, descricao, valor, data_aquisicao, data_inicio_uso, fornecedor_id,
                                             numero_nota, serie, chave_acesso, cfop, ncm, conta_sugerida, motivo)
    VALUES (d.empresa_id, d.id, NEW.n_item, left(COALESCE(NULLIF(trim(NEW.descricao), ''), 'Item ' || NEW.n_item), 200),
            CASE WHEN NEW.valor_contabil > 0 THEN NEW.valor_contabil ELSE NEW.valor_produto END,
            d.data_emissao, d.data_entrada_saida, d.participante_id, d.numero, d.serie, d.chave_acesso, v_cfop, NEW.ncm, v_conta, v_motivo)
    ON CONFLICT (documento_id, n_item) DO UPDATE   -- nota regravada: pendência ainda aberta acompanha os dados novos; cadastrada/descartada fica como está
        SET descricao = EXCLUDED.descricao, valor = EXCLUDED.valor, data_aquisicao = EXCLUDED.data_aquisicao, data_inicio_uso = EXCLUDED.data_inicio_uso,
            fornecedor_id = EXCLUDED.fornecedor_id, numero_nota = EXCLUDED.numero_nota, serie = EXCLUDED.serie, chave_acesso = EXCLUDED.chave_acesso,
            cfop = EXCLUDED.cfop, ncm = EXCLUDED.ncm, conta_sugerida = EXCLUDED.conta_sugerida, motivo = EXCLUDED.motivo
        WHERE imobilizado.bem_a_cadastrar.situacao = 'PENDENTE';
    RETURN NULL;
END $$;

CREATE TRIGGER trg_registrar_bem_a_cadastrar
    AFTER INSERT OR UPDATE OF cfop, cfop_escrituracao, produto_id, regra_fiscal_id ON fiscal.documento_item
    FOR EACH ROW EXECUTE FUNCTION imobilizado.fn_registrar_bem_a_cadastrar();

-- Bem excluído: o item da nota volta a ser pendência.
CREATE FUNCTION imobilizado.fn_bem_excluido() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    UPDATE imobilizado.bem_a_cadastrar SET situacao = 'PENDENTE', bem_id = NULL, resolvido_em = NULL, resolvido_por = NULL
     WHERE bem_id = OLD.id;
    RETURN OLD;
END $$;
CREATE TRIGGER trg_bem_excluido BEFORE DELETE ON imobilizado.bem FOR EACH ROW EXECUTE FUNCTION imobilizado.fn_bem_excluido();
