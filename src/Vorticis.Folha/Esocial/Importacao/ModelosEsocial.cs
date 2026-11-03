using System.Xml.Linq;

namespace Vorticis.Folha.Esocial.Importacao;

/// <summary>Um arquivo de entrada (XML do evento, XML com retorno/recibo ou ZIP já expandido) com o nome de origem.</summary>
public sealed record ArquivoEsocial(string Nome, byte[] Conteudo);

/// <summary>Um evento encontrado nos arquivos, ainda sem interpretar o conteúdo.</summary>
public sealed record EventoLido(string Tipo, string Id, string? Recibo, int IndRetif, string? ReciboAnterior, string Arquivo, XElement Evento)
{
    /// <summary>Instante de geração do evento (está no próprio Id: "ID" + tpInsc + nrInsc + AAAAMMDDHHMMSS + sequencial).</summary>
    public DateTime Momento => Id.Length >= 31 && DateTime.TryParseExact(Id.AsSpan(17, 14), "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
                                                                         System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
    public string Rotulo => $"{Tipo} {Id}";
}

public sealed record EmpregadorEsocial(string Inscricao, string? Nome, string? ClasseTributaria);

public sealed record EstabelecimentoEsocial(string Inscricao, string? Cnae, decimal? Rat, decimal? Fap);

public sealed record RubricaEsocial(string Codigo, string Tabela, string Descricao, string Natureza, int Tipo, string IncCp, string IncIrrf, string IncFgts,
                                    DateOnly Inicio, bool Excluida)
{
    /// <summary>P provento, D desconto, I informativa (tpRubr 3 e 4).</summary>
    public char TipoFolha => Tipo switch { 1 => 'P', 2 => 'D', _ => 'I' };
}

public sealed record LotacaoEsocial(string Codigo, string? Tipo, string? Fpas, string? CodTerceiros, bool Excluida);

public sealed record DependenteEsocial(string Tipo, string Nome, DateOnly Nascimento, string? Cpf, bool DepIrrf, bool DepSalarioFamilia, bool Incapaz);

/// <summary>Dados do contrato (S-2200 e S-2206): cada alteração traz o contrato inteiro, só o que veio preenchido substitui.</summary>
public sealed record ContratoEsocial(string? Cargo, string? Cbo, string? Categoria, decimal? Salario, int? UnidadeSalario, int? TipoContrato, DateOnly? Termino,
                                     decimal? HorasSemana, int? TipoJornada, string? DescricaoJornada, string? CnpjSindicato)
{
    public static readonly ContratoEsocial Vazio = new(null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>O contrato depois de uma alteração: o que a alteração informou substitui o que havia.</summary>
    public ContratoEsocial Mesclar(ContratoEsocial n)
        => new(n.Cargo ?? Cargo, n.Cbo ?? Cbo, n.Categoria ?? Categoria, n.Salario ?? Salario, n.UnidadeSalario ?? UnidadeSalario, n.TipoContrato ?? TipoContrato,
               n.Termino ?? Termino, n.HorasSemana ?? HorasSemana, n.TipoJornada ?? TipoJornada, n.DescricaoJornada ?? DescricaoJornada, n.CnpjSindicato ?? CnpjSindicato);
}

public sealed record AlteracaoContratoEsocial(DateOnly Data, string? Descricao, ContratoEsocial Contrato, string IdEvento);

public sealed record AfastamentoEsocial(DateOnly Inicio, DateOnly? Fim, string Motivo, string? Observacao, DateOnly? AquisitivoInicio, DateOnly? AquisitivoFim);

public sealed record DesligamentoEsocial(DateOnly Data, string? Motivo);

public sealed record EnderecoEsocial(string? Tipo, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cep, string? CodMunicipio, string? Uf);

/// <summary>Trabalhador consolidado: admissão (S-2200 ou S-2300) + alterações cadastrais e contratuais, afastamentos e desligamento, em ordem cronológica.</summary>
public sealed class TrabalhadorEsocial
{
    public required string Cpf { get; set; }
    public string Matricula { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Sexo { get; set; }
    public int? RacaCor { get; set; }
    public int? EstadoCivil { get; set; }
    public string? GrauInstrucao { get; set; }
    public DateOnly? Nascimento { get; set; }
    public EnderecoEsocial? Endereco { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public List<DependenteEsocial> Dependentes { get; set; } = [];
    public DateOnly Admissao { get; set; }
    public bool SemVinculo { get; set; }
    /// <summary>Contrato como veio na admissão (S-2200/S-2300).</summary>
    public ContratoEsocial ContratoInicial { get; set; } = ContratoEsocial.Vazio;
    /// <summary>Contrato atual: o inicial com todas as alterações (S-2206) aplicadas em ordem de data.</summary>
    public ContratoEsocial Contrato { get; set; } = ContratoEsocial.Vazio;
    public List<AlteracaoContratoEsocial> Alteracoes { get; } = [];
    public List<AfastamentoEsocial> Afastamentos { get; } = [];
    public DesligamentoEsocial? Desligamento { get; set; }
    public string IdEvento { get; set; } = "";
    public string Arquivo { get; set; } = "";
}

public sealed record ItemRemuneracaoEsocial(string Rubrica, string Tabela, decimal? Quantidade, decimal? Fator, decimal Valor, string? Lotacao);

public sealed record DemonstrativoEsocial(string Id, string? Categoria, IReadOnlyList<ItemRemuneracaoEsocial> Itens);

/// <summary>S-1200: remuneração de um trabalhador em um período (mês, ou ano para o 13º salário).</summary>
public sealed record RemuneracaoEsocial(string Cpf, DateOnly Periodo, int IndApuracao, IReadOnlyList<DemonstrativoEsocial> Demonstrativos, string? Recibo, string IdEvento);

public sealed record InfoPagamentoEsocial(DateOnly Data, int Tipo, DateOnly? PeriodoReferencia, string? Demonstrativo, decimal Liquido);

/// <summary>S-1210: pagamentos de um trabalhador feitos em um mês.</summary>
public sealed record PagamentoEsocial(string Cpf, DateOnly Periodo, IReadOnlyList<InfoPagamentoEsocial> Pagamentos, string IdEvento);

/// <summary>Tudo o que os arquivos contêm depois de aplicar retificações e exclusões, pronto para planejar a importação.</summary>
public sealed class DadosEsocial
{
    public EmpregadorEsocial? Empregador { get; set; }
    public List<EstabelecimentoEsocial> Estabelecimentos { get; } = [];
    public Dictionary<string, RubricaEsocial> Rubricas { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, LotacaoEsocial> Lotacoes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, TrabalhadorEsocial> Trabalhadores { get; } = new(StringComparer.Ordinal);
    public List<RemuneracaoEsocial> Remuneracoes { get; } = [];
    public List<PagamentoEsocial> Pagamentos { get; } = [];
    /// <summary>Quantidade de eventos usados, por tipo (S-1200 etc.).</summary>
    public Dictionary<string, int> Eventos { get; } = new(StringComparer.Ordinal);
    public int VersoesAnterioresIgnoradas { get; set; }
    public int Excluidos { get; set; }
    /// <summary>Raiz do CNPJ (8 dígitos) de cada empregador citado nos eventos.</summary>
    public HashSet<string> Empregadores { get; } = new(StringComparer.Ordinal);
    public List<Vorticis.Domain.Sped.Importacao.OcorrenciaImportacao> Ocorrencias { get; } = [];
    public DateOnly? PrimeiroPeriodo { get; set; }
    public DateOnly? UltimoPeriodo { get; set; }
    public string Sha256 { get; set; } = "";
    public int Arquivos { get; set; }
}
