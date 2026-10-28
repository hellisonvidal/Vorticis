using Vorticis.Application.Cadastros;
using Vorticis.Fiscal.Contabilizacao;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Guias;

namespace Vorticis.Fiscal.Tests;

public class OrdemDasRegrasTests
{
    private static RegraContabilizacao R(long id, string? cfop, TipoOperacao? op = null, long? part = null, int pri = 100) =>
        new(id, pri, op, cfop, part, null, "1.1", "3.1", "H");

    [Fact]
    public void Participante_vence_cfop_exato_que_vence_prefixo_independente_da_ordem()
    {
        var regras = new[] { R(1, "51", TipoOperacao.Saida, pri: 10), R(2, "510", TipoOperacao.Saida, pri: 20), R(3, "5102", TipoOperacao.Saida, pri: 30), R(4, null, TipoOperacao.Saida, part: 9, pri: 40) };
        Assert.Equal(4, MotorContabilizacao.Escolher(regras, TipoOperacao.Saida, "5102", 9)!.Id);
        Assert.Equal(3, MotorContabilizacao.Escolher(regras, TipoOperacao.Saida, "5102", 8)!.Id);
        Assert.Equal(2, MotorContabilizacao.Escolher(regras, TipoOperacao.Saida, "5101", 8)!.Id);
        Assert.Equal(1, MotorContabilizacao.Escolher(regras, TipoOperacao.Saida, "5199", 8)!.Id);
    }

    [Fact]
    public void Entre_regras_igualmente_especificas_vale_a_ordem_da_lista()
    {
        var regras = new[] { R(1, "5102", TipoOperacao.Saida, pri: 20), R(2, "5102", TipoOperacao.Saida, pri: 10) };
        Assert.Equal(2, MotorContabilizacao.Escolher(regras, TipoOperacao.Saida, "5102", null)!.Id);
    }
}

public class RegrasEParametrosTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Regras_em_abas_nova_vai_para_o_fim_e_subir_descer_troca_a_ordem()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, adm, empresa);
        var vm = new RegrasContabilizacaoViewModel(ctx);
        await vm.CarregarAsync();

        async Task Nova(string cfop)
        {
            vm.NovaCommand.Execute(null);
            (vm.Cfop, vm.Debito, vm.Credito) = (cfop, "1.1.02.001", "3.1.01.001");
            await vm.SalvarCommand.ExecuteAsync(null);
            Assert.Empty(vm.Erros);
        }

        vm.Aba = 0;
        await Nova("1102");
        await Nova("1403");
        vm.Aba = 1;
        await Nova("5102");
        Assert.Equal(["1102", "1403"], vm.RegrasEntrada.Select(x => x.Cfop));
        Assert.Equal(["5102"], vm.RegrasSaida.Select(x => x.Cfop));
        Assert.All(vm.RegrasEntrada, x => Assert.Equal(TipoOperacao.Entrada, x.Regra.Operacao));
        Assert.All(vm.RegrasSaida, x => Assert.Equal(TipoOperacao.Saida, x.Regra.Operacao));

        vm.Aba = 0;
        vm.Selecionada = vm.RegrasEntrada[1];
        await vm.SubirCommand.ExecuteAsync(null);
        Assert.Equal(["1403", "1102"], vm.RegrasEntrada.Select(x => x.Cfop));
        Assert.Equal("1403", vm.Selecionada!.Cfop);
        await vm.DescerCommand.ExecuteAsync(null);
        Assert.Equal(["1102", "1403"], vm.RegrasEntrada.Select(x => x.Cfop));
        Assert.Equal(["5102"], vm.RegrasSaida.Select(x => x.Cfop));
        var lista = (await db.App.Fiscal().Contabilizacao.ListarRegrasAsync(empresa, adm, ct)).Valor;
        Assert.Equal([10, 20], lista.Where(x => x.Operacao == TipoOperacao.Entrada).Select(x => x.Prioridade));
    }

    [Fact]
    public void Tributos_liberados_seguem_o_regime_e_a_atividade()
    {
        Assert.Equal(["DAS"], ServicoProvisoes.TributosDoRegime(RegimeTributario.SimplesNacional, "123", "4751201"));
        Assert.Equal(["IRPJ", "CSLL", "PIS", "COFINS", "ICMS"], ServicoProvisoes.TributosDoRegime(RegimeTributario.LucroPresumido, "123", "4751-2/01"));
        Assert.Equal(["IRPJ", "CSLL", "PIS", "COFINS", "ICMS", "IPI"], ServicoProvisoes.TributosDoRegime(RegimeTributario.LucroReal, null, "2599301"));
        Assert.Equal(["IRPJ", "CSLL", "PIS", "COFINS", "ISS"], ServicoProvisoes.TributosDoRegime(RegimeTributario.LucroPresumido, null, "6911701"));
        Assert.Empty(ServicoProvisoes.TributosDoRegime(RegimeTributario.ImuneIsenta, "1", "6911701"));
    }

    [Fact]
    public async Task Parametros_gravam_e_leem_as_contas_das_provisoes_so_dos_tributos_liberados()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("22333444000181");   // Lucro Presumido, comércio (CNAE 4751201)
        var adm = await db.AdminAsync();
        var vm = new ParametrosFiscalViewModel(new ContextoDeTeste(db.App, adm, empresa));
        await vm.CarregarAsync();
        var campos = vm.Grupos.SelectMany(g => g.Campos).ToDictionary(c => c.Chave);
        Assert.False(campos["provDebitoDAS"].Habilitado);
        Assert.True(campos["provDebitoIRPJ"].Habilitado);
        Assert.True(campos["provCreditoICMS"].Habilitado);
        Assert.False(campos["provDebitoIPI"].Habilitado);
        Assert.False(campos["provDebitoISS"].Habilitado);

        (campos["provDebitoIRPJ"].Texto, campos["provCreditoIRPJ"].Texto) = ("4.5.01.001", "2.1.04.004");
        (campos["provDebitoPIS"].Texto, campos["provCreditoPIS"].Texto) = ("3.2.01.005", "");
        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Contains(vm.Erros, e => e.Contains("PIS") && e.Contains("duas contas"));
        campos["provDebitoPIS"].Texto = "";
        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);

        var salvas = (await db.App.Fiscal().Provisoes.ListarContasAsync(empresa, adm, ct)).Valor;
        var irpj = Assert.Single(salvas);
        Assert.Equal(("IRPJ", "4.5.01.001", "2.1.04.004"), (irpj.Tributo, irpj.ContaDespesa, irpj.ContaPassivo));

        await vm.CarregarAsync();
        Assert.Equal("4.5.01.001", vm.Grupos.SelectMany(g => g.Campos).First(c => c.Chave == "provDebitoIRPJ").Texto);
    }
}
