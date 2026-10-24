using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Licencas;

namespace Vorticis.Licencas.Desktop;

public sealed partial class ModuloOpcao(string codigo) : ObservableObject
{
    public string Codigo { get; } = codigo;
    public string Nome => ModulosLicenca.Nome(Codigo);
    [ObservableProperty] private bool _marcado;
}

/// <summary>Licença já emitida (histórico local do gerenciador).</summary>
public sealed record LicencaEmitida(string Id, string Cliente, string? Documento, string Modulos, DateOnly EmitidaEm, DateOnly ValidaAte, int Usuarios, string Arquivo)
{
    public string Validade => ValidaAte.ToString("dd/MM/yyyy");
    public string Emissao => EmitidaEm.ToString("dd/MM/yyyy");
}

/// <summary>
/// Gerenciador de licenças (fornecedor): guarda a chave privada na pasta de dados do programa, emite as licenças (licenca.vlic) com
/// cliente, módulos, validade e número de usuários e mantém o histórico das emitidas. A chave pública correspondente precisa estar
/// embutida na suíte (src/Vorticis.Domain/Licencas/chave-publica.pem); "Exportar chave pública" gera esse arquivo.
/// </summary>
public sealed partial class GerenciadorViewModel : ObservableObject
{
    private readonly string _pasta;
    private string ArquivoChave => Path.Combine(_pasta, "chave-privada.pem");
    private string ArquivoHistorico => Path.Combine(_pasta, "licencas-emitidas.json");
    private string PastaLicencas => Path.Combine(_pasta, "licencas");

    public GerenciadorViewModel(string pasta)
    {
        _pasta = pasta;
        Directory.CreateDirectory(_pasta);
        foreach (var m in ModulosLicenca.Todos) Modulos.Add(new ModuloOpcao(m));
        CarregarHistorico();
        AtualizarSituacaoChave();
    }

    public static string PastaPadrao() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vorticis", "GerenciadorLicencas");

    public ObservableCollection<ModuloOpcao> Modulos { get; } = [];
    public ObservableCollection<LicencaEmitida> Emitidas { get; } = [];

    [ObservableProperty] private string _cliente = "";
    [ObservableProperty] private string _documento = "";
    [ObservableProperty] private DateTimeOffset? _validaAte = DateTimeOffset.Now.Date.AddYears(1);
    [ObservableProperty] private decimal? _usuarios = 5;
    [ObservableProperty] private string _situacaoChave = "";
    [ObservableProperty] private string _mensagem = "";
    [ObservableProperty] private bool _temChave;

    private void AtualizarSituacaoChave()
    {
        TemChave = File.Exists(ArquivoChave);
        if (!TemChave) { SituacaoChave = "Nenhuma chave do fornecedor. Importe a chave privada recebida ou crie um par novo."; return; }
        var pub = ChavesLicenca.PublicaDe(File.ReadAllText(ArquivoChave));
        SituacaoChave = pub.Trim() == ChavesLicenca.PublicaDoFornecedor.Trim()
            ? "Chave do fornecedor carregada: as licenças emitidas valem na versão atual da suíte."
            : "ATENÇÃO: esta chave não é a embutida nesta versão da suíte. Exporte a chave pública e atualize o programa antes de distribuir licenças.";
    }

    private void CarregarHistorico()
    {
        Emitidas.Clear();
        if (!File.Exists(ArquivoHistorico)) return;
        foreach (var l in JsonSerializer.Deserialize<List<LicencaEmitida>>(File.ReadAllText(ArquivoHistorico)) ?? []) Emitidas.Add(l);
    }

    private void SalvarHistorico() => File.WriteAllText(ArquivoHistorico, JsonSerializer.Serialize(Emitidas.ToList(), new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Cria um par de chaves novo (a privada fica na pasta do gerenciador; guarde uma cópia em local seguro).</summary>
    [RelayCommand]
    public void CriarChaves()
    {
        var (privada, _) = ChavesLicenca.Gerar();
        File.WriteAllText(ArquivoChave, privada);
        AtualizarSituacaoChave();
        Mensagem = $"Par de chaves criado. Guarde uma cópia de {ArquivoChave} em local seguro e exporte a chave pública para atualizar a suíte.";
    }

    public void ImportarChave(string arquivo)
    {
        try { ChavesLicenca.PublicaDe(File.ReadAllText(arquivo)); }
        catch (Exception) { Mensagem = "O arquivo escolhido não é uma chave privada válida (PEM)."; return; }
        File.Copy(arquivo, ArquivoChave, overwrite: true);
        AtualizarSituacaoChave();
        Mensagem = "Chave privada importada.";
    }

    public string? ExportarChavePublica(string destino)
    {
        if (!TemChave) { Mensagem = "Crie ou importe a chave antes."; return null; }
        File.WriteAllText(destino, ChavesLicenca.PublicaDe(File.ReadAllText(ArquivoChave)));
        Mensagem = $"Chave pública salva em {destino}. Substitua src/Vorticis.Domain/Licencas/chave-publica.pem por ela e gere uma nova versão da suíte.";
        return destino;
    }

    /// <summary>Emite a licença: assina, grava em licencas\&lt;cliente&gt;-&lt;id&gt;.vlic e registra no histórico. Devolve o caminho.</summary>
    [RelayCommand]
    private void Emitir() => EmitirLicenca();

    public string? EmitirLicenca()
    {
        if (!TemChave) { Mensagem = "Crie ou importe a chave do fornecedor antes de emitir licenças."; return null; }
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var doc = new string(Documento.Where(char.IsAsciiDigit).ToArray());
        var l = new Licenca(Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), Cliente.Trim(), doc.Length > 0 ? doc : null,
                            Modulos.Where(m => m.Marcado).Select(m => m.Codigo).ToList(), hoje,
                            ValidaAte is { } v ? DateOnly.FromDateTime(v.Date) : hoje.AddYears(1), (int)(Usuarios ?? 0));
        if (ArquivoLicenca.Validar(l) is { Count: > 0 } erros) { Mensagem = string.Join(" ", erros); return null; }
        var texto = ArquivoLicenca.Assinar(l, File.ReadAllText(ArquivoChave));
        Directory.CreateDirectory(PastaLicencas);
        var nome = string.Concat(l.Cliente.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var caminho = Path.Combine(PastaLicencas, $"{nome}-{l.Id}{ArquivoLicenca.Extensao}");
        File.WriteAllText(caminho, texto);
        Emitidas.Insert(0, new LicencaEmitida(l.Id, l.Cliente, l.Documento, l.ModulosTexto, l.EmitidaEm, l.ValidaAte, l.Usuarios, caminho));
        SalvarHistorico();
        Mensagem = $"Licença emitida: {caminho}. Envie o arquivo ao cliente; renomeie-o para licenca.vlic ou escolha-o no instalador ou na tela de entrada do Vorticis.";
        return caminho;
    }
}
