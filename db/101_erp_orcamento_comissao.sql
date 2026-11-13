-- =============================================================================
-- Vorticis — Migração 101: Gerencial — orçamentos de venda e comissão de vendedores
--   * erp.pedido.situacao ganha ORCAMENTO: o orçamento é um pedido de venda ainda não aprovado; ao aprovar, vira ABERTO.
--   * validade do orçamento, vendedor e percentual de comissão do pedido (base do relatório de comissões).
-- =============================================================================
ALTER TABLE erp.pedido DROP CONSTRAINT ck_pedido_situacao;
ALTER TABLE erp.pedido
    ADD CONSTRAINT ck_pedido_situacao CHECK (situacao IN ('ORCAMENTO', 'ABERTO', 'CONCLUIDO', 'CANCELADO')),
    ADD CONSTRAINT ck_pedido_orcamento_venda CHECK (situacao <> 'ORCAMENTO' OR tipo = 'VENDA'),
    ADD COLUMN validade      DATE,
    ADD COLUMN vendedor      VARCHAR(100),
    ADD COLUMN comissao_pct  NUMERIC(5,2) NOT NULL DEFAULT 0 CHECK (comissao_pct BETWEEN 0 AND 100);
ALTER TABLE erp.pedido ALTER COLUMN situacao TYPE VARCHAR(10);

-- O orçamento pode ser alterado (itens e condições) como o pedido em aberto; concluído e cancelado continuam travados.
CREATE OR REPLACE FUNCTION erp.fn_pedido_imutavel()
 RETURNS trigger
 LANGUAGE plpgsql
AS $$
DECLARE v_situacao VARCHAR;
BEGIN
    IF TG_OP = 'DELETE' AND COALESCE(current_setting('vorticis.excluindo_empresa', true), '') <> '' THEN RETURN OLD; END IF;
    IF TG_TABLE_NAME = 'pedido' THEN
        IF OLD.situacao NOT IN ('ABERTO', 'ORCAMENTO') THEN
            RAISE EXCEPTION 'O pedido % já está %: não pode ser alterado nem excluído.', OLD.numero, lower(OLD.situacao) USING ERRCODE = 'P0001';
        END IF;
    ELSE
        SELECT situacao INTO v_situacao FROM erp.pedido WHERE id = COALESCE(NEW.pedido_id, OLD.pedido_id);
        IF v_situacao IS NOT NULL AND v_situacao NOT IN ('ABERTO', 'ORCAMENTO') THEN
            RAISE EXCEPTION 'O pedido já está %: os itens não podem mudar.', lower(v_situacao) USING ERRCODE = 'P0001';
        END IF;
    END IF;
    RETURN COALESCE(NEW, OLD);
END $$;
