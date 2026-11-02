using System.Text.Json;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Sistema;

/// <summary>Uma alteração gravada na trilha de auditoria (core.auditoria).</summary>
public sealed record RegistroAuditoria(long Id, DateTimeOffset OcorridoEm, string Usuario, string Tabela, long? RegistroId, string Operacao,
                                       long? EmpresaId, string? Empresa, string? DadosAntes, string? DadosDepois)
{
    public string OperacaoTexto => Operacao switch { "INSERT" => "Inclusão", "UPDATE" => "Alteração", "DELETE" => "Exclusão", _ => Operacao };
    public string Cadastro => ServicoAuditoria.NomeDaTabela(Tabela);

    /// <summary>Campos que mudaram: (campo, antes, depois). Inclusão e exclusão mostram todos os campos preenchidos.</summary>
    public IReadOnlyList<(string Campo, string Antes, string Depois)> Diferencas()
    {
        var antes = Ler(DadosAntes);
        var depois = Ler(DadosDepois);
        return [.. antes.Keys.Union(depois.Keys)
            .Where(k => k is not ("partidas" or "senha_hash") && antes.GetValueOrDefault(k) != depois.GetValueOrDefault(k))
            .Select(k => (k, antes.GetValueOrDefault(k) ?? "", depois.GetValueOrDefault(k) ?? ""))
            .Where(d => d.Item2.Length > 0 || d.Item3.Length > 0)];
    }

    /// <summary>Resumo dos campos alterados (lista da tela).</summary>
    public string Resumo => Operacao == "UPDATE" ? string.Join(", ", Diferencas().Select(d => d.Campo).Take(6)) : "";

    private static Dictionary<string, string?> Ler(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind != JsonValueKind.Object ? [] : doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => p.Value.GetString(),
            _ => p.Value.GetRawText(),
        });
    }
}

public sealed record FiltroAuditoria(DateOnly De, DateOnly Ate, string? Usuario = null, string? Tabela = null, long? EmpresaId = null, int Limite = 2000);

public interface IRepositorioAuditoria
{
    Task<IReadOnlyList<RegistroAuditoria>> ConsultarAsync(FiltroAuditoria filtro, CancellationToken ct);
    Task<IReadOnlyList<string>> TabelasAsync(CancellationToken ct);
    /// <summary>Primeiro registro com a cadeia de hashes quebrada (null = íntegra).</summary>
    Task<long?> VerificarIntegridadeAsync(CancellationToken ct);
}

/// <summary>
/// Configurações › Histórico de alterações: quem incluiu, alterou ou excluiu cada cadastro e lançamento, quando, e o que mudou.
/// A trilha é gravada pelo próprio banco e encadeada por hash; a conferência mostra se alguém a alterou por fora do sistema.
/// Somente o administrador consulta.
/// </summary>
public sealed class ServicoAuditoria(IRepositorioAuditoria repositorio)
{
    public async Task<Resultado<IReadOnlyList<RegistroAuditoria>>> ConsultarAsync(SessaoUsuario sessao, FiltroAuditoria filtro, CancellationToken ct = default)
    {
        if (!sessao.Administrador) return Resultado<IReadOnlyList<RegistroAuditoria>>.Falha("Somente o administrador consulta o histórico de alterações.");
        if (filtro.Ate < filtro.De) return Resultado<IReadOnlyList<RegistroAuditoria>>.Falha("A data final é anterior à inicial.");
        return Resultado<IReadOnlyList<RegistroAuditoria>>.Ok(await repositorio.ConsultarAsync(filtro with { Limite = Math.Clamp(filtro.Limite, 1, 10000) }, ct));
    }

    public Task<IReadOnlyList<string>> TabelasAsync(CancellationToken ct = default) => repositorio.TabelasAsync(ct);

    public async Task<Resultado<long?>> VerificarIntegridadeAsync(SessaoUsuario sessao, CancellationToken ct = default)
        => sessao.Administrador ? Resultado<long?>.Ok(await repositorio.VerificarIntegridadeAsync(ct))
                                : Resultado<long?>.Falha("Somente o administrador confere a trilha de auditoria.");

    private static readonly Dictionary<string, string> Nomes = new()
    {
        ["core.empresa"] = "Empresas", ["core.empresa_regime"] = "Regime tributário", ["core.socio"] = "Sócios", ["core.contador"] = "Contadores",
        ["core.escritorio_contabil"] = "Escritórios contábeis", ["core.usuario_empresa"] = "Acessos às empresas", ["core.perfil"] = "Perfis de acesso",
        ["core.perfil_permissao"] = "Permissões dos perfis", ["core.conta_bancaria"] = "Contas bancárias", ["core.forma_pagamento"] = "Formas de pagamento",
        ["contabil.lancamento"] = "Lançamentos contábeis", ["contabil.plano_contas"] = "Plano de contas", ["contabil.periodo"] = "Períodos contábeis",
        ["contabil.centro_custo"] = "Centros de custo", ["contabil.orcamento"] = "Orçamento", ["contabil.historico_padrao"] = "Históricos padrão",
        ["fiscal.documento"] = "Notas fiscais", ["fiscal.produto"] = "Produtos", ["fiscal.duplicata"] = "Duplicatas", ["fiscal.duplicata_baixa"] = "Baixas de duplicatas",
        ["fiscal.regra_fiscal"] = "Regras fiscais", ["fiscal.regra_contabilizacao"] = "Regras de contabilização", ["fiscal.guia"] = "Guias de impostos",
        ["folha.funcionario"] = "Funcionários", ["folha.rubrica"] = "Rubricas", ["folha.competencia"] = "Competências da folha", ["folha.pro_labore"] = "Pró-labore",
        ["escritorio.pessoa"] = "Clientes do escritório", ["escritorio.tarefa"] = "Tarefas", ["escritorio.protocolo"] = "Protocolos",
        ["escritorio.contrato"] = "Contratos", ["escritorio.honorario_contrato"] = "Honorários", ["escritorio.honorario_cobranca"] = "Cobranças de honorários",
        ["erp.pedido"] = "Pedidos", ["erp.estoque_movimento"] = "Movimentos de estoque",
        ["condominio.unidade"] = "Unidades do condomínio", ["condominio.taxa"] = "Taxas do condomínio", ["condominio.cobranca"] = "Cobranças do condomínio",
        ["condominio.despesa"] = "Despesas do condomínio", ["condominio.assembleia"] = "Atas de assembleia",
    };

    /// <summary>Nome do cadastro para o usuário (a tabela técnica, se não houver nome conhecido).</summary>
    public static string NomeDaTabela(string tabela) => Nomes.TryGetValue(tabela, out var n) ? n : tabela;
}
