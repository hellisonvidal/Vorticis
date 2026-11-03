-- =============================================================================
-- Vorticis — Migração 092: excluir a empresa que tem inventário fiscal
--   O inventário (fiscal.inventario_item) referencia produtos sem exclusão em cascata e barrava a exclusão da empresa
--   (comum depois da importação de arquivos SPED, bloco H). Esta versão apaga o inventário antes; o resto é igual ao da 081.
-- =============================================================================
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
    DELETE FROM fiscal.inventario WHERE empresa_id = p_empresa;      -- os itens saem em cascata e referenciam produtos
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
