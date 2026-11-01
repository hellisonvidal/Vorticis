-- Reforma tributária do consumo (EC 132/2023, LC 214/2025): a NF-e passa a trazer, por item, o grupo IBSCBS (CST e classificação
-- tributária, base, IBS da UF, IBS do município e CBS). O Fiscal guarda esses valores na importação do XML para a apuração
-- assistida de IBS e CBS (débitos das saídas − créditos das entradas). As alíquotas de referência por ano ficam em
-- core.parametro_legal (chave REFORMA_IBS_CBS, semeada pelo programa).

ALTER TABLE fiscal.documento_item
    ADD COLUMN cst_ibscbs      VARCHAR(3),
    ADD COLUMN c_class_trib    VARCHAR(6),
    ADD COLUMN vl_bc_ibscbs    NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN aliq_ibs_uf     NUMERIC(7,4)  NOT NULL DEFAULT 0,
    ADD COLUMN vl_ibs_uf       NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN aliq_ibs_mun    NUMERIC(7,4)  NOT NULL DEFAULT 0,
    ADD COLUMN vl_ibs_mun      NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN aliq_cbs        NUMERIC(7,4)  NOT NULL DEFAULT 0,
    ADD COLUMN vl_cbs          NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN tem_ibscbs      BOOLEAN       NOT NULL DEFAULT FALSE;
