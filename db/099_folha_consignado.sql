-- =============================================================================
-- Vorticis — Migração 099: empréstimo consignado em folha (Lei nº 10.820/2003; Crédito do Trabalhador)
--   * folha.consignado: contrato do funcionário com a instituição (parcela, quantidade e 1ª competência de desconto).
--   * folha.consignado_parcela: parcelas lançadas como desconto (evento variável) em cada competência.
--   * Margem consignável em core.parametro_legal (CONSIGNADO_MARGEM), nunca no código.
-- =============================================================================
CREATE TABLE folha.consignado (
    id                    BIGSERIAL     PRIMARY KEY,
    empresa_id            BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    funcionario_id        BIGINT        NOT NULL REFERENCES folha.funcionario (id) ON DELETE CASCADE,
    instituicao           VARCHAR(100)  NOT NULL CHECK (btrim(instituicao) <> ''),
    contrato              VARCHAR(40)   NOT NULL CHECK (btrim(contrato) <> ''),
    valor_parcela         NUMERIC(15,2) NOT NULL CHECK (valor_parcela > 0),
    total_parcelas        SMALLINT      NOT NULL CHECK (total_parcelas BETWEEN 1 AND 120),
    primeira_competencia  DATE          NOT NULL CHECK (extract(day FROM primeira_competencia) = 1),
    situacao              VARCHAR(10)   NOT NULL DEFAULT 'ATIVO' CHECK (situacao IN ('ATIVO', 'QUITADO', 'CANCELADO')),
    observacao            VARCHAR(300),
    criado_em             TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT uq_folha_consignado UNIQUE (empresa_id, funcionario_id, instituicao, contrato)
);
CREATE INDEX ix_folha_consignado_func ON folha.consignado (empresa_id, funcionario_id);

CREATE TABLE folha.consignado_parcela (
    id             BIGSERIAL     PRIMARY KEY,
    consignado_id  BIGINT        NOT NULL REFERENCES folha.consignado (id) ON DELETE CASCADE,
    competencia    DATE          NOT NULL CHECK (extract(day FROM competencia) = 1),
    numero         SMALLINT      NOT NULL CHECK (numero > 0),
    valor          NUMERIC(15,2) NOT NULL CHECK (valor > 0),
    evento_id      BIGINT,
    CONSTRAINT uq_folha_consignado_parcela UNIQUE (consignado_id, competencia),
    CONSTRAINT uq_folha_consignado_numero UNIQUE (consignado_id, numero)
);

CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON folha.consignado FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();

INSERT INTO core.parametro_legal (chave, vigencia_inicio, vigencia_fim, valor, fundamento_legal) VALUES
('CONSIGNADO_MARGEM', '2022-08-04', NULL, '{"margemEmprestimo":35.0,"margemCartoes":10.0}',
 'Lei nº 10.820/2003, art. 1º, § 1º (Lei nº 14.431/2022): 35% da remuneração disponível para empréstimos e 10% para cartões (5% crédito e 5% benefício)');
