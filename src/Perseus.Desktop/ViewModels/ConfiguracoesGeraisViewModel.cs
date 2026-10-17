using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Sistema;

namespace Perseus.Desktop.ViewModels;

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

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeAlterar));
        if (Contexto.App is not { } app) return;
        var c = await app.ConfiguracoesGerais.ObterAsync();
        PartidaDobrada = c.ModoLancamento == ModoLancamento.PartidaDobrada;
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

    // ------------------------------------------------------------------ limpeza do banco (não recomendada)

    [ObservableProperty] private string _confirmacaoLimpeza = "";

    /// <summary>
    /// Apaga todas as empresas e cadastros. Pede para digitar LIMPAR, avisa de novo num diálogo, gera um backup antes
    /// (se não conseguir, pergunta se continua) e só então limpa.
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
        if (!await Contexto.ConfirmarAsync("Limpar banco de dados — NÃO RECOMENDADO",
                "ATENÇÃO: esta operação apaga DEFINITIVAMENTE todas as empresas e toda a escrituração (lançamentos, planos de contas, " +
                "períodos, imobilizado, clientes, fornecedores, sócios), além de contadores, escritórios, tabelas do plano referencial " +
                "e layouts de importação.\n\nFicam apenas os usuários, os perfis de acesso e as configurações.\n\n" +
                "Um backup será gerado antes. Esta opção não é recomendada. Deseja realmente limpar o banco?",
                "Sim, limpar tudo", "Cancelar")) return;
        string? copia = null;
        try
        {
            Ocupado = true;
            var b = await app.Backup.GerarAsync(sessao, Application.Sistema.ServicoBackup.PastaPadrao);
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
            var r = await app.ConfiguracoesGerais.LimparBancoAsync(sessao, ConfirmacaoLimpeza);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ConfirmacaoLimpeza = "";
            await Contexto.RecarregarEmpresasAsync();
            Mensagem = $"Banco de dados limpo: {r.Valor} empresa(s) excluída(s)." + (copia is null ? "" : $" Backup anterior à limpeza: {copia}");
        }
        catch (Exception ex) { Erros.Add(ex.Message); }
        finally { Ocupado = false; }
    }
}
