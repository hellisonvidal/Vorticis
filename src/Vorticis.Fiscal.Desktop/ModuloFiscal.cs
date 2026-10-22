using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop;

/// <summary>
/// Módulo Fiscal: importação de XML (NF-e, NFC-e, NFS-e nacional), consulta, contabilização automática, livros de entradas e saídas,
/// apuração do Simples Nacional, Lucro Presumido e Lucro Real, PIS/COFINS, retenções e guias de recolhimento.
/// </summary>
public sealed class ModuloFiscal : IModuloDesktop
{
    public string Nome => "Vorticis Fiscal";
    public string NomeCurto => "Fiscal";
    public string Subtitulo => "Escrituração e obrigações fiscais";
    public string NomeExecutavel => "VorticisFiscal";
    public string CorDestaque => "#C2410C";   // laranja
    public string CorBarra => "#7C2D12";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } = [];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Documentos fiscais",
            ItemMenu.Tela("Importação de XML (NF-e, NFC-e, NFS-e)", Telas.ImportacaoXml),
            ItemMenu.Tela("Consulta de documentos", Telas.Documentos),
            ItemMenu.Separador,
            ItemMenu.Tela("Regras de contabilização", Telas.RegrasContabilizacao),
            ItemMenu.Tela("Contabilizar notas fiscais", Telas.ContabilizarNotas),
            ItemMenu.Separador,
            ItemMenu.EmBreve("Conhecimentos de transporte (CT-e)")),
        ItemMenu.Submenu("_Apuração",
            ItemMenu.Tela("Simples Nacional (DAS)", Telas.Simples),
            ItemMenu.Tela("Lucro Presumido (IRPJ e CSLL)", Telas.Presumido),
            ItemMenu.Tela("Lucro Real (IRPJ, CSLL e LALUR)", Telas.Real),
            ItemMenu.Tela("PIS e COFINS", Telas.PisCofins),
            ItemMenu.EmBreve("ICMS e IPI"),
            ItemMenu.Separador,
            ItemMenu.Tela("Histórico de receita e folha", Telas.BaseMensal),
            ItemMenu.Tela("Contas dos tributos (provisões)", Telas.ContasTributos)),
        ItemMenu.Submenu("_Obrigações",
            ItemMenu.Tela("Retenções (IRRF, CSRF, INSS, ISS)", Telas.Retencoes),
            ItemMenu.Tela("Guias de recolhimento", Telas.Guias),
            ItemMenu.Separador,
            ItemMenu.EmBreve("SPED Fiscal — EFD ICMS/IPI"),
            ItemMenu.EmBreve("SPED EFD-Contribuições")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Livro de Registro de Entradas", Telas.LivroEntradas),
            ItemMenu.Tela("Livro de Registro de Saídas", Telas.LivroSaidas),
            ItemMenu.Tela("Resumo por CFOP", Telas.ResumoCfop)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+I", Telas.ImportacaoXml, "Importação de XML"),
        new("Ctrl+D", Telas.Documentos, "Consulta de documentos"),
    ];

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("ImportacaoXml", m => new ImportacaoXmlViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ImportacaoXml, "Documentos fiscais › Importação de XML", m => m.Pode(Permissao.FiscalDocumentosImportar));
        c.Instancia("Documentos", m => new DocumentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Documentos, "Documentos fiscais › Consulta de documentos", m => m.Pode(Permissao.FiscalDocumentosConsultar));
        c.Instancia("RegrasContabilizacao", m => new RegrasContabilizacaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.RegrasContabilizacao, "Documentos fiscais › Regras de contabilização", m => m.Pode(Permissao.FiscalContabilizar), v => v.CarregarAsync());
        c.Instancia("ContabilizarNotas", m => new ContabilizarNotasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ContabilizarNotas, "Documentos fiscais › Contabilizar notas fiscais", m => m.Pode(Permissao.FiscalContabilizar));

        c.Instancia("Simples", m => new ApuracaoSimplesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Simples, "Apuração › Simples Nacional", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("Presumido", m => new ApuracaoPresumidoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Presumido, "Apuração › Lucro Presumido", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("Real", m => new ApuracaoRealViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Real, "Apuração › Lucro Real", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("PisCofins", m => new ApuracaoPisCofinsViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.PisCofins, "Apuração › PIS e COFINS", m => m.Pode(Permissao.FiscalApurar));
        c.Instancia("BaseMensal", m => new BaseMensalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.BaseMensal, "Apuração › Histórico de receita e folha", m => m.Pode(Permissao.FiscalApurar) || m.Pode(Permissao.FiscalRetencoesManter), v => v.CarregarAsync());
        c.Instancia("ContasTributos", m => new ContasTributosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.ContasTributos, "Apuração › Contas dos tributos", m => m.Pode(Permissao.FiscalApurar), v => v.CarregarAsync());

        c.Instancia("Retencoes", m => new RetencoesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Retencoes, "Obrigações › Retenções", m => m.Pode(Permissao.FiscalRelatorios) || m.Pode(Permissao.FiscalRetencoesManter), v => v.ConsultarAsync());
        c.Instancia("Guias", m => new GuiasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Guias, "Obrigações › Guias de recolhimento", m => m.Pode(Permissao.FiscalRelatorios) || m.Pode(Permissao.FiscalRetencoesManter), v => v.ConsultarAsync());

        c.Instancia("LivroEntradas", m => new LivroEntradasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroEntradas, "Relatórios › Livro de Registro de Entradas", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("LivroSaidas", m => new LivroSaidasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.LivroSaidas, "Relatórios › Livro de Registro de Saídas", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ResumoCfop", m => new ResumoCfopViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ResumoCfop, "Relatórios › Resumo por CFOP", m => m.Pode(Permissao.FiscalRelatorios));
    }
}
