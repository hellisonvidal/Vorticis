using System.Text;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Integrações › Importar SPED: reconhecimento do arquivo, prévia, importação, relatório e desfazer (com importadores de mentira).</summary>
public class ImportacaoSpedTelaTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sped_tela_").FullName;

    public void Dispose() { try { Directory.Delete(_pasta, true); } catch (IOException) { } }

    private sealed class Falso(TipoArquivoSped tipo) : IImportadorSped
    {
        public List<HistoricoImportacao> Hist { get; } = [];
        public List<OpcoesImportacaoSped> Previas { get; } = [];
        public int Importacoes { get; private set; }
        public bool Bloqueia { get; set; }
        public TipoArquivoSped Tipo => tipo;
        public string Titulo => "Falso";
        public IReadOnlyList<ModoImportacao> Modos { get; } = [new("TUDO", "Tudo", "Importa tudo."), new("PLANO", "Só o plano", "Só o plano.")];

        public Task<Resultado<PreviaImportacao>> PreviaAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct = default)
        {
            Previas.Add(opcoes);
            var cab = new CabecalhoSped(tipo, "11222333000181", "EMPRESA TESTE", "SP", null, new(2025, 1, 1), new(2025, 12, 31));
            List<OcorrenciaImportacao> oc = Bloqueia ? [OcorrenciaImportacao.Erro(1, "0000", "CNPJ diferente.")] : [OcorrenciaImportacao.Aviso(5, "I050", "Conta sem natureza.")];
            return Task.FromResult(Resultado<PreviaImportacao>.Ok(new PreviaImportacao(cab, !Bloqueia, null, null,
                [new LinhaPrevia("I050", "Plano de contas", 10, 8, 2, 0, ["1.1.01"])], oc)));
        }

        public Task<Resultado<RelatorioImportacao>> ImportarAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId,
                                                                  IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default)
        {
            Importacoes++;
            progresso?.Report(new ProgressoImportacao("Plano", 1, 1));
            Hist.Add(new HistoricoImportacao(7, tipo.Codigo(), nomeArquivo, new(2025, 1, 1), new(2025, 12, 31), opcoes.Modo, DateTime.UtcNow, "admin", false, 8));
            return Task.FromResult(Resultado<RelatorioImportacao>.Ok(new RelatorioImportacao(7, nomeArquivo, tipo.Nome(), opcoes.Modo,
                [new Domain.Sped.Importacao.LinhaRelatorio("I050", "Plano de contas", 8, 2, 0)], [])));
        }

        public Task<IReadOnlyList<HistoricoImportacao>> HistoricoAsync(long empresaId, long usuarioId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<HistoricoImportacao>>([.. Hist]);

        public Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
        {
            var h = Hist.Single(x => x.Id == importacaoId);
            Hist[Hist.IndexOf(h)] = h with { Desfeita = true };
            return Task.FromResult(Resultado<RelatorioImportacao>.Ok(new RelatorioImportacao(importacaoId, h.Arquivo, tipo.Nome(), h.Modo,
                [new Domain.Sped.Importacao.LinhaRelatorio("I050", "Plano de contas", 8, 0, 0)], [], true)));
        }
    }

    private async Task<(ImportacaoSpedViewModel Vm, Falso Ecd)> CriarAsync(params TipoArquivoSped[] tipos)
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Novo(), 2025);
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa);
        var lista = tipos.Select(t => new Falso(t)).ToList();
        var vm = new ImportacaoSpedViewModel(ctx, "Importar SPED", "Aceita ECD.", _ => lista);
        await vm.CarregarAsync();
        return (vm, lista[0]);
    }

    private string Arquivo(string conteudo, string nome = "sped.txt")
    {
        var p = Path.Combine(_pasta, nome);
        File.WriteAllBytes(p, Encoding.Latin1.GetBytes(conteudo));
        return p;
    }

    private const string Ecd = "|0000|LECD|01012025|31122025|EMPRESA TESTE|11222333000181|SP|\r\n|9999|2|\r\n";

    [Fact]
    public async Task Arquivo_ecd_e_reconhecido_e_mostra_a_previa()
    {
        var (vm, _) = await CriarAsync(TipoArquivoSped.Ecd);
        vm.Arquivo = Arquivo(Ecd);
        await vm.LerArquivoAsync();
        Assert.Empty(vm.Erros);
        Assert.Equal("ECD (SPED Contábil)", vm.TipoDetectado);
        Assert.Equal(2, vm.Modos.Count);
        Assert.Equal("TUDO", vm.ModoSelecionado!.Codigo);
        Assert.True(vm.MostraOpcoesPlano);
        Assert.True(vm.MostraSignatarios);
        Assert.Equal("01/01/2025 a 31/12/2025", vm.Periodo);
        Assert.Single(vm.Previa);
        Assert.Single(vm.Ocorrencias);
        Assert.Contains("8 registro(s) novo(s)", vm.ResumoPrevia);
        Assert.True(vm.PreviaPronta);
        Assert.True(vm.ImportarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Arquivo_que_nao_e_sped_ou_e_de_outro_modulo_mostra_mensagem()
    {
        var (vm, _) = await CriarAsync(TipoArquivoSped.EfdIcmsIpi);
        vm.Arquivo = Arquivo("isto não é um arquivo SPED");
        await vm.LerArquivoAsync();
        Assert.NotEmpty(vm.Erros);
        Assert.False(vm.PreviaPronta);
        Assert.Empty(vm.Modos);

        vm.Arquivo = Arquivo(Ecd);                       // ECD numa tela só de EFD
        await vm.LerArquivoAsync();
        Assert.Contains(vm.Erros, e => e.Contains("ECD (SPED Contábil)") && e.Contains("não é importado por esta tela"));
        Assert.False(vm.ImportarCommand.CanExecute(null));

        vm.Arquivo = Path.Combine(_pasta, "nao_existe.txt");
        await vm.LerArquivoAsync();
        Assert.Contains(vm.Erros, e => e.Contains("Escolha o arquivo"));
    }

    [Fact]
    public async Task Previa_com_erro_bloqueia_a_importacao()
    {
        var (vm, ecd) = await CriarAsync(TipoArquivoSped.Ecd);
        ecd.Bloqueia = true;
        vm.Arquivo = Arquivo(Ecd);
        await vm.LerArquivoAsync();
        Assert.False(vm.PreviaPronta);
        Assert.False(vm.ImportarCommand.CanExecute(null));
        Assert.Contains("não é o da empresa ativa", vm.EmpresaDoArquivo);
        Assert.Equal("Erro", vm.Ocorrencias[0].Gravidade);
    }

    [Fact]
    public async Task Mudar_o_modo_ou_as_opcoes_refaz_a_previa_com_as_opcoes_escolhidas()
    {
        var (vm, ecd) = await CriarAsync(TipoArquivoSped.Ecd);
        vm.Arquivo = Arquivo(Ecd);
        await vm.LerArquivoAsync();
        var antes = ecd.Previas.Count;
        vm.ModoSelecionado = vm.Modos[1];
        vm.SubstituirPlano = true;
        for (var i = 0; i < 100 && ecd.Previas.Count < antes + 2; i++) await Task.Delay(50, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var ultima = ecd.Previas[^1];
        Assert.Equal("PLANO", ultima.Modo);
        Assert.True(ultima.SubstituirPlano);
        Assert.Equal("Só o plano", vm.ModoSelecionado!.Titulo);
        Assert.Equal("Só o plano.", vm.ModoDescricao);
    }

    [Fact]
    public async Task Importar_mostra_relatorio_atualiza_historico_e_desfazer_marca_como_desfeita()
    {
        var (vm, ecd) = await CriarAsync(TipoArquivoSped.Ecd);
        vm.Arquivo = Arquivo(Ecd, "ecd_2025.txt");
        await vm.LerArquivoAsync();

        await vm.ImportarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.Equal(1, ecd.Importacoes);
        Assert.Contains("Importação concluída", vm.Mensagem);
        Assert.Equal("8 registro(s) importado(s), 2 já existente(s) ignorado(s), 0 erro(s), 0 aviso(s).", vm.ResumoRelatorio);
        Assert.Single(vm.RelatorioLinhas);
        Assert.Contains("I050;\"Plano de contas\";8;2;0", vm.RelatorioCsv);
        Assert.Single(vm.Historico);
        Assert.False(vm.PreviaPronta);                  // o mesmo arquivo não importa de novo
        Assert.Equal(100, vm.Progresso);

        vm.HistoricoSelecionado = vm.Historico[0];
        Assert.True(vm.DesfazerCommand.CanExecute(null));
        await vm.DesfazerCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.Contains("Importação desfeita", vm.Mensagem);
        Assert.True(vm.Historico[0].Desfeita);
        Assert.Equal("Desfeita", vm.Historico[0].Situacao);
    }

    [Fact]
    public async Task Sem_empresa_ou_sessao_nada_e_feito()
    {
        BancoDeTeste.ExigirBanco();
        var vm = new ImportacaoSpedViewModel(new ContextoDeTeste(null, null, null), "Importar SPED", "", _ => []);
        await vm.CarregarAsync();
        Assert.Empty(vm.Historico);
        vm.Arquivo = Arquivo(Ecd);
        await vm.LerArquivoAsync();
        Assert.NotEmpty(vm.Erros);
        Assert.False(vm.ImportarCommand.CanExecute(null));
    }
}

/// <summary>CNPJ válido e diferente a cada chamada (o banco de teste é compartilhado entre as execuções).</summary>
internal static class CnpjsDeTeste
{
    public static string Novo()
    {
        var d = new int[14];
        var b = Random.Shared.NextInt64(10_000_000, 99_999_999).ToString() + "0001";
        for (var i = 0; i < 12; i++) d[i] = b[i] - '0';
        for (var n = 12; n < 14; n++)
        {
            var soma = 0; var peso = 2;
            for (var i = n - 1; i >= 0; i--) { soma += d[i] * peso; peso = peso == 9 ? 2 : peso + 1; }
            var r = soma % 11;
            d[n] = r < 2 ? 0 : 11 - r;
        }
        return string.Concat(d);
    }
}
