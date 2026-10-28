-- =============================================================================
-- Vorticis Fiscal — Migração 079: ajustes da apuração em Lançamentos › Ajustes (entrada e saída)
--   * fiscal.efd_ajuste_icms passa a guardar também os ajustes do ICMS-ST (E220) e do IPI (E530), o sentido (E = entrada: outros créditos
--     e estornos de débito; S = saída: outros débitos, estornos de crédito, deduções e débitos especiais; no IPI, entrada = crédito e
--     saída = débito), a observação (E112) e o documento fiscal relacionado (E113). Os dados existentes são mantidos como ICMS próprio.
--   * fiscal.codigo_ajuste: cadastro simples dos códigos de ajuste usados pela empresa (tabela 5.1.1 da UF; IPI: 3 posições).
-- =============================================================================

ALTER TABLE fiscal.efd_ajuste_icms DROP CONSTRAINT IF EXISTS efd_ajuste_icms_codigo_check;
ALTER TABLE fiscal.efd_ajuste_icms ALTER COLUMN codigo TYPE VARCHAR(8);
ALTER TABLE fiscal.efd_ajuste_icms
    ADD COLUMN imposto             VARCHAR(7)   NOT NULL DEFAULT 'ICMS' CHECK (imposto IN ('ICMS', 'ICMS_ST', 'IPI')),
    ADD COLUMN sentido             CHAR(1)      CHECK (sentido IN ('E', 'S')),
    ADD COLUMN observacao          VARCHAR(255),
    ADD COLUMN doc_participante_id BIGINT       REFERENCES core.participante (id) ON DELETE SET NULL,
    ADD COLUMN doc_modelo          VARCHAR(2),
    ADD COLUMN doc_serie           VARCHAR(3),
    ADD COLUMN doc_numero          VARCHAR(20),
    ADD COLUMN doc_data            DATE;

UPDATE fiscal.efd_ajuste_icms SET sentido = CASE WHEN substr(codigo, 4, 1) IN ('2', '3') THEN 'E' ELSE 'S' END;
ALTER TABLE fiscal.efd_ajuste_icms ALTER COLUMN sentido SET NOT NULL, ALTER COLUMN sentido SET DEFAULT 'S';

ALTER TABLE fiscal.efd_ajuste_icms ADD CONSTRAINT efd_ajuste_icms_codigo_check CHECK (
    (imposto = 'ICMS'    AND codigo ~ '^[A-Z]{2}0[0-5][0-9]{4}$') OR
    (imposto = 'ICMS_ST' AND codigo ~ '^[A-Z]{2}1[0-5][0-9]{4}$') OR
    (imposto = 'IPI'     AND codigo ~ '^[A-Z0-9]{3}$'));

CREATE TABLE fiscal.codigo_ajuste (
    empresa_id  BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    imposto     VARCHAR(7)    NOT NULL CHECK (imposto IN ('ICMS', 'ICMS_ST', 'IPI')),
    codigo      VARCHAR(8)    NOT NULL,
    descricao   VARCHAR(255)  NOT NULL,
    PRIMARY KEY (empresa_id, imposto, codigo)
);
