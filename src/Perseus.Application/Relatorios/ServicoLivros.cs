using Perseus.Application.Cadastros;
using Perseus.Application.Contabil;
using Perseus.Application.Seguranca;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Relatorios;

/// <summary>Dados dos termos do Livro Diário informados no momento da emissão.</summary>
public sealed record DadosTermos(int NumeroLivro, string RepresentanteNome, string RepresentanteCpf, string ContadorNome, string ContadorCrc,
                                 string? DataArquivamentoAtos = null);

/// <summary>
/// Livros e relatórios em PDF. O Livro Diário segue a ITG 2000: escrituração em ordem cronológica, folhas numeradas
/// sequencialmente e termos de abertura e de encerramento com identificação da entidade e assinaturas do
/// representante legal e do contador. Livro em papel ou PDF não substitui a ECD para quem é obrigado a ela.
/// </summary>
public sealed class ServicoLivros(ServicoRelatorios relatorios, ServicoDemonstracoes demonstracoes, IConsultasContabeis consultas,
                                  IRepositorioEmpresa empresas, Autorizador autorizador, IGeradorPdf gerador)
{
    public async Task<Resultado<ArquivoGerado>> DiarioAsync(long empresaId, DateOnly inicio, DateOnly fim, DadosTermos termos, long usuarioId,
                                                           CancellationToken ct = default)
    {
        var erros = new List<string>();
        if (termos.NumeroLivro < 1) erros.Add("Informe o número de ordem do livro.");
        if (string.IsNullOrWhiteSpace(termos.RepresentanteNome)) erros.Add("Informe o representante legal (assina os termos).");
        if (string.IsNullOrWhiteSpace(termos.ContadorNome) || string.IsNullOrWhiteSpace(termos.ContadorCrc)) erros.Add("Informe o contador e o CRC.");
        if (erros.Count > 0) return Resultado<ArquivoGerado>.Falha(erros);

        var diario = await relatorios.DiarioAsync(empresaId, inicio, fim, usuarioId, ct);
        if (!diario.Sucesso) return Resultado<ArquivoGerado>.Falha(diario.Erros);
        var e = await empresas.ObterAsync(empresaId, ct);
        if (e is null) return Resultado<ArquivoGerado>.Falha("Empresa não encontrada.");

        var linhas = new List<LinhaTabela>();
        foreach (var g in diario.Valor.GroupBy(l => (l.Numero, l.Data)))
            foreach (var l in g)
                linhas.Add(new LinhaTabela([
                    l.Sequencia == g.First().Sequencia ? l.Data.ToString("dd/MM/yyyy") : "",
                    l.Sequencia == g.First().Sequencia ? l.Numero.ToString() : "",
                    $"{l.CodigoConta} {l.DescricaoConta}",
                    l.DC == IndicadorDC.D ? Formato.Moeda(l.Valor) : "",
                    l.DC == IndicadorDC.C ? Formato.Moeda(l.Valor) : "",
                    l.Historico + (l.Status == StatusLancamento.Estornado ? " [ESTORNADO]" : "")]));
        var totalD = diario.Valor.Where(l => l.DC == IndicadorDC.D).Sum(l => l.Valor);
        var totalC = diario.Valor.Where(l => l.DC == IndicadorDC.C).Sum(l => l.Valor);
        linhas.Add(new LinhaTabela(["", "", "TOTAIS DO PERÍODO", Formato.Moeda(totalD), Formato.Moeda(totalC), ""], Negrito: true));

        var identificacao = IdentificacaoEmpresa(e, termos.DataArquivamentoAtos);
        var periodo = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";
        string[] assinaturas =
        [
            $"{termos.RepresentanteNome.Trim()}\nRepresentante legal{(string.IsNullOrWhiteSpace(termos.RepresentanteCpf) ? "" : " — CPF " + termos.RepresentanteCpf.Trim())}",
            $"{termos.ContadorNome.Trim()}\nContador — CRC {termos.ContadorCrc.Trim()}",
        ];
        var local = Local(e);

        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio(
            $"LIVRO DIÁRIO Nº {termos.NumeroLivro}",
            [$"{e.RazaoSocial} — CNPJ {FormatarCnpj(e.Cnpj)}", $"Período: {periodo}"],
            [new Bloco(Colunas: [new("Data", 1.9), new("Nº", 1.2, Alinhamento.Direita), new("Conta", 6.2), new("Débito", 2.6, Alinhamento.Direita),
                                 new("Crédito", 2.6, Alinhamento.Direita), new("Histórico", 12.2)], Linhas: linhas)],
            Paisagem: true,
            TermoAbertura: new Termo("TERMO DE ABERTURA",
                $"Contém este livro {{folhas}} folhas, numeradas tipograficamente de 1 a {{folhas}}, e servirá de LIVRO DIÁRIO Nº {termos.NumeroLivro} " +
                $"para o registro das operações do período de {periodo} da entidade:\n\n{identificacao}\n\n{local}, {inicio:dd/MM/yyyy}.",
                assinaturas),
            TermoEncerramento: new Termo("TERMO DE ENCERRAMENTO",
                $"Contém este livro {{folhas}} folhas, numeradas tipograficamente de 1 a {{folhas}}, e serviu de LIVRO DIÁRIO Nº {termos.NumeroLivro} " +
                $"para o registro das operações do período de {periodo} da entidade:\n\n{identificacao}\n\n{local}, {fim:dd/MM/yyyy}.",
                assinaturas),
            RotuloPagina: "Folha")));
    }

    /// <summary>Livro Razão de todas as contas analíticas com saldo ou movimento no período.</summary>
    public async Task<Resultado<ArquivoGerado>> RazaoAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ArquivoGerado>(usuarioId, empresaId, Permissao.RelatorioRazao, ct) is { } negado) return negado;
        var e = await empresas.ObterAsync(empresaId, ct);
        if (e is null) return Resultado<ArquivoGerado>.Falha("Empresa não encontrada.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var movimentos = (await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct)).ToDictionary(m => m.ContaId);
        var contas = plano.Where(c => c.Analitica && movimentos.TryGetValue(c.Id, out var m) && (m.SaldoAnterior != 0 || m.Debitos != 0 || m.Creditos != 0))
                          .OrderBy(c => c.Codigo, StringComparer.Ordinal).ToArray();

        var blocos = new List<Bloco>();
        foreach (var c in contas)
        {
            var r = await relatorios.RazaoAsync(empresaId, c.Codigo, inicio, fim, usuarioId, ct);
            if (!r.Sucesso) return Resultado<ArquivoGerado>.Falha(r.Erros);
            var linhas = new List<LinhaTabela> { new(["", "", "Saldo anterior", "", "", LinhaBalancete.Formatar(r.Valor.SaldoAnterior)], Negrito: true) };
            linhas.AddRange(r.Valor.Linhas.Select(l => new LinhaTabela([
                l.Data.ToString("dd/MM/yyyy"), l.Numero.ToString(), l.Historico + (l.Estornado ? " [ESTORNADO]" : ""),
                l.DC == IndicadorDC.D ? Formato.Moeda(l.Valor) : "", l.DC == IndicadorDC.C ? Formato.Moeda(l.Valor) : "",
                LinhaBalancete.Formatar(l.SaldoAcumulado)])));
            linhas.Add(new LinhaTabela(["", "", "Saldo final", "", "", LinhaBalancete.Formatar(r.Valor.SaldoFinal)], Negrito: true));
            blocos.Add(new Bloco($"{c.Codigo} — {c.Descricao}", Colunas: ColunasRazao, Linhas: linhas));
        }
        if (blocos.Count == 0) blocos.Add(new Bloco(Texto: "Nenhuma conta com saldo ou movimento no período."));
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio("LIVRO RAZÃO",
            [$"{e.RazaoSocial} — CNPJ {FormatarCnpj(e.Cnpj)}", $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}"], blocos, Paisagem: true)));
    }

    private static readonly Coluna[] ColunasRazao =
        [new("Data", 2.0), new("Nº", 1.3, Alinhamento.Direita), new("Histórico", 12.5), new("Débito", 2.8, Alinhamento.Direita),
         new("Crédito", 2.8, Alinhamento.Direita), new("Saldo", 3.2, Alinhamento.Direita)];

    public async Task<Resultado<ArquivoGerado>> BalanceteAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        var bal = await relatorios.BalanceteAsync(empresaId, inicio, fim, usuarioId, ct: ct);
        if (!bal.Sucesso) return Resultado<ArquivoGerado>.Falha(bal.Erros);
        var e = await empresas.ObterAsync(empresaId, ct);
        if (e is null) return Resultado<ArquivoGerado>.Falha("Empresa não encontrada.");
        var linhas = bal.Valor.Select(l => new LinhaTabela([l.Codigo, l.Descricao, LinhaBalancete.Formatar(l.SaldoAnterior),
            l.Debitos == 0 ? "" : Formato.Moeda(l.Debitos), l.Creditos == 0 ? "" : Formato.Moeda(l.Creditos), LinhaBalancete.Formatar(l.SaldoFinal)],
            Negrito: !l.Analitica, Recuo: l.Nivel - 1)).ToList();
        var analiticas = bal.Valor.Where(l => l.Analitica).ToArray();
        linhas.Add(new LinhaTabela(["", "TOTAIS (contas analíticas)", "", Formato.Moeda(analiticas.Sum(l => l.Debitos)),
                                    Formato.Moeda(analiticas.Sum(l => l.Creditos)), ""], Negrito: true));
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio("BALANCETE DE VERIFICAÇÃO",
            [$"{e.RazaoSocial} — CNPJ {FormatarCnpj(e.Cnpj)}", $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}"],
            [new Bloco(Colunas: [new("Conta", 2.8), new("Descrição", 7.0), new("Saldo anterior", 3.3, Alinhamento.Direita),
                                 new("Débitos", 3.0, Alinhamento.Direita), new("Créditos", 3.0, Alinhamento.Direita),
                                 new("Saldo final", 3.3, Alinhamento.Direita)], Linhas: linhas)],
            Paisagem: true)));
    }

    /// <summary>Balanço Patrimonial na data final e DRE do período, num único PDF, com assinaturas.</summary>
    public async Task<Resultado<ArquivoGerado>> DemonstracoesAsync(long empresaId, DateOnly inicio, DateOnly fim, short nivel, string? contador,
                                                                  string? representante, long usuarioId, CancellationToken ct = default)
    {
        var bp = await demonstracoes.BalancoAsync(empresaId, fim, nivel, usuarioId, ct);
        if (!bp.Sucesso) return Resultado<ArquivoGerado>.Falha(bp.Erros);
        var dre = await demonstracoes.DreAsync(empresaId, inicio, fim, nivel, usuarioId, ct);
        if (!dre.Sucesso) return Resultado<ArquivoGerado>.Falha(dre.Erros);
        var e = await empresas.ObterAsync(empresaId, ct);
        if (e is null) return Resultado<ArquivoGerado>.Falha("Empresa não encontrada.");

        static LinhaTabela L(LinhaDemonstracao l) => new([l.Descricao, l.Codigo, Formato.Moeda(l.Valor)],
            Negrito: l.Tipo != TipoLinhaDemonstracao.Conta, Recuo: l.Tipo == TipoLinhaDemonstracao.Total ? 0 : l.Nivel - 1);
        Coluna[] colunas = [new("Descrição", 11.0), new("Conta", 2.5), new("R$", 3.5, Alinhamento.Direita)];
        var assinaturas = string.Join("\n\n\n", new[] { representante, contador }.Where(s => !string.IsNullOrWhiteSpace(s))
                                                   .Select(s => "_______________________________________\n" + s));

        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio("DEMONSTRAÇÕES CONTÁBEIS",
            [$"{e.RazaoSocial} — CNPJ {FormatarCnpj(e.Cnpj)}"],
            [
                new Bloco($"BALANÇO PATRIMONIAL EM {fim:dd/MM/yyyy} — ATIVO", Colunas: colunas, Linhas: bp.Valor.Ativo.Select(L).ToArray()),
                new Bloco("PASSIVO E PATRIMÔNIO LÍQUIDO", Colunas: colunas, Linhas: bp.Valor.PassivoPl.Select(L).ToArray()),
                new Bloco($"DEMONSTRAÇÃO DO RESULTADO DO PERÍODO DE {inicio:dd/MM/yyyy} A {fim:dd/MM/yyyy}", Colunas: colunas,
                          Linhas: dre.Valor.Linhas.Select(L).ToArray(), NovaPagina: true),
                new Bloco(Texto: assinaturas.Length == 0 ? null : "\n\n" + assinaturas),
            ])));
    }

    private static string IdentificacaoEmpresa(EmpresaDetalhe e, string? dataArquivamento)
    {
        var linhas = new List<string> { e.RazaoSocial };
        linhas.Add($"CNPJ: {FormatarCnpj(e.Cnpj)}" + (string.IsNullOrWhiteSpace(e.Nire) ? "" :
            $" — NIRE: {e.Nire}" + (string.IsNullOrWhiteSpace(dataArquivamento) ? "" : $", atos constitutivos arquivados em {dataArquivamento}")));
        var endereco = string.Join(", ", new[] { e.Logradouro, e.Numero, e.Complemento, e.Bairro }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var cidade = $"{(string.IsNullOrWhiteSpace(e.Municipio) ? $"município IBGE {e.CodMunicipioIbge}" : e.Municipio)}/{e.Uf}" +
                     (string.IsNullOrWhiteSpace(e.Cep) ? "" : $" — CEP {e.Cep}");
        linhas.Add(endereco.Length > 0 ? $"Endereço: {endereco} — {cidade}" : $"Sede: {cidade}");
        return string.Join("\n", linhas);
    }

    private static string Local(EmpresaDetalhe e) => $"{(string.IsNullOrWhiteSpace(e.Municipio) ? e.Uf : e.Municipio + "/" + e.Uf)}";

    public static string FormatarCnpj(string c) => c.Length == 14 ? $"{c[..2]}.{c[2..5]}.{c[5..8]}/{c[8..12]}-{c[12..]}" : c;
}
