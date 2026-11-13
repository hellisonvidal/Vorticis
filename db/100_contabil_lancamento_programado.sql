-- =============================================================================
-- Vorticis — Migração 100: lançamentos programados (Contábil)
--   Lançamentos que se repetem todo mês (aluguel, pró-labore, honorários, seguros...), gerados por competência com um clique.
--   * contabil.lancamento_programado: débito, crédito, valor, histórico, dia do lançamento e vigência (competências).
--   * contabil.lancamento_programado_gerado: o lançamento gerado em cada competência (não gera duas vezes; excluído o
--     lançamento, a competência pode ser gerada de novo).
-- =============================================================================
CREATE TABLE contabil.lancamento_programado (
    id               BIGSERIAL     PRIMARY KEY,
    empresa_id       BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    descricao        VARCHAR(100)  NOT NULL CHECK (btrim(descricao) <> ''),
    conta_debito     VARCHAR(40)   NOT NULL CHECK (btrim(conta_debito) <> ''),
    conta_credito    VARCHAR(40)   NOT NULL CHECK (btrim(conta_credito) <> ''),
    valor            NUMERIC(15,2) NOT NULL CHECK (valor > 0),
    historico        VARCHAR(250)  NOT NULL CHECK (btrim(historico) <> ''),
    dia              SMALLINT      NOT NULL CHECK (dia BETWEEN 1 AND 31),
    inicio           DATE          NOT NULL CHECK (extract(day FROM inicio) = 1),
    fim              DATE          CHECK (fim IS NULL OR (extract(day FROM fim) = 1 AND fim >= inicio)),
    centro_custo     VARCHAR(40),
    ativo            BOOLEAN       NOT NULL DEFAULT TRUE,
    CONSTRAINT ck_programado_contas CHECK (conta_debito <> conta_credito)
);
CREATE INDEX ix_lancamento_programado_empresa ON contabil.lancamento_programado (empresa_id);

CREATE TABLE contabil.lancamento_programado_gerado (
    programado_id    BIGINT  NOT NULL REFERENCES contabil.lancamento_programado (id) ON DELETE CASCADE,
    competencia      DATE    NOT NULL CHECK (extract(day FROM competencia) = 1),
    lancamento_id    BIGINT  NOT NULL REFERENCES contabil.lancamento (id) ON DELETE CASCADE,
    PRIMARY KEY (programado_id, competencia)
);
CREATE INDEX ix_programado_gerado_lancamento ON contabil.lancamento_programado_gerado (lancamento_id);

CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON contabil.lancamento_programado FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();
