-- =====================================================================
-- Reforma tributária do consumo (EC 132/2023, LC 214/2025) preparada para 2027
--
-- * fiscal.classificacao_tributaria: classificações tributárias (cClassTrib)
--   do IBS e da CBS usadas pelo escritório, com o CST, o percentual de
--   redução da alíquota e se a aquisição gera crédito. A tabela oficial é
--   publicada pela Receita e pelo Comitê Gestor; o usuário cadastra as que usa.
-- * fiscal.documento_item: crédito de IBS/CBS na entrada, redução aplicada e
--   o Imposto Seletivo (CST, classificação, alíquota e valor) por item.
-- * fiscal.regra_fiscal: CST e classificação do IBS/CBS e se credita, para
--   preencher os itens das notas digitadas e importadas.
-- * fiscal.apuracao_reforma: apuração do IBS, da CBS e do IS encerrada no mês,
--   com o saldo credor que passa para o mês seguinte e o lançamento contábil.
-- * fiscal.conta_tributo: tributos IBS, CBS e IS (para IBS e CBS, a conta de
--   "despesa" é a conta do crédito a recuperar, no ativo).
-- * core.parametro_legal REFORMA_IBS_CBS: alíquotas e regras de 2026 a 2033
--   (2027: CBS plena, PIS/COFINS extintos, IPI zerado, Imposto Seletivo).
-- =====================================================================

CREATE TABLE fiscal.classificacao_tributaria (
    codigo          CHAR(6)       PRIMARY KEY CHECK (codigo ~ '^[0-9]{6}$'),
    cst             CHAR(3)       NOT NULL CHECK (cst ~ '^[0-9]{3}$'),
    descricao       VARCHAR(300)  NOT NULL CHECK (btrim(descricao) <> ''),
    reducao_pct     NUMERIC(7,4)  NOT NULL DEFAULT 0 CHECK (reducao_pct BETWEEN 0 AND 100),
    gera_credito    BOOLEAN       NOT NULL DEFAULT TRUE,
    ativo           BOOLEAN       NOT NULL DEFAULT TRUE
);
INSERT INTO fiscal.classificacao_tributaria (codigo, cst, descricao) VALUES
    ('000001', '000', 'Situações tributadas integralmente pelo IBS e pela CBS');

ALTER TABLE fiscal.documento_item
    ADD COLUMN credita_ibscbs  BOOLEAN       NOT NULL DEFAULT TRUE,
    ADD COLUMN reducao_ibscbs  NUMERIC(7,4)  NOT NULL DEFAULT 0,
    ADD COLUMN cst_is          VARCHAR(3),
    ADD COLUMN c_class_trib_is VARCHAR(6),
    ADD COLUMN aliq_is         NUMERIC(7,4)  NOT NULL DEFAULT 0,
    ADD COLUMN vl_is           NUMERIC(15,2) NOT NULL DEFAULT 0;

ALTER TABLE fiscal.regra_fiscal
    ADD COLUMN cst_ibscbs      CHAR(3)       CHECK (cst_ibscbs IS NULL OR cst_ibscbs ~ '^[0-9]{3}$'),
    ADD COLUMN c_class_trib    CHAR(6)       CHECK (c_class_trib IS NULL OR c_class_trib ~ '^[0-9]{6}$'),
    ADD COLUMN credita_ibscbs  BOOLEAN       NOT NULL DEFAULT TRUE;

CREATE TABLE fiscal.apuracao_reforma (
    id                   BIGSERIAL     PRIMARY KEY,
    empresa_id           BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    competencia          DATE          NOT NULL CHECK (extract(day FROM competencia) = 1),
    debito_cbs           NUMERIC(15,2) NOT NULL DEFAULT 0,
    debito_ibs           NUMERIC(15,2) NOT NULL DEFAULT 0,
    credito_cbs          NUMERIC(15,2) NOT NULL DEFAULT 0,
    credito_ibs          NUMERIC(15,2) NOT NULL DEFAULT 0,
    saldo_anterior_cbs   NUMERIC(15,2) NOT NULL DEFAULT 0,
    saldo_anterior_ibs   NUMERIC(15,2) NOT NULL DEFAULT 0,
    recolher_cbs         NUMERIC(15,2) NOT NULL DEFAULT 0,
    recolher_ibs         NUMERIC(15,2) NOT NULL DEFAULT 0,
    saldo_credor_cbs     NUMERIC(15,2) NOT NULL DEFAULT 0,
    saldo_credor_ibs     NUMERIC(15,2) NOT NULL DEFAULT 0,
    imposto_seletivo     NUMERIC(15,2) NOT NULL DEFAULT 0,
    lancamento_id        BIGINT        REFERENCES contabil.lancamento (id) ON DELETE SET NULL,
    encerrada_em         TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT uq_apuracao_reforma UNIQUE (empresa_id, competencia)
);
CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON fiscal.apuracao_reforma FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();

INSERT INTO core.parametro_legal (chave, vigencia_inicio, valor, fundamento_legal)
VALUES ('REFORMA_IBS_CBS', DATE '2027-01-01', '{"vigencia_inicio": "2027-01-01", "fundamento": "EC 132/2023; LC 214/2025 (transição, arts. 343 e seguintes): CBS e IBS de teste em 2026; de 2027 em diante, CBS plena e PIS/COFINS extintos", "anos": [{"ano": 2026, "cbs": 0.9, "ibs_uf": 0.1, "ibs_mun": 0.0, "pis_cofins": true, "ipi": true, "imposto_seletivo": false, "contabiliza": false, "observacao": "Ano de teste: CBS 0,9% e IBS 0,1% destacados no documento, compensáveis com PIS e COFINS; não há recolhimento nem contabilização."}, {"ano": 2027, "cbs": 11.2, "ibs_uf": 0.05, "ibs_mun": 0.05, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "CBS cobrada: alíquota de referência (11,3%, calculada pelo TCU) menos 0,1 ponto; IBS de 0,05% (UF) e 0,05% (município). PIS e COFINS extintos; IPI zerado, salvo produtos incentivados da Zona Franca de Manaus; começa o Imposto Seletivo. Confira a resolução do Senado e ajuste aqui, se necessário."}, {"ano": 2028, "cbs": 11.2, "ibs_uf": 0.05, "ibs_mun": 0.05, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "Mesmas regras de 2027. Confira a resolução do Senado."}, {"ano": 2029, "cbs": 11.3, "ibs_uf": null, "ibs_mun": null, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "2029: o IBS sobe e o ICMS e o ISS caem gradualmente até 2033; as alíquotas do IBS serão fixadas pelo Senado e pelos entes. Cadastre-as aqui quando publicadas."}, {"ano": 2030, "cbs": 11.3, "ibs_uf": null, "ibs_mun": null, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "2030: o IBS sobe e o ICMS e o ISS caem gradualmente até 2033; as alíquotas do IBS serão fixadas pelo Senado e pelos entes. Cadastre-as aqui quando publicadas."}, {"ano": 2031, "cbs": 11.3, "ibs_uf": null, "ibs_mun": null, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "2031: o IBS sobe e o ICMS e o ISS caem gradualmente até 2033; as alíquotas do IBS serão fixadas pelo Senado e pelos entes. Cadastre-as aqui quando publicadas."}, {"ano": 2032, "cbs": 11.3, "ibs_uf": null, "ibs_mun": null, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "2032: o IBS sobe e o ICMS e o ISS caem gradualmente até 2033; as alíquotas do IBS serão fixadas pelo Senado e pelos entes. Cadastre-as aqui quando publicadas."}, {"ano": 2033, "cbs": 11.3, "ibs_uf": null, "ibs_mun": null, "pis_cofins": false, "ipi": false, "imposto_seletivo": true, "contabiliza": true, "observacao": "2033: o IBS sobe e o ICMS e o ISS caem gradualmente até 2033; as alíquotas do IBS serão fixadas pelo Senado e pelos entes. Cadastre-as aqui quando publicadas."}], "cst": [{"codigo": "000", "descricao": "Tributação integral", "tributa": true}, {"codigo": "010", "descricao": "Tributação com alíquotas uniformes", "tributa": true}, {"codigo": "011", "descricao": "Tributação com alíquotas uniformes reduzidas", "tributa": true}, {"codigo": "200", "descricao": "Alíquota reduzida", "tributa": true}, {"codigo": "210", "descricao": "Redução de alíquota com redutor de base de cálculo", "tributa": true}, {"codigo": "220", "descricao": "Alíquota fixa", "tributa": true}, {"codigo": "221", "descricao": "Alíquota fixa proporcional", "tributa": true}, {"codigo": "222", "descricao": "Redução de base de cálculo", "tributa": true}, {"codigo": "400", "descricao": "Isenção", "tributa": false}, {"codigo": "410", "descricao": "Imunidade e não incidência", "tributa": false}, {"codigo": "510", "descricao": "Diferimento", "tributa": false}, {"codigo": "515", "descricao": "Diferimento com redução de alíquota", "tributa": false}, {"codigo": "550", "descricao": "Suspensão", "tributa": false}, {"codigo": "620", "descricao": "Tributação monofásica", "tributa": false}, {"codigo": "800", "descricao": "Transferência de crédito", "tributa": false}, {"codigo": "810", "descricao": "Ajustes", "tributa": false}, {"codigo": "820", "descricao": "Tributação em declaração de regime específico", "tributa": false}, {"codigo": "830", "descricao": "Exclusão da base de cálculo", "tributa": false}]}'::jsonb,
        'EC 132/2023; LC 214/2025: CBS plena e PIS/COFINS extintos a partir de 2027; IBS de transição; Imposto Seletivo')
ON CONFLICT (chave, vigencia_inicio) DO NOTHING;
