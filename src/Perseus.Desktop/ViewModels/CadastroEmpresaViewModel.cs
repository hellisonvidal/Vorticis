using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;

namespace Perseus.Desktop.ViewModels;

public partial class CadastroEmpresaViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<RegimeTributario> Regimes { get; } = Enum.GetValues<RegimeTributario>();

    [ObservableProperty] private string _cnpj = "";
    [ObservableProperty] private string _razaoSocial = "";
    [ObservableProperty] private string _nomeFantasia = "";
    [ObservableProperty] private string _naturezaJuridica = "2062";
    [ObservableProperty] private string _cnae = "";
    [ObservableProperty] private string _uf = "SP";
    [ObservableProperty] private string _municipioIbge = "";
    [ObservableProperty] private DateTime? _inicioAtividades = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private RegimeTributario _regime = RegimeTributario.SimplesNacional;
    [ObservableProperty] private decimal? _anoExercicio = DateTime.Today.Year;
    [ObservableProperty] private bool _importarPlanoModelo = true;

    [RelayCommand]
    private async Task CadastrarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        try
        {
            Ocupado = true;
            var r = await app.Empresas.CadastrarAsync(new NovaEmpresa(
                Cnpj, RazaoSocial, string.IsNullOrWhiteSpace(NomeFantasia) ? null : NomeFantasia.Trim(),
                NaturezaJuridica.Trim(), Cnae, Uf.Trim(), MunicipioIbge.Trim(),
                DateOnly.FromDateTime(InicioAtividades ?? DateTime.Today), Regime, (int)(AnoExercicio ?? DateTime.Today.Year),
                ImportarPlanoModelo));
            MostrarResultado(r, $"Empresa cadastrada com exercício {AnoExercicio} e períodos mensais abertos.");
            if (r.Sucesso)
            {
                (Cnpj, RazaoSocial, NomeFantasia) = ("", "", "");
                await Contexto.RecarregarEmpresasAsync(r.Valor);
            }
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
