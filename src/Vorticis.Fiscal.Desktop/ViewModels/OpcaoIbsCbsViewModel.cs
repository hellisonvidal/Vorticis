using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public sealed record OpcaoIbsCbsLinha(OpcaoIbsCbsEmpresa O)
{
    public string Semestre => O.SemestreTexto;
    public string Opcao => O.Opcao.Rotulo();
    public string Vigencia => O.VigenciaFim is { } f ? $"{O.VigenciaInicio:dd/MM/yyyy} a {f:dd/MM/yyyy}" : $"{O.VigenciaInicio:dd/MM/yyyy} em diante";
    public string Pedido => O.DataPedido?.ToString("dd/MM/yyyy") ?? "";
    public string Protocolo => O.Protocolo ?? "";
}

/// <summary>
/// Cadastros › Opção IBS/CBS do Simples: histórico, por semestre, da escolha da empresa entre recolher o IBS e a CBS dentro do DAS (padrão) ou pelo
/// regime regular (fora do DAS). A ficha abre em janela; pedido fora do prazo mostra aviso (o prazo vem dos parâmetros legais).
/// </summary>
public partial class OpcaoIbsCbsViewModel(IContexto contexto) : ModuloComFichaViewModel(contexto)
{
    public static IReadOnlyList<string> Opcoes { get; } = [OpcaoIbsCbs.DentroDoDas.Rotulo(), OpcaoIbsCbs.RegimeRegular.Rotulo()];

    public override string TituloFicha => Selecionada is { } s ? $"Opção do IBS/CBS: {s.Semestre}" : "Nova opção do IBS/CBS";
    protected override bool HaRegistroParaAbrir => Selecionada is not null;
    protected override string AvisoSelecione => "Selecione o semestre na lista.";
    protected override void AoDescartarFicha() { if (Selecionada is { } s) OnSelecionadaChanged(s); else LimparFormulario(); }

    public ObservableCollection<OpcaoIbsCbsLinha> Lista { get; } = [];
    [ObservableProperty] private OpcaoIbsCbsLinha? _selecionada;
    [ObservableProperty] private DateTime? _vigencia = new DateTime(Math.Max(2027, DateTime.Today.Year), DateTime.Today.Month <= 6 ? 1 : 7, 1);
    [ObservableProperty] private int _opcao;
    [ObservableProperty] private DateTime? _dataPedido;
    [ObservableProperty] private string _protocolo = "";
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private string _situacaoAtual = "";
    [ObservableProperty] private string _avisosTexto = "";

    public bool PodeManter => Contexto.Pode(Permissao.FiscalApurar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    partial void OnSelecionadaChanged(OpcaoIbsCbsLinha? value)
    {
        if (value?.O is not { } o) return;
        (Vigencia, Opcao, DataPedido, Protocolo, Observacao) = (o.VigenciaInicio.ToDateTime(TimeOnly.MinValue), (int)o.Opcao,
            o.DataPedido?.ToDateTime(TimeOnly.MinValue), o.Protocolo ?? "", o.Observacao ?? "");
    }

    private void LimparFormulario()
    {
        Selecionada = null;
        (Opcao, DataPedido, Protocolo, Observacao) = (0, null, "", "");
    }

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Opcao.ListarAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var o in r.Valor) Lista.Add(new OpcaoIbsCbsLinha(o));
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var atual = (await app.Fiscal().Opcao.AtualAsync(empresa, hoje, usuario)).Valor;
        SituacaoAtual = hoje < ServicoOpcaoIbsCbs.PrimeiraVigencia
            ? $"Hoje: {atual.Rotulo()}. A opção do semestre vale a partir de 01/01/2027; até lá o IBS e a CBS são só de teste."
            : $"Opção em vigor neste semestre: {atual.Rotulo()}.";
    });

    [RelayCommand]
    private void Nova()
    {
        LimparFormulario();
        AvisosTexto = "";
        AbrirFichaNova();
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var o = new OpcaoIbsCbsEmpresa(Selecionada?.O.Id ?? 0, Data(Vigencia, default), null, (OpcaoIbsCbs)Math.Clamp(Opcao, 0, 1),
            DataPedido is { } d ? DateOnly.FromDateTime(d) : null, Protocolo, Observacao);
        var r = await app.Fiscal().Opcao.GravarAsync(empresa, o, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        LimparFormulario();
        AvisosTexto = string.Join(Environment.NewLine, r.Valor.Avisos);
        Mensagem = r.Valor.Avisos.Count == 0
            ? $"Opção do {r.Valor.Opcao.SemestreTexto} gravada: {r.Valor.Opcao.Opcao.Rotulo()}."
            : $"Opção do {r.Valor.Opcao.SemestreTexto} gravada, mas confira: " + string.Join(" ", r.Valor.Avisos);
        FichaAberta = false;
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.O is not { } o) { Erros.Add("Selecione o semestre na lista."); return; }
        var r = await app.Fiscal().Opcao.ExcluirAsync(empresa, o.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        LimparFormulario();
        Mensagem = "Opção excluída: o semestre volta a valer a opção anterior (ou \"dentro do DAS\", se não houver).";
    }, Permissao.FiscalApurar);
}
