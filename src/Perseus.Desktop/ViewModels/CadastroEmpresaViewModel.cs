using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;

namespace Perseus.Desktop.ViewModels;

/// <summary>Cadastros › Empresas: cadastro com pré-preenchimento pela base pública do CNPJ.</summary>
public partial class CadastroEmpresaViewModel(MainViewModel main) : ModuloViewModel(main)
{
    private DadosCnpj? _consultado;

    public static IReadOnlyList<RegimeTributario> Regimes { get; } = Enum.GetValues<RegimeTributario>();

    public MainViewModel Main => main;

    [ObservableProperty] private string _cnpj = "";
    [ObservableProperty] private string _razaoSocial = "";
    [ObservableProperty] private string _nomeFantasia = "";
    [ObservableProperty] private string _naturezaJuridica = "2062";
    [ObservableProperty] private string _cnae = "";
    [ObservableProperty] private string _uf = "SP";
    [ObservableProperty] private string _municipioIbge = "";
    [ObservableProperty] private string _municipio = "";
    [ObservableProperty] private string _cep = "";
    [ObservableProperty] private string _logradouro = "";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _complemento = "";
    [ObservableProperty] private string _bairro = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private DateTime? _inicioAtividades = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private RegimeTributario _regime = RegimeTributario.SimplesNacional;
    [ObservableProperty] private decimal? _anoExercicio = DateTime.Today.Year;
    [ObservableProperty] private bool _importarPlanoModelo = true;
    [ObservableProperty] private string? _origemDados;
    [ObservableProperty] private string? _aviso;

    /// <summary>Busca os dados do CNPJ na base pública da Receita e preenche o formulário (o usuário revisa antes de salvar).</summary>
    [RelayCommand]
    private async Task ConsultarCnpjAsync()
    {
        Erros.Clear();
        (Mensagem, Aviso, OrigemDados) = (null, null, null);
        if (main.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        try
        {
            Ocupado = true;
            var r = await app.Empresas.ConsultarCnpjAsync(Cnpj);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            Preencher(r.Valor);
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void Preencher(DadosCnpj d)
    {
        _consultado = d;
        RazaoSocial = d.RazaoSocial;
        NomeFantasia = d.NomeFantasia ?? "";
        NaturezaJuridica = d.NaturezaJuridica ?? NaturezaJuridica;
        Cnae = d.CnaePrincipal ?? "";
        Uf = d.Uf ?? Uf;
        MunicipioIbge = d.CodMunicipioIbge ?? "";
        Municipio = d.Municipio ?? "";
        Cep = d.Cep ?? "";
        Logradouro = d.Logradouro ?? "";
        Numero = d.Numero ?? "";
        Complemento = d.Complemento ?? "";
        Bairro = d.Bairro ?? "";
        Email = d.Email ?? "";
        Telefone = d.Telefone ?? "";
        if (d.DataInicioAtividades is { } ini)
        {
            InicioAtividades = ini.ToDateTime(TimeOnly.MinValue);
            if (AnoExercicio < ini.Year) AnoExercicio = ini.Year;
        }
        if (d.RegimeSugerido is { } regime) Regime = regime;

        OrigemDados = $"Dados de {d.Fonte} em {d.ConsultadoEm.ToLocalTime():dd/MM/yyyy HH:mm}. Situação cadastral: {d.SituacaoCadastral ?? "não informada"}." +
                      (d.RegimeSugerido is null ? " Regime não consta da base pública (Presumido/Real): confirme abaixo." : "");
        if (!d.Ativa) Aviso = $"Atenção: a situação cadastral do CNPJ é {d.SituacaoCadastral ?? "desconhecida"}.";
        Mensagem = "Dados preenchidos. Revise e clique em Cadastrar.";
    }

    [RelayCommand]
    private async Task CadastrarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (main.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        if (!main.Pode(Domain.Seguranca.Permissao.AdministrarSistema)) { Erros.Add("Somente administradores cadastram empresas."); return; }
        try
        {
            Ocupado = true;
            // A origem só é registrada se o CNPJ cadastrado for o mesmo que foi consultado.
            var origem = _consultado is not null && Domain.Cadastros.Cnpj.SomenteDigitos(_consultado.Cnpj) == Domain.Cadastros.Cnpj.SomenteDigitos(Cnpj)
                ? _consultado : null;
            var r = await app.Empresas.CadastrarAsync(new NovaEmpresa(
                Cnpj, RazaoSocial, string.IsNullOrWhiteSpace(NomeFantasia) ? null : NomeFantasia.Trim(),
                NaturezaJuridica.Trim(), Cnae, Uf.Trim(), MunicipioIbge.Trim(),
                DateOnly.FromDateTime(InicioAtividades ?? DateTime.Today), Regime, (int)(AnoExercicio ?? DateTime.Today.Year),
                ImportarPlanoModelo,
                new ComplementoEmpresa(Cep, Logradouro, Numero, Complemento, Bairro, Municipio, Email, Telefone,
                                       origem?.SituacaoCadastral, origem?.Fonte, origem?.ConsultadoEm)));
            MostrarResultado(r, $"Empresa cadastrada com exercício {AnoExercicio} e períodos mensais abertos.");
            if (r.Sucesso)
            {
                var socios = origem?.QuadroSocietario ?? [];
                if (socios.Count > 0 && main.Sessao is { } sessao
                    && await app.Responsaveis.IncluirDoQsaAsync(r.Valor, socios, origem!.Fonte, sessao.Id) is { Sucesso: true, Valor: > 0 } incl)
                    Mensagem += $" {incl.Valor} sócio(s) incluído(s) do quadro societário (complete CPF e representante em Cadastros › Sócios).";
                LimparFormulario();
                await main.RecarregarEmpresasAsync(r.Valor);
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

    [RelayCommand]
    private void LimparFormulario()
    {
        (Cnpj, RazaoSocial, NomeFantasia, Cnae, MunicipioIbge, Municipio) = ("", "", "", "", "", "");
        (Cep, Logradouro, Numero, Complemento, Bairro, Email, Telefone) = ("", "", "", "", "", "", "");
        (OrigemDados, Aviso, _consultado) = (null, null, null);
    }
}
