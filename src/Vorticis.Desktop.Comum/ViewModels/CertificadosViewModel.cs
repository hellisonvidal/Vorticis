using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Sistema;

namespace Vorticis.Desktop.Comum.ViewModels;

public sealed record CertificadoLinha(CertificadoDigital C, DateTimeOffset Agora)
{
    public string Tipo => C.Tipo;
    public string Descricao => C.Descricao;
    public string Titular => C.Titular;
    public string Documento => C.Documento switch
    {
        { Length: 14 } d => Domain.Cadastros.Cnpj.Formatar(d),
        { Length: 11 } d => Domain.Cadastros.Cpf.Formatar(d),
        var d => d ?? "",
    };
    public string Validade => $"{C.ValidoDe.ToLocalTime():dd/MM/yyyy} a {C.ValidoAte.ToLocalTime():dd/MM/yyyy}";
    public string Situacao => C.Situacao(Agora);
    public bool Alerta => C.DiasParaVencer(Agora) <= 30;
    public string Emissor => C.Emissor;
}

/// <summary>
/// Configurações › Certificados digitais (administrador): repositório dos certificados A1 (.pfx importado) e A3
/// (token/cartão, registrado a partir do Windows) usados na comunicação com órgãos públicos.
/// </summary>
public partial class CertificadosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<CertificadoLinha> Lista { get; } = [];
    public ObservableCollection<DadosCertificado> Instalados { get; } = [];
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ExcluirCommand))] private CertificadoLinha? _selecionado;
    [ObservableProperty] private DadosCertificado? _instaladoSelecionado;

    // Importação de A1
    [ObservableProperty] private string _arquivo = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _conferencia = "";
    [ObservableProperty] private string _avisoVencimento = "";

    public bool PodeAlterar => Contexto.Sessao?.Administrador == true;

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeAlterar));
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var r = await app.Certificados.ListarAsync(sessao);
        if (!EhAtual(g)) return;
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var c in r.Valor) Lista.Add(new CertificadoLinha(c, app.Certificados.Agora));
        var alertas = Lista.Where(l => l.Alerta).Select(l => $"{l.Descricao}: {l.Situacao.ToLowerInvariant()}").ToList();
        AvisoVencimento = alertas.Count == 0 ? "" : "Atenção — " + string.Join("; ", alertas) + ".";
    }

    partial void OnArquivoChanged(string value) => Conferencia = "";

    /// <summary>Confere o .pfx com a senha (titular, CNPJ/CPF e validade) sem gravar.</summary>
    [RelayCommand]
    private void Conferir()
    {
        Erros.Clear();
        Mensagem = null;
        if (Ler() is { } d)
            Conferencia = $"{d.Titular}{(d.Documento is { } doc ? $" — {Formatar(doc)}" : "")} · válido até {d.ValidoAte.ToLocalTime():dd/MM/yyyy} · emissor {d.Emissor}";
    }

    private DadosCertificado? Ler()
    {
        if (string.IsNullOrWhiteSpace(Arquivo) || !File.Exists(Arquivo)) { Erros.Add("Escolha o arquivo do certificado A1 (.pfx ou .p12)."); return null; }
        var r = ServicoCertificados.LerPfx(File.ReadAllBytes(Arquivo), Senha);
        if (!r.Sucesso) { MostrarResultado(r, ""); return null; }
        return r.Valor;
    }

    [RelayCommand]
    private Task ImportarAsync() => ExecutarAdminAsync(async (app, sessao) =>
    {
        if (string.IsNullOrWhiteSpace(Arquivo) || !File.Exists(Arquivo)) { Erros.Add("Escolha o arquivo do certificado A1 (.pfx ou .p12)."); return; }
        var r = await app.Certificados.ImportarA1Async(sessao, await File.ReadAllBytesAsync(Arquivo), Senha, Descricao, Contexto.EmpresaId);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (Arquivo, Senha, Descricao, Conferencia) = ("", "", "", "");
        await CarregarAsync();
        Mensagem = "Certificado A1 importado. A senha não é gravada: ela será pedida quando o certificado for usado.";
    });

    /// <summary>Lê os certificados com chave privada instalados no Windows (tokens e cartões A3).</summary>
    [RelayCommand]
    private void LerInstalados()
    {
        Erros.Clear();
        Instalados.Clear();
        try
        {
            foreach (var d in ServicoCertificados.ListarInstaladosNoWindows()) Instalados.Add(d);
            Mensagem = Instalados.Count == 0
                ? "Nenhum certificado com chave privada no repositório do Windows. Conecte o token/cartão e tente de novo."
                : $"{Instalados.Count} certificado(s) encontrado(s). Escolha um e clique em Registrar A3.";
        }
        catch (Exception ex) { Erros.Add($"Não foi possível ler o repositório do Windows: {ex.Message}"); }
    }

    [RelayCommand]
    private Task RegistrarA3Async() => ExecutarAdminAsync(async (app, sessao) =>
    {
        if (InstaladoSelecionado is not { } d) { Erros.Add("Escolha um certificado instalado."); return; }
        var r = await app.Certificados.RegistrarA3Async(sessao, d, Descricao, Contexto.EmpresaId);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Descricao = "";
        await CarregarAsync();
        Mensagem = "Certificado A3 registrado.";
    });

    private bool TemSelecionado() => Selecionado is not null;

    [RelayCommand(CanExecute = nameof(TemSelecionado))]
    private Task ExcluirAsync() => ExecutarAdminAsync(async (app, sessao) =>
    {
        if (Selecionado is not { } s) return;
        if (!await Contexto.ConfirmarAsync("Excluir certificado", $"Excluir o certificado \"{s.Descricao}\" do repositório?" +
                (s.Tipo == "A1" ? "\n\nO arquivo .pfx guardado será apagado; tenha uma cópia se for usá-lo de novo." : ""), "Excluir", "Cancelar"))
            return;
        var r = await app.Certificados.ExcluirAsync(sessao, s.C.Id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = $"Certificado \"{s.Descricao}\" excluído.";
    });

    private async Task ExecutarAdminAsync(Func<Infra.VorticisApp, Domain.Seguranca.SessaoUsuario, Task> acao)
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        if (!sessao.Administrador) { Erros.Add("Somente o administrador mantém os certificados digitais."); return; }
        try
        {
            Ocupado = true;
            await acao(app, sessao);
        }
        catch (Exception ex) { Erros.Add(ex.Message); }
        finally { Ocupado = false; }
    }

    private static string Formatar(string doc) => doc.Length == 14 ? Domain.Cadastros.Cnpj.Formatar(doc) : Domain.Cadastros.Cpf.Formatar(doc);
}
