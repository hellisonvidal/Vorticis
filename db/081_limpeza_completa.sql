-- Exclusão de empresa e limpeza do banco completas, para "Limpar banco de dados" recomeçar o sistema do zero.
--  * A trava das notas com duplicata baixada (053) conferia o sinal com o nome antigo ("perseus.excluindo_empresa"):
--    a exclusão da empresa, e com ela a limpeza do banco, parava em "tem duplicata baixada".
--  * Tabelas criadas depois da exclusão de empresa (Gerencial, Condomínio, Escritório, Folha...) barravam a exclusão
--    pelas chaves estrangeiras: core.fn_apagar_em_cascata apaga antes, pelas chaves, tudo o que depende do registro.
--  * A limpeza apaga também as preferências e configurações gravadas (menus, layouts, tema, eSocial...), e o
--    sistema volta a pedir todos os cadastros, como numa instalação nova.

CREATE OR REPLACE FUNCTION fiscal.fn_documento_protege_duplicatas()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM fiscal.duplicata_baixa b JOIN fiscal.duplicata d ON d.id = b.duplicata_id WHERE d.documento_id = OLD.id)
       AND NOT core.excluindo_empresa(OLD.empresa_id) THEN
        RAISE EXCEPTION 'A nota % tem duplicata baixada: estorne as baixas antes de excluí-la', OLD.numero;
    END IF;
    RETURN OLD;
END $$;

-- Apaga de p_tabela as linhas de p_filtro e, antes, as linhas das tabelas que as referenciam sem ON DELETE CASCADE/SET NULL
-- (recursivo, pelas chaves estrangeiras de uma coluna). Só para exclusão de empresa e limpeza do banco.
CREATE OR REPLACE FUNCTION core.fn_apagar_em_cascata(p_tabela REGCLASS, p_filtro TEXT, p_nivel INTEGER DEFAULT 0)
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    r RECORD;
BEGIN
    IF p_nivel > 15 THEN
        RAISE EXCEPTION 'Dependências demais ao apagar %', p_tabela;
    END IF;
    FOR r IN
        SELECT c.conrelid::regclass AS filho, fa.attname AS col_filho, pa.attname AS col_pai
          FROM pg_constraint c
          JOIN pg_attribute fa ON fa.attrelid = c.conrelid AND fa.attnum = c.conkey[1]
          JOIN pg_attribute pa ON pa.attrelid = c.confrelid AND pa.attnum = c.confkey[1]
         WHERE c.contype = 'f' AND c.confrelid = p_tabela AND c.conrelid <> p_tabela
           AND c.confdeltype IN ('a', 'r') AND cardinality(c.conkey) = 1
    LOOP
        PERFORM core.fn_apagar_em_cascata(r.filho, format('%I IN (SELECT %I FROM %s WHERE %s)', r.col_filho, r.col_pai, p_tabela, p_filtro), p_nivel + 1);
    END LOOP;
    EXECUTE format('DELETE FROM %s WHERE %s', p_tabela, p_filtro);
END $$;

CREATE OR REPLACE FUNCTION core.fn_excluir_empresa(p_empresa BIGINT)
RETURNS INTEGER LANGUAGE plpgsql AS $$
DECLARE
    v_lancamentos INTEGER;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM core.empresa WHERE id = p_empresa) THEN
        RAISE EXCEPTION 'Empresa % não encontrada', p_empresa;
    END IF;
    PERFORM set_config('vorticis.excluindo_empresa', p_empresa::text, true);

    SELECT count(*) INTO v_lancamentos FROM contabil.lancamento WHERE empresa_id = p_empresa;
    DELETE FROM contabil.conciliacao WHERE empresa_id = p_empresa;
    DELETE FROM imobilizado.depreciacao WHERE bem_id IN (SELECT id FROM imobilizado.bem WHERE empresa_id = p_empresa);
    DELETE FROM imobilizado.bem WHERE empresa_id = p_empresa;
    DELETE FROM contabil.mapeamento_referencial WHERE empresa_id = p_empresa;
    DELETE FROM contabil.partida WHERE lancamento_id IN (SELECT id FROM contabil.lancamento WHERE empresa_id = p_empresa);
    DELETE FROM contabil.lancamento WHERE empresa_id = p_empresa;
    DELETE FROM contabil.saldo_mensal WHERE empresa_id = p_empresa;
    DELETE FROM contabil.historico_padrao WHERE empresa_id = p_empresa;
    DELETE FROM contabil.plano_contas WHERE empresa_id = p_empresa;
    DELETE FROM contabil.centro_custo WHERE empresa_id = p_empresa;
    DELETE FROM contabil.periodo WHERE exercicio_id IN (SELECT id FROM contabil.exercicio WHERE empresa_id = p_empresa);
    DELETE FROM contabil.exercicio WHERE empresa_id = p_empresa;
    DELETE FROM fiscal.conta_consumo WHERE empresa_id = p_empresa;   -- referencia o participante (migração 066)
    PERFORM core.fn_apagar_em_cascata('core.participante', format('empresa_id = %s', p_empresa));
    DELETE FROM core.socio WHERE empresa_id = p_empresa;
    DELETE FROM core.empresa_contabilidade WHERE empresa_id = p_empresa;
    DELETE FROM core.usuario_empresa WHERE empresa_id = p_empresa;
    DELETE FROM core.empresa_regime WHERE empresa_id = p_empresa;
    DELETE FROM core.estabelecimento WHERE empresa_id = p_empresa;
    PERFORM core.fn_apagar_em_cascata('core.empresa', format('id = %s', p_empresa));   -- o que sobrou dos demais módulos

    PERFORM set_config('vorticis.excluindo_empresa', '', true);
    RETURN v_lancamentos;
END $$;


-- Travas de alteração (pedido concluído, período fiscal e competência da folha fechados) não barram a exclusão da empresa inteira.
CREATE OR REPLACE FUNCTION erp.fn_pedido_imutavel()
 RETURNS trigger
 LANGUAGE plpgsql
AS $$
DECLARE v_situacao VARCHAR;
BEGIN
    IF TG_OP = 'DELETE' AND COALESCE(current_setting('vorticis.excluindo_empresa', true), '') <> '' THEN RETURN OLD; END IF;
    IF TG_TABLE_NAME = 'pedido' THEN
        IF OLD.situacao <> 'ABERTO' THEN
            RAISE EXCEPTION 'O pedido % já está %: não pode ser alterado nem excluído.', OLD.numero, lower(OLD.situacao) USING ERRCODE = 'P0001';
        END IF;
    ELSE
        SELECT situacao INTO v_situacao FROM erp.pedido WHERE id = COALESCE(NEW.pedido_id, OLD.pedido_id);
        IF v_situacao IS NOT NULL AND v_situacao <> 'ABERTO' THEN
            RAISE EXCEPTION 'O pedido já está %: os itens não podem mudar.', lower(v_situacao) USING ERRCODE = 'P0001';
        END IF;
    END IF;
    RETURN COALESCE(NEW, OLD);
END $$;

CREATE OR REPLACE FUNCTION fiscal.fn_protege_periodo_fechado()
 RETURNS trigger
 LANGUAGE plpgsql
AS $$
DECLARE
    v_ignorar TEXT[] := ARRAY['lancamento_id', 'contabilizado_em', 'contabilizado_por', 'atualizado_em'];
BEGIN
    -- exclusão em cascata da empresa (limpeza do banco, exclusão da empresa) não é bloqueada
    IF TG_OP = 'DELETE' AND (core.excluindo_empresa(OLD.empresa_id) OR NOT EXISTS (SELECT 1 FROM core.empresa WHERE id = OLD.empresa_id)) THEN RETURN OLD; END IF;
    -- só a marcação de contabilização mudou: permitido
    IF TG_OP = 'UPDATE' AND (to_jsonb(NEW) - v_ignorar) = (to_jsonb(OLD) - v_ignorar) THEN RETURN NEW; END IF;
    IF TG_OP IN ('UPDATE', 'DELETE') AND fiscal.fn_mes_fechado(OLD.empresa_id, OLD.data_entrada_saida) THEN
        RAISE EXCEPTION 'O período fiscal de % está fechado: reabra o mês em Apuração › Fechamento do período fiscal para alterar ou excluir a nota %',
            to_char(OLD.data_entrada_saida, 'MM/YYYY'), OLD.numero USING ERRCODE = 'check_violation';
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') AND fiscal.fn_mes_fechado(NEW.empresa_id, NEW.data_entrada_saida) THEN
        RAISE EXCEPTION 'O período fiscal de % está fechado: reabra o mês em Apuração › Fechamento do período fiscal para lançar a nota %',
            to_char(NEW.data_entrada_saida, 'MM/YYYY'), NEW.numero USING ERRCODE = 'check_violation';
    END IF;
    RETURN COALESCE(NEW, OLD);
END $$;

CREATE OR REPLACE FUNCTION fiscal.fn_protege_periodo_fechado_item()
 RETURNS trigger
 LANGUAGE plpgsql
AS $$
DECLARE
    v_doc fiscal.documento;
BEGIN
    SELECT * INTO v_doc FROM fiscal.documento WHERE id = COALESCE(NEW.documento_id, OLD.documento_id);
    IF NOT FOUND OR (TG_OP = 'DELETE' AND core.excluindo_empresa(v_doc.empresa_id)) THEN RETURN COALESCE(NEW, OLD); END IF;   -- documento sendo excluído (em cascata)
    IF fiscal.fn_mes_fechado(v_doc.empresa_id, v_doc.data_entrada_saida) THEN
        RAISE EXCEPTION 'O período fiscal de % está fechado: reabra o mês em Apuração › Fechamento do período fiscal para alterar os itens da nota %',
            to_char(v_doc.data_entrada_saida, 'MM/YYYY'), v_doc.numero USING ERRCODE = 'check_violation';
    END IF;
    RETURN COALESCE(NEW, OLD);
END $$;

CREATE OR REPLACE FUNCTION folha.fn_pro_labore_competencia_aberta()
 RETURNS trigger
 LANGUAGE plpgsql
AS $$
DECLARE
    v_empresa BIGINT := COALESCE(NEW.empresa_id, OLD.empresa_id);
    v_comp    DATE   := COALESCE(NEW.competencia, OLD.competencia);
BEGIN
    IF TG_OP = 'DELETE' AND core.excluindo_empresa(v_empresa) THEN RETURN OLD; END IF;
    IF EXISTS (SELECT 1 FROM folha.competencia c
                WHERE c.empresa_id = v_empresa AND c.competencia = v_comp AND c.status = 'FECHADA') THEN
        RAISE EXCEPTION 'A competência % da folha está fechada. Reabra-a para alterar o pró-labore.', to_char(v_comp, 'MM/YYYY')
            USING ERRCODE = 'P0001';
    END IF;
    RETURN COALESCE(NEW, OLD);
END $$;

-- Limpeza do banco: como numa instalação nova. Além das empresas (com tudo o que é delas), apaga contadores, escritórios,
-- certificados, tabelas do plano referencial, layouts, todos os usuários (com o que ficou ligado a eles), perfis
-- personalizados, preferências de menu e as configurações gravadas (só a pasta de backup, que é da instalação, fica).
-- O próximo acesso é o de implantação, que cadastra o administrador; depois o sistema pede de novo os perfis, o escritório,
-- as empresas e os demais cadastros.
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
