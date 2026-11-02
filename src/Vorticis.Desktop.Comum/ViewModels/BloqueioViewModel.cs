using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Bloqueio por inatividade (Configurações gerais › Preferências): depois de alguns minutos sem uso do teclado e do mouse, as telas
/// ficam cobertas e o usuário digita a senha para continuar — nada do que estava aberto se perde. Um por programa (a Suite tem uma sessão só).
/// </summary>
public partial class BloqueioViewModel : ObservableObject
{
    public static BloqueioViewModel Atual { get; } = new();

    public BloqueioViewModel() { }

    /// <summary>Último uso do teclado ou do mouse em qualquer janela do programa.</summary>
    public DateTime UltimaAtividade { get; private set; } = DateTime.UtcNow;

    /// <summary>Desliga a conferência automática da janela (testes automatizados, que ficam muito tempo sem teclado e mouse).</summary>
    public static bool ConferenciaAutomatica { get; set; } = true;

    /// <summary>Relógio (substituível nos testes).</summary>
    public Func<DateTime> Agora { get; set; } = () => DateTime.UtcNow;

    [ObservableProperty] private bool _ativo;
    [ObservableProperty] private string _usuario = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string? _erro;

    /// <summary>Confere a senha do usuário da sessão (o login normal: tentativas erradas contam para o bloqueio de 15 minutos).</summary>
    public Func<string, Task<bool>>? Verificar { get; set; }

    /// <summary>Sai do sistema (volta ao login) em vez de digitar a senha.</summary>
    public Action? AoSair { get; set; }

    private static bool _instalado;

    /// <summary>Acompanha o uso do teclado e do mouse em todas as janelas.</summary>
    public static void Instalar()
    {
        if (_instalado) return;
        _instalado = true;
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((s, e) => Atual.Registrar(), handledEventsToo: true);
        InputElement.PointerMovedEvent.AddClassHandler<TopLevel>((s, e) => Atual.Registrar(), handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>((s, e) => Atual.Registrar(), handledEventsToo: true);
    }

    public void Registrar()
    {
        if (!Ativo) UltimaAtividade = Agora();
    }

    /// <summary>Bloqueia se passou o tempo sem uso (0 minutos = desligado). Devolve verdadeiro se bloqueou agora.</summary>
    public bool Conferir(int minutos, string? usuario)
    {
        if (Ativo || minutos <= 0 || usuario is null) return false;
        if (Agora() - UltimaAtividade < TimeSpan.FromMinutes(minutos)) return false;
        Bloquear(usuario);
        return true;
    }

    public void Bloquear(string usuario)
    {
        (Usuario, Senha, Erro) = (usuario, "", null);
        Ativo = true;
    }

    [RelayCommand]
    private async Task DesbloquearAsync()
    {
        Erro = null;
        if (Senha.Length == 0) { Erro = "Digite a sua senha."; return; }
        var ok = Verificar is null || await Verificar(Senha);
        Senha = "";
        if (!ok) { Erro = "Senha incorreta (depois de 5 tentativas o usuário fica bloqueado por 15 minutos)."; return; }
        Ativo = false;
        UltimaAtividade = Agora();
    }

    [RelayCommand]
    private void Sair()
    {
        (Ativo, Senha, Erro) = (false, "", null);
        UltimaAtividade = Agora();
        AoSair?.Invoke();
    }
}
