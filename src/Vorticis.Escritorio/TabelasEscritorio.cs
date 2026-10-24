using Vorticis.Application.Relatorios;

namespace Vorticis.Escritorio;

/// <summary>Relatórios › Honorários do Escritório: relação de contratos e relatório de honorários (cobranças do período).</summary>
public static class TabelasEscritorio
{
    public const string TituloContratos = "Relatório de contratos";
    public const string TituloHonorarios = "Relatório de honorários";

    private static LinhaRelatorio L(bool destaque, params object?[] v) => new(v, destaque);

    public static TabelaRelatorio Contratos(IReadOnlyList<ContratoEscritorio> contratos, bool somenteAtivos)
    {
        var lista = contratos.Where(c => !somenteAtivos || c.Ativo).OrderBy(c => c.Numero).ToList();
        var linhas = lista.Select(c => L(false, c.Numero, c.Modelo == ModeloContrato.Cliente ? "Cliente" : "Fornecedor", c.PessoaNome ?? "", c.Objeto,
                                         c.Valor, c.Periodicidade.Rotulo(), c.Inicio, c.Fim, c.Ativo ? "Ativo" : "Encerrado")).ToList();
        decimal Mensal(ModeloContrato m) => lista.Where(c => c.Ativo && c.Modelo == m && c.Periodicidade == Periodicidade.Mensal).Sum(c => c.Valor);
        linhas.Add(L(true, null, "TOTAL", $"{lista.Count} contrato(s)", $"Mensal ativo: clientes {Vorticis.Domain.Comum.Formato.Moeda(Mensal(ModeloContrato.Cliente))}; fornecedores {Vorticis.Domain.Comum.Formato.Moeda(Mensal(ModeloContrato.Fornecedor))}",
                     lista.Sum(c => c.Valor), "", null, null, ""));
        return new TabelaRelatorio(TituloContratos,
        [
            new("Nº", TipoColuna.Inteiro, 1.1), new("Tipo", LarguraCm: 2), new("Cliente / fornecedor"), new("Objeto"), new("Valor", TipoColuna.Valor),
            new("Periodicidade", LarguraCm: 2.2), new("Início", TipoColuna.Data), new("Fim", TipoColuna.Data), new("Situação", LarguraCm: 2),
        ], linhas);
    }

    public static TabelaRelatorio Honorarios(IReadOnlyList<CobrancaHonorario> cobrancas, DateOnly hoje)
    {
        var lista = cobrancas.OrderBy(c => c.Competencia).ThenBy(c => c.PessoaNome).ToList();
        var linhas = lista.Select(c => L(false, c.Competencia.ToString("MM/yyyy"), c.PessoaNome ?? "", c.Vencimento, c.Valor,
                                         c.Vencida(hoje) ? "Vencida" : c.Situacao.Rotulo(), c.RecebidoEm, c.ValorRecebido ?? 0m)).ToList();
        var validas = lista.Where(c => c.Situacao != SituacaoCobranca.Cancelada).ToList();
        linhas.Add(L(true, "TOTAL", $"{validas.Count} cobrança(s); em aberto {Vorticis.Domain.Comum.Formato.Moeda(validas.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))}",
                     null, validas.Sum(c => c.Valor), "", null, lista.Sum(c => c.ValorRecebido ?? 0m)));
        return new TabelaRelatorio(TituloHonorarios,
        [
            new("Competência", LarguraCm: 2), new("Cliente"), new("Vencimento", TipoColuna.Data), new("Valor", TipoColuna.Valor), new("Situação", LarguraCm: 2),
            new("Recebido em", TipoColuna.Data), new("Valor recebido", TipoColuna.Valor),
        ], linhas);
    }
}
