using System.IO.Compression;
using System.Text;
using Vorticis.Folha.Esocial.Importacao;

namespace Vorticis.Folha.Tests;

/// <summary>Eventos do eSocial de exemplo (empresa de CNPJ 12345678000195) como baixados do portal ou exportados por outro sistema.</summary>
internal static class AmostrasEsocial
{
    public const string Cnpj = "12345678000195";
    public const string CpfMaria = "52998224725";
    public const string CpfJoao = "11144477735";

    private static int _seq;

    public static string Id(DateTime momento) => $"ID1{Cnpj}{momento:yyyyMMddHHmmss}{++_seq % 100000:00000}";

    public static string Evento(string nome, string id, string corpo, string versao = "v_S_01_03_00")
        => $"""<?xml version="1.0" encoding="UTF-8"?><eSocial xmlns="http://www.esocial.gov.br/schema/evt/{nome}/{versao}"><{nome} Id="{id}">{corpo}</{nome}></eSocial>""";

    /// <summary>Evento dentro do envelope de retorno com o recibo (como no download do portal): o recibo está no elemento com o mesmo Id.</summary>
    public static string ComRecibo(string nome, string id, string corpo, string recibo)
        => $"""
            <?xml version="1.0" encoding="UTF-8"?><retornoDownload><arquivo><evento Id="{id}">{Evento(nome, id, corpo).Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "")}</evento>
            <retornoEvento Id="{id}"><eSocial xmlns="http://www.esocial.gov.br/schema/evt/retornoEvento/v1_2_1"><retornoEvento Id="{id}"><processamento><cdResposta>201</cdResposta></processamento><recibo><nrRecibo>{recibo}</nrRecibo><hash>abc</hash></recibo></retornoEvento></eSocial></retornoEvento></arquivo></retornoDownload>
            """;

    private const string IdeEmp = "<ideEmpregador><tpInsc>1</tpInsc><nrInsc>12345678</nrInsc></ideEmpregador>";

    public static string S1000(DateTime t) => Evento("evtInfoEmpregador", Id(t),
        $"<ideEvento><tpAmb>1</tpAmb><procEmi>1</procEmi><verProc>x</verProc></ideEvento>{IdeEmp}<infoEmpregador><inclusao><idePeriodo><iniValid>2020-01</iniValid></idePeriodo><infoCadastro><nmRazao>EMPRESA ORIGEM</nmRazao><classTrib>99</classTrib></infoCadastro></inclusao></infoEmpregador>");

    public static string S1005(DateTime t) => Evento("evtTabEstab", Id(t),
        $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<infoEstab><inclusao><ideEstab><tpInsc>1</tpInsc><nrInsc>{Cnpj}</nrInsc><iniValid>2020-01</iniValid></ideEstab><dadosEstab><cnaePrep>4751201</cnaePrep><aliqGilrat><aliqRat>3</aliqRat><fap>1.2000</fap></aliqGilrat></dadosEstab></inclusao></infoEstab>");

    public static string S1010(DateTime t, string cod, string desc, string nat, int tp, string cp, string irrf, string fgts, string operacao = "inclusao")
        => Evento("evtTabRubrica", Id(t),
            $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<infoRubrica><{operacao}><ideRubrica><codRubr>{cod}</codRubr><ideTabRubr>TAB1</ideTabRubr><iniValid>2020-01</iniValid></ideRubrica>" +
            $"<dadosRubrica><dscRubr>{desc}</dscRubr><natRubr>{nat}</natRubr><tpRubr>{tp}</tpRubr><codIncCP>{cp}</codIncCP><codIncIRRF>{irrf}</codIncIRRF><codIncFGTS>{fgts}</codIncFGTS></dadosRubrica></{operacao}></infoRubrica>");

    public static string S1020(DateTime t) => Evento("evtTabLotacao", Id(t),
        $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<infoLotacao><inclusao><ideLotacao><codLotacao>LOT01</codLotacao><iniValid>2020-01</iniValid></ideLotacao><dadosLotacao><tpLotacao>01</tpLotacao><fpasLotacao><fpas>515</fpas><codTercs>0079</codTercs></fpasLotacao></dadosLotacao></inclusao></infoLotacao>");

    public static string S2200(DateTime t, string cpf, string nome, string matricula, string adm, decimal salario, string cargo, string nasc = "1990-05-20", string sexo = "F", string extra = "")
        => Evento("evtAdmissao", Id(t),
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<trabalhador><cpfTrab>{cpf}</cpfTrab><nmTrab>{nome}</nmTrab><sexo>{sexo}</sexo><racaCor>1</racaCor><estCiv>2</estCiv><grauInstr>09</grauInstr>" +
            $"<nascimento><dtNascto>{nasc}</dtNascto><paisNascto>105</paisNascto><paisNac>105</paisNac></nascimento>" +
            "<endereco><brasil><tpLograd>R</tpLograd><dscLograd>Rua das Flores</dscLograd><nrLograd>10</nrLograd><bairro>Centro</bairro><cep>01310100</cep><codMunic>3550308</codMunic><uf>SP</uf></brasil></endereco>" +
            $"{extra}<contato><fonePrinc>11999998888</fonePrinc><emailPrinc>func@exemplo.com.br</emailPrinc></contato></trabalhador>" +
            $"<vinculo><matricula>{matricula}</matricula><tpRegTrab>1</tpRegTrab><tpRegPrev>1</tpRegPrev><cadIni>N</cadIni><infoRegimeTrab><infoCeletista><dtAdm>{adm}</dtAdm><tpAdmissao>1</tpAdmissao><indAdmissao>1</indAdmissao><tpRegJor>1</tpRegJor><natAtividade>1</natAtividade><cnpjSindCategProf>11222333000181</cnpjSindCategProf></infoCeletista></infoRegimeTrab>" +
            $"<infoContrato><nmCargo>{cargo}</nmCargo><CBOCargo>252105</CBOCargo><codCateg>101</codCateg><remuneracao><vrSalFx>{salario:0.00}</vrSalFx><undSalFixo>5</undSalFixo></remuneracao><duracao><tpContr>1</tpContr></duracao>" +
            "<localTrabalho><localTrabGeral><tpInsc>1</tpInsc><nrInsc>" + Cnpj + "</nrInsc></localTrabGeral></localTrabalho><horContratual><qtdHrsSem>44</qtdHrsSem><tpJornada>9</tpJornada><tmpParc>0</tmpParc><dscJorn>Segunda a sexta</dscJorn></horContratual></infoContrato></vinculo>");

    public const string Filho = "<dependente><tpDep>03</tpDep><nmDep>Pedro Filho</nmDep><dtNascto>2015-03-10</dtNascto><cpfDep>12345678909</cpfDep><depIRRF>S</depIRRF><depSF>S</depSF><incTrab>N</incTrab></dependente>";

    public static string S2206(DateTime t, string cpf, string matricula, string data, decimal salario, string? cargo = null)
        => Evento("evtAltContratual", Id(t),
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<ideVinculo><cpfTrab>{cpf}</cpfTrab><matricula>{matricula}</matricula></ideVinculo>" +
            $"<altContratual><dtAlteracao>{data}</dtAlteracao><dscAlt>Reajuste</dscAlt><vinculo><tpRegPrev>1</tpRegPrev><infoContrato>{(cargo is null ? "" : $"<nmCargo>{cargo}</nmCargo>")}<codCateg>101</codCateg><remuneracao><vrSalFx>{salario:0.00}</vrSalFx><undSalFixo>5</undSalFixo></remuneracao></infoContrato></vinculo></altContratual>");

    public static string S2230Inicio(DateTime t, string cpf, string matricula, string ini, string motivo)
        => Evento("evtAfastTemp", Id(t),
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<ideVinculo><cpfTrab>{cpf}</cpfTrab><matricula>{matricula}</matricula></ideVinculo><infoAfastamento><iniAfastamento><dtIniAfast>{ini}</dtIniAfast><codMotAfast>{motivo}</codMotAfast></iniAfastamento></infoAfastamento>");

    public static string S2230Fim(DateTime t, string cpf, string matricula, string fim)
        => Evento("evtAfastTemp", Id(t),
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<ideVinculo><cpfTrab>{cpf}</cpfTrab><matricula>{matricula}</matricula></ideVinculo><infoAfastamento><fimAfastamento><dtTermAfast>{fim}</dtTermAfast></fimAfastamento></infoAfastamento>");

    public static string S2299(DateTime t, string cpf, string matricula, string data, string motivo = "02")
        => Evento("evtDeslig", Id(t),
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<ideVinculo><cpfTrab>{cpf}</cpfTrab><matricula>{matricula}</matricula></ideVinculo><infoDeslig><mtvDeslig>{motivo}</mtvDeslig><dtDeslig>{data}</dtDeslig><indPagtoAPI>N</indPagtoAPI></infoDeslig>");

    /// <summary>S-1200 com os itens (rubrica, quantidade, valor).</summary>
    public static (string Id, string Xml) S1200(DateTime t, string cpf, string matricula, string perApur, params (string Rubrica, decimal? Qtd, decimal Valor)[] itens)
        => S1200Completo(t, cpf, matricula, perApur, 1, null, null, "MENSAL", itens);

    public static (string Id, string Xml) S1200Completo(DateTime t, string cpf, string matricula, string perApur, int indApuracao, int? indRetif, string? reciboAnterior, string dm,
                                                         params (string Rubrica, decimal? Qtd, decimal Valor)[] itens)
    {
        var id = Id(t);
        var linhas = string.Concat(itens.Select(i => $"<itensRemun><codRubr>{i.Rubrica}</codRubr><ideTabRubr>TAB1</ideTabRubr>{(i.Qtd is { } q ? $"<qtdRubr>{q:0.##}</qtdRubr>" : "")}<vrRubr>{i.Valor:0.00}</vrRubr></itensRemun>"));
        var xml = Evento("evtRemun", id,
            $"<ideEvento><indRetif>{indRetif ?? 1}</indRetif>{(reciboAnterior is null ? "" : $"<nrRecibo>{reciboAnterior}</nrRecibo>")}<indApuracao>{indApuracao}</indApuracao><perApur>{perApur}</perApur><tpAmb>1</tpAmb></ideEvento>{IdeEmp}" +
            $"<ideTrabalhador><cpfTrab>{cpf}</cpfTrab></ideTrabalhador><dmDev><ideDmDev>{dm}</ideDmDev><codCateg>101</codCateg><infoPerApur><ideEstabLot><tpInsc>1</tpInsc><nrInsc>{Cnpj}</nrInsc><codLotacao>LOT01</codLotacao>" +
            $"<remunPerApur><matricula>{matricula}</matricula>{linhas}</remunPerApur></ideEstabLot></infoPerApur></dmDev>");
        return (id, xml);
    }

    public static string S1210(DateTime t, string cpf, string perApur, string dtPgto, string perRef, decimal liquido)
        => Evento("evtPgtos", Id(t),
            $"<ideEvento><indRetif>1</indRetif><perApur>{perApur}</perApur><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<ideBenef><cpfBenef>{cpf}</cpfBenef><infoPgto><dtPgto>{dtPgto}</dtPgto><tpPgto>1</tpPgto><perRef>{perRef}</perRef><ideDmDev>MENSAL</ideDmDev><vrLiq>{liquido:0.00}</vrLiq></infoPgto></ideBenef>");

    public static string S3000(DateTime t, string tpEvento, string recibo, string cpf, string perApur)
        => Evento("evtExclusao", Id(t),
            $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{IdeEmp}<infoExclusao><tpEvento>{tpEvento}</tpEvento><nrRecEvt>{recibo}</nrRecEvt><ideTrabalhador><cpfTrab>{cpf}</cpfTrab></ideTrabalhador><ideFolhaPagto><indApuracao>1</indApuracao><perApur>{perApur}</perApur></ideFolhaPagto></infoExclusao>");

    public static ArquivoEsocial Arq(string nome, string xml) => new(nome, Encoding.UTF8.GetBytes(xml));

    public static byte[] Zip(params ArquivoEsocial[] arquivos)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var a in arquivos)
            {
                var e = z.CreateEntry(a.Nome);
                using var s = e.Open();
                s.Write(a.Conteudo);
            }
        return ms.ToArray();
    }

    /// <summary>Conjunto completo: tabelas, duas admissões, alterações fora de ordem, afastamentos, desligamento, remunerações (com retificação e exclusão) e pagamento.</summary>
    public static List<ArquivoEsocial> Conjunto()
    {
        var t0 = new DateTime(2026, 1, 10, 9, 0, 0);
        DateTime T(int s) => t0.AddSeconds(s);
        var l = new List<ArquivoEsocial>
        {
            Arq("S1000.xml", S1000(T(1))), Arq("S1005.xml", S1005(T(2))), Arq("S1020.xml", S1020(T(3))),
            Arq("S1010_0001.xml", S1010(T(4), "0001", "Salário", "1000", 1, "11", "01", "11")),
            Arq("S1010_0002.xml", S1010(T(5), "0002", "Horas extras 50%", "1003", 1, "11", "01", "11")),
            Arq("S1010_0003.xml", S1010(T(6), "0003", "13º salário", "5001", 1, "12", "09", "12")),
            Arq("S1010_0050.xml", S1010(T(7), "0050", "INSS", "9201", 2, "00", "11", "00")),
            Arq("S1010_0051.xml", S1010(T(8), "0051", "IRRF", "9203", 2, "00", "00", "00")),
            Arq("S2200_maria.xml", S2200(T(10), CpfMaria, "Maria da Silva", "M001", "2023-02-01", 3000m, "Analista contábil", extra: Filho)),
            Arq("S2200_joao.xml", S2200(T(11), CpfJoao, "João Souza", "M002", "2024-03-01", 2000m, "Auxiliar administrativo", "1985-01-02", "M")),
            // alterações fora de ordem: vale a de 2025-01-01 (4.000), com o histórico das duas
            Arq("S2206_b.xml", S2206(T(21), CpfMaria, "M001", "2025-01-01", 4000m)),
            Arq("S2206_a.xml", S2206(T(20), CpfMaria, "M001", "2024-03-01", 3500m, "Analista contábil sênior")),
            Arq("S2230_ferias_ini.xml", S2230Inicio(T(30), CpfMaria, "M001", "2025-01-02", "15")),
            Arq("S2230_ferias_fim.xml", S2230Fim(T(31), CpfMaria, "M001", "2025-01-31")),
            Arq("S2230_doenca_ini.xml", S2230Inicio(T(32), CpfMaria, "M001", "2025-06-10", "01")),
            Arq("S2230_doenca_fim.xml", S2230Fim(T(33), CpfMaria, "M001", "2025-06-20")),
            Arq("S2299_joao.xml", S2299(T(40), CpfJoao, "M002", "2025-02-28")),
        };
        // Maria: janeiro a março de 2025 (a de fevereiro foi retificada: vale a segunda); 13º de 2024 (apuração anual)
        var jan = S1200(T(50), CpfMaria, "M001", "2025-01", ("0001", null, 4000m), ("0002", 10m, 300m), ("0050", null, 400m), ("0051", null, 200m));
        var fev1 = S1200(T(51), CpfMaria, "M001", "2025-02", ("0001", null, 4000m), ("0002", 4m, 100m), ("0050", null, 400m), ("0051", null, 200m));
        var fev2 = S1200Completo(T(60), CpfMaria, "M001", "2025-02", 1, 2, "1.1.0000000000000000001", "MENSAL", ("0001", null, 4000m), ("0002", 10m, 300m), ("0050", null, 400m), ("0051", null, 200m));
        var mar = S1200(T(52), CpfMaria, "M001", "2025-03", ("0001", null, 4000m), ("0002", 10m, 300m), ("0050", null, 400m), ("0051", null, 200m));
        var d13 = S1200Completo(T(53), CpfMaria, "M001", "2024", 2, null, null, "DECIMO", ("0003", null, 3500m), ("0050", null, 300m));
        l.Add(Arq("S1200_maria_2025-01.xml", jan.Xml));
        l.Add(Arq("S1200_maria_2025-02_v1.xml", ComRecibo("evtRemun", fev1.Id, ExtrairCorpo(fev1.Xml, "evtRemun"), "1.1.0000000000000000001")));
        l.Add(Arq("S1200_maria_2025-02_v2.xml", fev2.Xml));
        l.Add(Arq("S1200_maria_2025-03.xml", mar.Xml));
        l.Add(Arq("S1200_maria_2024_13.xml", d13.Xml));
        // João: janeiro (excluída pelo S-3000) e fevereiro
        var joaoJan = S1200(T(54), CpfJoao, "M002", "2025-01", ("0001", null, 2000m), ("0050", null, 150m));
        var joaoFev = S1200(T(55), CpfJoao, "M002", "2025-02", ("0001", null, 2000m), ("0050", null, 150m));
        l.Add(Arq("S1200_joao_2025-01.xml", ComRecibo("evtRemun", joaoJan.Id, ExtrairCorpo(joaoJan.Xml, "evtRemun"), "1.1.0000000000000000077")));
        l.Add(Arq("S1200_joao_2025-02.xml", joaoFev.Xml));
        l.Add(Arq("S3000_joao_2025-01.xml", S3000(T(70), "S1200", "1.1.0000000000000000077", CpfJoao, "2025-01")));
        l.Add(Arq("S1210_maria_2025-01.xml", S1210(T(80), CpfMaria, "2025-01", "2025-02-05", "2025-01", 3500m)));
        return l;
    }

    private static string ExtrairCorpo(string xml, string nome)
    {
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        var evt = doc.Descendants().First(e => e.Name.LocalName == nome);
        return string.Concat(evt.Nodes().Select(n => n.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))).Replace(" xmlns=\"http://www.esocial.gov.br/schema/evt/" + nome + "/v_S_01_03_00\"", "");
    }
}
