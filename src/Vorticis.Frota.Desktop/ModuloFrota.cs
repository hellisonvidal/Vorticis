using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Frota.Desktop.ViewModels;

namespace Vorticis.Frota.Desktop;

/// <summary>
/// Módulo Frota (piloto): a empresa ativa é a transportadora. Veículos, motoristas, abastecimentos, manutenção, viagens e vencimentos.
/// Permissões FROTA_* conferidas nos serviços. CT-e/MDF-e e rastreamento ficam para a fase 2.
/// </summary>
public sealed class ModuloFrota : IModuloDesktop
{
    public string Nome => "Vorticis Frota";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Frota;
    public string NomeCurto => "Frota";
    public string Subtitulo => "Controle de frota para transportadoras (piloto)";
    public string NomeExecutavel => "VorticisFrota";
    public string CorDestaque => "#475569";   // grafite-aço (ícone e selo do módulo)
    public string CorBarra => "#1E293B";      // asfalto
    public bool UsaCalendarioObrigacoes => false;
    public string RotuloEmpresas => "Transportadoras";
    public string RotuloEmpresaAtiva => "Transportadora";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Veículos", FrotaTelas.Veiculos),
        ItemMenu.Tela("Motoristas", FrotaTelas.Motoristas),
        ItemMenu.Tela("Planos de manutenção", FrotaTelas.Planos),
        ItemMenu.Tela("Documentos e vencimentos", FrotaTelas.Documentos),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Operação",
            ItemMenu.Tela("Abastecimentos", FrotaTelas.Abastecimentos),
            ItemMenu.Tela("Ordens de serviço", FrotaTelas.Ordens),
            ItemMenu.Tela("Viagens", FrotaTelas.Viagens)),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Painel da frota", FrotaTelas.Painel),
            ItemMenu.Tela("Custos por veículo", FrotaTelas.RelCustos),
            ItemMenu.Tela("Consumo de combustível", FrotaTelas.RelConsumo),
            ItemMenu.Tela("Resultado das viagens", FrotaTelas.RelViagens),
            ItemMenu.Tela("Vencimentos e manutenções", FrotaTelas.RelVencimentos)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+B", FrotaTelas.Abastecimentos, "Abastecimentos"),
        new("Ctrl+P", FrotaTelas.Painel, "Painel da frota"),
    ];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);
    private static readonly Permissao[] Todas = [Permissao.FrotaCadastros, Permissao.FrotaOperacao, Permissao.FrotaRelatorios];

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("PainelFrota", m => new PainelFrotaViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Painel, "Relatórios › Painel da frota", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Veiculos", m => new VeiculosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Veiculos, "Cadastros › Veículos", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Motoristas", m => new MotoristasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Motoristas, "Cadastros › Motoristas", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("PlanosManutencao", m => new PlanosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Planos, "Cadastros › Planos de manutenção", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("DocumentosFrota", m => new DocumentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Documentos, "Cadastros › Documentos e vencimentos", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Abastecimentos", m => new AbastecimentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Abastecimentos, "Operação › Abastecimentos", m => Pode(m, Permissao.FrotaOperacao, Permissao.FrotaRelatorios), v => v.CarregarAsync());
        c.Instancia("OrdensServico", m => new OrdensViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Ordens, "Operação › Ordens de serviço", m => Pode(m, Permissao.FrotaOperacao, Permissao.FrotaRelatorios), v => v.CarregarAsync());
        c.Instancia("Viagens", m => new ViagensViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Viagens, "Operação › Viagens", m => Pode(m, Permissao.FrotaOperacao, Permissao.FrotaRelatorios), v => v.CarregarAsync());
        Relatorio(c, "RelCustosFrota", FrotaTelas.RelCustos, Vorticis.Frota.TabelasFrota.TituloCustos);
        Relatorio(c, "RelConsumoFrota", FrotaTelas.RelConsumo, Vorticis.Frota.TabelasFrota.TituloConsumo);
        Relatorio(c, "RelViagensFrota", FrotaTelas.RelViagens, Vorticis.Frota.TabelasFrota.TituloViagens);
        Relatorio(c, "RelVencimentosFrota", FrotaTelas.RelVencimentos, Vorticis.Frota.TabelasFrota.TituloVencimentos);
        c.Instancia("ParametrosModulo", m => new ParametrosFrotaViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }

    private static void Relatorio(CatalogoTelas c, string instancia, string chave, string titulo) =>
        c.Instancia(instancia, m => new RelatorioFrotaViewModel(m, titulo), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(chave, $"Relatórios › {titulo}", m => Pode(m, Permissao.FrotaRelatorios), v => v.CarregarAsync());
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloFrota());
