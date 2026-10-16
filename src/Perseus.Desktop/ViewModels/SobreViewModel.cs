using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perseus.Desktop.ViewModels;

public partial class SobreViewModel(MainViewModel main) : ModuloViewModel(main)
{
    public static string Versao { get; } =
        typeof(SobreViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1";

    [ObservableProperty] private string _conexao = "";
    [ObservableProperty] private string _usuario = "";

    public void Atualizar()
    {
        Conexao = Configuracao.Descrever(main.ConnectionString);
        Usuario = main.UsuarioRotulo;
    }
}
