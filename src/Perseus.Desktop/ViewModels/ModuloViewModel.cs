using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;
using Perseus.Infra;

namespace Perseus.Desktop.ViewModels;

/// <summary>Base dos módulos: acesso ao contexto e exibição padronizada de mensagens e erros.</summary>
public abstract partial class ModuloViewModel(IContexto contexto) : ObservableObject
{
    protected IContexto Contexto { get; } = contexto;

    public ObservableCollection<string> Erros { get; } = [];

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    protected static DateOnly Data(DateTime? d, DateOnly padrao) => d is { } v ? DateOnly.FromDateTime(v) : padrao;

    protected void MostrarResultado(Resultado r, string sucesso)
    {
        Erros.Clear();
        foreach (var e in r.Erros) Erros.Add(e);
        Mensagem = r.Sucesso ? sucesso : null;
    }

    /// <summary>Executa uma ação que exige empresa selecionada, convertendo exceções em mensagem.</summary>
    /// <param name="exige">Permissão necessária na empresa ativa (verificada antes de executar).</param>
    protected async Task ExecutarAsync(Func<PerseusApp, long, Task> acao, Permissao exige = Permissao.ConsultarRelatorios)
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        if (Contexto.EmpresaId is not { } empresa) { Erros.Add("Selecione uma empresa."); return; }
        if (!Contexto.Pode(exige)) { Erros.Add("Seu perfil nesta empresa não permite esta operação."); return; }
        try
        {
            Ocupado = true;
            await acao(app, empresa);
        }
        catch (Exception ex)
        {
            Erros.Add(ex.Message);
        }
        finally
        {
            Ocupado = false;
        }
    }
}
