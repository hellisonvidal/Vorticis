using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Sistema;

namespace Vorticis.Desktop.Comum.ViewModels;

public sealed record AuditoriaLinha(RegistroAuditoria R)
{
    public string Quando => R.OcorridoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
    public string Usuario => R.Usuario;
    public string Operacao => R.OperacaoTexto;
    public string Cadastro => R.Cadastro;
    public string Registro => R.RegistroId?.ToString() ?? "";
    public string Empresa => R.EmpresaId is { } e ? $"{e:0000} – {R.Empresa}" : "";
    public string Campos => R.Resumo;
}

public sealed record DiferencaLinha(string Campo, string Antes, string Depois);

public sealed record OpcaoCadastro(string Valor, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Configurações › Histórico de alterações (somente administrador): consulta e conferência da trilha de auditoria.</summary>
public partial class AuditoriaViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<AuditoriaLinha> Lista { get; } = [];
    public ObservableCollection<DiferencaLinha> Detalhes { get; } = [];
    public ObservableCollection<OpcaoCadastro> Cadastros { get; } = [new("", "Todos os cadastros")];

    [ObservableProperty] private DateTime? _de = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _ate = DateTime.Today;
    [ObservableProperty] private string _usuario = "";
    [ObservableProperty] private OpcaoCadastro? _cadastro;
    [ObservableProperty] private bool _soEmpresaAtiva;
    [ObservableProperty] private AuditoriaLinha? _selecionado;
    [ObservableProperty] private string _integridade = "";

    partial void OnSelecionadoChanged(AuditoriaLinha? value)
    {
        Detalhes.Clear();
        foreach (var (c, a, d) in value?.R.Diferencas() ?? []) Detalhes.Add(new DiferencaLinha(c, a, d));
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is { } app && Cadastros.Count == 1)
        {
            foreach (var t in (await app.Auditoria.TabelasAsync()).Select(t => new OpcaoCadastro(t, ServicoAuditoria.NomeDaTabela(t))).OrderBy(o => o.Texto))
                Cadastros.Add(t);
        }
        Cadastro ??= Cadastros[0];
        await ConsultarAsync();
    }

    [RelayCommand]
    private async Task ConsultarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var filtro = new FiltroAuditoria(Data(De, DateOnly.FromDateTime(DateTime.Today.AddDays(-30))), Data(Ate, DateOnly.FromDateTime(DateTime.Today)),
                                         Usuario, Cadastro?.Valor, SoEmpresaAtiva ? Contexto.EmpresaId : null);
        var r = await app.Auditoria.ConsultarAsync(sessao, filtro);
        if (!EhAtual(g)) return;
        Lista.Clear();
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var x in r.Valor) Lista.Add(new AuditoriaLinha(x));
        Mensagem = r.Valor.Count >= filtro.Limite
            ? $"Mostrando as {filtro.Limite} alterações mais recentes do período; use os filtros para ver as demais."
            : $"{r.Valor.Count} alteração(ões) no período.";
    }

    [RelayCommand]
    private async Task VerificarAsync()
    {
        Erros.Clear();
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        Integridade = "Conferindo a trilha inteira...";
        var r = await app.Auditoria.VerificarIntegridadeAsync(sessao);
        Integridade = !r.Sucesso ? r.Erros[0]
            : r.Valor is { } id ? $"ATENÇÃO: a trilha foi alterada por fora do sistema a partir do registro nº {id}. Guarde os backups e procure o suporte."
            : $"Trilha íntegra: nenhuma alteração por fora do sistema (conferida em {DateTime.Now:dd/MM/yyyy HH:mm}).";
    }
}
