using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Dctf;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>
/// Obrigações › DCTFWeb (MIT): meses do ano com a situação da apuração e da declaração. Com o mês fechado, Preparar declaração grava os
/// débitos do MIT (exportáveis para lançar ou conferir na DCTFWeb); depois da transmissão no e-CAC, registra-se o número do recibo.
/// </summary>
public partial class DctfWebViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    public ObservableCollection<MesDctf> Meses { get; } = [];
    [ObservableProperty] private int _ano = DateTime.Today.Year;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemMes))] private MesDctf? _mes;
    [ObservableProperty] private string _recibo = "";
    [ObservableProperty] private DateTime? _dataTransmissao = DateTime.Today;
    private DateOnly _exibida;

    public bool TemMes => Mes is not null;
    public bool PodeApurar => Contexto.Pode(Permissao.FiscalApurar);

    protected override string TituloRelatorio => "DCTFWeb · débitos do MIT";
    protected override string Parametros => $"Competência {_exibida:MM/yyyy}";
    protected override string NomeArquivo => $"dctfweb_mit_{_exibida:yyyy_MM}";
    protected override bool Paisagem => false;

    public override void AtualizarPermissoes()
    {
        base.AtualizarPermissoes();
        OnPropertyChanged(nameof(PodeApurar));
    }

    partial void OnAnoChanged(int value) => _ = CarregarAsync();

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().DctfWeb.ListarAsync(empresa, Ano, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var atual = Mes?.Competencia;
        Meses.Clear();
        foreach (var m in r.Valor!) Meses.Add(m);
        Mes = Meses.FirstOrDefault(m => m.Competencia == atual);
    });

    /// <summary>Tabela da declaração: débitos do MIT por código de receita e, informativos, os que vêm da EFD-Reinf e do eSocial.</summary>
    public static TabelaRelatorio Tabela(DeclaracaoDctf d)
    {
        var linhas = d.Debitos.Select(x => new LinhaRelatorio([x.Tributo, x.CodigoReceita, x.Descricao, x.Valor])).ToList();
        linhas.Add(new LinhaRelatorio(["Total do MIT", null, d.SemMovimento ? "Sem débitos: declaração sem movimento" : null, d.Total], true));
        foreach (var x in d.Informativos) linhas.Add(new LinhaRelatorio([x.Tributo, x.CodigoReceita, x.Descricao, x.Valor]));
        return new TabelaRelatorio($"DCTFWeb {d.Competencia:MM/yyyy} · débitos do MIT",
            [new("Tributo", TipoColuna.Texto, 2.5), new("Código", TipoColuna.Texto, 2), new("Descrição", TipoColuna.Texto, 8), new("Valor", TipoColuna.Valor, 3)], linhas,
            "Os débitos do MIT saem das guias DARF das apurações (Obrigações › Guias de recolhimento). As linhas \"pela EFD-Reinf\" e \"pelo eSocial\" chegam à DCTFWeb sozinhas: só conferência.");
    }

    private Task Exibir(Func<VorticisApp, long, long, DateOnly, Task<Vorticis.Domain.Comum.Resultado<DeclaracaoDctf>>> obter, string mensagem) =>
        ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Mes is not { } m) { Erros.Add("Escolha o mês na lista."); return; }
            var r = await obter(app, empresa, usuario, m.Competencia);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            _exibida = m.Competencia;
            Mostrar(Tabela(r.Valor!));
            await CarregarAsync();
            Mensagem = mensagem;
        }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task PreverAsync() => Exibir((app, e, u, c) => app.Fiscal().DctfWeb.PreverAsync(e, c, u), "Prévia dos débitos do mês (nada foi gravado).");

    [RelayCommand]
    private Task PrepararAsync() => Exibir((app, e, u, c) => app.Fiscal().DctfWeb.PrepararAsync(e, c, u),
        "Declaração preparada. Lance os débitos no MIT da DCTFWeb (e-CAC), transmita e registre aqui o número do recibo.");

    [RelayCommand]
    private Task RegistrarTransmissaoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Mes is not { } m) { Erros.Add("Escolha o mês na lista."); return; }
        if (DataTransmissao is not { } d) { Erros.Add("Informe a data da transmissão."); return; }
        var r = await app.Fiscal().DctfWeb.RegistrarTransmissaoAsync(empresa, m.Competencia, Recibo, DateOnly.FromDateTime(d), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = $"Transmissão da DCTFWeb de {m.Competencia:MM/yyyy} registrada (recibo {Recibo.Trim()}).";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task DesfazerTransmissaoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Mes is not { } m) { Erros.Add("Escolha o mês na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Desfazer transmissão", $"Desfazer o registro da transmissão de {m.Competencia:MM/yyyy}? Faça isso só para retificar a declaração.", "Desfazer", "Cancelar")) return;
        var r = await app.Fiscal().DctfWeb.DesfazerTransmissaoAsync(empresa, m.Competencia, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Registro da transmissão desfeito: reabra o mês, corrija, prepare de novo e transmita a retificadora.";
    }, Permissao.FiscalApurar);
}
