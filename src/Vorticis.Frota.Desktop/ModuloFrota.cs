using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Frota.Desktop.ViewModels;

namespace Vorticis.Frota.Desktop;

/// <summary>
/// Módulo Frota: a empresa ativa é a transportadora. Veículos, motoristas, abastecimentos, viagens, vencimentos e manutenção (ordens de serviço
/// com autorização e notas de compra).
/// Permissões FROTA_* conferidas nos serviços. CT-e/MDF-e e rastreamento ficam para a fase 2.
/// </summary>
public sealed class ModuloFrota : IModuloDesktop
{
    public string Nome => "Vorticis Frota";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Frota;
    public string NomeCurto => "Frota";
    public string Subtitulo => "Coordenação de frota e manutenção para transportadoras";
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
        ItemMenu.Tela("Documentos e vencimentos", FrotaTelas.Documentos),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Operação",
            ItemMenu.Tela("Abastecimentos", FrotaTelas.Abastecimentos),
            ItemMenu.Tela("Viagens", FrotaTelas.Viagens)),
        ItemMenu.Submenu("_Manutenção",
            ItemMenu.Tela("Planos de manutenção", FrotaTelas.Planos),
            ItemMenu.Tela("Peças e serviços", FrotaTelas.Itens),
            ItemMenu.Tela("Ordens de serviço", FrotaTelas.Ordens),
            ItemMenu.Tela("Autorização de serviços", FrotaTelas.Autorizacao),
            ItemMenu.Tela("Notas de compra", FrotaTelas.Notas)),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Painel da frota", FrotaTelas.Painel),
            ItemMenu.Tela("Custos por veículo", FrotaTelas.RelCustos),
            ItemMenu.Tela("Consumo de combustível", FrotaTelas.RelConsumo),
            ItemMenu.Tela("Resultado das viagens", FrotaTelas.RelViagens),
            ItemMenu.Tela("Vencimentos e manutenções", FrotaTelas.RelVencimentos),
            ItemMenu.Separador,
            ItemMenu.Tela("Acompanhamento das ordens de serviço", FrotaTelas.RelAcompanhamento),
            ItemMenu.Tela("Aprovações de serviços", FrotaTelas.RelAprovacoes),
            ItemMenu.Tela("Notas fiscais de compra", FrotaTelas.RelNotas)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+B", FrotaTelas.Abastecimentos, "Abastecimentos"),
        new("Ctrl+P", FrotaTelas.Painel, "Painel da frota"),
        new("Ctrl+O", FrotaTelas.Ordens, "Ordens de serviço"),
    ];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);
    private static readonly Permissao[] Todas = [Permissao.FrotaCadastros, Permissao.FrotaOperacao, Permissao.FrotaRelatorios, Permissao.FrotaManutencao, Permissao.FrotaAprovar];
    private static readonly Permissao[] Manutencao = [Permissao.FrotaManutencao, Permissao.FrotaAprovar, Permissao.FrotaRelatorios];

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
            .Tela(FrotaTelas.Planos, "Manutenção › Planos de manutenção", m => Pode(m, Manutencao), v => v.CarregarAsync());
        c.Instancia("DocumentosFrota", m => new DocumentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Documentos, "Cadastros › Documentos e vencimentos", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Abastecimentos", m => new AbastecimentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Abastecimentos, "Operação › Abastecimentos", m => Pode(m, Permissao.FrotaOperacao, Permissao.FrotaRelatorios), v => v.CarregarAsync());
        c.Instancia("ItensManutencao", m => new ItensManutencaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Itens, "Manutenção › Peças e serviços", m => Pode(m, Manutencao), v => v.CarregarAsync());
        c.Instancia("OrdensServico", m => new OrdensViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Ordens, "Manutenção › Ordens de serviço", m => Pode(m, Manutencao), v => v.CarregarAsync());
        c.Instancia("AutorizacaoServicos", m => new AutorizacaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Autorizacao, "Manutenção › Autorização de serviços", m => Pode(m, Manutencao), v => v.CarregarAsync());
        c.Instancia("NotasCompra", m => new NotasCompraViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Notas, "Manutenção › Notas de compra", m => Pode(m, Manutencao), v => v.CarregarAsync());
        c.Instancia("Viagens", m => new ViagensViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FrotaTelas.Viagens, "Operação › Viagens", m => Pode(m, Permissao.FrotaOperacao, Permissao.FrotaRelatorios), v => v.CarregarAsync());
        Relatorio(c, "RelCustosFrota", FrotaTelas.RelCustos, Vorticis.Frota.TabelasFrota.TituloCustos);
        Relatorio(c, "RelConsumoFrota", FrotaTelas.RelConsumo, Vorticis.Frota.TabelasFrota.TituloConsumo);
        Relatorio(c, "RelViagensFrota", FrotaTelas.RelViagens, Vorticis.Frota.TabelasFrota.TituloViagens);
        Relatorio(c, "RelVencimentosFrota", FrotaTelas.RelVencimentos, Vorticis.Frota.TabelasFrota.TituloVencimentos);
        Relatorio(c, "RelOrdensFrota", FrotaTelas.RelAcompanhamento, Vorticis.Frota.TabelasFrota.TituloAcompanhamento);
        Relatorio(c, "RelAprovacoesFrota", FrotaTelas.RelAprovacoes, Vorticis.Frota.TabelasFrota.TituloAprovacoes);
        Relatorio(c, "RelNotasFrota", FrotaTelas.RelNotas, Vorticis.Frota.TabelasFrota.TituloNotas);
        c.Instancia("ParametrosModulo", m => new ParametrosFrotaViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }

    private static void Relatorio(CatalogoTelas c, string instancia, string chave, string titulo) =>
        c.Instancia(instancia, m => new RelatorioFrotaViewModel(m, titulo), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(chave, $"Relatórios › {titulo}", m => Pode(m, Permissao.FrotaRelatorios), v => v.CarregarAsync());
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloFrota());
