using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop;

/// <summary>
/// Módulo Fiscal: digitação de notas de entrada e saída, produtos, importação de XML (NF-e, NFC-e, CT-e, NFS-e nacional), consulta, contabilização automática, livros de entradas e saídas,
/// duplicatas (contas a pagar e a receber), apuração do Simples Nacional, Lucro Presumido e Lucro Real, PIS/COFINS, retenções e guias de recolhimento.
/// </summary>
public sealed class ModuloFiscal : IModuloDesktop
{
    public void PrepararServicos(Vorticis.Infra.VorticisApp app) => _ = app.Fiscal();

    public string Nome => "Vorticis Fiscal";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Fiscal;
    public string NomeCurto => "Fiscal";
    public string Subtitulo => "Escrituração e obrigações fiscais";
    public string NomeExecutavel => "VorticisFiscal";
    public string CorDestaque => "#D97706";   // âmbar (ícone e selo do módulo)
    public string CorBarra => "#78350F";      // marrom-âmbar

    /// <summary>Recurso ligado com a contabilização em lote (padrão): mostra Documentos fiscais › Contabilizar notas fiscais.</summary>
    public const string RecursoContabilizacaoLote = "contabilizacao_lote";

    public async Task<IReadOnlySet<string>> RecursosAtivosAsync(Vorticis.Desktop.Comum.ViewModels.MainViewModelBase main)
    {
        if (main.App is not { } app || main.EmpresaId is not { } empresa || main.Sessao is not { } sessao) return new HashSet<string>();
        var online = await app.Fiscal().Contabilizacao.ContabilizacaoOnlineAsync(empresa, sessao.Id);
        return online is { Sucesso: true, Valor: true } ? new HashSet<string>() : new HashSet<string> { RecursoContabilizacaoLote };
    }

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Produtos", Telas.Produtos),
        ItemMenu.Tela("Regras fiscais", Telas.RegrasFiscais),
        ItemMenu.Tela("Regras de contabilização", Telas.RegrasContabilizacao),
        ItemMenu.Tela("Centros de custo", Vorticis.Desktop.Comum.ViewModels.TelasComuns.CentrosCusto),
        ItemMenu.Tela("Formas de pagamento", Vorticis.Desktop.Comum.ViewModels.TelasComuns.FormasPagamento),
        ItemMenu.Tela("Contas bancárias", Vorticis.Desktop.Comum.ViewModels.TelasComuns.ContasBancarias),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Lançamentos",
            ItemMenu.Submenu("_Documentos fiscais",
                ItemMenu.Tela("Entrada", Telas.NotaEntrada),
                ItemMenu.Tela("Saída", Telas.NotaSaida)),
            ItemMenu.Tela("Duplicatas (contas a pagar e a receber)", Telas.Duplicatas),
            ItemMenu.Tela("Conciliação de duplicatas", Telas.ConciliacaoDuplicatas),
            ItemMenu.Separador,
            ItemMenu.Submenu("_Ajustes",
                ItemMenu.Tela("Ajustes de entrada", Telas.AjustesEntrada),
                ItemMenu.Tela("Ajustes de saída", Telas.AjustesSaida),
                ItemMenu.Tela("Códigos de ajuste", Telas.CodigosAjuste)),
            ItemMenu.Tela("Contas de energia, gás, água e comunicação", Telas.ContasConsumo),
            ItemMenu.Tela("Inventário de estoque (bloco H)", Telas.Inventario)),
        ItemMenu.Submenu("_Documentos fiscais",
            ItemMenu.Tela("Consulta de documentos", Telas.Documentos),
            ItemMenu.Tela("Contabilizar notas fiscais", Telas.ContabilizarNotas).SoCom(RecursoContabilizacaoLote),
            ItemMenu.Separador,
            ItemMenu.Tela("Importar NFS-e", Telas.ImportacaoNfse),
            ItemMenu.Tela("Importar CT-e", Telas.ImportacaoCte),
            ItemMenu.Tela("Consulta de CT-e", Telas.ConsultaCte)),
        ItemMenu.Submenu("_Integrações",
            ItemMenu.Tela("Importação de XML em lote (NF-e, NFC-e, NFS-e, CT-e)", Telas.ImportacaoXml)),
        ItemMenu.Submenu("_Apuração",
            ItemMenu.Tela("Simples Nacional (DAS)", Telas.Simples),
            ItemMenu.Tela("Lucro Presumido (IRPJ e CSLL)", Telas.Presumido),
            ItemMenu.Tela("Lucro Real (IRPJ, CSLL e LALUR)", Telas.Real),
            ItemMenu.Tela("PIS e COFINS", Telas.PisCofins),
            ItemMenu.EmBreve("ICMS e IPI"),
            ItemMenu.Separador,
            ItemMenu.Tela("Histórico de receita e folha", Telas.BaseMensal),
            ItemMenu.Tela("Planejamento tributário", Telas.Planejamento),
            ItemMenu.Separador,
            ItemMenu.Tela("Fechamento do período fiscal", Telas.FechamentoFiscal),
            ItemMenu.Separador,
            ItemMenu.Submenu("_Obrigações",
                ItemMenu.Tela("Retenções (IRRF, CSRF, INSS, ISS)", Telas.Retencoes),
                ItemMenu.Tela("Guias de recolhimento", Telas.Guias),
                ItemMenu.Tela("DCTFWeb (MIT)", Telas.DctfWeb),
                ItemMenu.Separador,
                ItemMenu.Submenu("SPED",
                    ItemMenu.Tela("EFD ICMS/IPI (SPED Fiscal)", Telas.EfdIcms),
                    ItemMenu.Tela("EFD-Contribuições (PIS/COFINS)", Telas.EfdContribuicoes)))),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Submenu("_Livros e apuração",
                ItemMenu.Tela("Livro de Registro de Entradas", Telas.LivroEntradas),
                ItemMenu.Tela("Livro de Registro de Saídas", Telas.LivroSaidas),
                ItemMenu.Tela("Livro de Apuração do ICMS", Telas.LivroApuracaoIcms),
                ItemMenu.Tela("Livro de Apuração do IPI", Telas.LivroApuracaoIpi),
                ItemMenu.Separador,
                ItemMenu.Tela("Resumo da apuração do ICMS", Telas.ResumoApuracaoIcms),
                ItemMenu.Tela("Relação de ajustes da apuração", Telas.RelacaoAjustes),
                ItemMenu.Tela("Apuração do IPI", Telas.ApuracaoIpiSped),
                ItemMenu.Tela("Apuração do ICMS-ST", Telas.ApuracaoIcmsSt),
                ItemMenu.Tela("Conferência da EFD ICMS/IPI", Telas.ConferenciaEfd)),
            ItemMenu.Submenu("_Resumos",
                ItemMenu.Tela("Resumo de vendas", Telas.ResumoVendas),
                ItemMenu.Tela("Resumo de compras", Telas.ResumoCompras),
                ItemMenu.Tela("Resumo por CFOP", Telas.ResumoCfop),
                ItemMenu.Tela("Notas por centro de custo", Telas.NotasPorCentro)),
            ItemMenu.Separador,
            ItemMenu.Submenu("_Duplicatas",
                ItemMenu.Tela("Duplicatas a pagar", Telas.RelDuplicatasPagar),
                ItemMenu.Tela("Duplicatas a receber", Telas.RelDuplicatasReceber),
                ItemMenu.Tela("Posição por vencimento", Telas.RelPosicaoDuplicatas),
                ItemMenu.Separador,
                ItemMenu.Tela("Relatório de recebimentos", Telas.RelRecebimentos),
                ItemMenu.Tela("Relatório de pagamentos", Telas.RelPagamentos)),
            ItemMenu.Separador,
            ItemMenu.Tela("Planejamento tributário", Telas.Planejamento),
            ItemMenu.Separador,
            ItemMenu.Tela("Produtos cadastrados", Telas.RelProdutos),
            ItemMenu.Tela("Itens das notas fiscais", Telas.RelItensNotas)),
    ];

    /// <summary>Ajuda › Dicas: as dicas deste módulo (as gerais vêm do casco).</summary>
    public IReadOnlyList<Dica> Dicas { get; } =
    [
        new("Fiscal", "Importe os XML em lote", "Integrações › Importação de XML em lote lê uma pasta inteira de NF-e, NFC-e, NFS-e e CT-e; o que já foi importado é ignorado.", Telas.ImportacaoXml),
        new("Fiscal", "Regra por prefixo de CFOP", "Nas Regras de contabilização, uma regra com o prefixo 51 vale para todos os CFOP 51xx; crie regras específicas só para as exceções.", Telas.RegrasContabilizacao),
        new("Fiscal", "Feche o período depois de apurar", "Apuração › Fechamento do período fiscal trava as notas do mês; a DCTFWeb do mês só é preparada com o período fechado.", Telas.FechamentoFiscal),
        new("Fiscal", "DCTFWeb com o recibo guardado", "Depois de transmitir a DCTFWeb no e-CAC, registre o recibo em Apuração › Obrigações › DCTFWeb: o mês transmitido não reabre por engano.", Telas.DctfWeb),
        new("Fiscal", "Planeje o regime antes de janeiro", "A opção pelo Simples Nacional vai até o último dia útil de janeiro e o regime do Lucro Presumido é definido no primeiro pagamento do ano: simule antes em Apuração › Planejamento tributário.", Telas.Planejamento),
        new("Fiscal", "Fator R decide o anexo", "Nos serviços sujeitos ao Fator R, folha dos 12 meses de 28% ou mais da receita leva ao Anexo III (mais barato); abaixo disso, Anexo V.", Telas.Simples),
        new("Fiscal", "Histórico de receita e folha", "Para empresas que começaram a usar o sistema no meio do ano, informe a receita e a folha dos meses anteriores: o RBT12 e o Fator R dependem disso.", Telas.BaseMensal),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+I", Telas.ImportacaoXml, "Importação de XML"),
        new("Ctrl+D", Telas.Documentos, "Consulta de documentos"),
    ];

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("ImportacaoXml", m => new ImportacaoXmlViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ImportacaoXml, "Integrações › Importação de XML em lote", m => m.Pode(Permissao.FiscalDocumentosImportar));
        c.Instancia("ImportacaoNfse", m => new ImportacaoNfseViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ImportacaoNfse, "Documentos fiscais › Importar NFS-e", m => m.Pode(Permissao.FiscalDocumentosImportar));
        c.Instancia("ImportacaoCte", m => new ImportacaoCteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ImportacaoCte, "Documentos fiscais › Importar CT-e", m => m.Pode(Permissao.FiscalDocumentosImportar));
        c.Instancia("ConsultaCte", m => new ConsultaCteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ConsultaCte, "Documentos fiscais › Consulta de CT-e", m => m.Pode(Permissao.FiscalDocumentosConsultar));
        c.Instancia("Documentos", m => new DocumentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Documentos, "Documentos fiscais › Consulta de documentos", m => m.Pode(Permissao.FiscalDocumentosConsultar));
        c.Instancia("RegrasContabilizacao", m => new RegrasContabilizacaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.RegrasContabilizacao, "Cadastros › Regras de contabilização",
                  m => m.Pode(Permissao.FiscalContabilizar) || m.Pode(Permissao.FiscalApurar) || m.Pode(Permissao.FiscalDuplicatasManter), v => v.CarregarAsync());
        c.Instancia("ContabilizarNotas", m => new ContabilizarNotasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ContabilizarNotas, "Documentos fiscais › Contabilizar notas fiscais", m => m.Pode(Permissao.FiscalContabilizar));

        c.Instancia("NotaEntrada", m => new NotaEntradaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.NotaEntrada, "Lançamentos › Documentos fiscais › Entrada", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("NotaSaida", m => new NotaSaidaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.NotaSaida, "Lançamentos › Documentos fiscais › Saída", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("Duplicatas", m => new DuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.Duplicatas, "Lançamentos › Duplicatas", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("ConciliacaoDuplicatas", m => new ConciliacaoDuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ConciliacaoDuplicatas, "Lançamentos › Conciliação de duplicatas", m => m.Pode(Permissao.FiscalDuplicatasBaixar));
        c.Instancia("Produtos", m => new ProdutosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.Produtos, "Cadastros › Produtos", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("RegrasFiscais", m => new RegrasFiscaisViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.RegrasFiscais, "Cadastros › Regras fiscais", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("ParametrosModulo", m => new ParametrosFiscalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Vorticis.Desktop.Comum.ViewModels.TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());

        c.Instancia("Simples", m => new ApuracaoSimplesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Simples, "Apuração › Simples Nacional", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("Presumido", m => new ApuracaoPresumidoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Presumido, "Apuração › Lucro Presumido", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("Real", m => new ApuracaoRealViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Real, "Apuração › Lucro Real", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("PisCofins", m => new ApuracaoPisCofinsViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.PisCofins, "Apuração › PIS e COFINS", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("NotasPorCentro", m => new NotasPorCentroViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.NotasPorCentro, "Relatórios › Resumos › Notas por centro de custo", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("DctfWeb", m => new DctfWebViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.DctfWeb, "Apuração › Obrigações › DCTFWeb (MIT)", m => m.Pode(Permissao.FiscalApurar) || m.Pode(Permissao.FiscalRelatorios), v => v.CarregarAsync());
        c.Instancia("Planejamento", m => new PlanejamentoTributarioViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.Planejamento, "Apuração › Planejamento tributário", m => m.Pode(Permissao.FiscalApurar) || m.Pode(Permissao.FiscalRelatorios), v => v.CarregarAsync());
        c.Instancia("BaseMensal", m => new BaseMensalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.BaseMensal, "Apuração › Histórico de receita e folha", m => m.Pode(Permissao.FiscalApurar) || m.Pode(Permissao.FiscalRetencoesManter), v => v.CarregarAsync());
        c.Instancia("AjustesEntrada", m => new AjustesEntradaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.AjustesEntrada, "Lançamentos › Ajustes › Ajustes de entrada", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("AjustesSaida", m => new AjustesSaidaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.AjustesSaida, "Lançamentos › Ajustes › Ajustes de saída", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("CodigosAjuste", m => new CodigosAjusteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.CodigosAjuste, "Lançamentos › Ajustes › Códigos de ajuste", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("ContasConsumo", m => new ContasConsumoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.ContasConsumo, "Lançamentos › Contas de consumo", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("Inventario", m => new InventarioViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.Inventario, "Lançamentos › Inventário de estoque", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("FechamentoFiscal", m => new FechamentoFiscalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.FechamentoFiscal, "Apuração › Fechamento do período fiscal", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());

        c.Instancia("Retencoes", m => new RetencoesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Retencoes, "Apuração › Obrigações › Retenções", m => m.Pode(Permissao.FiscalRelatorios) || m.Pode(Permissao.FiscalRetencoesManter), v => v.ConsultarAsync());
        c.Instancia("Guias", m => new GuiasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Guias, "Apuração › Obrigações › Guias de recolhimento", m => m.Pode(Permissao.FiscalRelatorios) || m.Pode(Permissao.FiscalRetencoesManter), v => v.ConsultarAsync());

        c.Instancia("EfdIcms", m => new EfdIcmsAssistenteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(), aoLimpar: v => v.Limpar(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.EfdIcms, "Apuração › Obrigações › SPED › EFD ICMS/IPI", m => m.Pode(Permissao.FiscalSpedGerar), v => v.CarregarAsync());
        c.Instancia("EfdContribuicoes", m => new EfdContribuicoesAssistenteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(), aoLimpar: v => v.Limpar(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.EfdContribuicoes, "Apuração › Obrigações › SPED › EFD-Contribuições", m => m.Pode(Permissao.FiscalSpedGerar), v => v.CarregarAsync());

        c.Instancia("LivroEntradas", m => new LivroEntradasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroEntradas, "Relatórios › Livros e apuração › Livro de Registro de Entradas", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("LivroApuracaoIcms", m => new LivroApuracaoIcmsViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroApuracaoIcms, "Relatórios › Livros e apuração › Livro de Apuração do ICMS", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("LivroApuracaoIpi", m => new LivroApuracaoIpiViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroApuracaoIpi, "Relatórios › Livros e apuração › Livro de Apuração do IPI", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ResumoApuracaoIcms", m => new ResumoApuracaoIcmsViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ResumoApuracaoIcms, "Relatórios › Livros e apuração › Resumo da apuração do ICMS", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelacaoAjustes", m => new RelacaoAjustesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelacaoAjustes, "Relatórios › Livros e apuração › Relação de ajustes da apuração", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ApuracaoIpiSped", m => new ApuracaoIpiSpedViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ApuracaoIpiSped, "Relatórios › Livros e apuração › Apuração do IPI", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ApuracaoIcmsSt", m => new ApuracaoIcmsStViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ApuracaoIcmsSt, "Relatórios › Livros e apuração › Apuração do ICMS-ST", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ConferenciaEfd", m => new ConferenciaEfdViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ConferenciaEfd, "Relatórios › Livros e apuração › Conferência da EFD ICMS/IPI", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("LivroSaidas", m => new LivroSaidasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroSaidas, "Relatórios › Livros e apuração › Livro de Registro de Saídas", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelProdutos", m => new RelatorioProdutosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelProdutos, "Relatórios › Produtos cadastrados", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelItensNotas", m => new RelatorioItensNotasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelItensNotas, "Relatórios › Itens das notas fiscais", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ResumoCfop", m => new ResumoCfopViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ResumoCfop, "Relatórios › Resumos › Resumo por CFOP", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ResumoVendas", m => new ResumoVendasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ResumoVendas, "Relatórios › Resumos › Resumo de vendas", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ResumoCompras", m => new ResumoComprasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ResumoCompras, "Relatórios › Resumos › Resumo de compras", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelRecebimentos", m => new RelRecebimentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelRecebimentos, "Relatórios › Duplicatas › Relatório de recebimentos", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelPagamentos", m => new RelPagamentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelPagamentos, "Relatórios › Duplicatas › Relatório de pagamentos", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelDuplicatasPagar", m => new RelDuplicatasPagarViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelDuplicatasPagar, "Relatórios › Duplicatas › Duplicatas a pagar", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelDuplicatasReceber", m => new RelDuplicatasReceberViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelDuplicatasReceber, "Relatórios › Duplicatas › Duplicatas a receber", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelPosicaoDuplicatas", m => new RelPosicaoDuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.RelPosicaoDuplicatas, "Relatórios › Duplicatas › Posição por vencimento", m => m.Pode(Permissao.FiscalRelatorios));
    }
}
