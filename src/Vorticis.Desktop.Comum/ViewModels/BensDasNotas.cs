using System.Runtime.CompilerServices;
using Vorticis.Application.Contabil;
using Vorticis.Application.Imobilizado;
using Vorticis.Infra;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Ponte entre o Fiscal (notas de entrada) e o Contábil (Imobilizado): itens de nota que são bens do ativo imobilizado ficam registrados
/// em "Bens a cadastrar das notas"; esta classe decide, conforme o tipo de contabilização da empresa, se o cadastro de bens abre já
/// preenchido (on-line) ou se a pendência espera a Contabilização em lote, e entrega os itens à tela de bens da mesma janela.
/// </summary>
public static class BensDasNotas
{
    /// <summary>Chave da tela Imobilizado › Cadastro de bens (Contábil).</summary>
    public const string TelaCadastroBens = "bens";
    /// <summary>Chave da tela Imobilizado › Bens a cadastrar das notas (Contábil).</summary>
    public const string TelaBensACadastrar = "bensacadastrar";

    private static readonly ConditionalWeakTable<VorticisApp, List<long>> Caixa = new();

    /// <summary>Deixa os itens para a tela de bens da mesma janela, que os lê ao abrir.</summary>
    public static void Entregar(IContexto contexto, IEnumerable<long> pendencias)
    {
        if (contexto.App is not { } app) return;   // a caixa é da conexão: na Suite os módulos compartilham a mesma
        var lista = Caixa.GetOrCreateValue(app);
        lock (lista) lista.AddRange(pendencias.Where(i => !lista.Contains(i)));
    }

    /// <summary>Recebe (uma vez só) os itens entregues.</summary>
    public static IReadOnlyList<long> Retirar(IContexto contexto)
    {
        if (contexto.App is not { } app || !Caixa.TryGetValue(app, out var lista)) return [];
        lock (lista)
        {
            var itens = lista.ToList();
            lista.Clear();
            return itens;
        }
    }

    /// <summary>O Contábil está nesta janela (ou na Suite) e o usuário pode abrir o cadastro de bens?</summary>
    public static bool PodeAbrirCadastro(IContexto contexto)
        => contexto is MainViewModelBase m && m.PodeAbrirNestaJanela(TelaCadastroBens);

    /// <summary>Abre o cadastro de bens preenchido com os itens. False = o Contábil não está disponível (os itens continuam pendentes).</summary>
    public static bool AbrirCadastro(IContexto contexto, IEnumerable<long> pendencias)
    {
        var ids = pendencias.ToList();
        if (ids.Count == 0 || !PodeAbrirCadastro(contexto)) return false;
        Entregar(contexto, ids);
        if (((MainViewModelBase)contexto).AbrirNestaJanela(TelaCadastroBens)) return true;
        Retirar(contexto);
        return false;
    }

    /// <summary>
    /// Depois de gravar ou importar notas de entrada: se há bens a cadastrar nelas, abre o cadastro de bens (contabilização on-line) ou só
    /// avisa que a pendência espera a Contabilização em lote. Devolve o texto para a mensagem da tela (vazio se não há bens).
    /// </summary>
    public static async Task<string> AposGravarNotasAsync(IContexto contexto, VorticisApp app, long empresa, long usuario,
                                                          IReadOnlyCollection<long>? documentoIds = null, IReadOnlyCollection<string>? chaves = null)
    {
        if ((documentoIds is null || documentoIds.Count == 0) && (chaves is null || chaves.Count == 0)) return "";
        var pendentes = await app.Imobilizado.ListarBensACadastrarAsync(empresa, usuario, true, null,
            documentoIds is { Count: > 0 } ? documentoIds : null, chaves is { Count: > 0 } ? chaves : null);
        if (pendentes.Count == 0) return "";
        var texto = $"{pendentes.Count} item(ns) da nota são bens do ativo imobilizado";
        if (await app.Pendencias.ModoDaEmpresaAsync(empresa) == ModoContabilizacao.Lote)
            return $" {texto}: registrados em Imobilizado › Bens a cadastrar das notas (a Contabilização em lote oferece o cadastro).";
        if (AbrirCadastro(contexto, pendentes.Select(p => p.Id)))
            return $" {texto}: o Cadastro de bens foi aberto já preenchido para você conferir e salvar (Cancelar deixa o item em Bens a cadastrar das notas).";
        return $" {texto}: registrados em Imobilizado › Bens a cadastrar das notas. Abra o Contábil para cadastrar (não disponível neste programa/perfil).";
    }
}
