-- Fiscal › Apuração › Planejamento tributário: simulações gravadas por empresa e ano (receitas, folha, despesas e percentuais),
-- comparando Simples Nacional, Lucro Presumido e Lucro Real. O limite do Presumido e a alíquota patronal ficam em
-- core.parametro_legal (chave PLANEJAMENTO_TRIBUTARIO, semeada pelo programa).

CREATE TABLE fiscal.planejamento_tributario (
    id            BIGSERIAL PRIMARY KEY,
    empresa_id    BIGINT       NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    ano           INTEGER      NOT NULL CHECK (ano BETWEEN 2000 AND 2100),
    descricao     VARCHAR(80)  NOT NULL CHECK (btrim(descricao) <> ''),
    dados         JSONB        NOT NULL,
    atualizado_em TIMESTAMPTZ  NOT NULL DEFAULT now(),
    atualizado_por BIGINT      REFERENCES core.usuario (id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX uq_planejamento_tributario ON fiscal.planejamento_tributario (empresa_id, ano, lower(descricao));

CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON fiscal.planejamento_tributario FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
