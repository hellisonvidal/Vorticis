using Avalonia.Threading;
using Vorticis.Application.Contabil;
using Vorticis.Application.Imobilizado;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal;
using Vorticis.Fiscal.Documentos;
using Vorticis.Infra.Tests;
using Vorticis.Suite.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Nota de entrada com imobilizado: on-line abre o Cadastro de bens preenchido (Suite), em lote só registra, Fiscal sozinho só avisa.</summary>
public class BensDasNotasTelasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static async Task Esperar(CancellationToken ct, Func<bool>? ate = null, int ciclos = 100)
    {
        for (var i = 0; i < ciclos && ate?.Invoke() != true; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
    }

    private static ItemFiscal Item(string descricao, decimal valor, string cfop = "1551", string ncm = "84714900") =>
        new(0, null, descricao, ncm, null, cfop, "UN", 1, valor, 0, 0, 0, 0, 0, 0, "00", "0", 0, 0, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, 0, 0, 0);

    private async Task<long> NotaAsync(long empresa, long part, long adm, CancellationToken ct, string numero, params ItemFiscal[] itens)
    {
        var data = new DateOnly(2026, 3, 10);
        var completos = itens.Select(CalculadoraNota.Completar).ToList();
        var t = CalculadoraNota.Totalizar(completos, new TotaisDocumento(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0));
        var r = await db.App.Fiscal().Digitacao.GravarAsync(empresa,
            new DocumentoDigitado(0, TipoOperacao.Entrada, ModeloDocumento.NFe55, "1", numero, null, data, data.AddDays(1), "Compra", part, null, t, itens), adm, ct);
        Assert.True(r.Sucesso, string.Join(";", r.Erros));
        return r.Valor;
    }

    [Fact]
    public async Task Suite_on_line_abre_o_cadastro_de_bens_preenchido_e_em_lote_so_registra()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var empresa = await db.CriarEmpresaAsync("27183946000359");
        var part = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, uf, cliente, fornecedor) VALUES (@e, '7', 'Informática Alfa', '11222333000181', 'SP', FALSE, TRUE) RETURNING id", ("e", empresa));
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);
            var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
            var janela = new MainWindow();
            janela.UsarSuite(suite);
            janela.Show();
            await suite.Principal.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            await Esperar(ct, () => suite.Modulos.All(m => m.Vm.Logado));
            var fiscal = suite.Modulos.Single(m => m.Nome == "Fiscal");
            var contabil = suite.Modulos.Single(m => m.Nome == "Contábil");
            suite.Trocar(fiscal);
            fiscal.Vm.EmpresaSelecionada = fiscal.Vm.EmpresasPermitidas.Single(e => e.Id == empresa);
            contabil.Vm.EmpresaSelecionada = contabil.Vm.EmpresasPermitidas.Single(e => e.Id == empresa);
            await Esperar(ct);
            var main = (Vorticis.Desktop.ViewModels.MainViewModel)contabil.Vm;

            // contabilização on-line (padrão): a nota com 2 itens de ativo abre o Cadastro de bens do Contábil, preenchido com o 1º item
            var nota1 = await NotaAsync(empresa, part, adm, ct, "1001", Item("Notebook Dell", 4500m), Item("Papel A4", 100m, "1102"), Item("Monitor 24", 900m, "2551"));
            var texto = await BensDasNotas.AposGravarNotasAsync(fiscal.Vm, db.App, empresa, adm, [nota1]);
            Assert.Contains("2 item(ns)", texto);
            Assert.Contains("Cadastro de bens foi aberto", texto);
            Assert.Same(contabil, suite.Ativo);                                   // trocou para o Contábil na mesma janela
            Assert.Same(main.Bens, main.Conteudo);
            await Esperar(ct, () => main.Bens.EmInclusao && main.Bens.Descricao.Length > 0);
            var bens = main.Bens;
            Assert.True(bens.EmInclusao);
            Assert.Equal(("Notebook Dell", "4.500,00", "1001/1", "7"), (bens.Descricao, bens.Valor, bens.NotaFiscal, bens.Fornecedor));
            Assert.Equal(new DateTime(2026, 3, 11), bens.InicioUso);
            Assert.Equal("08", bens.Grupo?.Codigo);                               // NCM 8471: computadores, com vida útil e contas do grupo
            Assert.Equal(("60", "1.2.03.004"), (bens.VidaUtil, bens.ContaBem));
            Assert.True(bens.TemAvisoNota);
            Assert.Contains("mais 1 item", bens.AvisoNota);

            // Salvar grava o bem vinculado ao item e já abre o próximo
            await bens.SalvarCommand.ExecuteAsync(null);
            Assert.Empty(bens.Erros);
            Assert.True(bens.EmInclusao);
            Assert.Equal(("Monitor 24", "900,00"), (bens.Descricao, bens.Valor));
            Assert.Single(await db.App.Imobilizado.ListarBensAsync(empresa, adm, ct));
            Assert.Equal(BemACadastrar.Cadastrado, (await db.App.Imobilizado.ListarBensACadastrarAsync(empresa, adm, false, null, [nota1], null, ct)).Single(i => i.NItem == 1).Situacao);

            // Cancelar deixa o item pendente
            bens.CancelarCommand.Execute(null);
            Assert.True(bens.EmConsulta);
            Assert.False(bens.TemAvisoNota);
            var pendente = Assert.Single(await db.App.Imobilizado.ListarBensACadastrarAsync(empresa, adm, ct: ct));
            Assert.Equal("Monitor 24", pendente.Descricao);

            // Imobilizado › Bens a cadastrar das notas: lista a pendência; Cadastrar reabre o cadastro preenchido; Descartar tira da lista
            main.AbrirCommand.Execute(Vorticis.Desktop.ViewModels.Telas.BensACadastrar);
            var lista = main.BensACadastrar;
            await Esperar(ct, () => lista.Linhas.Count == 1);
            Assert.Equal(("Monitor 24", "A cadastrar"), (lista.Linhas[0].Descricao, lista.Linhas[0].Situacao));
            Assert.Contains("1 item(ns)", lista.Resumo);
            lista.CadastrarCommand.Execute(null);
            Assert.Contains("Marque", lista.Erros.Single());
            lista.Linhas[0].Marcada = true;
            lista.CadastrarCommand.Execute(null);
            Assert.Same(main.Bens, main.Conteudo);
            await Esperar(ct, () => main.Bens.EmInclusao && main.Bens.Descricao == "Monitor 24");
            Assert.True(main.Bens.EmInclusao);
            main.Bens.CancelarCommand.Execute(null);
            main.AbrirCommand.Execute(Vorticis.Desktop.ViewModels.Telas.BensACadastrar);
            await Esperar(ct, () => lista.Linhas.Count == 1);
            lista.Linhas[0].Marcada = true;
            var descarte = lista.DescartarCommand.ExecuteAsync(null);
            await Esperar(ct, () => main.Confirmacao is not null);
            main.ResponderConfirmacaoCommand.Execute(true);
            await descarte;
            Assert.Empty(lista.Linhas);
            Assert.Equal("Nenhum bem das notas esperando cadastro.", lista.Resumo);

            // contabilização EM LOTE: só registra a pendência (a tela de lote oferece o cadastro depois)
            Assert.True((await db.App.Pendencias.GravarModoAsync(empresa, ModoContabilizacao.Lote, adm, ct)).Sucesso);
            suite.Trocar(fiscal);
            var nota2 = await NotaAsync(empresa, part, adm, ct, "1002", Item("Servidor", 12000m));
            var textoLote = await BensDasNotas.AposGravarNotasAsync(fiscal.Vm, db.App, empresa, adm, [nota2]);
            Assert.Contains("Bens a cadastrar das notas", textoLote);
            Assert.Contains("Contabilização em lote", textoLote);
            Assert.Same(fiscal, suite.Ativo);                                     // não abriu nada
            Assert.False(main.Bens.EmEdicao);
            suite.Trocar(contabil);
            main.AbrirCommand.Execute(TelasComuns.ContabilizacaoLote);
            await Esperar(ct, () => main.ContabilizacaoLote.TemBensPendentes);
            Assert.True(main.ContabilizacaoLote.TemBensPendentes);
            Assert.Contains("1 item(ns)", main.ContabilizacaoLote.BensPendentes);
            main.ContabilizacaoLote.CadastrarBensDasNotasCommand.Execute(null);
            Assert.Same(main.Bens, main.Conteudo);
            await Esperar(ct, () => main.Bens.EmInclusao && main.Bens.Descricao == "Servidor");
            Assert.Equal(("Servidor", "12.000,00"), (main.Bens.Descricao, main.Bens.Valor));
            main.Bens.CancelarCommand.Execute(null);
            await db.App.Pendencias.GravarModoAsync(empresa, ModoContabilizacao.Online, adm, ct);

            // Fiscal em executável separado (Contábil indisponível): só registra a pendência e avisa
            var sozinho = new Vorticis.Fiscal.Desktop.MainViewModel();
            var nota3 = await NotaAsync(empresa, part, adm, ct, "1003", Item("Impressora", 800m, ncm: "84433299"));
            var textoSozinho = await BensDasNotas.AposGravarNotasAsync(sozinho, db.App, empresa, adm, [nota3]);
            Assert.Contains("registrados em Imobilizado › Bens a cadastrar das notas", textoSozinho);
            Assert.Contains("Abra o Contábil", textoSozinho);
            Assert.Equal(2, (await db.App.Imobilizado.ListarBensACadastrarAsync(empresa, adm, ct: ct)).Count);   // Servidor + Impressora
            return true;
        }, ct));
        Assert.True(ok);
    }
}
