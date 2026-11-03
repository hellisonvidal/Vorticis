using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Dados;
using Vorticis.Folha.Esocial.Importacao;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Tests;

/// <summary>Folha › Integrações › Importar eSocial: leitura dos XMLs (puros, com recibo, ZIP), consolidação, prévia, importação e desfazer.</summary>
public class ImportacaoEsocialTests(BancoFolha db) : IClassFixture<BancoFolha>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------------ leitura e consolidação (sem banco)

    [Fact]
    public void Le_xml_puro_xml_com_recibo_e_zip()
    {
        var puro = AmostrasEsocial.Arq("a.xml", AmostrasEsocial.S1000(new DateTime(2026, 1, 1)));
        var id = AmostrasEsocial.Id(new DateTime(2026, 1, 2));
        var comRecibo = AmostrasEsocial.Arq("b.xml", AmostrasEsocial.ComRecibo("evtTabLotacao", id, "<ideEvento><tpAmb>1</tpAmb></ideEvento><ideEmpregador><tpInsc>1</tpInsc><nrInsc>12345678</nrInsc></ideEmpregador><infoLotacao><inclusao><ideLotacao><codLotacao>L1</codLotacao><iniValid>2020-01</iniValid></ideLotacao><dadosLotacao><tpLotacao>01</tpLotacao></dadosLotacao></inclusao></infoLotacao>", "1.1.0000000000000000042"));
        var zip = new ArquivoEsocial("lote.zip", AmostrasEsocial.Zip(puro, comRecibo));

        var expandidos = LeitorEsocial.Expandir(zip.Nome, zip.Conteudo);
        Assert.Equal(2, expandidos.Count);
        var oc = new List<OcorrenciaImportacao>();
        var (eventos, _) = LeitorEsocial.Ler(expandidos, oc);
        Assert.Equal(2, eventos.Count);
        Assert.Equal("S-1000", eventos.Single(e => e.Tipo == "S-1000").Tipo);
        Assert.Equal("1.1.0000000000000000042", eventos.Single(e => e.Tipo == "S-1020").Recibo);
        Assert.Null(eventos.Single(e => e.Tipo == "S-1000").Recibo);
        Assert.Empty(oc);
    }

    [Fact]
    public void Arquivo_invalido_e_arquivo_sem_eventos_viram_avisos()
    {
        var oc = new List<OcorrenciaImportacao>();
        var (eventos, _) = LeitorEsocial.Ler([AmostrasEsocial.Arq("ruim.xml", "<eSocial><abre>"), AmostrasEsocial.Arq("vazio.xml", "<retornoEnvioLoteEventos><status/></retornoEnvioLoteEventos>")], oc);
        Assert.Empty(eventos);
        Assert.Equal(2, oc.Count(o => o.Gravidade == GravidadeOcorrencia.Aviso));
    }

    [Fact]
    public void Consolida_a_ultima_versao_aplica_exclusoes_e_alteracoes_em_ordem_cronologica()
    {
        var d = ConsolidadorEsocial.Consolidar(AmostrasEsocial.Conjunto());

        Assert.Equal("EMPRESA ORIGEM", d.Empregador?.Nome);
        Assert.Equal("99", d.Empregador?.ClasseTributaria);
        Assert.Equal(3m, d.Estabelecimentos.Single().Rat);
        Assert.Equal(5, d.Rubricas.Count);
        Assert.Equal('D', d.Rubricas["0050"].TipoFolha);
        Assert.Equal(2, d.Trabalhadores.Count);

        var maria = d.Trabalhadores[AmostrasEsocial.CpfMaria];
        Assert.Equal(new DateOnly(2023, 2, 1), maria.Admissao);
        Assert.Equal(4000m, maria.Contrato.Salario);                  // a alteração mais recente vale, mesmo chegando antes no arquivo
        Assert.Equal(3000m, maria.ContratoInicial.Salario);
        Assert.Equal("Analista contábil sênior", maria.Contrato.Cargo);
        Assert.Equal(2, maria.Alteracoes.Count);
        Assert.Single(maria.Dependentes);
        Assert.Equal("Pedro Filho", maria.Dependentes[0].Nome);
        Assert.True(maria.Dependentes[0].DepSalarioFamilia);
        Assert.Equal(2, maria.Afastamentos.Count);
        Assert.Equal(new DateOnly(2025, 1, 31), maria.Afastamentos[0].Fim);
        Assert.Equal("15", maria.Afastamentos[0].Motivo);
        Assert.Equal("3550308", maria.Endereco?.CodMunicipio);

        Assert.Equal(new DateOnly(2025, 2, 28), d.Trabalhadores[AmostrasEsocial.CpfJoao].Desligamento?.Data);

        // Maria: jan, fev (retificada), mar de 2025 e o 13º de 2024; João: só fevereiro (janeiro foi excluído pelo S-3000)
        Assert.Equal(4, d.Remuneracoes.Count(r => r.Cpf == AmostrasEsocial.CpfMaria));
        Assert.Single(d.Remuneracoes, r => r.Cpf == AmostrasEsocial.CpfJoao);
        var fev = d.Remuneracoes.Single(r => r.Cpf == AmostrasEsocial.CpfMaria && r.Periodo == new DateOnly(2025, 2, 1));
        Assert.Equal(300m, fev.Demonstrativos[0].Itens.Single(i => i.Rubrica == "0002").Valor);   // a versão retificadora
        Assert.Equal(1, d.Excluidos);
        Assert.True(d.VersoesAnterioresIgnoradas >= 1);
        var decimo = d.Remuneracoes.Single(r => r.IndApuracao == 2);
        Assert.Equal(new DateOnly(2024, 12, 1), decimo.Periodo);
        Assert.Single(d.Pagamentos);
        Assert.Equal(new DateOnly(2025, 2, 5), d.Pagamentos[0].Pagamentos[0].Data);
        Assert.Contains("12345678", d.Empregadores);
        Assert.DoesNotContain(d.Ocorrencias, o => o.Gravidade == GravidadeOcorrencia.Erro);
    }

    [Fact]
    public void Classifica_o_tipo_de_folha_pelo_codigo_e_pelas_naturezas()
    {
        Assert.Equal("MENSAL", PlanejadorEsocial.Classificar("MENSAL", 1, ["1000", "1003", "9201"]));
        Assert.Equal("DECIMO_2", PlanejadorEsocial.Classificar("X", 2, ["5001"]));
        Assert.Equal("DECIMO_1", PlanejadorEsocial.Classificar("X", 1, ["5001"]));
        Assert.Equal("FERIAS", PlanejadorEsocial.Classificar("X", 1, ["1020", "9201"]));
        Assert.Equal("MENSAL", PlanejadorEsocial.Classificar("X", 1, ["1000", "1020"]));
        Assert.Equal("RESCISAO", PlanejadorEsocial.Classificar("X", 1, ["6000", "6003"]));
        Assert.Equal("FERIAS", PlanejadorEsocial.Classificar("FER12", 1, ["1000"]));
        Assert.Equal(("MENSAL", 2500m, (string?)null), PlanejadorEsocial.SalarioMensal(2500m, 5));
        Assert.Equal(("HORISTA", 20m, (string?)null), PlanejadorEsocial.SalarioMensal(20m, 1));
        Assert.Equal(6000m, PlanejadorEsocial.SalarioMensal(200m, 2).Salario);
    }

    // ------------------------------------------------------------------ integração com o banco

    private async Task<(long Empresa, long Admin)> PrepararAsync(string cnpj)
    {
        BancoFolha.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync(cnpj);
        return (empresa, await db.AdminAsync());
    }

    private static readonly IReadOnlyList<ArquivoEsocial> Arquivos = AmostrasEsocial.Conjunto();

    [Fact]
    public async Task Previa_mostra_o_que_e_novo_e_a_importacao_leva_cadastros_historico_ferias_e_afastamentos()
    {
        var (empresa, adm) = await PrepararAsync(AmostrasEsocial.Cnpj);
        var svc = db.Folha.ImportacaoEsocial;
        var analise = await svc.AnalisarAsync(empresa, Arquivos, "pasta de teste", adm, Ct);
        Assert.True(analise.Sucesso, string.Join("; ", analise.Erros));

        var opcoes = new OpcoesImportacaoEsocial();
        var previa = await svc.PreviaAsync(empresa, analise.Valor, opcoes, adm, Ct);
        Assert.True(previa.Sucesso, string.Join("; ", previa.Erros));
        var plano = previa.Valor.Plano;
        Assert.Equal(0, plano.ErrosPontuais);
        Assert.True(previa.Valor.PodeImportar(opcoes), previa.Valor.Resumo(opcoes));
        Assert.Equal(2, plano.Funcionarios.Count);
        Assert.Equal(5, plano.Rubricas.Count);
        Assert.Equal(5, plano.Calculos.Count);                          // 4 de Maria + 1 de João
        Assert.Single(plano.Ferias);
        Assert.Single(plano.Afastamentos);                              // a doença; as férias vão para o controle de férias
        Assert.Equal(2, plano.Cargos.Count);
        Assert.Single(plano.Departamentos);
        Assert.Equal(4, plano.CompetenciasAFechar.Count);               // 2024-12, 2025-01, 2025-02, 2025-03
        Assert.NotNull(plano.Parametros);
        Assert.Equal(3m, plano.Parametros!.RatAliquota);
        Assert.Equal(1.2m, plano.Parametros.Fap);
        var linha = plano.Linhas.Single(l => l.Registro == "S-2200");
        Assert.Equal(2, linha.Novos);
        Assert.Contains(plano.GradeRubricas, g => g.Origem == "0002" && g.Variavel && g.Situacao == "Nova");

        var prog = new List<ProgressoImportacao>();
        var rel = await svc.ImportarAsync(empresa, analise.Valor, opcoes, adm, new Progress<ProgressoImportacao>(prog.Add), Ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(2, rel.Valor.Linhas.Single(l => l.Registro == "S-2200").Importados);
        Assert.Equal(5, rel.Valor.Linhas.Single(l => l.Registro == "S-1200").Importados);
        Assert.Contains("S-1200", rel.Valor.ParaCsv());

        // cadastros
        var funcs = await db.Folha.Cadastros.ListarFuncionariosAsync(empresa, adm, Ct);
        var maria = funcs.Single(f => f.Cpf == AmostrasEsocial.CpfMaria);
        var joao = funcs.Single(f => f.Cpf == AmostrasEsocial.CpfJoao);
        Assert.Equal(4000m, maria.Salario);
        Assert.Equal("ATIVO", maria.Situacao);
        Assert.Equal("Analista contábil sênior", maria.CargoNome);
        Assert.NotNull(maria.DepartamentoId);
        Assert.Single(maria.Dependentes);
        Assert.True(maria.Dependentes[0].DepSalarioFamilia);
        Assert.Equal("DESLIGADO", joao.Situacao);
        Assert.Equal(new DateOnly(2025, 2, 28), joao.Desligamento);
        Assert.Equal("SEM_JUSTA_CAUSA", joao.MotivoDesligamento);
        var alteracoes = (await new RepositorioCadastrosFolha(db.App.DataSource).ListarAlteracoesAsync(empresa, Ct)).Where(a => a.FuncionarioId == maria.Id).OrderBy(a => a.Data).ToList();
        Assert.Equal(2, alteracoes.Count);
        Assert.Equal((3000m, 3500m), (alteracoes[0].SalarioAnterior!.Value, alteracoes[0].SalarioNovo!.Value));
        Assert.Equal((3500m, 4000m), (alteracoes[1].SalarioAnterior!.Value, alteracoes[1].SalarioNovo!.Value));
        Assert.Equal(3m, (await db.Folha.Cadastros.ObterParametrosAsync(empresa, adm, Ct)).RatAliquota);

        // rubricas: a de horas extras entra nas médias
        var rubricas = await db.Folha.Cadastros.ListarRubricasAsync(empresa, adm, Ct);
        Assert.True(rubricas.Single(r => r.Codigo == "0002").Variavel);
        Assert.False(rubricas.Single(r => r.Codigo == "0001").Variavel);

        // histórico de holerites
        var movimento = new RepositorioMovimentoFolha(db.App.DataSource);
        var mar = (await movimento.ListarCalculosAsync(empresa, new DateOnly(2025, 3, 1), TipoFolha.Mensal, true, Ct)).Single();
        Assert.Equal(4300m, mar.TotalProventos);
        Assert.Equal(600m, mar.TotalDescontos);
        Assert.Equal(3700m, mar.Liquido);
        Assert.Equal(4300m, mar.BaseInss);
        Assert.Equal(4300m - 400m, mar.BaseIrrf);                        // rendimento menos a dedução da previdência
        Assert.Equal(400m, mar.Inss);
        Assert.Equal(200m, mar.Irrf);
        Assert.True(mar.Fgts > 0);
        Assert.Contains(mar.Itens, i => i.Codigo == "0002" && i.Referencia == 10m && i.Valor == 300m);
        var jan = (await movimento.ListarCalculosAsync(empresa, new DateOnly(2025, 1, 1), TipoFolha.Mensal, false, Ct)).Single();
        Assert.Equal(new DateOnly(2025, 2, 5), jan.DataPagamento);        // S-1210
        var decimo = (await movimento.ListarCalculosAsync(empresa, new DateOnly(2024, 12, 1), TipoFolha.Decimo2, false, Ct)).Single();
        Assert.Equal(3500m, decimo.TotalProventos);

        // o que a folha precisa para continuar: médias (3 meses com horas extras de 300), competências fechadas e férias usadas
        var (soma, meses) = await movimento.VariaveisUltimosMesesAsync(empresa, maria.Id, new DateOnly(2025, 4, 1), Ct);
        Assert.Equal((900m, 3), (soma, meses));
        Assert.Equal(300m, PeriodosFerias.MediaSimples(soma, meses));
        Assert.True((await movimento.ObterCompetenciaAsync(empresa, new DateOnly(2025, 1, 1), Ct)).Fechada);
        Assert.False((await movimento.ObterCompetenciaAsync(empresa, new DateOnly(2025, 4, 1), Ct)).Fechada);
        var ferias = await db.Folha.Folha.ListarFeriasAsync(empresa, maria.Id, adm, Ct);
        Assert.Equal(30, ferias.Single().DiasGozo);
        var saldos = await db.Folha.Folha.ListarSaldosFeriasAsync(empresa, maria.Id, new DateOnly(2025, 6, 1), adm, Ct);
        Assert.Equal(new DateOnly(2023, 2, 1), saldos[0].Periodo.Inicio);
        Assert.Equal(30, saldos[0].DiasUsados);
        var afast = (await db.Folha.Cadastros.ListarAfastamentosAsync(empresa, adm, Ct)).Single();
        Assert.Equal("01", afast.CodigoMotivo);
        Assert.Equal(new DateOnly(2025, 6, 20), afast.Fim);

        // a folha continua: maio é calculada com a base importada e depois disso a importação não pode mais ser desfeita
        var calculo = await db.Folha.Folha.CalcularMensalAsync(empresa, new DateOnly(2025, 5, 1), adm, maria.Id, Ct);
        Assert.True(calculo.Sucesso, string.Join("; ", calculo.Erros));
        var historico = await svc.HistoricoAsync(empresa, adm, Ct);
        Assert.Single(historico);
        var desfazer = await svc.DesfazerAsync(empresa, historico[0].Id, adm, Ct);
        Assert.False(desfazer.Sucesso);
        Assert.Contains("já têm", string.Join(' ', desfazer.Erros));

        // o mesmo material não entra duas vezes
        var de_novo = await svc.ImportarAsync(empresa, analise.Valor, opcoes, adm, ct: Ct);
        Assert.False(de_novo.Sucesso);
        Assert.Contains("já foi importado", string.Join(' ', de_novo.Erros));
        Assert.NotEmpty(prog);
    }

    [Fact]
    public async Task Desfazer_remove_o_que_a_importacao_criou_e_permite_importar_de_novo()
    {
        var (empresa, adm) = await PrepararAsync("98765432000198");
        var svc = db.Folha.ImportacaoEsocial;
        var analise = (await svc.AnalisarAsync(empresa, Arquivos.Select(a => a with { Conteudo = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(a.Conteudo).Replace("12345678", "98765432")) }).ToList(), "teste", adm, Ct)).Valor;
        var rel = await svc.ImportarAsync(empresa, analise, new OpcoesImportacaoEsocial(), adm, ct: Ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(2, (await db.Folha.Cadastros.ListarFuncionariosAsync(empresa, adm, Ct)).Count);

        var h = (await svc.HistoricoAsync(empresa, adm, Ct)).Single();
        Assert.False(h.Desfeita);
        var undo = await svc.DesfazerAsync(empresa, h.Id, adm, Ct);
        Assert.True(undo.Sucesso, string.Join("; ", undo.Erros));
        Assert.True(undo.Valor.Desfeita);
        Assert.Empty(await db.Folha.Cadastros.ListarFuncionariosAsync(empresa, adm, Ct));
        Assert.Empty(await db.Folha.Cadastros.ListarAfastamentosAsync(empresa, adm, Ct));
        var movimento = new RepositorioMovimentoFolha(db.App.DataSource);
        Assert.Empty(await movimento.ListarCalculosAsync(empresa, new DateOnly(2025, 3, 1), null, false, Ct));
        Assert.False((await movimento.ObterCompetenciaAsync(empresa, new DateOnly(2025, 1, 1), Ct)).Fechada);
        Assert.DoesNotContain(await db.Folha.Cadastros.ListarRubricasAsync(empresa, adm, Ct), r => r.Codigo == "0002");
        Assert.True((await svc.HistoricoAsync(empresa, adm, Ct)).Single().Desfeita);
        Assert.False((await svc.DesfazerAsync(empresa, h.Id, adm, Ct)).Sucesso);   // já desfeita

        var de_novo = await svc.ImportarAsync(empresa, analise, new OpcoesImportacaoEsocial(), adm, ct: Ct);
        Assert.True(de_novo.Sucesso, string.Join("; ", de_novo.Erros));
    }

    [Fact]
    public async Task Mapeia_rubricas_para_as_existentes_aponta_conflitos_e_ignora_itens_com_erro()
    {
        var (empresa, adm) = await PrepararAsync("11444777000161");
        var svc = db.Folha.ImportacaoEsocial;
        Assert.True((await db.Folha.Cadastros.GravarRubricaAsync(empresa, new Rubrica(0, "0777", "Desconto qualquer", 'D', "9900", "00", "00", "00", false, false), adm, Ct)).Sucesso);
        var padrao = (await db.Folha.Cadastros.ListarRubricasAsync(empresa, adm, Ct)).Single(r => r.Codigo == RubricasPadrao.Salario);

        var arquivos = Arquivos.Select(a => a with { Conteudo = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(a.Conteudo).Replace("12345678", "11444777")) }).ToList();
        arquivos.Add(AmostrasEsocial.Arq("S1010_0777.xml", AmostrasEsocial.S1010(new DateTime(2026, 1, 10, 9, 0, 9), "0777", "Gratificação especial", "1000", 1, "11", "01", "11").Replace("12345678", "11444777")));
        // um funcionário sem data de nascimento (erro pontual) e uma remuneração de CPF sem admissão (aviso)
        arquivos.Add(AmostrasEsocial.Arq("S2200_sem_nasc.xml", AmostrasEsocial.S2200(new DateTime(2026, 1, 10, 9, 5, 0), "39053344705", "Sem Nascimento", "M009", "2024-01-01", 1800m, "Auxiliar")
            .Replace("<nascimento><dtNascto>1990-05-20</dtNascto><paisNascto>105</paisNascto><paisNac>105</paisNac></nascimento>", "").Replace("12345678", "11444777")));
        var analise = (await svc.AnalisarAsync(empresa, arquivos, "teste", adm, Ct)).Valor;

        var opcoes = new OpcoesImportacaoEsocial(Rubricas: [new DecisaoRubrica("0001", RubricasPadrao.Salario, false)]);
        var previa = (await svc.PreviaAsync(empresa, analise, opcoes, adm, Ct)).Valor;
        var grade = previa.Plano.GradeRubricas;
        Assert.Equal(("Existente", true), (grade.Single(g => g.Origem == "0001").Situacao, grade.Single(g => g.Origem == "0001").Existente));
        Assert.Equal(RubricasPadrao.Salario, grade.Single(g => g.Origem == "0001").Destino);
        var mesmoNome = grade.Single(g => g.Origem == "0002");           // "Horas extras 50%" já existe no Vorticis com outro código
        Assert.Equal(("Existente", RubricasPadrao.HoraExtra50), (mesmoNome.Situacao, mesmoNome.Destino));
        var conflito = grade.Single(g => g.Origem == "0777");            // o código 0777 é de outra rubrica
        Assert.Equal("Conflito", conflito.Situacao);
        Assert.Equal("E0777", conflito.Destino);                          // sugestão: código alternativo
        Assert.DoesNotContain(previa.Plano.Rubricas, r => r.Codigo is "0001" or "0002");
        Assert.Contains(previa.Plano.Rubricas, r => r.Codigo == "E0777");

        Assert.Equal(1, previa.Plano.ErrosPontuais);
        Assert.False(previa.PodeImportar(opcoes));
        Assert.Contains("Ignorar", previa.Resumo(opcoes));
        var recusada = await svc.ImportarAsync(empresa, analise, opcoes, adm, ct: Ct);
        Assert.False(recusada.Sucesso);

        var ignorando = opcoes with { IgnorarComErro = true };
        var rel = await svc.ImportarAsync(empresa, analise, ignorando, adm, ct: Ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(1, rel.Valor.Linhas.Single(l => l.Registro == "S-2200").ComErro);
        Assert.Equal(2, (await db.Folha.Cadastros.ListarFuncionariosAsync(empresa, adm, Ct)).Count);
        var movimento = new RepositorioMovimentoFolha(db.App.DataSource);
        var mar = (await movimento.ListarCalculosAsync(empresa, new DateOnly(2025, 3, 1), TipoFolha.Mensal, true, Ct)).Single();
        Assert.Contains(mar.Itens, i => i.Codigo == RubricasPadrao.Salario && i.Valor == 4000m);   // o salário foi para a rubrica escolhida
        Assert.Contains(mar.Itens, i => i.Codigo == RubricasPadrao.HoraExtra50 && i.Valor == 300m);
    }

    [Fact]
    public async Task Recusa_arquivos_de_outro_empregador_e_exige_permissao()
    {
        var (empresa, adm) = await PrepararAsync("45997418000153");
        var svc = db.Folha.ImportacaoEsocial;
        var analise = (await svc.AnalisarAsync(empresa, Arquivos, "teste", adm, Ct)).Valor;      // eventos do empregador 12345678
        var previa = (await svc.PreviaAsync(empresa, analise, new OpcoesImportacaoEsocial(), adm, Ct)).Valor;
        Assert.False(previa.Plano.CnpjConfere);
        Assert.False(previa.PodeImportar(new OpcoesImportacaoEsocial()));
        var rel = await svc.ImportarAsync(empresa, analise, new OpcoesImportacaoEsocial(), adm, ct: Ct);
        Assert.False(rel.Sucesso);
        Assert.Contains("empregador", string.Join(' ', rel.Erros));

        var semAcesso = await svc.AnalisarAsync(empresa, Arquivos, "teste", 999999, Ct);
        Assert.False(semAcesso.Sucesso);
        Assert.Empty(await svc.HistoricoAsync(empresa, 999999, Ct));
        var vazio = await svc.AnalisarAsync(empresa, [], "teste", adm, Ct);
        Assert.False(vazio.Sucesso);
    }
}
