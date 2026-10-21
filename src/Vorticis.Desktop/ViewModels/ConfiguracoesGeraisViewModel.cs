using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Sistema;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Configurações › Configurações gerais (administrador): tipo de contabilização da tela de lançamento.</summary>
public partial class ConfiguracoesGeraisViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PartidaSimples))] private bool _partidaDobrada = true;

    public bool PartidaSimples
    {
        get => !PartidaDobrada;
        set => PartidaDobrada = !value;
    }

    public bool PodeAlterar => Contexto.Sessao?.Administrador == true;

    public static string SenhaPadrao => ServicoConfiguracoesGerais.SenhaAdministradorPadrao;

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeAlterar));
        if (Contexto.App is not { } app) return;
        var c = await app.ConfiguracoesGerais.ObterAsync();
        PartidaDobrada = c.ModoLancamento == ModoLancamento.PartidaDobrada;
        SenhaPadraoEmUso = await app.ConfiguracoesGerais.UsandoSenhaPadraoAsync();
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var r = await app.ConfiguracoesGerais.SalvarAsync(sessao,
            new ConfiguracoesGerais(PartidaDobrada ? ModoLancamento.PartidaDobrada : ModoLancamento.PartidaSimples));
        MostrarResultado(r, "Configurações salvas. A tela de lançamento passa a usar o novo modo na próxima abertura.");
    }

    // ------------------------------------------------------------------ senha de administrador

    [ObservableProperty] private bool _senhaPadraoEmUso;
    [ObservableProperty] private string _senhaAtual = "";
    [ObservableProperty] private string _novaSenha = "";
    [ObservableProperty] private string _confirmacaoNovaSenha = "";

    /// <summary>Troca a senha de administrador (pedida para restaurar backups e limpar o banco).</summary>
    [RelayCommand]
    private async Task AlterarSenhaAdministradorAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var r = await app.ConfiguracoesGerais.AlterarSenhaAdministradorAsync(sessao, SenhaAtual, NovaSenha, ConfirmacaoNovaSenha);
        MostrarResultado(r, "Senha de administrador alterada.");
        if (!r.Sucesso) return;
        (SenhaAtual, NovaSenha, ConfirmacaoNovaSenha) = ("", "", "");
        SenhaPadraoEmUso = await app.ConfiguracoesGerais.UsandoSenhaPadraoAsync();
    }

    // ------------------------------------------------------------------ limpeza do banco (não recomendada)

    [ObservableProperty] private string _confirmacaoLimpeza = "";

    /// <summary>
    /// Apaga todas as empresas, cadastros e usuários. Pede para digitar LIMPAR e, num alerta, a senha de administrador;
    /// gera um backup antes (se não conseguir, pergunta se continua), limpa e volta à tela de login, onde se entra com
    /// o administrador padrão (ADMIN / VORTICIS).
    /// </summary>
    [RelayCommand]
    private async Task LimparBancoAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        if (!sessao.Administrador) { Erros.Add("Somente o administrador pode limpar o banco de dados."); return; }
        if (ConfirmacaoLimpeza.Trim() != ServicoConfiguracoesGerais.TextoConfirmacaoLimpeza)
        {
            Erros.Add($"Para continuar, digite {ServicoConfiguracoesGerais.TextoConfirmacaoLimpeza} no campo de confirmação.");
            return;
        }
        var senha = await Contexto.SolicitarSenhaAsync("Limpar banco de dados — NÃO RECOMENDADO",
            "ATENÇÃO: esta operação apaga DEFINITIVAMENTE todas as empresas e toda a escrituração, os cadastros, os usuários e os " +
            "perfis personalizados. Ao final o sistema volta à tela de login, onde se entra com o administrador padrão " +
            $"(usuário {ServicoConfiguracoesGerais.LoginAdministradorPadrao}, senha {ServicoConfiguracoesGerais.SenhaAdministradorPadrao}).\n\n" +
            "Um backup será gerado antes. Para confirmar, informe a senha de administrador:", "Limpar tudo", "Cancelar");
        if (senha is null) return;
        if (!await app.ConfiguracoesGerais.VerificarSenhaAdministradorAsync(senha)) { Erros.Add("Senha de administrador incorreta. Nada foi apagado."); return; }
        string? copia = null;
        try
        {
            Ocupado = true;
            var b = await app.Backup.GerarAsync(sessao, await app.ConfiguracoesGerais.PastaBackupAsync());
            if (b.Sucesso) copia = b.Valor;
        }
        catch (Exception) { /* pergunta abaixo */ }
        finally { Ocupado = false; }
        if (copia is null && !await Contexto.ConfirmarAsync("Backup não gerado",
                "Não foi possível gerar o backup antes da limpeza. Limpar o banco mesmo assim (sem possibilidade de recuperar)?",
                "Limpar sem backup", "Cancelar")) return;
        try
        {
            Ocupado = true;
            var r = await app.ConfiguracoesGerais.LimparBancoAsync(sessao, ConfirmacaoLimpeza, senha);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ConfirmacaoLimpeza = "";
            await Contexto.ReiniciarParaLoginAsync(
                $"Banco de dados limpo ({r.Valor} empresa(s) excluída(s)). Entre com o usuário {ServicoConfiguracoesGerais.LoginAdministradorPadrao} " +
                $"e a senha {ServicoConfiguracoesGerais.SenhaAdministradorPadrao}." + (copia is null ? "" : $" Backup anterior à limpeza: {copia}"));
        }
        catch (Exception ex) { Erros.Add(ex.Message); }
        finally { Ocupado = false; }
    }
}
