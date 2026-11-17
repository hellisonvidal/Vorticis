-- =====================================================================
-- Simples Nacional a partir de 2027 (LC 214/2025, LC 227/2026, Res. CGSN 186/2026, 190/2026 e 194/2026)
--
-- * fiscal.opcao_ibscbs_simples: histórico, por empresa, da opção do semestre pelo IBS/CBS "dentro do DAS" ou pelo "regime regular"
--   (IBS/CBS recolhidos fora do DAS, com débitos e créditos). A opção vale até ser renunciada: a vigência começa em 1º/1 ou 1º/7.
-- * fiscal.documento.emitente_simples: o emitente da nota é optante do Simples (CRT 1 ou 2 da NF-e); NULL = não informado.
--   Usado para separar, na apuração do IBS/CBS, o crédito de fornecedores do Simples.
-- * core.parametro_legal SIMPLES_NACIONAL (vigência 2027-01-01): mesmas faixas e alíquotas, repartição com CBS e IBS no lugar de
--   PIS/COFINS, limites (sublimite com IBS, exportação, excesso de 20%, início de atividade). A REPARTIÇÃO É SEMENTE: conferir com a
--   Resolução CGSN 190/2026 (anexos) e ajustar na tela de parâmetros legais.
-- * core.parametro_legal SIMPLES_OPCAO_IBSCBS: janelas de opção e cancelamento (datas como dado, não como código).
-- =====================================================================

CREATE TABLE fiscal.opcao_ibscbs_simples (
    id              BIGSERIAL     PRIMARY KEY,
    empresa_id      BIGINT        NOT NULL REFERENCES core.empresa (id) ON DELETE CASCADE,
    vigencia_inicio DATE          NOT NULL CHECK (extract(day FROM vigencia_inicio) = 1 AND extract(month FROM vigencia_inicio) IN (1, 7)),
    vigencia_fim    DATE,
    opcao           VARCHAR(20)   NOT NULL CHECK (opcao IN ('DENTRO_DO_DAS', 'REGIME_REGULAR')),
    data_pedido     DATE,
    protocolo       VARCHAR(60),
    observacao      VARCHAR(500),
    criado_por      BIGINT        REFERENCES core.usuario (id),
    criado_em       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT ck_opcao_ibscbs_vigencia CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio),
    CONSTRAINT uq_opcao_ibscbs_simples UNIQUE (empresa_id, vigencia_inicio)
);
CREATE TRIGGER trg_auditoria AFTER INSERT OR UPDATE OR DELETE ON fiscal.opcao_ibscbs_simples FOR EACH ROW EXECUTE FUNCTION core.fn_auditoria();

ALTER TABLE fiscal.documento ADD COLUMN emitente_simples BOOLEAN;

INSERT INTO core.parametro_legal (chave, vigencia_inicio, valor, fundamento_legal) VALUES
('SIMPLES_NACIONAL', DATE '2027-01-01', $json${
 "vigencia_inicio": "2027-01-01",
 "fundamento": "LC 123/2006 (Anexos I a V, alíquotas e faixas inalteradas) com a repartição do DAS a partir de 01/01/2027 (CBS no lugar de PIS/COFINS; IBS): LC 214/2025, LC 227/2026, Res. CGSN 186/2026, 190/2026 e 194/2026",
 "limite_rbt12": 4800000,
 "sublimite_icms_iss": 3600000,
 "limite_fator_r": 0.28,
 "minimo_das": 10,
 "reparticao_a_conferir": true,
 "observacao": "CONFERIR com a Resolução CGSN 190/2026 (anexos): os percentuais de repartição abaixo foram semeados da repartição de 2018, com PIS + COFINS somados na CBS e IBS em 0 (em 2027 e 2028 o IBS é de teste). Ajuste faixa a faixa em Cadastros > Parâmetros legais, chave SIMPLES_NACIONAL, vigência 2027.",
 "limite_exportacao": 3600000,
 "excesso_pct": 20,
 "limite_inicio_atividade_mensal": 300000,
 "sublimite_inclui_ibs": true,
 "receita_exclui_ibscbs_regime_regular": true,
 "anexos": {
  "I": [
   {
    "ate": 180000,
    "aliquota": 4,
    "deduzir": 0,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 15.5,
     "IBS": 0,
     "CPP": 41.5,
     "ICMS": 34
    }
   },
   {
    "ate": 360000,
    "aliquota": 7.3,
    "deduzir": 5940,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 15.5,
     "IBS": 0,
     "CPP": 41.5,
     "ICMS": 34
    }
   },
   {
    "ate": 720000,
    "aliquota": 9.5,
    "deduzir": 13860,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 15.5,
     "IBS": 0,
     "CPP": 42,
     "ICMS": 33.5
    }
   },
   {
    "ate": 1800000,
    "aliquota": 10.7,
    "deduzir": 22500,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 15.5,
     "IBS": 0,
     "CPP": 42,
     "ICMS": 33.5
    }
   },
   {
    "ate": 3600000,
    "aliquota": 14.3,
    "deduzir": 87300,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 15.5,
     "IBS": 0,
     "CPP": 42,
     "ICMS": 33.5
    }
   },
   {
    "ate": 4800000,
    "aliquota": 19,
    "deduzir": 378000,
    "reparticao": {
     "IRPJ": 13.5,
     "CSLL": 10,
     "CBS": 34.4,
     "IBS": 0,
     "CPP": 42.1,
     "ICMS": 0
    }
   }
  ],
  "II": [
   {
    "ate": 180000,
    "aliquota": 4.5,
    "deduzir": 0,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 14.0,
     "IBS": 0,
     "CPP": 37.5,
     "IPI": 7.5,
     "ICMS": 32
    }
   },
   {
    "ate": 360000,
    "aliquota": 7.8,
    "deduzir": 5940,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 14.0,
     "IBS": 0,
     "CPP": 37.5,
     "IPI": 7.5,
     "ICMS": 32
    }
   },
   {
    "ate": 720000,
    "aliquota": 10,
    "deduzir": 13860,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 14.0,
     "IBS": 0,
     "CPP": 37.5,
     "IPI": 7.5,
     "ICMS": 32
    }
   },
   {
    "ate": 1800000,
    "aliquota": 11.2,
    "deduzir": 22500,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 14.0,
     "IBS": 0,
     "CPP": 37.5,
     "IPI": 7.5,
     "ICMS": 32
    }
   },
   {
    "ate": 3600000,
    "aliquota": 14.7,
    "deduzir": 85500,
    "reparticao": {
     "IRPJ": 5.5,
     "CSLL": 3.5,
     "CBS": 14.0,
     "IBS": 0,
     "CPP": 37.5,
     "IPI": 7.5,
     "ICMS": 32
    }
   },
   {
    "ate": 4800000,
    "aliquota": 30,
    "deduzir": 720000,
    "reparticao": {
     "IRPJ": 8.5,
     "CSLL": 7.5,
     "CBS": 25.5,
     "IBS": 0,
     "CPP": 23.5,
     "IPI": 35,
     "ICMS": 0
    }
   }
  ],
  "III": [
   {
    "ate": 180000,
    "aliquota": 6,
    "deduzir": 0,
    "reparticao": {
     "IRPJ": 4,
     "CSLL": 3.5,
     "CBS": 15.6,
     "IBS": 0,
     "CPP": 43.4,
     "ISS": 33.5
    }
   },
   {
    "ate": 360000,
    "aliquota": 11.2,
    "deduzir": 9360,
    "reparticao": {
     "IRPJ": 4,
     "CSLL": 3.5,
     "CBS": 17.1,
     "IBS": 0,
     "CPP": 43.4,
     "ISS": 32
    }
   },
   {
    "ate": 720000,
    "aliquota": 13.5,
    "deduzir": 17640,
    "reparticao": {
     "IRPJ": 4,
     "CSLL": 3.5,
     "CBS": 16.6,
     "IBS": 0,
     "CPP": 43.4,
     "ISS": 32.5
    }
   },
   {
    "ate": 1800000,
    "aliquota": 16,
    "deduzir": 35640,
    "reparticao": {
     "IRPJ": 4,
     "CSLL": 3.5,
     "CBS": 16.6,
     "IBS": 0,
     "CPP": 43.4,
     "ISS": 32.5
    }
   },
   {
    "ate": 3600000,
    "aliquota": 21,
    "deduzir": 125640,
    "reparticao": {
     "IRPJ": 4,
     "CSLL": 3.5,
     "CBS": 15.6,
     "IBS": 0,
     "CPP": 43.4,
     "ISS": 33.5
    }
   },
   {
    "ate": 4800000,
    "aliquota": 33,
    "deduzir": 648000,
    "reparticao": {
     "IRPJ": 35,
     "CSLL": 15,
     "CBS": 19.5,
     "IBS": 0,
     "CPP": 30.5,
     "ISS": 0
    }
   }
  ],
  "IV": [
   {
    "ate": 180000,
    "aliquota": 4.5,
    "deduzir": 0,
    "reparticao": {
     "IRPJ": 18.8,
     "CSLL": 15.2,
     "CBS": 21.5,
     "IBS": 0,
     "ISS": 44.5
    }
   },
   {
    "ate": 360000,
    "aliquota": 9,
    "deduzir": 8100,
    "reparticao": {
     "IRPJ": 19.8,
     "CSLL": 15.2,
     "CBS": 25.0,
     "IBS": 0,
     "ISS": 40
    }
   },
   {
    "ate": 720000,
    "aliquota": 10.2,
    "deduzir": 12420,
    "reparticao": {
     "IRPJ": 20.8,
     "CSLL": 15.2,
     "CBS": 24.0,
     "IBS": 0,
     "ISS": 40
    }
   },
   {
    "ate": 1800000,
    "aliquota": 14,
    "deduzir": 39780,
    "reparticao": {
     "IRPJ": 17.8,
     "CSLL": 19.2,
     "CBS": 23.0,
     "IBS": 0,
     "ISS": 40
    }
   },
   {
    "ate": 3600000,
    "aliquota": 22,
    "deduzir": 183780,
    "reparticao": {
     "IRPJ": 18.8,
     "CSLL": 19.2,
     "CBS": 22.0,
     "IBS": 0,
     "ISS": 40
    }
   },
   {
    "ate": 4800000,
    "aliquota": 33,
    "deduzir": 828000,
    "reparticao": {
     "IRPJ": 53.5,
     "CSLL": 21.5,
     "CBS": 25.0,
     "IBS": 0,
     "ISS": 0
    }
   }
  ],
  "V": [
   {
    "ate": 180000,
    "aliquota": 15.5,
    "deduzir": 0,
    "reparticao": {
     "IRPJ": 25,
     "CSLL": 15,
     "CBS": 17.15,
     "IBS": 0,
     "CPP": 28.85,
     "ISS": 14
    }
   },
   {
    "ate": 360000,
    "aliquota": 18,
    "deduzir": 4500,
    "reparticao": {
     "IRPJ": 23,
     "CSLL": 15,
     "CBS": 17.15,
     "IBS": 0,
     "CPP": 27.85,
     "ISS": 17
    }
   },
   {
    "ate": 720000,
    "aliquota": 19.5,
    "deduzir": 9900,
    "reparticao": {
     "IRPJ": 24,
     "CSLL": 15,
     "CBS": 18.15,
     "IBS": 0,
     "CPP": 23.85,
     "ISS": 19
    }
   },
   {
    "ate": 1800000,
    "aliquota": 20.5,
    "deduzir": 17100,
    "reparticao": {
     "IRPJ": 21,
     "CSLL": 15,
     "CBS": 19.15,
     "IBS": 0,
     "CPP": 23.85,
     "ISS": 21
    }
   },
   {
    "ate": 3600000,
    "aliquota": 23,
    "deduzir": 62100,
    "reparticao": {
     "IRPJ": 23,
     "CSLL": 12.5,
     "CBS": 17.15,
     "IBS": 0,
     "CPP": 23.85,
     "ISS": 23.5
    }
   },
   {
    "ate": 4800000,
    "aliquota": 30.5,
    "deduzir": 540000,
    "reparticao": {
     "IRPJ": 35,
     "CSLL": 15.5,
     "CBS": 20.0,
     "IBS": 0,
     "CPP": 29.5,
     "ISS": 0
    }
   }
  ]
 }
}$json$::jsonb,
'LC 123/2006 (Anexos I a V, alíquotas e faixas inalteradas) com a repartição do DAS a partir de 01/01/2027 (CBS no lugar de PIS/COFINS; IBS): LC 214/2025, LC 227/2026, Res. CGSN 186/2026, 190/2026 e 194/2026')
ON CONFLICT (chave, vigencia_inicio) DO NOTHING;

INSERT INTO core.parametro_legal (chave, vigencia_inicio, valor, fundamento_legal) VALUES
('SIMPLES_OPCAO_IBSCBS', DATE '2026-01-01', $json${
 "vigencia_inicio": "2026-01-01",
 "fundamento": "LC 214/2025; LC 227/2026; Res. CGSN 186/2026, 190/2026 e 194/2026 (opção do semestre pelo IBS/CBS dentro do DAS ou pelo regime regular)",
 "padrao": "DENTRO_DO_DAS",
 "observacao": "CONFERIR as janelas com a Resolução CGSN 190/2026 e 194/2026 antes de cada semestre. Edite as datas em Cadastros > Parâmetros legais, chave SIMPLES_OPCAO_IBSCBS.",
 "regras": [
  {
   "semestre": 1,
   "opcao_de": {
    "mes": 9,
    "dia": 1,
    "ano_relativo": -1
   },
   "opcao_ate": {
    "mes": 9,
    "dia": 30,
    "ano_relativo": -1
   },
   "cancelamento_de": null,
   "cancelamento_ate": null,
   "descricao": "1º semestre (1º/1 a 30/6): opção em setembro do ano anterior; cancelamento do 1º semestre ainda sem janela cadastrada (conferir)."
  },
  {
   "semestre": 2,
   "opcao_de": {
    "mes": 3,
    "dia": 1,
    "ano_relativo": 0
   },
   "opcao_ate": {
    "mes": 3,
    "dia": 31,
    "ano_relativo": 0
   },
   "cancelamento_de": {
    "mes": 3,
    "dia": 1,
    "ano_relativo": 0
   },
   "cancelamento_ate": {
    "mes": 5,
    "dia": 31,
    "ano_relativo": 0
   },
   "descricao": "2º semestre (1º/7 a 31/12): opção de 1º a 31 de março; efeitos de 1º/7; cancelamento até 31 de maio."
  }
 ],
 "excecoes": [
  {
   "ano": 2027,
   "semestre": 1,
   "opcao_simples_ate": "2026-10-15",
   "opcao_regime_regular_de": "2026-09-01",
   "opcao_regime_regular_ate": "2026-10-30",
   "cancelamento_de": "2026-11-03",
   "cancelamento_ate": "2026-12-20",
   "fundamento": "Res. CGSN 194/2026 (DOU 28/09/2026)",
   "observacao": "Prazos prorrogados para o 1º semestre de 2027. O início do prazo da opção pelo regime regular (01/09/2026) deve ser conferido."
  }
 ],
 "abertura_cnpj": {
  "de": "2026-10-01",
  "ate": "2026-12-31",
  "observacao": "Empresas abertas de 01/10 a 31/12/2026 escolhem na inscrição; o pedido não precisa estar nas janelas."
 }
}$json$::jsonb,
'LC 214/2025; LC 227/2026; Res. CGSN 186/2026, 190/2026 e 194/2026 (opção do semestre pelo IBS/CBS dentro do DAS ou pelo regime regular)')
ON CONFLICT (chave, vigencia_inicio) DO NOTHING;
