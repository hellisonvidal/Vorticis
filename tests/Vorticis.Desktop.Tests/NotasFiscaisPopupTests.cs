using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Fiscal;
using Vorticis.Application.Cadastros;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Desktop.Views;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Sefaz;
using Vorticis.Fiscal.Tests;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Notas de entrada e de saída pela interface (janela headless 1366x768): grade de consulta + pop-up de digitação com as abas
/// Nota, Itens e Tributos, importação por XML e por chave de acesso (SEFAZ simulada por um servidor HTTP local).
/// </summary>
public class NotasFiscaisPopupTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 10);

    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t =>
        {
            if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions);
            else if (t.IsCanceled) tcs.SetCanceled();
            else tcs.SetResult(t.Result);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static async Task Aguardar(Func<bool> condicao)
    {
        for (var i = 0; i < 300 && !condicao(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Chave de 44 dígitos com DV módulo 11 (implementação independente da do código).</summary>
    private static string Chave(string cnpjEmit, int numero)
    {
        var b = $"3526{"03"}{cnpjEmit}55001{numero:D9}1{numero:D8}"[..43];
        int soma = 0, peso = 2;
        foreach (var c in b.Reverse()) { soma += (c - '0') * peso; peso = peso == 9 ? 2 : peso + 1; }
        var r = soma % 11;
        return b + (r < 2 ? 0 : 11 - r);
    }

    private static void Foto(Window janela, string nome)
    {
        if (Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS") is not { } pasta) return;
        Dispatcher.UIThread.RunJobs();
        janela.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, nome + ".png"));
    }

    private sealed record Cenario(Window Janela, DigitacaoDocumentoView View, DigitacaoDocumentoViewModel Vm);

    private static async Task<Cenario> AbrirAsync(DigitacaoDocumentoViewModel vm)
    {
        var view = new DigitacaoDocumentoView { DataContext = vm };
        var janela = new Window { Content = view, Width = 1366, Height = 768 };
        janela.Show();
        await vm.CarregarAsync();
        vm.FiltroInicio = new DateTime(2026, 3, 1);
        vm.FiltroFim = new DateTime(2026, 3, 31);
        await vm.ConsultarCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        return new Cenario(janela, view, vm);
    }

    private async Task<long> FornecedorAsync(long empresa, long adm, string cnpj, string nome)
    {
        var r = await db.App.Participantes.GravarAsync(empresa, new Participante(0, "", nome, null, cnpj, null, "SP", null, null, null, null, false, true, null, true), adm);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        return r.Valor;
    }

    [Fact]
    public async Task Fluxo_completo_digitado_novo_editar_consultar_excluir_pelo_popup()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync(XmlsDeTeste.CnpjEmpresa);
        await FornecedorAsync(empresa, adm, XmlsDeTeste.CnpjFornecedor, "Fornecedor Alfa Ltda");
        var falhas = new List<string>();
        void Confere(bool ok, string msg) { if (!ok) falhas.Add(msg); }

        await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaEntradaViewModel(new ContextoTeste(db.App, adm, empresa));
            var (janela, view, _) = await AbrirAsync(vm);
            Confere(vm.Lista.Count == 0 && !vm.DigitacaoAberta, "grade deveria abrir vazia e sem pop-up");

            // ---- novo lançamento: abre o pop-up modal
            vm.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var popup = view.PopupAberto;
            Confere(vm.DigitacaoAberta && popup is not null, "Novo lançamento deve abrir o pop-up");
            if (popup is null) return true;
            popup.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var conteudo = (NotaDigitacaoView)popup.Content!;
            var abas = conteudo.GetVisualDescendants().OfType<TabControl>().First();
            Confere(abas.Items.OfType<TabItem>().Select(t => t.Header?.ToString()).SequenceEqual(["Nota", "Itens", "Tributos", "Parcelas", "Escrituração"]),
                    "abas: " + string.Join(",", abas.Items.OfType<TabItem>().Select(t => t.Header)));
            Confere(popup.Width <= 1366 && popup.Height <= 768, $"pop-up maior que 1366x768: {popup.Width}x{popup.Height}");
            var gravar = conteudo.GetVisualDescendants().OfType<Button>().First(b => b.Name == "BotaoGravar");
            var pos = Avalonia.VisualExtensions.TranslatePoint(gravar, new Avalonia.Point(0, gravar.Bounds.Height), conteudo);
            Confere(pos is { } p && p.Y <= conteudo.Bounds.Height + 0.5, $"botão Gravar fora da área visível: {pos} em {conteudo.Bounds}");

            Foto(popup, "nota_popup_aba_nota");
            // ---- cabeçalho: participante por CNPJ, itens, tributos
            vm.Numero = "1001";
            vm.Emissao = new DateTime(2026, 3, 10);
            vm.Participante = Vorticis.Domain.Cadastros.Cnpj.Formatar(XmlsDeTeste.CnpjFornecedor);
            await Aguardar(() => vm.ParticipanteInfo.Contains("cadastrado: "));
            Confere(vm.ParticipanteInfo.Contains("Fornecedor Alfa"), "participante pelo CNPJ: " + vm.ParticipanteInfo);
            Confere(((TextBox)conteudo.FindControl<TextBox>("CampoNumero")!).Text == "1001", "campo Número não reflete o VM");
            vm.Item.Descricao = "Parafuso";
            vm.Item.Cfop = "1102";
            vm.Item.Quantidade = "2";
            vm.Item.Unitario = "10";
            vm.Item.BcIcms = "20";
            vm.Item.AliqIcms = "18";
            Confere(vm.Item.Icms == "3,6", $"ICMS = base x alíquota deveria ser 3,6, veio '{vm.Item.Icms}'");
            vm.Item.Icms = "3";                                   // sobrescreve o valor calculado
            Confere(vm.Item.Icms == "3", "o valor do ICMS deveria aceitar correção manual");
            await vm.AdicionarItemCommand.ExecuteAsync(null);
            Confere(vm.Itens.Count == 1 && vm.Itens[0].Item.Icms == 3m && vm.Itens[0].Item.BcIcms == 20m, "item sem os tributos digitados");
            Confere(vm.Totais.Contains("ICMS R$ 3,00") || vm.Totais.Contains("ICMS 3,00"), "totais da nota: " + vm.Totais);
            vm.ParcelasQuantidade = "2";
            vm.GerarParcelasCommand.Execute(null);
            Confere(vm.Parcelas.Count == 2 && vm.Parcelas.Sum(x => x.Valor) == 20m, "parcelas devem somar o total da nota");

            abas.SelectedIndex = 1; Foto(popup, "nota_popup_aba_itens");
            abas.SelectedIndex = 2; vm.ItemSelecionado = vm.Itens[0]; Foto(popup, "nota_popup_aba_tributos");
            abas.SelectedIndex = 0;
            // ---- gravar: fecha o pop-up e atualiza a grade
            await vm.SalvarCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.Erros.Count == 0, "erros ao gravar: " + string.Join("; ", vm.Erros));
            Confere(!vm.DigitacaoAberta && view.PopupAberto is null, "gravar deveria fechar o pop-up");
            Confere(vm.Mensagem == "Nota gravada.", "mensagem após gravar: " + vm.Mensagem);
            Confere(vm.Lista.Count == 1 && vm.Lista[0].Numero == "1001", "grade deveria listar a nota gravada");
            Confere(vm.TotaisLista.StartsWith("1 nota(s)") && vm.TotaisLista.Contains("20,00"), "rodapé: " + vm.TotaisLista);
            var gravada = (await db.App.Fiscal().Digitacao.ObterAsync(empresa, vm.Lista[0].Id, adm, ct)).Valor;
            Confere(gravada.Itens[0] is { Icms: 3m, AliqIcms: 18m, BcIcms: 20m } && gravada.Totais.Icms == 3m, "tributos não gravaram no banco");

            // ---- editar (duplo clique / botão): recarrega, altera alíquota => valor recalcula
            vm.Selecionada = vm.Lista[0];
            await vm.EditarCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.DigitacaoAberta && view.PopupAberto is not null && vm.Itens.Count == 1 && vm.Parcelas.Count == 2, "editar deveria abrir o pop-up com a nota");
            vm.ItemSelecionado = vm.Itens[0];
            Confere(vm.Item.Descricao == "Parafuso" && vm.Item.Quantidade == "2", "item selecionado não carregou no formulário");
            vm.Item.AliqIcms = "12";
            Confere(vm.Item.Icms == "2,4", $"alterar a alíquota deveria recalcular o ICMS (2,4): '{vm.Item.Icms}'");
            await vm.AtualizarItemCommand.ExecuteAsync(null);
            Confere(vm.Itens.Count == 1 && vm.Itens[0].Item.Icms == 2.4m, $"atualizar item: {vm.Itens.Count} {vm.Itens.FirstOrDefault()?.Item.Icms} {string.Join(";", vm.Erros)}");
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && !vm.DigitacaoAberta, "erros ao gravar a alteração: " + string.Join("; ", vm.Erros));
            gravada = (await db.App.Fiscal().Digitacao.ObterAsync(empresa, vm.Lista[0].Id, adm, ct)).Valor;
            Confere(gravada.Itens[0].Icms == 2.4m && gravada.Totais.Icms == 2.4m, "alteração não gravou");

            // ---- consultar: somente leitura
            vm.Selecionada = vm.Lista[0];
            await vm.VisualizarCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.DigitacaoAberta && vm.SomenteConsulta && !vm.PodeEditar && !vm.PodeEditarEscrituracao, "consultar deve abrir somente leitura");
            view.PopupAberto?.Close();
            Dispatcher.UIThread.RunJobs();
            Confere(!vm.DigitacaoAberta && !vm.SomenteConsulta, "fechar o pop-up (X) deve encerrar a digitação");

            // ---- excluir
            vm.Selecionada = vm.Lista[0];
            await vm.ExcluirCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 0 && vm.Mensagem == "Nota excluída.", "excluir: " + vm.Mensagem + string.Join(";", vm.Erros));
            janela.Close();
            return true;
        }, ct));
        Assert.Empty(falhas);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", empresa)));
    }

    [Fact]
    public async Task Nota_importada_abre_no_popup_e_so_a_escrituracao_grava()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("27865757000102");
        var pasta = Directory.CreateTempSubdirectory("popup_imp").FullName;
        var arq = Path.Combine(pasta, "compra.xml");
        await File.WriteAllBytesAsync(arq, XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, "27865757000102", 1, 810, Mar,
            [new ItemNfe("5102", 500m, Icms: 90m)], chave: Chave(XmlsDeTeste.CnpjFornecedor, 810), nomeEmit: "Fornecedor Beta")), ct);
        var falhas = new List<string>();
        void Confere(bool ok, string msg) { if (!ok) falhas.Add(msg); }
        await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaEntradaViewModel(new ContextoTeste(db.App, adm, empresa));
            var (janela, view, _) = await AbrirAsync(vm);
            vm.Importador.CompletarPeloCnpj = false;
            await vm.Importador.ImportarArquivosAsync([arq]);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.Lista.Count == 1 && vm.Lista[0].Origem == "XML", "nota importada deveria aparecer na grade");
            await vm.EditarCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.DigitacaoAberta && vm.Importada && !vm.PodeEditar && vm.PodeEditarEscrituracao, "nota importada: dados bloqueados, escrituração liberada");
            Confere(vm.Itens.Count == 1 && vm.Itens[0].Item.Icms == 90m, "tributos do XML deveriam aparecer nos itens");
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("Gravar escrituração")), "Gravar nota numa importada deveria orientar");
            vm.ItemSelecionado = vm.Itens[0];
            vm.Item.Cfop = "1102";
            await vm.AplicarEscrituracaoItemCommand.ExecuteAsync(null);
            vm.Observacoes = "Conferida";
            await vm.SalvarEscrituracaoCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && !vm.DigitacaoAberta && vm.Mensagem == "Escrituração gravada.", "escrituração: " + string.Join(";", vm.Erros) + vm.Mensagem);
            var g = (await db.App.Fiscal().Digitacao.ObterAsync(empresa, vm.Lista[0].Id, adm, ct)).Valor;
            Confere(g.Itens[0].Cfop == "1102" && g.Itens[0].CfopOriginal == "5102" && g.Observacoes == "Conferida", "escrituração não gravou");
            janela.Close();
            return true;
        }, ct));
        Assert.Empty(falhas);
        Directory.Delete(pasta, true);
    }

    private static X509Certificate2 CertificadoDeTeste()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=EMPRESA TESTE:27865757000102", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
    }

    /// <summary>Servidor local que responde como o NFeDistribuicaoDFe (SOAP); guarda as requisições recebidas.</summary>
    private sealed class SefazSimulada : IDisposable
    {
        private readonly HttpListener _l = new();
        public List<string> Corpos { get; } = [];
        public Func<string, string> Resposta { get; set; } = _ => "";
        public Uri Url { get; }

        public SefazSimulada()
        {
            var porta = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            porta.Start();
            var n = ((IPEndPoint)porta.LocalEndpoint).Port;
            porta.Stop();
            Url = new Uri($"http://127.0.0.1:{n}/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx");
            _l.Prefixes.Add($"http://127.0.0.1:{n}/");
            _l.Start();
            _ = Task.Run(async () =>
            {
                while (_l.IsListening)
                {
                    HttpListenerContext c;
                    try { c = await _l.GetContextAsync(); } catch { return; }
                    using var leitor = new StreamReader(c.Request.InputStream);
                    var corpo = await leitor.ReadToEndAsync();
                    lock (Corpos) Corpos.Add(corpo);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(Resposta(corpo));
                    c.Response.ContentType = "application/soap+xml; charset=utf-8";
                    await c.Response.OutputStream.WriteAsync(bytes);
                    c.Response.Close();
                }
            });
        }

        public void Dispose() { _l.Stop(); _l.Close(); }
    }

    [Fact]
    public async Task Importa_por_xml_e_por_chave_preenchendo_o_popup_para_conferencia()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        const string cnpjEmpresa = "11444777000161";
        var empresa = await db.CriarEmpresaAsync(cnpjEmpresa);
        var pasta = Directory.CreateTempSubdirectory("popup_xml").FullName;
        var chave901 = Chave(XmlsDeTeste.CnpjFornecedor, 901);
        var chave902 = Chave(XmlsDeTeste.CnpjFornecedor, 902);
        var chave903 = Chave(XmlsDeTeste.CnpjFornecedor, 903);
        var xml901 = Path.Combine(pasta, "n901.xml");
        await File.WriteAllBytesAsync(xml901, XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpjEmpresa, 1, 901, Mar,
            [new ItemNfe("5102", 300m, Icms: 54m)], chave: chave901, nomeEmit: "Fornecedor Gama SA")), ct);
        var xml902 = XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpjEmpresa, 1, 902, Mar, [new ItemNfe("5102", 120m)], chave: chave902, nomeEmit: "Fornecedor Gama SA");
        var cert = CertificadoDeTeste();
        using var sefaz = new SefazSimulada();
        var falhas = new List<string>();
        void Confere(bool ok, string msg) { if (!ok) falhas.Add(msg); }

        await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaEntradaViewModel(new ContextoTeste(db.App, adm, empresa))
            {
                ObterCertificado = () => Task.FromResult(Vorticis.Domain.Comum.Resultado<X509Certificate2>.Ok(cert)),
                CriarClienteSefaz = c => new ClienteNfe(new HttpClient(), c, true, sefaz.Url),
            };
            var (janela, view, _) = await AbrirAsync(vm);

            // ---- por XML: preenche, não grava; depois "Gravar nota"
            vm.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            await vm.ImportarArquivoParaConferenciaAsync(xml901);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.Erros.Count == 0, "importar XML: " + string.Join("; ", vm.Erros));
            Confere(vm.Numero == "901" && vm.Chave == chave901 && vm.Itens.Count == 1, $"campos do XML: {vm.Numero} {vm.Chave} {vm.Itens.Count}");
            Confere(vm.Itens[0].Cfop == "1102" && vm.Itens[0].Item.Icms == 54m && vm.Itens[0].Item.AliqIcms == 18m, "CFOP de entrada / ICMS do XML");
            Confere(vm.TemNovoParticipante && vm.NovoNome == "Fornecedor Gama SA", "fornecedor novo vindo do XML");
            Confere(vm.Mensagem?.Contains("nada foi gravado") == true, "aviso de conferência: " + vm.Mensagem);
            Confere(await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", empresa)) == 0, "o XML não deveria gravar antes da conferência");
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && !vm.DigitacaoAberta, "gravar após importar XML: " + string.Join("; ", vm.Erros));
            Confere(await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e AND chave_acesso = @c", ("e", empresa), ("c", chave901)) == 1, "nota do XML não gravou");

            // ---- por chave: já lançada => carrega a que está no sistema, sem SEFAZ
            vm.NovoCommand.Execute(null);
            vm.ChaveImportacao = chave901;
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Confere(vm.Numero == "901" && vm.Mensagem?.Contains("já está lançada") == true && sefaz.Corpos.Count == 0, $"chave local: {vm.Numero} {vm.Mensagem} {string.Join(";", vm.Erros)}");

            // ---- validação da chave
            vm.NovoCommand.Execute(null);
            vm.ChaveImportacao = "123";
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("44 dígitos")), "chave curta");
            vm.ChaveImportacao = new string('1', 44);
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("dígito verificador")), "dígito verificador");

            // ---- sem certificado: mensagem clara
            var antes = vm.ObterCertificado;
            vm.ObterCertificado = () => Task.FromResult(Vorticis.Domain.Comum.Resultado<X509Certificate2>.Falha("Não há certificado padrão válido."));
            vm.ChaveImportacao = chave902;
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("certificado digital da empresa")) && sefaz.Corpos.Count == 0, "sem certificado: " + string.Join(";", vm.Erros));
            vm.ObterCertificado = antes;

            // ---- SEFAZ: XML completo
            sefaz.Resposta = _ => DistribuicaoDfeTestesSoap(138, "Documento localizado", ("procNFe_v4.00.xsd", xml902));
            vm.ChaveImportacao = chave902;
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Confere(vm.Erros.Count == 0 && vm.Numero == "902" && vm.Itens.Count == 1 && vm.Itens[0].Item.ValorProduto == 120m, "SEFAZ completa: " + string.Join(";", vm.Erros) + vm.Numero);
            Confere(sefaz.Corpos.Count == 1 && sefaz.Corpos[0].Contains($"<consChNFe><chNFe>{chave902}</chNFe></consChNFe>") && sefaz.Corpos[0].Contains("<tpAmb>1</tpAmb>")
                    && sefaz.Corpos[0].Contains($"<CNPJ>{cnpjEmpresa}</CNPJ>"), "requisição à SEFAZ: " + string.Join("|", sefaz.Corpos));
            Confere(await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e AND chave_acesso = @c", ("e", empresa), ("c", chave902)) == 0, "SEFAZ não deveria gravar sozinha");

            // ---- SEFAZ: só o resumo => avisa da manifestação
            vm.NovoCommand.Execute(null);
            sefaz.Resposta = _ => DistribuicaoDfeTestesSoap(138, "Documento localizado", ("resNFe_v1.01.xsd", "<resNFe/>"));
            vm.ChaveImportacao = chave903;
            await vm.ImportarChaveCommand.ExecuteAsync(null);
            Confere(vm.PodeManifestar && vm.Erros.Any(e => e.Contains("manifestação")) && vm.Itens.Count == 0, "resumo: " + string.Join(";", vm.Erros));
            vm.CancelarDigitacao();
            janela.Close();
            return true;
        }, ct));
        Assert.Empty(falhas);
        cert.Dispose();
        Directory.Delete(pasta, true);
    }

    private static string DistribuicaoDfeTestesSoap(int cStat, string motivo, params (string Schema, string Xml)[] docs)
    {
        static string Zip(string xml)
        {
            using var ms = new MemoryStream();
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(System.Text.Encoding.UTF8.GetBytes(xml));
            return Convert.ToBase64String(ms.ToArray());
        }
        return "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body><nfeDistDFeInteresseResponse xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe\">"
               + $"<nfeDistDFeInteresseResult><retDistDFeInt xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"1.01\"><tpAmb>1</tpAmb><cStat>{cStat}</cStat><xMotivo>{motivo}</xMotivo>"
               + (docs.Length == 0 ? "" : "<loteDistDFeInt>" + string.Concat(docs.Select((d, i) => $"<docZip NSU=\"{i + 1:D15}\" schema=\"{d.Schema}\">{Zip(d.Xml)}</docZip>")) + "</loteDistDFeInt>")
               + "</retDistDFeInt></nfeDistDFeInteresseResult></nfeDistDFeInteresseResponse></soap:Body></soap:Envelope>";
    }

    [Fact]
    public async Task Grade_filtra_por_participante_numero_origem_e_situacao_e_mostra_os_totais()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var fornA = await FornecedorAsync(empresa, adm, XmlsDeTeste.CnpjFornecedor, "Alfa Peças Ltda");
        var fornB = await FornecedorAsync(empresa, adm, XmlsDeTeste.CnpjCliente, "Beta Comércio SA");
        var pasta = Directory.CreateTempSubdirectory("popup_flt").FullName;
        var arq = Path.Combine(pasta, "c.xml");
        await File.WriteAllBytesAsync(arq, XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, "11222333000181", 1, 3003, Mar,
            [new ItemNfe("5102", 70m)], chave: Chave(XmlsDeTeste.CnpjFornecedor, 3003))), ct);
        var falhas = new List<string>();
        void Confere(bool ok, string msg) { if (!ok) falhas.Add(msg); }
        await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaEntradaViewModel(new ContextoTeste(db.App, adm, empresa));
            var (janela, _, _) = await AbrirAsync(vm);
            async Task Lancar(string numero, string cnpj, string valor)
            {
                vm.NovoCommand.Execute(null);
                vm.Numero = numero;
                vm.Emissao = new DateTime(2026, 3, 10);
                vm.Participante = cnpj;
                await Aguardar(() => vm.ParticipanteInfo.Contains("cadastrado: "));
                vm.Item.Descricao = "Item " + numero;
                vm.Item.Cfop = "1102";
                vm.Item.Unitario = valor;
                await vm.AdicionarItemCommand.ExecuteAsync(null);
                await vm.SalvarCommand.ExecuteAsync(null);
                Confere(vm.Erros.Count == 0, $"lançar {numero}: " + string.Join(";", vm.Erros));
            }
            await Lancar("2001", XmlsDeTeste.CnpjFornecedor, "100");
            await Lancar("2002", XmlsDeTeste.CnpjCliente, "250");
            vm.Importador.CompletarPeloCnpj = false;
            await vm.Importador.ImportarArquivosAsync([arq]);
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 3 && vm.TotaisLista.StartsWith("3 nota(s)") && vm.TotaisLista.Contains("420,00"), "totais gerais: " + vm.TotaisLista);

            vm.FiltroParticipante = "beta";
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 1 && vm.Lista[0].Numero == "2002" && vm.TotaisLista.Contains("250,00"), "filtro por participante");
            vm.FiltroParticipante = XmlsDeTeste.CnpjFornecedor[..8];
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 2, "filtro por parte do CNPJ: " + vm.Lista.Count);
            await vm.LimparFiltrosCommand.ExecuteAsync(null);
            vm.FiltroNumero = "3003";
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 1 && vm.Lista[0].Origem == "XML", "filtro por número");
            vm.FiltroNumero = "";
            vm.FiltroOrigem = 1;
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 2 && vm.Lista.All(l => l.Origem == "Digitada"), "filtro por origem digitada");
            vm.FiltroOrigem = 2;
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 1, "filtro por origem importada");
            vm.FiltroOrigem = 0;
            vm.FiltroSituacao = 3;   // contabilizadas
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 0 && vm.TotaisLista.StartsWith("0 nota(s)"), "filtro por situação contabilizada");
            vm.FiltroSituacao = 4;
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Lista.Count == 3, "filtro por situação não contabilizada");
            vm.FiltroFim = new DateTime(2026, 2, 1);
            await vm.ConsultarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("período")), "período invertido");
            janela.Close();
            return true;
        }, ct));
        Assert.Empty(falhas);
        Directory.Delete(pasta, true);
        _ = (fornA, fornB);
    }
    [Fact]
    public async Task Saida_de_servico_com_cliente_novo_por_cpf_calcula_iss_e_regrava_com_parcelas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("26637142000158");
        var falhas = new List<string>();
        void Confere(bool ok, string msg) { if (!ok) falhas.Add(msg); }
        await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaSaidaViewModel(new ContextoTeste(db.App, adm, empresa));
            var (janela, view, _) = await AbrirAsync(vm);
            vm.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            vm.Modelo = 2;   // NFS-e
            vm.Numero = "77";
            vm.Emissao = new DateTime(2026, 3, 5);
            vm.Participante = "529.982.247-25";
            await vm.BuscarParticipanteCommand.ExecuteAsync(null);
            Confere(vm.TemNovoParticipante, "CPF novo deveria pedir o cadastro");
            vm.NovoNome = "Maria Cliente";
            vm.Item.Descricao = "Consultoria";
            vm.Item.Cfop = "NFSE";
            vm.Item.Quantidade = "1";
            vm.Item.Unitario = "1000";
            vm.Item.BcIss = "1000";
            vm.Item.AliqIss = "5";
            Confere(vm.Item.Iss == "50", "ISS = base x alíquota: " + vm.Item.Iss);
            await vm.AdicionarItemCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && vm.Itens.Count == 1, "item de serviço: " + string.Join(";", vm.Erros));
            vm.ParcelasQuantidade = "3";
            vm.ParcelasPrimeiroVencimento = new DateTime(2026, 4, 5);
            vm.GerarParcelasCommand.Execute(null);
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && !vm.DigitacaoAberta && vm.Lista.Count == 1, "gravar saída: " + string.Join(";", vm.Erros));
            var id = vm.Lista.Count == 1 ? vm.Lista[0].Id : 0;
            var g = (await db.App.Fiscal().Digitacao.ObterAsync(empresa, id, adm, ct)).Valor;
            Confere(g.Totais.Iss == 50m && g.Totais.Total == 1000m && g.Modelo == ModeloDocumento.NFSe, "totais da NFS-e");
            Confere(await db.EscalarAsync<long>("SELECT count(*) FROM core.participante WHERE empresa_id = @e AND cpf_cnpj = '52998224725' AND cliente", ("e", empresa)) == 1, "cliente novo não cadastrou");

            // regravar com a emissão depois do vencimento da 1a parcela: o sistema recusa com mensagem clara; corrigindo, grava
            await vm.EditarCommand.ExecuteAsync(null);
            vm.Selecionada = vm.Lista[0];
            await vm.EditarCommand.ExecuteAsync(null);
            Confere(vm.DigitacaoAberta && vm.Parcelas.Count == 3 && vm.Participante == "Maria Cliente", $"editar saída: {vm.Parcelas.Count} {vm.Participante}");
            vm.Emissao = new DateTime(2026, 4, 20);
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Any(e => e.Contains("anterior à emissão")) && vm.DigitacaoAberta, "vencimento antes da emissão: " + string.Join(";", vm.Erros));
            vm.GerarParcelasCommand.Execute(null);   // primeiro vencimento 05/04 ainda antes: ajusta
            vm.ParcelasPrimeiroVencimento = new DateTime(2026, 5, 20);
            vm.GerarParcelasCommand.Execute(null);
            await vm.SalvarCommand.ExecuteAsync(null);
            Confere(vm.Erros.Count == 0 && !vm.DigitacaoAberta, "regravar: " + string.Join(";", vm.Erros));
            janela.Close();
            return true;
        }, ct));
        Assert.Empty(falhas);
    }
}
