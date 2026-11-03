using System.IO.Compression;
using System.Text;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Desktop.ViewModels;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Folha › Integrações › Importar eSocial: escolher pasta/ZIP, prévia, mapeamento de rubricas, importar, relatório e desfazer (ViewModel com banco).</summary>
public class ImportacaoEsocialTelaTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("esocial_tela_").FullName;
    private const string Cnpj = "12345678000195";

    public void Dispose() { try { Directory.Delete(_pasta, true); } catch (IOException) { } }

    private static string Evento(string nome, string id, string corpo)
        => $"""<?xml version="1.0" encoding="UTF-8"?><eSocial xmlns="http://www.esocial.gov.br/schema/evt/{nome}/v_S_01_03_00"><{nome} Id="{id}">{corpo}</{nome}></eSocial>""";

    private const string Emp = "<ideEmpregador><tpInsc>1</tpInsc><nrInsc>12345678</nrInsc></ideEmpregador>";

    private void Gravar(string nome, string xml) => File.WriteAllText(Path.Combine(_pasta, nome), xml, Encoding.UTF8);

    private void EventosDeExemplo()
    {
        Gravar("S1010_0001.xml", Evento("evtTabRubrica", $"ID1{Cnpj}20260110090001{1:00000}",
            $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{Emp}<infoRubrica><inclusao><ideRubrica><codRubr>0001</codRubr><ideTabRubr>T</ideTabRubr><iniValid>2020-01</iniValid></ideRubrica><dadosRubrica><dscRubr>Salário base</dscRubr><natRubr>1000</natRubr><tpRubr>1</tpRubr><codIncCP>11</codIncCP><codIncIRRF>01</codIncIRRF><codIncFGTS>11</codIncFGTS></dadosRubrica></inclusao></infoRubrica>"));
        Gravar("S1010_0002.xml", Evento("evtTabRubrica", $"ID1{Cnpj}20260110090002{2:00000}",
            $"<ideEvento><tpAmb>1</tpAmb></ideEvento>{Emp}<infoRubrica><inclusao><ideRubrica><codRubr>0002</codRubr><ideTabRubr>T</ideTabRubr><iniValid>2020-01</iniValid></ideRubrica><dadosRubrica><dscRubr>Comissão de vendas</dscRubr><natRubr>1207</natRubr><tpRubr>1</tpRubr><codIncCP>11</codIncCP><codIncIRRF>01</codIncIRRF><codIncFGTS>11</codIncFGTS></dadosRubrica></inclusao></infoRubrica>"));
        Gravar("S2200.xml", Evento("evtAdmissao", $"ID1{Cnpj}20260110090003{3:00000}",
            $"<ideEvento><indRetif>1</indRetif><tpAmb>1</tpAmb></ideEvento>{Emp}<trabalhador><cpfTrab>52998224725</cpfTrab><nmTrab>Maria da Silva</nmTrab><sexo>F</sexo><racaCor>1</racaCor><estCiv>2</estCiv><grauInstr>09</grauInstr><nascimento><dtNascto>1990-05-20</dtNascto></nascimento></trabalhador>" +
            "<vinculo><matricula>M001</matricula><tpRegTrab>1</tpRegTrab><tpRegPrev>1</tpRegPrev><cadIni>N</cadIni><infoRegimeTrab><infoCeletista><dtAdm>2023-02-01</dtAdm><tpAdmissao>1</tpAdmissao></infoCeletista></infoRegimeTrab><infoContrato><nmCargo>Analista</nmCargo><CBOCargo>252105</CBOCargo><codCateg>101</codCateg><remuneracao><vrSalFx>3000.00</vrSalFx><undSalFixo>5</undSalFixo></remuneracao></infoContrato></vinculo>"));
        Gravar("S1200.xml", Evento("evtRemun", $"ID1{Cnpj}20260110090004{4:00000}",
            $"<ideEvento><indRetif>1</indRetif><indApuracao>1</indApuracao><perApur>2025-03</perApur><tpAmb>1</tpAmb></ideEvento>{Emp}<ideTrabalhador><cpfTrab>52998224725</cpfTrab></ideTrabalhador>" +
            $"<dmDev><ideDmDev>MENSAL</ideDmDev><codCateg>101</codCateg><infoPerApur><ideEstabLot><tpInsc>1</tpInsc><nrInsc>{Cnpj}</nrInsc><codLotacao>L1</codLotacao><remunPerApur><matricula>M001</matricula>" +
            "<itensRemun><codRubr>0001</codRubr><ideTabRubr>T</ideTabRubr><vrRubr>3000.00</vrRubr></itensRemun><itensRemun><codRubr>0002</codRubr><ideTabRubr>T</ideTabRubr><vrRubr>500.00</vrRubr></itensRemun></remunPerApur></ideEstabLot></infoPerApur></dmDev>"));
    }

    private async Task<(ImportacaoEsocialViewModel Vm, long Empresa, long Admin)> CriarAsync()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync(Cnpj, 2025);
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa);
        var vm = new ImportacaoEsocialViewModel(ctx);
        await vm.CarregarAsync();
        return (vm, empresa, adm);
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        for (var i = 0; i < 100 && !condicao(); i++) await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(condicao());
    }

    [Fact]
    public async Task Le_a_pasta_mostra_a_previa_mapeia_rubricas_importa_e_desfaz()
    {
        var (vm, empresa, adm) = await CriarAsync();
        EventosDeExemplo();

        await vm.LerCaminhosAsync([_pasta]);
        Assert.Empty(vm.Erros);
        Assert.True(vm.PreviaPronta, vm.ResumoPrevia);
        Assert.Contains("pronto para importar", vm.ResumoPrevia);
        Assert.Contains("4 arquivo(s) lido(s)", vm.ResumoLeitura);
        Assert.Equal(1, vm.Previa.Single(l => l.Registro == "S-2200").Novos);
        Assert.Equal(2, vm.Previa.Single(l => l.Registro == "S-1010").Novos);
        Assert.Equal(1, vm.Previa.Single(l => l.Registro == "S-1200").Novos);
        Assert.Equal(2, vm.Rubricas.Count);
        Assert.True(vm.TemRubricas);
        var comissao = vm.Rubricas.Single(r => r.Origem == "0002");
        Assert.True(comissao.Variavel);                                   // comissão entra nas médias
        Assert.Equal("Nova", comissao.Situacao);

        // o contador manda o salário do sistema antigo (0001) para a rubrica padrão do Vorticis (1000)
        var salario = vm.Rubricas.Single(r => r.Origem == "0001");
        var padrao = salario.Opcoes.Single(o => o.Codigo == "1000");
        salario.Destino = padrao;
        await EsperarAsync(() => vm.Rubricas.Single(r => r.Origem == "0001").DestinoFinal == "1000");
        Assert.Equal("Existente", vm.Rubricas.Single(r => r.Origem == "0001").Situacao);
        Assert.Equal(1, vm.Previa.Single(l => l.Registro == "S-1010").Novos);

        await vm.ImportarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.Contains("Importação concluída", vm.Mensagem);
        Assert.Equal(100, vm.Progresso);
        Assert.Equal(1, vm.RelatorioLinhas.Single(l => l.Registro == "S-2200").Importados);
        Assert.Contains("S-1200", vm.RelatorioCsv);
        Assert.Single(vm.Historico);
        Assert.False(vm.PreviaPronta);                                    // o mesmo material não é importado duas vezes
        Assert.Single(await Vorticis.Folha.ServicosFolha.De(db.App).Cadastros.ListarFuncionariosAsync(empresa, adm, TestContext.Current.CancellationToken));

        vm.SalvarRelatorioCommand.Execute(null);
        Assert.NotNull(vm.ArquivoRelatorio);
        Assert.True(File.Exists(vm.ArquivoRelatorio));

        vm.HistoricoSelecionado = vm.Historico[0];
        await vm.DesfazerCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.True(vm.Historico[0].Desfeita);
        Assert.Empty(await Vorticis.Folha.ServicosFolha.De(db.App).Cadastros.ListarFuncionariosAsync(empresa, adm, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Aceita_zip_e_avisa_quando_nao_ha_arquivo_ou_evento()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("98765432000198", 2025);
        var adm = await db.AdminAsync();
        var vm = new ImportacaoEsocialViewModel(new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa));

        await vm.LerCaminhosAsync([]);
        Assert.NotEmpty(vm.Erros);

        await vm.LerCaminhosAsync([Path.Combine(_pasta, "nao_existe")]);
        Assert.Contains(vm.Erros, e => e.Contains("Não foi possível ler") || e.Contains("Não encontrei"));
        Assert.False(vm.PreviaPronta);

        var vazio = Path.Combine(_pasta, "vazia");
        Directory.CreateDirectory(vazio);
        await vm.LerCaminhosAsync([vazio]);
        Assert.Contains(vm.Erros, e => e.Contains(".xml"));

        // ZIP com um XML que não é do eSocial: nenhum evento reconhecido
        var zip = Path.Combine(_pasta, "lote.zip");
        using (var fs = File.Create(zip))
        using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            using (var w = new StreamWriter(z.CreateEntry("qualquer.xml").Open())) w.Write("<outra><coisa/></outra>");
        await vm.LerCaminhosAsync([zip]);
        Assert.Contains(vm.Erros, e => e.Contains("nenhum evento", StringComparison.OrdinalIgnoreCase));
        Assert.False(vm.PreviaPronta);
    }
}
