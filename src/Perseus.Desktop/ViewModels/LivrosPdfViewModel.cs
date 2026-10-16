using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Relatorios;
using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

/// <summary>Relatórios › Livros e relatórios em PDF.</summary>
public partial class LivrosPdfViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private decimal? _numeroLivro = 1;
    [ObservableProperty] private string _representante = "";
    [ObservableProperty] private string _representanteCpf = "";
    [ObservableProperty] private string _contador = "";
    [ObservableProperty] private string _crc = "";
    [ObservableProperty] private string _dataArquivamento = "";
    [ObservableProperty] private string? _ultimoArquivo;

    public bool PodeDiario => Contexto.Pode(Permissao.RelatorioDiario);
    public bool PodeRazao => Contexto.Pode(Permissao.RelatorioRazao);
    public bool PodeBalancete => Contexto.Pode(Permissao.RelatorioBalancete);
    public bool PodeDemonstracoes => Contexto.Pode(Permissao.RelatorioDemonstracoes);

    /// <summary>Preenche os termos com o representante legal (Sócios) e o responsável técnico (Contadores) da empresa.</summary>
    public async Task PreencherResponsaveisAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var rep = await app.Responsaveis.RepresentanteLegalAsync(empresa);
        var cont = await app.Responsaveis.ResponsavelTecnicoAsync(empresa);
        if (!EhAtual(g)) return;
        (Representante, RepresentanteCpf) = rep is null ? ("", "") : (rep.Nome, rep.CpfCnpj is { Length: 11 } c ? Domain.Cadastros.Cpf.Formatar(c) : rep.CpfCnpj ?? "");
        (Contador, Crc) = cont is null ? ("", "") : (cont.Nome, cont.CrcCompleto);
    }

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeDiario));
        OnPropertyChanged(nameof(PodeRazao));
        OnPropertyChanged(nameof(PodeBalancete));
        OnPropertyChanged(nameof(PodeDemonstracoes));
    }

    private (DateOnly Ini, DateOnly Fim) Periodo()
    {
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        return (ini, Data(Fim, ini));
    }

    private string Assinatura(string nome, string complemento) =>
        string.IsNullOrWhiteSpace(nome) ? "" : $"{nome.Trim()}{(string.IsNullOrWhiteSpace(complemento) ? "" : " — " + complemento.Trim())}";

    private void Concluir(Resultado<ArquivoGerado> r, string nome)
    {
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        UltimoArquivo = ArquivosGerados.Salvar(nome, r.Valor.Conteudo);
        Mensagem = $"{r.Valor.Paginas} página(s) gerada(s): {UltimoArquivo}";
    }

    private string Nome(string tipo) { var (i, f) = Periodo(); return $"{tipo}_{i:yyyyMMdd}_{f:yyyyMMdd}.pdf"; }

    [RelayCommand]
    private Task DiarioAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var (ini, fim) = Periodo();
        Concluir(await app.Livros.DiarioAsync(empresa, ini, fim,
            new DadosTermos((int)(NumeroLivro ?? 1), Representante, RepresentanteCpf, Contador, Crc,
                            string.IsNullOrWhiteSpace(DataArquivamento) ? null : DataArquivamento.Trim()), usuario), Nome($"livro_diario_{NumeroLivro}"));
    }, Permissao.RelatorioDiario);

    [RelayCommand]
    private Task RazaoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var (ini, fim) = Periodo();
        Concluir(await app.Livros.RazaoAsync(empresa, ini, fim, usuario), Nome("livro_razao"));
    }, Permissao.RelatorioRazao);

    [RelayCommand]
    private Task BalanceteAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var (ini, fim) = Periodo();
        Concluir(await app.Livros.BalanceteAsync(empresa, ini, fim, usuario), Nome("balancete"));
    }, Permissao.RelatorioBalancete);

    [RelayCommand]
    private Task DemonstracoesAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var (ini, fim) = Periodo();
        Concluir(await app.Livros.DemonstracoesAsync(empresa, ini, fim, 3, Assinatura(Contador, string.IsNullOrWhiteSpace(Crc) ? "" : "Contador — CRC " + Crc),
            Assinatura(Representante, "Representante legal"), usuario), Nome("demonstracoes"));
    }, Permissao.RelatorioDemonstracoes);
}
