using System.Xml.Linq;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Folha.Esocial.Importacao;

/// <summary>
/// Transforma os eventos lidos em dados prontos para importar. Regras: eventos excluídos (S-3000) e substituídos por retificação (indRetif 2 com o
/// recibo anterior) saem; de duas versões do mesmo fato (mesmo trabalhador, período, data) vale a mais recente; o que sobra é aplicado em ordem
/// cronológica (tabelas, depois eventos não periódicos pela data do fato, depois S-1200/S-1210 por período).
/// </summary>
public static class ConsolidadorEsocial
{
    public static DadosEsocial Consolidar(IReadOnlyList<ArquivoEsocial> arquivos)
    {
        var dados = new DadosEsocial { Arquivos = arquivos.Count, Sha256 = LeitorEsocial.Impressao(arquivos) };
        var (eventos, naoSuportados) = LeitorEsocial.Ler(arquivos, dados.Ocorrencias);
        foreach (var (nome, qtd) in naoSuportados.OrderBy(x => x.Key))
            dados.Ocorrencias.Add(OcorrenciaImportacao.Info(0, nome, $"{qtd} evento(s) {nome} não são importados por esta tela (ignorados)."));
        Consolidar(eventos, dados);
        return dados;
    }

    internal static void Consolidar(List<EventoLido> eventos, DadosEsocial dados)
    {
        var descartados = new HashSet<string>(StringComparer.Ordinal);
        var porRecibo = eventos.Where(e => e.Recibo is not null).GroupBy(e => e.Recibo!).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Momento).First());

        // S-3000: exclui o evento que tem o recibo informado.
        foreach (var ex in eventos.Where(e => e.Tipo == "S-3000"))
        {
            descartados.Add(ex.Id);
            var recibo = ex.Evento.Cam("infoExclusao").Txt("nrRecEvt");
            if (recibo is not null && porRecibo.TryGetValue(recibo, out var alvo))
            {
                if (descartados.Add(alvo.Id)) dados.Excluidos++;
            }
            else
                dados.Ocorrencias.Add(OcorrenciaImportacao.Aviso(0, ex.Rotulo, $"Exclusão (S-3000) do recibo {recibo ?? "?"}: o evento excluído não está nos arquivos."));
        }

        // Retificação: o evento retificador substitui o do recibo anterior.
        foreach (var r in eventos.Where(e => e.IndRetif == 2 && e.ReciboAnterior is not null && !descartados.Contains(e.Id)))
            if (porRecibo.TryGetValue(r.ReciboAnterior!, out var anterior) && anterior.Id != r.Id && descartados.Add(anterior.Id))
                dados.VersoesAnterioresIgnoradas++;

        // Mesmo fato enviado mais de uma vez: vale o mais recente.
        var restantes = eventos.Where(e => !descartados.Contains(e.Id)).ToList();
        foreach (var grupo in restantes.Select(e => (Evento: e, Chave: Chave(e))).Where(x => x.Chave is not null).GroupBy(x => x.Chave!))
        {
            var ordenados = grupo.OrderByDescending(x => x.Evento.Momento).ThenByDescending(x => x.Evento.Recibo, StringComparer.Ordinal).ToList();
            foreach (var velho in ordenados.Skip(1)) { descartados.Add(velho.Evento.Id); dados.VersoesAnterioresIgnoradas++; }
        }

        var aplicar = eventos.Where(e => !descartados.Contains(e.Id))
            .Select(e => (Evento: e, Data: DataDoFato(e)))
            .OrderBy(x => Grupo(x.Evento.Tipo)).ThenBy(x => x.Data).ThenBy(x => x.Evento.Momento).ThenBy(x => x.Evento.Id, StringComparer.Ordinal)
            .ToList();
        var infoPerAnt = 0;
        foreach (var (e, _) in aplicar)
        {
            try
            {
                if (Xe.Digitos(e.Evento.Cam("ideEmpregador").Txt("nrInsc")) is { Length: >= 8 } emp) dados.Empregadores.Add(emp[..8]);
                Aplicar(e, dados, ref infoPerAnt);
                dados.Eventos[e.Tipo] = dados.Eventos.GetValueOrDefault(e.Tipo) + 1;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException or ArgumentException)
            {
                dados.Ocorrencias.Add(OcorrenciaImportacao.ErroPontual(0, e.Rotulo, $"Evento fora do leiaute esperado ({e.Arquivo}): {ex.Message}"));
            }
        }
        if (infoPerAnt > 0)
            dados.Ocorrencias.Add(OcorrenciaImportacao.Info(0, "S-1200", $"{infoPerAnt} remuneração(ões) de períodos anteriores (infoPerAnt) não são importadas."));
    }

    private static int Grupo(string tipo) => tipo switch { "S-1000" or "S-1005" or "S-1010" or "S-1020" => 0, "S-1200" or "S-1210" => 2, _ => 1 };

    private static string? Chave(EventoLido e)
    {
        var x = e.Evento;
        var cpf = Xe.Digitos(x.Cam("ideTrabalhador").Txt("cpfTrab") ?? x.Cam("ideVinculo").Txt("cpfTrab") ?? x.Cam("ideBenef").Txt("cpfBenef") ?? x.Cam("trabalhador").Txt("cpfTrab")
                             ?? x.Cam("ideTrabSemVinculo").Txt("cpfTrab"));
        var mat = x.Cam("ideVinculo").Txt("matricula") ?? x.Cam("vinculo").Txt("matricula") ?? "";
        return e.Tipo switch
        {
            "S-1200" => $"{e.Tipo}|{cpf}|{x.Cam("ideEvento").Txt("indApuracao") ?? "1"}|{x.Cam("ideEvento").Txt("perApur")}",
            "S-1210" => $"{e.Tipo}|{cpf}|{x.Cam("ideEvento").Txt("perApur")}",
            "S-2200" => $"{e.Tipo}|{cpf}|{mat}",
            "S-2299" => $"{e.Tipo}|{cpf}|{mat}",
            "S-2206" => $"{e.Tipo}|{cpf}|{mat}|{x.Cam("altContratual").Txt("dtAlteracao")}",
            "S-2230" => $"{e.Tipo}|{cpf}|{mat}|{x.Cam("infoAfastamento", "iniAfastamento").Txt("dtIniAfast")}|{x.Cam("infoAfastamento", "fimAfastamento").Txt("dtTermAfast")}",
            "S-2300" => $"{e.Tipo}|{cpf}",
            "S-2399" => $"{e.Tipo}|{cpf}",
            _ => null,
        };
    }

    private static DateOnly DataDoFato(EventoLido e)
    {
        var x = e.Evento;
        DateOnly? d = e.Tipo switch
        {
            "S-1000" => Xe.Periodo(x.Cam("infoEmpregador").Primeiro().Txt("idePeriodo", "iniValid")),
            "S-1005" => Xe.Periodo(x.Cam("infoEstab").Primeiro().Txt("ideEstab", "iniValid")),
            "S-1010" => Xe.Periodo(x.Cam("infoRubrica").Primeiro().Txt("ideRubrica", "iniValid")),
            "S-1020" => Xe.Periodo(x.Cam("infoLotacao").Primeiro().Txt("ideLotacao", "iniValid")),
            "S-2200" => Xe.Dt(x.Desc("dtAdm")?.Value),
            "S-2205" => Xe.Dt(x.Cam("alteracao").Txt("dtAlteracao")),
            "S-2206" => Xe.Dt(x.Cam("altContratual").Txt("dtAlteracao")),
            "S-2230" => Xe.Dt(x.Cam("infoAfastamento", "iniAfastamento").Txt("dtIniAfast")) ?? Xe.Dt(x.Cam("infoAfastamento", "fimAfastamento").Txt("dtTermAfast")),
            "S-2299" => Xe.Dt(x.Cam("infoDeslig").Txt("dtDeslig")),
            "S-2300" => Xe.Dt(x.Desc("dtInicio")?.Value),
            "S-2399" => Xe.Dt(x.Cam("infoTSVTermino").Txt("dtTerm")),
            "S-1200" or "S-1210" => Xe.Periodo(x.Cam("ideEvento").Txt("perApur")),
            _ => null,
        };
        return d ?? DateOnly.MinValue;
    }

    // ------------------------------------------------------------------ aplicação por tipo

    private static void Aplicar(EventoLido e, DadosEsocial dados, ref int infoPerAnt)
    {
        switch (e.Tipo)
        {
            case "S-1000": S1000(e, dados); break;
            case "S-1005": S1005(e, dados); break;
            case "S-1010": S1010(e, dados); break;
            case "S-1020": S1020(e, dados); break;
            case "S-2200": S2200(e, dados); break;
            case "S-2205": S2205(e, dados); break;
            case "S-2206": S2206(e, dados); break;
            case "S-2230": S2230(e, dados); break;
            case "S-2299": S2299(e, dados); break;
            case "S-2300": S2300(e, dados); break;
            case "S-2399": S2399(e, dados); break;
            case "S-1200": S1200(e, dados, ref infoPerAnt); break;
            case "S-1210": S1210(e, dados); break;
        }
    }

    private static void S1000(EventoLido e, DadosEsocial d)
    {
        var x = e.Evento;
        var op = x.Cam("infoEmpregador").Primeiro();
        if (op is null || op.Name.LocalName == "exclusao") return;
        var info = op.Filho("infoCadastro");
        d.Empregador = new EmpregadorEsocial(Xe.Digitos(x.Cam("ideEmpregador").Txt("nrInsc")), info.Txt("nmRazao"), info.Txt("classTrib"));
    }

    private static void S1005(EventoLido e, DadosEsocial d)
    {
        var op = e.Evento.Cam("infoEstab").Primeiro();
        if (op is null || op.Name.LocalName == "exclusao") return;
        var ide = op.Filho("ideEstab");
        var dados = op.Filho("dadosEstab");
        var insc = Xe.Digitos(ide.Txt("nrInsc"));
        d.Estabelecimentos.RemoveAll(s => s.Inscricao == insc);
        d.Estabelecimentos.Add(new EstabelecimentoEsocial(insc, dados.Txt("cnaePrep"), Xe.Num(dados.Cam("aliqGilrat").Txt("aliqRat")), Xe.Num(dados.Cam("aliqGilrat").Txt("fap"))));
    }

    private static void S1010(EventoLido e, DadosEsocial d)
    {
        var op = e.Evento.Cam("infoRubrica").Primeiro();
        if (op is null) return;
        var ide = op.Filho("ideRubrica");
        var cod = ide.Txt("codRubr") ?? throw new FormatException("rubrica sem codRubr");
        var inicio = Xe.Periodo(ide.Txt("iniValid")) ?? DateOnly.MinValue;
        if (op.Name.LocalName == "exclusao")
        {
            if (d.Rubricas.TryGetValue(cod, out var atual)) d.Rubricas[cod] = atual with { Excluida = true };
            return;
        }
        var dr = op.Filho("dadosRubrica") ?? throw new FormatException("rubrica sem dadosRubrica");
        d.Rubricas[cod] = new RubricaEsocial(cod, ide.Txt("ideTabRubr") ?? "", dr.Txt("dscRubr") ?? $"Rubrica {cod}", dr.Txt("natRubr") ?? "9900",
            Xe.Int(dr.Txt("tpRubr")) ?? 1, dr.Txt("codIncCP") ?? "00", dr.Txt("codIncIRRF") ?? "00", dr.Txt("codIncFGTS") ?? "00", inicio, false);
    }

    private static void S1020(EventoLido e, DadosEsocial d)
    {
        var op = e.Evento.Cam("infoLotacao").Primeiro();
        if (op is null) return;
        var cod = op.Txt("ideLotacao", "codLotacao") ?? throw new FormatException("lotação sem codLotacao");
        if (op.Name.LocalName == "exclusao")
        {
            if (d.Lotacoes.TryGetValue(cod, out var atual)) d.Lotacoes[cod] = atual with { Excluida = true };
            return;
        }
        var dl = op.Filho("dadosLotacao");
        d.Lotacoes[cod] = new LotacaoEsocial(cod, dl.Txt("tpLotacao"), dl.Txt("fpasLotacao", "fpas"), dl.Txt("fpasLotacao", "codTercs"), false);
    }

    // ---- trabalhadores

    private static List<DependenteEsocial> LerDependentes(XElement? trab)
        => trab.Lista("dependente").Select(x => new DependenteEsocial(x.Txt("tpDep") ?? "03", x.Txt("nmDep") ?? "", Xe.Dt(x.Txt("dtNascto")) ?? DateOnly.MinValue,
            Xe.Digitos(x.Txt("cpfDep")) is { Length: 11 } c ? c : null, Xe.Sim(x.Txt("depIRRF")), Xe.Sim(x.Txt("depSF")), Xe.Sim(x.Txt("incTrab")))).ToList();

    private static EnderecoEsocial? LerEndereco(XElement? trab)
    {
        var b = trab.Cam("endereco", "brasil");
        return b is null ? null : new EnderecoEsocial(b.Txt("tpLograd"), b.Txt("dscLograd"), b.Txt("nrLograd"), b.Txt("complemento"), b.Txt("bairro"), Xe.Digitos(b.Txt("cep")),
                                                      b.Txt("codMunic"), b.Txt("uf"));
    }

    private static ContratoEsocial LerContrato(XElement? contrato, XElement? vinculo)
        => new(contrato.Txt("nmCargo") ?? contrato.Cam("cargoFuncao").Txt("nmCargo"), contrato.Txt("CBOCargo") ?? contrato.Cam("cargoFuncao").Txt("CBOCargo"), contrato.Txt("codCateg"),
               Xe.Num(contrato.Desc("vrSalFx")?.Value), Xe.Int(contrato.Desc("undSalFixo")?.Value), Xe.Int(contrato.Cam("duracao").Txt("tpContr")),
               Xe.Dt(contrato.Cam("duracao").Txt("dtTerm")), Xe.Num(contrato.Cam("horContratual").Txt("qtdHrsSem")), Xe.Int(contrato.Cam("horContratual").Txt("tpJornada")),
               contrato.Cam("horContratual").Txt("dscJorn") ?? contrato.Cam("horContratual").Txt("dscTpJorn"), Xe.Limpar(vinculo.Desc("cnpjSindCategProf")?.Value));

    private static void PreencherPessoa(TrabalhadorEsocial t, XElement? trab)
    {
        t.Nome = trab.Txt("nmTrab") ?? t.Nome;
        t.Sexo = trab.Txt("sexo") ?? t.Sexo;
        t.RacaCor = Xe.Int(trab.Txt("racaCor")) ?? t.RacaCor;
        t.EstadoCivil = Xe.Int(trab.Txt("estCiv")) ?? t.EstadoCivil;
        t.GrauInstrucao = trab.Txt("grauInstr") ?? t.GrauInstrucao;
        t.Nascimento = Xe.Dt(trab.Txt("nascimento", "dtNascto")) ?? t.Nascimento;
        t.Endereco = LerEndereco(trab) ?? t.Endereco;
        t.Email = trab.Txt("contato", "emailPrinc") ?? t.Email;
        t.Telefone = Xe.Digitos(trab.Txt("contato", "fonePrinc")) is { Length: > 0 } f ? f : t.Telefone;
    }

    private static void S2200(EventoLido e, DadosEsocial d)
    {
        var trab = e.Evento.Filho("trabalhador") ?? throw new FormatException("admissão sem trabalhador");
        var vinc = e.Evento.Filho("vinculo") ?? throw new FormatException("admissão sem vínculo");
        var cpf = Xe.Digitos(trab.Txt("cpfTrab"));
        if (cpf.Length != 11) throw new FormatException("CPF do trabalhador inválido");
        var adm = Xe.Dt(vinc.Desc("dtAdm")?.Value) ?? Xe.Dt(vinc.Desc("dtExercicio")?.Value) ?? throw new FormatException("admissão sem dtAdm");
        var t = new TrabalhadorEsocial { Cpf = cpf, Matricula = vinc.Txt("matricula") ?? cpf, Admissao = adm, IdEvento = e.Id, Arquivo = e.Arquivo };
        PreencherPessoa(t, trab);
        t.Dependentes = LerDependentes(trab);
        t.ContratoInicial = t.Contrato = LerContrato(vinc.Filho("infoContrato"), vinc);
        if (d.Trabalhadores.TryGetValue(cpf, out var antigo) && antigo.Matricula != t.Matricula)
        {
            d.Ocorrencias.Add(OcorrenciaImportacao.Aviso(0, e.Rotulo, antigo.Admissao <= adm
                ? $"CPF {cpf} com mais de um vínculo: vale o mais recente (matrícula {t.Matricula}); o da matrícula {antigo.Matricula} não é importado (o Vorticis guarda um vínculo por CPF)."
                : $"CPF {cpf} com mais de um vínculo: vale o mais recente (matrícula {antigo.Matricula}); o da matrícula {t.Matricula} não é importado."));
            if (antigo.Admissao > adm) return;
        }
        d.Trabalhadores[cpf] = t;
    }

    private static void S2205(EventoLido e, DadosEsocial d)
    {
        var cpf = Xe.Digitos(e.Evento.Cam("ideTrabalhador").Txt("cpfTrab"));
        if (!d.Trabalhadores.TryGetValue(cpf, out var t)) { d.Ocorrencias.Add(OcorrenciaImportacao.Info(0, e.Rotulo, $"Alteração cadastral do CPF {cpf} sem admissão nos arquivos: ignorada.")); return; }
        var dados = e.Evento.Cam("alteracao", "dadosTrabalhador");
        PreencherPessoa(t, dados);
        if (dados.Lista("dependente").Any()) t.Dependentes = LerDependentes(dados);
    }

    private static TrabalhadorEsocial? DoVinculo(EventoLido e, DadosEsocial d, out string cpf)
    {
        var ide = e.Evento.Filho("ideVinculo");
        cpf = Xe.Digitos(ide.Txt("cpfTrab"));
        var mat = ide.Txt("matricula");
        if (!d.Trabalhadores.TryGetValue(cpf, out var t) || (mat is not null && t.Matricula != mat))
        {
            d.Ocorrencias.Add(OcorrenciaImportacao.Info(0, e.Rotulo, $"Evento do CPF {cpf}{(mat is null ? "" : $" matrícula {mat}")} sem admissão correspondente nos arquivos: ignorado."));
            return null;
        }
        return t;
    }

    private static void S2206(EventoLido e, DadosEsocial d)
    {
        if (DoVinculo(e, d, out _) is not { } t) return;
        var alt = e.Evento.Filho("altContratual") ?? throw new FormatException("alteração sem altContratual");
        var data = Xe.Dt(alt.Txt("dtAlteracao")) ?? throw new FormatException("alteração sem dtAlteracao");
        var vinc = alt.Filho("vinculo");
        var novo = LerContrato(vinc.Filho("infoContrato"), vinc);
        t.Alteracoes.Add(new AlteracaoContratoEsocial(data, alt.Txt("dscAlt"), novo, e.Id));
        t.Contrato = t.Contrato.Mesclar(novo);
    }

    private static void S2230(EventoLido e, DadosEsocial d)
    {
        if (DoVinculo(e, d, out _) is not { } t) return;
        var info = e.Evento.Filho("infoAfastamento") ?? throw new FormatException("afastamento sem infoAfastamento");
        if (info.Filho("iniAfastamento") is { } ini)
        {
            var data = Xe.Dt(ini.Txt("dtIniAfast")) ?? throw new FormatException("afastamento sem dtIniAfast");
            var per = ini.Desc("perAquis");
            t.Afastamentos.Add(new AfastamentoEsocial(data, null, ini.Txt("codMotAfast") ?? "99", ini.Txt("observacao"), Xe.Dt(per.Txt("dtInicio")), Xe.Dt(per.Txt("dtFim"))));
        }
        else if (info.Filho("fimAfastamento") is { } fim)
        {
            var termino = Xe.Dt(fim.Txt("dtTermAfast")) ?? throw new FormatException("término sem dtTermAfast");
            var i = t.Afastamentos.FindLastIndex(a => a.Fim is null && a.Inicio <= termino);
            if (i < 0) d.Ocorrencias.Add(OcorrenciaImportacao.Info(0, e.Rotulo, $"Término de afastamento em {termino:dd/MM/yyyy} sem o início nos arquivos: ignorado."));
            else t.Afastamentos[i] = t.Afastamentos[i] with { Fim = termino };
        }
    }

    private static void S2299(EventoLido e, DadosEsocial d)
    {
        if (DoVinculo(e, d, out _) is not { } t) return;
        var info = e.Evento.Filho("infoDeslig") ?? throw new FormatException("desligamento sem infoDeslig");
        t.Desligamento = new DesligamentoEsocial(Xe.Dt(info.Txt("dtDeslig")) ?? throw new FormatException("desligamento sem dtDeslig"), info.Txt("mtvDeslig"));
    }

    private static void S2300(EventoLido e, DadosEsocial d)
    {
        var trab = e.Evento.Filho("trabalhador") ?? throw new FormatException("S-2300 sem trabalhador");
        var info = e.Evento.Filho("infoTSVInicio") ?? throw new FormatException("S-2300 sem infoTSVInicio");
        var cpf = Xe.Digitos(trab.Txt("cpfTrab"));
        if (cpf.Length != 11) throw new FormatException("CPF do trabalhador inválido");
        var t = new TrabalhadorEsocial
        {
            Cpf = cpf, Matricula = info.Txt("matricula") ?? cpf, SemVinculo = true, IdEvento = e.Id, Arquivo = e.Arquivo,
            Admissao = Xe.Dt(info.Txt("dtInicio")) ?? throw new FormatException("S-2300 sem dtInicio"),
        };
        PreencherPessoa(t, trab);
        t.Dependentes = LerDependentes(trab);
        t.ContratoInicial = t.Contrato = new ContratoEsocial(info.Desc("nmCargo")?.Value.Trim(), info.Desc("CBOCargo")?.Value.Trim(), info.Txt("codCateg"), Xe.Num(info.Desc("vrSalFx")?.Value),
                                         Xe.Int(info.Desc("undSalFixo")?.Value), null, Xe.Dt(info.Desc("dtPrevTerm")?.Value), null, null, null, null);
        d.Trabalhadores[cpf] = t;
    }

    private static void S2399(EventoLido e, DadosEsocial d)
    {
        var cpf = Xe.Digitos(e.Evento.Cam("ideTrabSemVinculo").Txt("cpfTrab"));
        if (!d.Trabalhadores.TryGetValue(cpf, out var t) || !t.SemVinculo) { d.Ocorrencias.Add(OcorrenciaImportacao.Info(0, e.Rotulo, $"Término (S-2399) do CPF {cpf} sem início nos arquivos: ignorado.")); return; }
        var info = e.Evento.Filho("infoTSVTermino");
        t.Desligamento = new DesligamentoEsocial(Xe.Dt(info.Txt("dtTerm")) ?? throw new FormatException("S-2399 sem dtTerm"), info.Txt("mtvDesligTSV"));
    }

    // ---- periódicos

    private static void RegistrarPeriodo(DadosEsocial d, DateOnly p)
    {
        if (d.PrimeiroPeriodo is null || p < d.PrimeiroPeriodo) d.PrimeiroPeriodo = p;
        if (d.UltimoPeriodo is null || p > d.UltimoPeriodo) d.UltimoPeriodo = p;
    }

    private static void S1200(EventoLido e, DadosEsocial d, ref int infoPerAnt)
    {
        var ide = e.Evento.Filho("ideEvento");
        var ind = Xe.Int(ide.Txt("indApuracao")) ?? 1;
        var per = Xe.Periodo(ide.Txt("perApur")) ?? throw new FormatException("S-1200 sem perApur");
        var cpf = Xe.Digitos(e.Evento.Cam("ideTrabalhador").Txt("cpfTrab"));
        if (cpf.Length != 11) throw new FormatException("CPF do trabalhador inválido");
        var dms = new List<DemonstrativoEsocial>();
        foreach (var dm in e.Evento.Lista("dmDev"))
        {
            var itens = new List<ItemRemuneracaoEsocial>();
            foreach (var lot in dm.Cam("infoPerApur").Lista("ideEstabLot"))
            {
                var cod = lot.Txt("codLotacao");
                foreach (var it in lot.Descendants().Where(x => x.Name.LocalName == "itensRemun"))
                    itens.Add(new ItemRemuneracaoEsocial(it.Txt("codRubr") ?? throw new FormatException("item sem codRubr"), it.Txt("ideTabRubr") ?? "",
                        Xe.Num(it.Txt("qtdRubr")), Xe.Num(it.Txt("fatorRubr")), Xe.Num(it.Txt("vrRubr")) ?? 0m, cod));
            }
            if (dm.Filho("infoPerAnt") is not null) infoPerAnt++;
            dms.Add(new DemonstrativoEsocial(dm.Txt("ideDmDev") ?? "", dm.Txt("codCateg"), itens));
        }
        RegistrarPeriodo(d, per);
        d.Remuneracoes.Add(new RemuneracaoEsocial(cpf, per, ind, dms, e.Recibo, e.Id));
    }

    private static void S1210(EventoLido e, DadosEsocial d)
    {
        var per = Xe.Periodo(e.Evento.Cam("ideEvento").Txt("perApur")) ?? throw new FormatException("S-1210 sem perApur");
        var ben = e.Evento.Filho("ideBenef") ?? throw new FormatException("S-1210 sem ideBenef");
        var cpf = Xe.Digitos(ben.Txt("cpfBenef"));
        var lista = new List<InfoPagamentoEsocial>();
        foreach (var ip in ben.Lista("infoPgto"))
        {
            var dt = Xe.Dt(ip.Txt("dtPgto")) ?? throw new FormatException("pagamento sem dtPgto");
            var tp = Xe.Int(ip.Txt("tpPgto")) ?? 1;
            var detalhes = ip.Lista("detPgtoFl").ToList();
            if (detalhes.Count == 0)
                lista.Add(new InfoPagamentoEsocial(dt, tp, Xe.Periodo(ip.Txt("perRef")), ip.Txt("ideDmDev"), Xe.Num(ip.Txt("vrLiq")) ?? 0m));
            else
                foreach (var f in detalhes)
                    lista.Add(new InfoPagamentoEsocial(dt, tp, Xe.Periodo(f.Txt("perRef")), f.Txt("ideDmDev"), Xe.Num(f.Txt("vrLiq")) ?? 0m));
        }
        d.Pagamentos.Add(new PagamentoEsocial(cpf, per, lista, e.Id));
    }
}
