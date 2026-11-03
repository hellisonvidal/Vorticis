using System.Security.Cryptography.X509Certificates;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Esocial;
using Vorticis.Folha.Esocial.Importacao;

namespace Vorticis.Folha.Servicos;

public sealed record OpcoesDownloadEsocial(DateOnly Inicio, DateOnly Fim, bool Tabelas = true, bool Remuneracoes = true, bool Pagamentos = true, bool Trabalhadores = true,
                                           IReadOnlyList<string>? CpfsExtras = null);

public sealed record ResultadoDownloadEsocial(IReadOnlyList<ArquivoEsocial> Arquivos, int Identificadores, int Baixados, IReadOnlyList<string> Avisos);

/// <summary>
/// Baixar do eSocial: consulta os identificadores dos eventos do empregador (tabelas, S-1200 e S-1210 por competência, eventos dos trabalhadores) e baixa
/// cada evento pelo Id, usando o certificado do empregador, do procurador ou do contador. O resultado alimenta a mesma importação dos arquivos.
/// </summary>
public sealed class ServicoDownloadEsocial(ServicoTransmissaoEsocial transmissao, IRepositorioCadastrosFolha cadastros, Autorizador autorizador)
{
    /// <summary>Início do eSocial para os eventos dos trabalhadores (não há como ser anterior).</summary>
    public static readonly DateOnly InicioEsocial = new(2018, 1, 1);

    public Func<X509Certificate2, AmbienteEsocial, string, IClienteDownloadEsocial> FabricaCliente { get; set; } = (c, a, raiz) => ClienteDownloadEsocial.Criar(c, a, raiz);
    public Func<DateOnly> Hoje { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    public async Task<Resultado<ResultadoDownloadEsocial>> BaixarAsync(long empresaId, OpcoesDownloadEsocial op, AmbienteEsocial ambiente, PapelTransmissor papel, X509Certificate2 certificado,
                                                                        long usuarioId, IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ResultadoDownloadEsocial>(usuarioId, empresaId, Permissao.FolhaESocial, ct) is { } negado) return negado;
        if (await autorizador.NegarAsync<ResultadoDownloadEsocial>(usuarioId, empresaId, Permissao.FolhaCadastrosManter, ct) is { } negado2) return negado2;
        if (op.Fim < op.Inicio) return Resultado<ResultadoDownloadEsocial>.Falha("A competência final deve ser igual ou posterior à inicial.");
        var empresa = await cadastros.ObterEmpresaAsync(empresaId, ct);
        if (empresa is null) return Resultado<ResultadoDownloadEsocial>.Falha("Empresa não encontrada.");
        var transmissor = await transmissao.ResolverTransmissorAsync(empresaId, papel, certificado, ct);
        if (!transmissor.Sucesso) return Resultado<ResultadoDownloadEsocial>.Falha(transmissor.Erros);

        var inicio = new DateOnly(op.Inicio.Year, op.Inicio.Month, 1);
        var fim = new DateOnly(op.Fim.Year, op.Fim.Month, 1);
        var meses = new List<DateOnly>();
        for (var m = inicio; m <= fim; m = m.AddMonths(1)) meses.Add(m);
        var raiz = new string(empresa.Cnpj.Where(char.IsDigit).ToArray());
        var cliente = FabricaCliente(certificado, ambiente, raiz);
        var avisos = new List<string>();
        var ids = new Dictionary<string, IdentificadorEvento>(StringComparer.Ordinal);
        var arquivos = new List<ArquivoEsocial>();
        var passo = 0;
        var total = 0;
        void Andamento(string etapa) { passo++; progresso?.Report(new ProgressoImportacao(etapa, Math.Min(passo, Math.Max(total, passo)), Math.Max(total, passo))); }

        async Task<bool> ListarAsync(string etapa, Func<Task<RespostaIdentificadores>> consulta, string rotulo)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var r = await consulta();
                Andamento(etapa);
                if (!r.Sucesso) { avisos.Add($"{rotulo}: {r.Codigo} — {r.Descricao}"); return true; }
                foreach (var e in r.Eventos) ids.TryAdd(e.Id, e);
                return true;
            }
            catch (ErroComunicacaoEsocial ex) { avisos.Add($"{rotulo}: {ex.Message}"); return false; }
        }

        total = (op.Tabelas ? 4 : 0) + meses.Count * ((op.Remuneracoes ? 1 : 0) + (op.Pagamentos ? 1 : 0));
        if (op.Tabelas)
            foreach (var tp in new[] { "S-1000", "S-1005", "S-1010", "S-1020" })
                if (!await ListarAsync("Consultando tabelas", () => cliente.ListarTabelaAsync(tp, InicioEsocial, Hoje(), ct), $"Tabela {tp}")) break;
        foreach (var mes in meses)
        {
            if (op.Remuneracoes && !await ListarAsync("Consultando remunerações", () => cliente.ListarEmpregadorAsync("S-1200", mes, ct), $"S-1200 {mes:MM/yyyy}")) break;
            if (op.Pagamentos && !await ListarAsync("Consultando pagamentos", () => cliente.ListarEmpregadorAsync("S-1210", mes, ct), $"S-1210 {mes:MM/yyyy}")) break;
        }

        // 1ª rodada de downloads: tabelas, S-1200 e S-1210
        var baixados = 0;
        var baixadosIds = new HashSet<string>(StringComparer.Ordinal);
        async Task BaixarPendentesAsync(string etapa)
        {
            var pendentes = ids.Keys.Where(i => !baixadosIds.Contains(i)).ToList();
            for (var i = 0; i < pendentes.Count; i += ClienteDownloadEsocial.IdsPorSolicitacao)
            {
                ct.ThrowIfCancellationRequested();
                var lote = pendentes.Skip(i).Take(ClienteDownloadEsocial.IdsPorSolicitacao).ToList();
                try
                {
                    var r = await cliente.BaixarPorIdAsync(lote, ct);
                    if (!r.Sucesso) { avisos.Add($"Download de {lote.Count} evento(s): {r.Codigo} — {r.Descricao}"); continue; }
                    arquivos.AddRange(r.Arquivos);
                    foreach (var id in lote) baixadosIds.Add(id);
                    baixados += lote.Count;
                    total += 1;
                    Andamento(etapa);
                }
                catch (ErroComunicacaoEsocial ex) { avisos.Add(ex.Message); return; }
            }
        }
        total += (ids.Count + ClienteDownloadEsocial.IdsPorSolicitacao - 1) / ClienteDownloadEsocial.IdsPorSolicitacao;
        await BaixarPendentesAsync("Baixando eventos");

        // eventos dos trabalhadores (S-2200, S-2206, S-2230, S-2299...): dos CPFs achados nas remunerações e dos informados
        if (op.Trabalhadores)
        {
            var cpfs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var c in op.CpfsExtras ?? []) if (Digitos(c) is { Length: 11 } d) cpfs.Add(d);
            var lidos = new List<OcorrenciaImportacao>();
            foreach (var e in LeitorEsocial.Ler(arquivos, lidos).Eventos.Where(e => e.Tipo == "S-1200" || e.Tipo == "S-1210"))
                if (Digitos(e.Evento.Elements().FirstOrDefault(x => x.Name.LocalName is "ideTrabalhador" or "ideBenef")?.Elements().FirstOrDefault(x => x.Name.LocalName is "cpfTrab" or "cpfBenef")?.Value) is { Length: 11 } cpf)
                    cpfs.Add(cpf);
            total += cpfs.Count;
            foreach (var cpf in cpfs)
                if (!await ListarAsync("Consultando trabalhadores", () => cliente.ListarTrabalhadorAsync(cpf, InicioEsocial, Hoje(), ct), $"Trabalhador {cpf}")) break;
            total += (ids.Count - baixadosIds.Count + ClienteDownloadEsocial.IdsPorSolicitacao - 1) / ClienteDownloadEsocial.IdsPorSolicitacao;
            await BaixarPendentesAsync("Baixando eventos dos trabalhadores");
        }

        if (arquivos.Count == 0 && avisos.Count > 0) return Resultado<ResultadoDownloadEsocial>.Falha(avisos.Prepend("Nenhum evento foi baixado do eSocial:"));
        if (arquivos.Count == 0) return Resultado<ResultadoDownloadEsocial>.Falha("O eSocial não devolveu nenhum evento para o período. Confira a competência e o ambiente (produção ou restrita).");
        return Resultado<ResultadoDownloadEsocial>.Ok(new ResultadoDownloadEsocial(arquivos, ids.Count, baixados, avisos));
    }

    private static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
}
