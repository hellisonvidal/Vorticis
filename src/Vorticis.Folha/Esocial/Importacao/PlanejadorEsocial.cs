using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Esocial.Importacao;

// ---------------------------------------------------------------------------------------------------- entrada do planejamento

public sealed record FuncionarioExistente(long Id, string Cpf, string Matricula, DateOnly Admissao);

/// <summary>O que a empresa já tem na folha: serve para separar o que é novo do que já existe e para achar conflitos.</summary>
public sealed class SituacaoFolha
{
    public string Cnpj { get; init; } = "";
    public IReadOnlyList<FuncionarioExistente> Funcionarios { get; init; } = [];
    public IReadOnlyList<Rubrica> Rubricas { get; init; } = [];
    public IReadOnlySet<string> Departamentos { get; init; } = new HashSet<string>();
    public IReadOnlyList<Cargo> Cargos { get; init; } = [];
    /// <summary>Parâmetros de encargos gravados da empresa; nulo se a empresa ainda não tem (valem os padrões).</summary>
    public ParametroEmpresa? Parametros { get; init; }
    public IReadOnlySet<(string Cpf, DateOnly Competencia, string Tipo)> Calculos { get; init; } = new HashSet<(string, DateOnly, string)>();
    public IReadOnlySet<DateOnly> CompetenciasFechadas { get; init; } = new HashSet<DateOnly>();
    public IReadOnlySet<(string Cpf, DateOnly Gozo)> Ferias { get; init; } = new HashSet<(string, DateOnly)>();
    public IReadOnlySet<(string Cpf, DateOnly Inicio, string Motivo)> Afastamentos { get; init; } = new HashSet<(string, DateOnly, string)>();
    public IReadOnlyList<ParametroLegal> ParametrosLegais { get; init; } = [];
    public DateOnly Hoje { get; init; } = DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>Escolha do contador para uma rubrica do arquivo: a rubrica de destino (existente ou nova) e se entra nas médias.</summary>
public sealed record DecisaoRubrica(string Origem, string Destino, bool Variavel);

public sealed record OpcoesImportacaoEsocial(bool IgnorarComErro = false, bool ImportarRemuneracoes = true, bool ImportarFerias = true, bool FecharCompetencias = true,
                                             bool AtualizarParametros = false, DateOnly? De = null, DateOnly? Ate = null, IReadOnlyList<DecisaoRubrica>? Rubricas = null);

// ---------------------------------------------------------------------------------------------------- saída do planejamento

/// <summary>Linha da grade de rubricas da prévia: o que o arquivo traz, o destino sugerido e a situação.</summary>
public sealed record RubricaPlano(string Origem, string Descricao, string Natureza, char Tipo, string IncInss, string IncIrrf, string IncFgts, string Destino,
                                  bool Existente, bool Variavel, string Situacao, string? Aviso, int Usos);

public sealed record NovaRubrica(string Codigo, string Descricao, char Tipo, string Natureza, string IncInss, string IncIrrf, string IncFgts, bool Variavel, bool Ativa);
public sealed record NovoDepartamento(string Codigo, string Nome);
public sealed record NovoCargo(string Nome, string? Cbo);
public sealed record NovaAlteracao(DateOnly Data, string Descricao, decimal? SalarioAnterior, decimal? SalarioNovo, string? CargoAnterior, string? CargoNovo);

public sealed record NovoFuncionario(string Matricula, string Nome, string Cpf, DateOnly Nascimento, string Sexo, int RacaCor, int EstadoCivil, string GrauInstrucao,
    string? Email, string? Telefone, EnderecoEsocial? Endereco, DateOnly Admissao, string Categoria, string TipoContrato, DateOnly? Termino, string? Cargo, string? Cbo,
    string? Departamento, string TipoSalario, decimal Salario, decimal JornadaSemanal, decimal JornadaMensal, bool Periculosidade, string? CnpjSindicato, int TipoJornada,
    string? DescricaoJornada, string Situacao, DateOnly? Desligamento, string? MotivoDesligamento, IReadOnlyList<DependenteEsocial> Dependentes,
    IReadOnlyList<NovaAlteracao> Alteracoes);

public sealed record NovoAfastamento(string Cpf, DateOnly Inicio, DateOnly? Fim, string Motivo, string? Observacao);
public sealed record NovasFerias(string Cpf, DateOnly AquisitivoInicio, DateOnly AquisitivoFim, DateOnly Gozo, int Dias);
public sealed record ItemCalculoImportado(string Rubrica, string Descricao, char Tipo, decimal Referencia, decimal Valor);

public sealed record CalculoImportado(string Cpf, DateOnly Competencia, string Tipo, DateOnly? Pagamento, IReadOnlyList<ItemCalculoImportado> Itens, decimal TotalProventos,
    decimal TotalDescontos, decimal BaseInss, decimal BaseIrrf, decimal BaseFgts, decimal Inss, decimal Irrf, decimal Fgts, decimal SalarioFamilia, string Memoria);

public sealed class PlanoEsocial
{
    public List<LinhaPrevia> Linhas { get; } = [];
    public List<OcorrenciaImportacao> Ocorrencias { get; } = [];
    public List<RubricaPlano> GradeRubricas { get; } = [];
    public List<NovaRubrica> Rubricas { get; } = [];
    public List<NovoDepartamento> Departamentos { get; } = [];
    public List<NovoCargo> Cargos { get; } = [];
    public List<NovoFuncionario> Funcionarios { get; } = [];
    public List<NovoAfastamento> Afastamentos { get; } = [];
    public List<NovasFerias> Ferias { get; } = [];
    public List<CalculoImportado> Calculos { get; } = [];
    public SortedSet<DateOnly> CompetenciasAFechar { get; } = [];
    /// <summary>Parâmetros de encargos a gravar (novos ou atualizados); nulo se nada muda.</summary>
    public ParametroEmpresa? Parametros { get; set; }
    public bool ParametrosNovos { get; set; }
    public Dictionary<string, string> Descricoes { get; } = [];
    public Dictionary<string, int> Erros { get; } = [];
    public Dictionary<string, int> Existentes { get; } = [];
    public DateOnly? Inicio { get; set; }
    public DateOnly? Fim { get; set; }
    public bool CnpjConfere { get; set; } = true;
    public int ErrosPontuais => Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Erro && o.Pontual);
    public bool TemErroImpeditivo => Ocorrencias.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual);
}

// ---------------------------------------------------------------------------------------------------- planejador

public static class PlanejadorEsocial
{
    /// <summary>Naturezas de rubrica (Tabela 3 do eSocial) que entram nas médias: DSR, horas extras, adicional noturno e comissões, como as rubricas padrão da folha.</summary>
    private static readonly HashSet<string> NaturezasVariaveis = ["1002", "1003", "1205", "1207"];

    public static string Alternativo(string codigo, ISet<string> usados)
    {
        var baseCodigo = "E" + codigo;
        if (baseCodigo.Length > 10) baseCodigo = baseCodigo[..10];
        var c = baseCodigo;
        for (var n = 2; usados.Contains(c); n++)
        {
            var suf = n.ToString();
            c = baseCodigo[..Math.Min(baseCodigo.Length, 10 - suf.Length)] + suf;
        }
        return c;
    }

    public static string? DescricaoMotivo(string? mtv) => mtv switch
    {
        "01" => "JUSTA_CAUSA", "02" or "03" or "05" or "09" or "10" => "SEM_JUSTA_CAUSA", "04" or "07" => "PEDIDO_DEMISSAO", "06" => "TERMINO_CONTRATO", "33" => "ACORDO_484A",
        _ => null,
    };

    private static decimal Arred(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static PlanoEsocial Planejar(DadosEsocial d, SituacaoFolha sit, OpcoesImportacaoEsocial op)
    {
        var plano = new PlanoEsocial { Inicio = d.PrimeiroPeriodo, Fim = d.UltimoPeriodo };
        plano.Ocorrencias.AddRange(d.Ocorrencias);
        void Erro(string reg, string rotulo, string motivo) { plano.Ocorrencias.Add(OcorrenciaImportacao.ErroPontual(0, rotulo, motivo)); plano.Erros[reg] = plano.Erros.GetValueOrDefault(reg) + 1; }
        void Aviso(string rotulo, string motivo) => plano.Ocorrencias.Add(OcorrenciaImportacao.Aviso(0, rotulo, motivo));
        var cnpj = Xe.Digitos(sit.Cnpj);

        // ---- empregador
        var raiz = cnpj.Length >= 8 ? cnpj[..8] : cnpj;
        var outros = d.Empregadores.Where(e => e != raiz).ToList();
        if (d.Empregadores.Count > 0 && (outros.Count > 0))
        {
            plano.CnpjConfere = false;
            plano.Ocorrencias.Add(OcorrenciaImportacao.Erro(0, "ideEmpregador", $"Os arquivos são do empregador de CNPJ iniciado em {string.Join(", ", d.Empregadores.Order())}, " +
                $"mas a empresa ativa tem CNPJ {cnpj} (raiz {raiz}). Troque de empresa ou confira os arquivos."));
        }
        if (d.Eventos.Count == 0 && !plano.TemErroImpeditivo)
            plano.Ocorrencias.Add(OcorrenciaImportacao.Erro(0, "arquivos", "Nenhum evento do eSocial importável foi encontrado nos arquivos."));

        // ---- rubricas
        var existentes = sit.Rubricas.ToDictionary(r => r.Codigo, StringComparer.Ordinal);
        var usos = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in d.Remuneracoes)
            foreach (var dm in r.Demonstrativos)
                foreach (var it in dm.Itens) usos[it.Rubrica] = usos.GetValueOrDefault(it.Rubrica) + 1;
        var decisoes = (op.Rubricas ?? []).ToDictionary(x => x.Origem, StringComparer.Ordinal);
        var codigosUsados = new HashSet<string>(existentes.Keys, StringComparer.Ordinal);
        var criadas = new Dictionary<string, NovaRubrica>(StringComparer.Ordinal);
        var destinos = new Dictionary<string, RubricaPlano>(StringComparer.Ordinal);   // origem -> plano
        var origens = d.Rubricas.Values.Where(r => !r.Excluida || usos.ContainsKey(r.Codigo)).OrderBy(r => r.Codigo, StringComparer.Ordinal).ToList();
        int rubNovas = 0, rubExist = 0, rubConflito = 0;
        var amostrasRub = new List<string>();
        foreach (var o in origens)
        {
            char tipo = o.TipoFolha;
            string destino; string situacao; string? aviso = null;
            var existenteMesmoCodigo = existentes.GetValueOrDefault(o.Codigo);
            var porNome = sit.Rubricas.FirstOrDefault(r => r.Tipo == tipo && string.Equals(r.Descricao, o.Descricao, StringComparison.OrdinalIgnoreCase));
            if (decisoes.TryGetValue(o.Codigo, out var dec) && !string.IsNullOrWhiteSpace(dec.Destino))
                destino = dec.Destino.Trim();
            else if (existenteMesmoCodigo is not null && existenteMesmoCodigo.Tipo == tipo)
                destino = o.Codigo;
            else if (porNome is not null)
                destino = porNome.Codigo;
            else if (existenteMesmoCodigo is not null)
            {
                destino = Alternativo(o.Codigo, codigosUsados);
                aviso = $"O código {o.Codigo} já é de outra rubrica do Vorticis ({existenteMesmoCodigo.Descricao}); esta será criada como {destino}.";
            }
            else
                destino = o.Codigo;
            bool variavel;
            if (existentes.TryGetValue(destino, out var ex))
            {
                variavel = ex.Variavel;
                situacao = "Existente";
                if (ex.Tipo != tipo) aviso = $"A rubrica {destino} do Vorticis é {(ex.Tipo == 'P' ? "provento" : ex.Tipo == 'D' ? "desconto" : "informativa")}, mas a do arquivo é {(tipo == 'P' ? "provento" : tipo == 'D' ? "desconto" : "informativa")}.";
                else if (ex.Natureza != o.Natureza) aviso = $"A natureza difere (Vorticis {ex.Natureza}, arquivo {o.Natureza}).";
                if (aviso is not null) { situacao = "Conflito"; rubConflito++; } else rubExist++;
            }
            else
            {
                variavel = decisoes.TryGetValue(o.Codigo, out var dv) ? dv.Variavel : NaturezasVariaveis.Contains(o.Natureza) && tipo == 'P';
                situacao = aviso is null ? "Nova" : "Conflito";
                if (aviso is not null) rubConflito++;
                if (destino.Length > 10 || destino.Length == 0) { Erro("S-1010", $"rubrica {o.Codigo}", $"O código de destino \"{destino}\" é inválido (use até 10 caracteres)."); continue; }
                if (!criadas.ContainsKey(destino))
                {
                    var nova = new NovaRubrica(destino, o.Descricao.Length > 100 ? o.Descricao[..100] : o.Descricao, tipo, o.Natureza.PadLeft(4, '0')[..4], Dois(o.IncCp), Dois(o.IncIrrf), Dois(o.IncFgts), variavel, !o.Excluida);
                    criadas[destino] = nova; codigosUsados.Add(destino); plano.Rubricas.Add(nova); rubNovas++;
                }
                else situacao = "Nova (junta com a mesma de destino)";
            }
            var linha = new RubricaPlano(o.Codigo, o.Descricao, o.Natureza, tipo, Dois(o.IncCp), Dois(o.IncIrrf), Dois(o.IncFgts), destino, existentes.ContainsKey(destino), variavel, situacao, aviso,
                                         usos.GetValueOrDefault(o.Codigo));
            destinos[o.Codigo] = linha;
            plano.GradeRubricas.Add(linha);
            if (aviso is not null) Aviso($"rubrica {o.Codigo}", aviso + " Escolha outra rubrica de destino na grade de rubricas, se necessário.");
            if (amostrasRub.Count < 4) amostrasRub.Add($"{o.Codigo} {o.Descricao}");
        }
        plano.Linhas.Add(new LinhaPrevia("S-1010", "Rubricas", origens.Count, rubNovas, rubExist, rubConflito, amostrasRub));
        plano.Descricoes["S-1010"] = "Rubricas";
        plano.Existentes["S-1010"] = rubExist;

        // propriedades da rubrica de destino (para classificar e somar os holerites)
        (char Tipo, string Natureza, string IncInss, string IncIrrf, string IncFgts, string Descricao)? Dest(string origem)
        {
            if (destinos.TryGetValue(origem, out var p))
            {
                if (existentes.TryGetValue(p.Destino, out var ex)) return (ex.Tipo, ex.Natureza, ex.IncInss, ex.IncIrrf, ex.IncFgts, ex.Descricao);
                if (criadas.TryGetValue(p.Destino, out var nr)) return (nr.Tipo, nr.Natureza, nr.IncInss, nr.IncIrrf, nr.IncFgts, nr.Descricao);
            }
            if (existentes.TryGetValue(origem, out var e2)) return (e2.Tipo, e2.Natureza, e2.IncInss, e2.IncIrrf, e2.IncFgts, e2.Descricao);
            return null;
        }
        string CodigoDestino(string origem) => destinos.TryGetValue(origem, out var p) ? p.Destino : origem;

        // ---- parâmetros da empresa (S-1000, S-1005, S-1020)
        PlanejarParametros(d, sit, op, plano);

        // ---- trabalhadores
        var porCpf = sit.Funcionarios.ToDictionary(f => f.Cpf, StringComparer.Ordinal);
        var matriculas = sit.Funcionarios.ToDictionary(f => f.Matricula, f => f.Cpf, StringComparer.OrdinalIgnoreCase);
        var cargosExistentes = sit.Cargos.ToDictionary(c => c.Nome.Trim().ToUpperInvariant(), c => c, StringComparer.Ordinal);
        var cargosNovos = new Dictionary<string, NovoCargo>(StringComparer.Ordinal);
        var lotacaoDe = new Dictionary<string, string>(StringComparer.Ordinal);
        var periculoso = new HashSet<string>(StringComparer.Ordinal);
        var insalubre = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in d.Remuneracoes.OrderBy(r => r.Periodo))
        {
            foreach (var dm in r.Demonstrativos)
                foreach (var it in dm.Itens)
                {
                    if (it.Lotacao is not null) lotacaoDe[r.Cpf] = it.Lotacao;
                    var nat = d.Rubricas.TryGetValue(it.Rubrica, out var ro) ? ro.Natureza : Dest(it.Rubrica)?.Natureza;
                    if (nat == "1203") periculoso.Add(r.Cpf);
                    if (nat == "1202") insalubre.Add(r.Cpf);
                }
        }
        var importaveis = new Dictionary<string, (string Matricula, DateOnly Admissao)>(StringComparer.Ordinal);   // CPF -> funcionário (novo ou já existente)
        foreach (var f in sit.Funcionarios) importaveis[f.Cpf] = (f.Matricula, f.Admissao);
        var matriculasArquivo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int fNovos = 0, fExist = 0, fSemVinculo = 0;
        var amostrasF = new List<string>();
        var deptsUsados = new SortedSet<string>(StringComparer.Ordinal);
        var admitidosNovos = new Dictionary<string, NovoFuncionario>(StringComparer.Ordinal);
        foreach (var t in d.Trabalhadores.Values.OrderBy(t => t.Nome, StringComparer.Ordinal))
        {
            var rot = $"{t.Nome} (CPF {t.Cpf})";
            var cat = t.Contrato.Categoria ?? (t.SemVinculo ? "" : "101");
            if (t.SemVinculo && !cat.StartsWith('9'))
            {
                fSemVinculo++;
                Aviso(rot, $"Trabalhador sem vínculo (categoria {cat}): não é funcionário da folha. Sócios e diretores ficam em Cadastros › Sócios (pró-labore).");
                continue;
            }
            if (porCpf.ContainsKey(t.Cpf)) { fExist++; plano.Existentes["S-2200"] = fExist; continue; }
            if (string.IsNullOrWhiteSpace(t.Nome)) { Erro("S-2200", rot, "Funcionário sem nome."); continue; }
            if (t.Nascimento is not { } nasc || nasc == DateOnly.MinValue) { Erro("S-2200", rot, "Funcionário sem data de nascimento no arquivo."); continue; }
            if (t.Sexo is not ("M" or "F")) { Erro("S-2200", rot, "Funcionário sem sexo (M ou F) no arquivo."); continue; }
            var mat = t.Matricula.Length > 30 ? t.Matricula[..30] : t.Matricula;
            if (matriculas.TryGetValue(mat, out var outro) && outro != t.Cpf) { Erro("S-2200", rot, $"A matrícula {mat} já pertence a outro funcionário do Vorticis (CPF {outro})."); continue; }
            if (matriculasArquivo.TryGetValue(mat, out var outro2) && outro2 != t.Cpf) { Erro("S-2200", rot, $"A matrícula {mat} aparece para dois CPFs nos arquivos."); continue; }
            matriculasArquivo[mat] = t.Cpf;

            var c = t.Contrato;
            var (tipoSal, salario, aviso) = SalarioMensal(c.Salario, c.UnidadeSalario);
            if (salario <= 0) { Erro("S-2200", rot, "Funcionário sem salário (vrSalFx) no arquivo."); continue; }
            if (aviso is not null) Aviso(rot, aviso);
            var categoria = cat.Length == 3 ? cat : "101";
            var tipoContrato = categoria == "103" ? "APRENDIZ" : categoria.StartsWith('9') ? "ESTAGIO" : (c.TipoContrato == 2 || c.Termino is not null) ? "DETERMINADO" : "INDETERMINADO";
            var semanal = c.HorasSemana is > 0 and <= 80 ? c.HorasSemana.Value : 44m;
            string? cargo = string.IsNullOrWhiteSpace(c.Cargo) ? null : (c.Cargo.Trim().Length > 100 ? c.Cargo.Trim()[..100] : c.Cargo.Trim());
            if (cargo is not null && !cargosExistentes.ContainsKey(cargo.ToUpperInvariant()) && !cargosNovos.ContainsKey(cargo.ToUpperInvariant()))
                cargosNovos[cargo.ToUpperInvariant()] = new NovoCargo(cargo, c.Cbo is { Length: 6 } cb && cb.All(char.IsAsciiDigit) ? cb : null);
            string? dep = lotacaoDe.GetValueOrDefault(t.Cpf);
            if (dep is not null && dep.Length > 20) { Aviso(rot, $"Lotação \"{dep}\" tem mais de 20 caracteres: departamento não preenchido."); dep = null; }
            if (dep is not null) deptsUsados.Add(dep);

            // situação
            var emAfastamento = t.Afastamentos.Any(a => a.Motivo != "15" && a.Inicio <= sit.Hoje && (a.Fim is null || a.Fim >= sit.Hoje));
            var situacao = t.Desligamento is not null ? "DESLIGADO" : emAfastamento ? "AFASTADO" : "ATIVO";
            if (t.Desligamento is { } dl && dl.Data < t.Admissao) { Erro("S-2200", rot, $"Desligamento em {dl.Data:dd/MM/yyyy} anterior à admissão em {t.Admissao:dd/MM/yyyy}."); continue; }
            var motivo = t.Desligamento is { } d2 ? DescricaoMotivo(d2.Motivo) : null;
            if (t.Desligamento is not null && motivo is null) Aviso(rot, $"Motivo de desligamento {t.Desligamento.Motivo} sem correspondente no Vorticis: motivo não preenchido.");
            if (insalubre.Contains(t.Cpf)) Aviso(rot, "Recebia adicional de insalubridade: informe o percentual (10%, 20% ou 40%) no cadastro do funcionário.");

            var alteracoes = new List<NovaAlteracao>();
            var atual = t.ContratoInicial;
            foreach (var a in t.Alteracoes.OrderBy(a => a.Data).ThenBy(a => a.IdEvento, StringComparer.Ordinal))
            {
                var prox = atual.Mesclar(a.Contrato);
                var salAnt = SalarioMensal(atual.Salario, atual.UnidadeSalario).Salario;
                var salNovo = SalarioMensal(prox.Salario, prox.UnidadeSalario).Salario;
                if (salAnt != salNovo || !string.Equals(atual.Cargo, prox.Cargo, StringComparison.OrdinalIgnoreCase))
                    alteracoes.Add(new NovaAlteracao(a.Data, Trunc(a.Descricao ?? "Alteração contratual importada do eSocial", 200), salAnt, salNovo, atual.Cargo is null ? null : Trunc(atual.Cargo, 100),
                                                     prox.Cargo is null ? null : Trunc(prox.Cargo, 100)));
                atual = prox;
            }
            var end = t.Endereco;
            var tpJornada = c.TipoJornada is 2 or 3 or 4 or 5 or 6 or 7 ? c.TipoJornada.Value : 9;
            var novo = new NovoFuncionario(mat, Trunc(t.Nome, 150), t.Cpf, nasc, t.Sexo, t.RacaCor is >= 1 and <= 6 ? t.RacaCor.Value : 6, t.EstadoCivil is >= 1 and <= 5 ? t.EstadoCivil.Value : 1,
                t.GrauInstrucao is { Length: 2 } gi ? gi : "09", t.Email is null ? null : Trunc(t.Email, 150), t.Telefone is null ? null : Trunc(t.Telefone, 20),
                end is null ? null : end with
                {
                    Tipo = end.Tipo is { Length: <= 4 } ? end.Tipo : null, Logradouro = end.Logradouro is null ? null : Trunc(end.Logradouro, 100), Numero = end.Numero is null ? null : Trunc(end.Numero, 15),
                    Complemento = end.Complemento is null ? null : Trunc(end.Complemento, 60), Bairro = end.Bairro is null ? null : Trunc(end.Bairro, 60),
                    Cep = end.Cep is { Length: 8 } ? end.Cep : null, CodMunicipio = end.CodMunicipio is { Length: 7 } ? end.CodMunicipio : null, Uf = end.Uf is { Length: 2 } ? end.Uf : null,
                },
                t.Admissao, categoria, tipoContrato, c.Termino, cargo, c.Cbo is { Length: 6 } cb2 && cb2.All(char.IsAsciiDigit) ? cb2 : null, dep, tipoSal, salario, semanal, Math.Round(semanal * 5, 2),
                periculoso.Contains(t.Cpf), c.CnpjSindicato is { Length: 14 } sd && sd.All(char.IsAsciiDigit) ? sd : null, tpJornada, c.DescricaoJornada is null ? null : Trunc(c.DescricaoJornada, 300),
                situacao, t.Desligamento?.Data, motivo, t.Dependentes.Where(x => x.Nascimento != DateOnly.MinValue && x.Nome.Length > 0).ToList(), alteracoes);
            plano.Funcionarios.Add(novo);
            admitidosNovos[t.Cpf] = novo;
            importaveis[t.Cpf] = (mat, t.Admissao);
            fNovos++;
            if (amostrasF.Count < 4) amostrasF.Add(novo.Nome);
        }
        plano.Linhas.Add(new LinhaPrevia("S-2200", "Funcionários (admissões)", fNovos + fExist + plano.Erros.GetValueOrDefault("S-2200"), fNovos, fExist, 0, amostrasF));
        plano.Descricoes["S-2200"] = "Funcionários (admissões)";
        plano.Cargos.AddRange(cargosNovos.Values.OrderBy(c => c.Nome, StringComparer.Ordinal));
        plano.Linhas.Add(new LinhaPrevia("CARGO", "Cargos", cargosNovos.Count + cargosExistentes.Count(c => d.Trabalhadores.Values.Any(t => string.Equals(t.Contrato.Cargo?.Trim(), c.Value.Nome.Trim(), StringComparison.OrdinalIgnoreCase))),
                                         cargosNovos.Count, cargosExistentes.Count(c => d.Trabalhadores.Values.Any(t => string.Equals(t.Contrato.Cargo?.Trim(), c.Value.Nome.Trim(), StringComparison.OrdinalIgnoreCase))), 0,
                                         cargosNovos.Values.Take(4).Select(c => c.Nome).ToList()));
        plano.Descricoes["CARGO"] = "Cargos";

        // ---- departamentos (S-1020 e lotações usadas)
        var lotacoesUsadas = new SortedSet<string>(deptsUsados, StringComparer.Ordinal);
        foreach (var l in d.Lotacoes.Values.Where(l => !l.Excluida && l.Codigo.Length <= 20)) lotacoesUsadas.Add(l.Codigo);
        int dNovos = 0, dExist = 0;
        foreach (var cod in lotacoesUsadas)
        {
            if (sit.Departamentos.Contains(cod)) { dExist++; continue; }
            plano.Departamentos.Add(new NovoDepartamento(cod, $"Lotação {cod}"));
            dNovos++;
        }
        plano.Linhas.Add(new LinhaPrevia("S-1020", "Lotações (departamentos)", dNovos + dExist, dNovos, dExist, 0, plano.Departamentos.Take(4).Select(x => x.Codigo).ToList()));
        plano.Descricoes["S-1020"] = "Lotações (departamentos)";
        plano.Existentes["S-1020"] = dExist;

        // alterações, desligamentos e dependentes (dos funcionários novos)
        int altN = plano.Funcionarios.Sum(f => f.Alteracoes.Count), altTot = d.Trabalhadores.Values.Sum(t => t.Alteracoes.Count);
        plano.Linhas.Add(new LinhaPrevia("S-2206", "Alterações contratuais (salário e cargo)", altTot, altN, 0, 0, []));
        plano.Descricoes["S-2206"] = "Alterações contratuais";
        plano.Existentes["S-2206"] = Math.Max(0, altTot - altN);
        int deslN = plano.Funcionarios.Count(f => f.Desligamento is not null);
        plano.Linhas.Add(new LinhaPrevia("S-2299", "Desligamentos", d.Trabalhadores.Values.Count(t => t.Desligamento is not null), deslN, 0, 0, []));
        plano.Descricoes["S-2299"] = "Desligamentos";

        // ---- afastamentos e férias
        PlanejarAfastamentos(d, sit, op, plano, importaveis, porCpf, Erro, Aviso);

        // ---- remunerações e pagamentos
        if (op.ImportarRemuneracoes) PlanejarRemuneracoes(d, sit, op, plano, importaveis, Dest, CodigoDestino, d.Rubricas, Erro, Aviso);

        if (plano.Inicio is null && plano.Funcionarios.Count > 0) plano.Inicio = plano.Funcionarios.Min(f => f.Admissao);
        return plano;
    }

    private static string Dois(string s) => s.Length >= 2 ? s[..2] : s.PadLeft(2, '0');

    private static string Trunc(string s, int n) => s.Length > n ? s[..n] : s;

    /// <summary>Converte o salário contratual para mensal (ou valor da hora, nos horistas).</summary>
    public static (string Tipo, decimal Salario, string? Aviso) SalarioMensal(decimal? valor, int? unidade)
    {
        var v = valor ?? 0m;
        return unidade switch
        {
            1 => ("HORISTA", v, null),
            2 => ("MENSAL", Arred(v * 30m), "Salário por dia convertido em mensal (x 30): confira o valor."),
            3 => ("MENSAL", Arred(v * 30m / 7m), "Salário por semana convertido em mensal (x 30 / 7): confira o valor."),
            4 => ("MENSAL", Arred(v * 2m), "Salário por quinzena convertido em mensal (x 2): confira o valor."),
            6 or 7 => ("MENSAL", v, "Unidade de salário por tarefa/não aplicável tratada como mensal: confira o valor."),
            _ => ("MENSAL", v, null),
        };
    }

    // ------------------------------------------------------------------ parâmetros

    private static void PlanejarParametros(DadosEsocial d, SituacaoFolha sit, OpcoesImportacaoEsocial op, PlanoEsocial plano)
    {
        var estab = d.Estabelecimentos.FirstOrDefault();
        var lot = d.Lotacoes.Values.FirstOrDefault(l => !l.Excluida && l.Fpas is not null);
        var emp = d.Empregador;
        if (emp?.ClasseTributaria is null && estab is null && lot is null) return;
        var atual = sit.Parametros ?? ParametroEmpresa.Padrao;
        var novo = atual with
        {
            RatAliquota = estab?.Rat ?? atual.RatAliquota,
            Fap = estab?.Fap ?? atual.Fap,
            Fpas = lot?.Fpas is { Length: 3 } f ? f : atual.Fpas,
            CodTerceiros = lot?.CodTerceiros is { Length: 4 } c ? c : atual.CodTerceiros,
            ClassTrib = emp?.ClasseTributaria is { Length: 2 } ct ? ct : atual.ClassTrib,
        };
        var difere = novo != atual;
        int n = 0, e = 0, conf = 0;
        if (sit.Parametros is null) { plano.Parametros = novo; plano.ParametrosNovos = true; n = 1; }
        else if (!difere) e = 1;
        else if (op.AtualizarParametros) { plano.Parametros = novo; n = 1; }
        else
        {
            conf = 1;
            plano.Ocorrencias.Add(OcorrenciaImportacao.Aviso(0, "S-1000/S-1005/S-1020",
                $"Os parâmetros de encargos da empresa diferem do arquivo (RAT {atual.RatAliquota} x {novo.RatAliquota}, FAP {atual.Fap} x {novo.Fap}, FPAS {atual.Fpas} x {novo.Fpas}, " +
                $"terceiros {atual.CodTerceiros} x {novo.CodTerceiros}, classificação tributária {atual.ClassTrib} x {novo.ClassTrib}). Marque \"Atualizar os parâmetros de encargos\" para usar os do arquivo."));
        }
        plano.Linhas.Add(new LinhaPrevia("S-1000", "Parâmetros de encargos (RAT, FAP, FPAS, terceiros)", 1, n, e, conf, []));
        plano.Descricoes["S-1000"] = "Parâmetros de encargos";
        plano.Existentes["S-1000"] = e;
    }

    // ------------------------------------------------------------------ afastamentos e férias

    private static void PlanejarAfastamentos(DadosEsocial d, SituacaoFolha sit, OpcoesImportacaoEsocial op, PlanoEsocial plano,
        Dictionary<string, (string Matricula, DateOnly Admissao)> importaveis, Dictionary<string, FuncionarioExistente> porCpf, Action<string, string, string> erro, Action<string, string> aviso)
    {
        int aN = 0, aE = 0, fN = 0, fE = 0;
        var usadosPeriodo = new Dictionary<(string, DateOnly), int>();
        foreach (var t in d.Trabalhadores.Values.OrderBy(t => t.Cpf, StringComparer.Ordinal))
        {
            if (!importaveis.TryGetValue(t.Cpf, out var vinc)) continue;
            if (plano.Funcionarios.All(x => x.Cpf != t.Cpf) && !porCpf.ContainsKey(t.Cpf)) continue;   // funcionário com erro: nada dele entra
            var rot = $"{t.Nome} (CPF {t.Cpf})";
            foreach (var a in t.Afastamentos.OrderBy(a => a.Inicio))
            {
                if (a.Fim is { } fim && fim < a.Inicio) { erro("S-2230", rot, $"Afastamento com término ({fim:dd/MM/yyyy}) antes do início ({a.Inicio:dd/MM/yyyy})."); continue; }
                var motivo = a.Motivo.Length == 1 ? "0" + a.Motivo : a.Motivo;
                if (motivo.Length != 2) { erro("S-2230", rot, $"Motivo de afastamento \"{a.Motivo}\" inválido."); continue; }
                if (motivo == "15")
                {
                    if (!op.ImportarFerias) continue;
                    if (a.Fim is not { } termino) { aviso(rot, $"Férias iniciadas em {a.Inicio:dd/MM/yyyy} sem término nos arquivos: não importadas."); continue; }
                    if (sit.Ferias.Contains((t.Cpf, a.Inicio))) { fE++; continue; }
                    var dias = termino.DayNumber - a.Inicio.DayNumber + 1;
                    if (dias is < 5 or > 30) { aviso(rot, $"Férias de {a.Inicio:dd/MM/yyyy} com {dias} dia(s): o Vorticis guarda períodos de 5 a 30 dias; não importadas."); continue; }
                    var periodos = PeriodosFerias.Calcular(vinc.Admissao, a.Inicio);
                    PeriodoAquisitivo? escolhido = null;
                    if (a.AquisitivoInicio is { } ai) escolhido = periodos.OrderBy(p => Math.Abs(p.Inicio.DayNumber - ai.DayNumber)).FirstOrDefault();
                    escolhido ??= periodos.FirstOrDefault(p => p.Completo(a.Inicio) && usadosPeriodo.GetValueOrDefault((t.Cpf, p.Inicio)) + dias <= 30) ?? periodos.LastOrDefault(p => p.Completo(a.Inicio));
                    if (escolhido is null) { aviso(rot, $"Férias de {a.Inicio:dd/MM/yyyy} sem período aquisitivo completo desde a admissão ({vinc.Admissao:dd/MM/yyyy}): não importadas."); continue; }
                    usadosPeriodo[(t.Cpf, escolhido.Inicio)] = usadosPeriodo.GetValueOrDefault((t.Cpf, escolhido.Inicio)) + dias;
                    plano.Ferias.Add(new NovasFerias(t.Cpf, escolhido.Inicio, escolhido.Fim, a.Inicio, dias));
                    fN++;
                    continue;
                }
                if (sit.Afastamentos.Contains((t.Cpf, a.Inicio, motivo))) { aE++; continue; }
                plano.Afastamentos.Add(new NovoAfastamento(t.Cpf, a.Inicio, a.Fim, motivo, a.Observacao is null ? null : Trunc(a.Observacao, 200)));
                aN++;
            }
        }
        plano.Linhas.Add(new LinhaPrevia("S-2230", "Afastamentos", aN + aE, aN, aE, 0, []));
        plano.Descricoes["S-2230"] = "Afastamentos";
        plano.Existentes["S-2230"] = aE;
        plano.Linhas.Add(new LinhaPrevia("FERIAS", "Férias gozadas (períodos aquisitivos usados)", fN + fE, fN, fE, 0, []));
        plano.Descricoes["FERIAS"] = "Férias gozadas";
        plano.Existentes["FERIAS"] = fE;
    }

    // ------------------------------------------------------------------ remunerações (S-1200) e pagamentos (S-1210)

    private static void PlanejarRemuneracoes(DadosEsocial d, SituacaoFolha sit, OpcoesImportacaoEsocial op, PlanoEsocial plano,
        Dictionary<string, (string Matricula, DateOnly Admissao)> importaveis,
        Func<string, (char Tipo, string Natureza, string IncInss, string IncIrrf, string IncFgts, string Descricao)?> dest, Func<string, string> codigoDestino,
        Dictionary<string, RubricaEsocial> rubricasArquivo, Action<string, string, string> erro, Action<string, string> aviso)
    {
        int cN = 0, cE = 0, pN = 0, semCadastro = 0;
        var semFgts = false;
        var ignoradosCpf = new HashSet<string>(StringComparer.Ordinal);
        var rubricaSemCadastro = new HashSet<string>(StringComparer.Ordinal);
        var pagamentos = d.Pagamentos.SelectMany(p => p.Pagamentos.Select(x => (p.Cpf, p.Periodo, Info: x))).ToList();
        var calculosCriados = new HashSet<(string, DateOnly, string)>();
        foreach (var r in d.Remuneracoes.OrderBy(r => r.Periodo).ThenBy(r => r.Cpf, StringComparer.Ordinal))
        {
            if (op.De is { } de && r.Periodo < new DateOnly(de.Year, de.Month, 1)) continue;
            if (op.Ate is { } ate && r.Periodo > new DateOnly(ate.Year, ate.Month, 1)) continue;
            if (!importaveis.ContainsKey(r.Cpf) || (plano.Funcionarios.All(f => f.Cpf != r.Cpf) && !sit.Funcionarios.Any(f => f.Cpf == r.Cpf)))
            {
                if (ignoradosCpf.Add(r.Cpf)) semCadastro++;
                continue;
            }
            var rot = $"S-1200 {r.Cpf} {r.Periodo:MM/yyyy}";
            var porTipo = new Dictionary<string, List<(DemonstrativoEsocial Dm, List<(ItemRemuneracaoEsocial It, (char Tipo, string Natureza, string IncInss, string IncIrrf, string IncFgts, string Descricao) R)> Itens)>>();
            var falhou = false;
            foreach (var dm in r.Demonstrativos)
            {
                var itens = new List<(ItemRemuneracaoEsocial, (char, string, string, string, string, string))>();
                foreach (var it in dm.Itens)
                {
                    if (dest(it.Rubrica) is not { } rb)
                    {
                        if (rubricaSemCadastro.Add(it.Rubrica))
                            erro("S-1200", rot, $"A rubrica {it.Rubrica} não está nos eventos S-1010 enviados nem no cadastro de rubricas. Inclua o arquivo do S-1010 (tabela de rubricas) na importação.");
                        falhou = true;
                        continue;
                    }
                    if (it.Valor != 0 || it.Quantidade is > 0) itens.Add((it, rb));
                }
                if (itens.Count == 0) continue;
                var nat = itens.Select(x => rubricasArquivo.TryGetValue(x.Item1.Rubrica, out var ro) ? ro.Natureza : x.Item2.Item2).ToList();
                var tipo = Classificar(dm.Id, r.IndApuracao, nat);
                if (!porTipo.TryGetValue(tipo, out var lista)) porTipo[tipo] = lista = [];
                lista.Add((dm, itens.Select(x => (x.Item1, x.Item2)).ToList()));
            }
            if (falhou) { continue; }
            foreach (var (tipo, dms) in porTipo)
            {
                var competencia = tipo == "DECIMO_2" && r.IndApuracao == 2 ? new DateOnly(r.Periodo.Year, 12, 1) : r.Periodo;
                if (sit.Calculos.Contains((r.Cpf, competencia, tipo)) || !calculosCriados.Add((r.Cpf, competencia, tipo))) { cE++; continue; }
                var itensFinais = new List<ItemCalculoImportado>();
                decimal prov = 0, desc = 0, bInss = 0, bIrrf = 0, bFgts = 0, inss = 0, irrf = 0, sf = 0, dedIrrf = 0;
                foreach (var (dm, itens) in dms)
                    foreach (var (it, rb) in itens)
                    {
                        var valor = Arred(it.Valor);
                        var referencia = it.Quantidade ?? it.Fator ?? 0m;
                        itensFinais.Add(new ItemCalculoImportado(codigoDestino(it.Rubrica), Trunc(rb.Descricao, 100), rb.Tipo, referencia, valor));
                        if (rb.Tipo == 'P')
                        {
                            prov += valor;
                            if (rb.IncInss.StartsWith('1')) bInss += valor;
                            if (rb.IncIrrf is "01" or "09" or "02" or "03" or "04" or "05" or "06" or "07" or "08") bIrrf += valor;
                            if (rb.IncFgts.StartsWith('1')) bFgts += valor;
                            if (rb.Natureza == "1409") sf += valor;
                        }
                        else if (rb.Tipo == 'D')
                        {
                            desc += valor;
                            if (rb.Natureza is "9201" or "9202") inss += valor;
                            else if (rb.Natureza is "9203" or "9204") irrf += valor;
                            if (rb.IncIrrf.StartsWith('1') || rb.IncIrrf.StartsWith('3')) dedIrrf += valor;
                        }
                    }
                bIrrf = Math.Max(0, bIrrf - dedIrrf);
                var categoria = dms[0].Dm.Categoria ?? "101";
                decimal fgts = 0;
                if (TabelasLegais.FgtsEm(competencia, sit.ParametrosLegais) is { } fg)
                    fgts = Arred(bFgts * (categoria == "103" ? fg.AliquotaAprendiz : fg.Aliquota) / 100m);
                else if (!semFgts)
                {
                    semFgts = true;
                    aviso("FGTS", $"Não há tabela legal do FGTS vigente em {competencia:MM/yyyy} (cadastre em Cadastros › Tabelas legais): o FGTS do histórico anterior fica zerado.");
                }
                // S-1210: data do pagamento
                DateOnly? pagamento = null;
                foreach (var (dm, _) in dms)
                    foreach (var p in pagamentos.Where(p => p.Cpf == r.Cpf && p.Info.Demonstrativo == dm.Id && (p.Info.PeriodoReferencia is null || p.Info.PeriodoReferencia == r.Periodo)))
                    {
                        pagamento = pagamento is null || p.Info.Data < pagamento ? p.Info.Data : pagamento;
                    }
                plano.Calculos.Add(new CalculoImportado(r.Cpf, competencia, tipo, pagamento, itensFinais, prov, desc, bInss, bIrrf, bFgts, inss, irrf, fgts, sf,
                    $"Importado do eSocial (S-1200, evento {r.IdEvento}{(r.Recibo is null ? "" : $", recibo {r.Recibo}")})"));
                cN++;
                if (op.FecharCompetencias && !sit.CompetenciasFechadas.Contains(competencia)) plano.CompetenciasAFechar.Add(competencia);
            }
        }
        if (semCadastro > 0)
            aviso("S-1200", $"Remunerações de {semCadastro} CPF(s) sem funcionário cadastrado (admissão não está nos arquivos nem no Vorticis) não foram importadas.");
        plano.Linhas.Add(new LinhaPrevia("S-1200", "Remunerações (holerites do histórico)", cN + cE + plano.Erros.GetValueOrDefault("S-1200"), cN, cE, 0,
                                         plano.Calculos.Take(3).Select(c => $"{c.Competencia:MM/yyyy} {c.Cpf}").ToList()));
        plano.Descricoes["S-1200"] = "Remunerações (histórico de holerites)";
        plano.Existentes["S-1200"] = cE;

        // pagamentos casados com um holerite importado
        foreach (var p in pagamentos)
        {
            if (op.De is { } de && p.Periodo < new DateOnly(de.Year, de.Month, 1)) continue;
            if (op.Ate is { } ate && p.Periodo > new DateOnly(ate.Year, ate.Month, 1)) continue;
            pN++;
        }
        var casados = plano.Calculos.Count(c => c.Pagamento is not null);
        plano.Linhas.Add(new LinhaPrevia("S-1210", "Pagamentos (datas de pagamento dos holerites)", pN, casados, 0, Math.Max(0, pN - casados), []));
        plano.Descricoes["S-1210"] = "Pagamentos";
        if (pN > casados) aviso("S-1210", $"{pN - casados} pagamento(s) não casam com um holerite importado (S-1200 ausente, já existente ou fora do período): ignorados.");
    }

    /// <summary>Tipo de folha de um demonstrativo: pelo código (FER, RESC, DECIMO), pelo 13º anual (indApuracao 2) ou pela natureza das rubricas.</summary>
    public static string Classificar(string ideDmDev, int indApuracao, IReadOnlyList<string> naturezas)
    {
        if (indApuracao == 2) return "DECIMO_2";
        var id = ideDmDev.ToUpperInvariant();
        if (id.StartsWith("RESC", StringComparison.Ordinal)) return "RESCISAO";
        if (id.StartsWith("FER", StringComparison.Ordinal)) return "FERIAS";
        if (naturezas.Any(n => n.StartsWith('6'))) return "RESCISAO";
        if (naturezas.Any(n => n.StartsWith("50", StringComparison.Ordinal))) return "DECIMO_1";
        if (naturezas.Any(n => n is "1020" or "1021" or "1022" or "1023") && !naturezas.Any(n => n == "1000")) return "FERIAS";
        return "MENSAL";
    }
}
