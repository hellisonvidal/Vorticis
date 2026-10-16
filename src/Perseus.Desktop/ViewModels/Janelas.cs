using CommunityToolkit.Mvvm.ComponentModel;

namespace Perseus.Desktop.ViewModels;

/// <summary>Função aberta em janela flutuante (uma janela por tela; reabrir a mesma função traz a janela para a frente).</summary>
public sealed partial class FuncaoAberta(object viewModel, string tela, string titulo) : ObservableObject
{
    public object ViewModel { get; } = viewModel;
    [ObservableProperty] private string _tela = tela;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Rotulo))] private string _titulo = titulo;

    /// <summary>Nome curto para a barra de janelas (último trecho do caminho do menu).</summary>
    public string Rotulo => Titulo.Split('›').Last().Trim();
    [ObservableProperty] private bool _ativa;
}

/// <summary>Pergunta ao usuário (Sim/Não) exibida em diálogo modal; a resposta conclui <see cref="Resposta"/>.</summary>
public sealed class PedidoConfirmacao(string titulo, string mensagem, string textoSim, string textoNao)
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Titulo { get; } = titulo;
    public string Mensagem { get; } = mensagem;
    public string TextoSim { get; } = textoSim;
    public string TextoNao { get; } = textoNao;
    public Task<bool> Resposta => _tcs.Task;

    public void Responder(bool sim) => _tcs.TrySetResult(sim);
}
