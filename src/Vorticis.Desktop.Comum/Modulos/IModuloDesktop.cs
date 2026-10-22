namespace Vorticis.Desktop.Comum.Modulos;

/// <summary>
/// Ponto de extensão do casco: cada executável da suíte (Contábil, Fiscal, Folha) descreve a si mesmo com uma
/// implementação desta interface. O casco cuida de banco, login, empresas, usuários, perfis, certificados, backup,
/// configurações e ajuda; o módulo fornece só o que é dele: nome, menus, atalhos e as telas.
/// <para>
/// Como um módulo se registra: implementa <see cref="IModuloDesktop"/>, devolve seus menus (cada item aponta para uma
/// chave de tela) e, em <see cref="RegistrarTelas"/>, declara as telas (instância do ViewModel + chave + título +
/// permissão + carga). O XAML do módulo associa cada ViewModel à sua View (DataTemplate) em App.axaml.
/// </para>
/// </summary>
public interface IModuloDesktop
{
    /// <summary>Nome completo do módulo ("Vorticis Contábil"): título da janela, tela Sobre e mensagens.</summary>
    string Nome { get; }

    /// <summary>Indicador deste módulo no cadastro da empresa (core.empresa): a seleção do topo lista só as empresas ativas nele.</summary>
    Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa { get; }

    /// <summary>Nome curto, mostrado na tela inicial ("Contábil").</summary>
    string NomeCurto { get; }

    /// <summary>Descrição de uma linha, mostrada no login e na tela Sobre ("Escrituração Fiscal Contábil").</summary>
    string Subtitulo { get; }

    /// <summary>Nome do executável sem extensão ("VorticisContabil"); também nomeia o ícone (Assets/VorticisContabil.ico).</summary>
    string NomeExecutavel { get; }

    /// <summary>
    /// Cor de destaque do módulo (#RRGGBB), para o usuário não confundir qual sistema está usando: faixa sob os menus,
    /// nome do módulo no login e na tela inicial, cabeçalho das janelas das funções. Contábil verde, Fiscal laranja,
    /// Folha roxo (os ícones de instalador/icone/gerar_icone.py usam as mesmas cores).
    /// </summary>
    string CorDestaque { get; }

    /// <summary>Cor da barra de menus (#RRGGBB): um tom escuro da família da cor de destaque.</summary>
    string CorBarra { get; }

    /// <summary>
    /// Itens do módulo no menu Cadastros, depois dos cadastros comuns (Empresas, Contadores, Clientes, Fornecedores).
    /// Vazio se o módulo não tem cadastros próprios.
    /// </summary>
    IReadOnlyList<ItemMenu> ItensCadastros { get; }

    /// <summary>Menus do módulo, entre Cadastros e Configurações (que, como Ajuda, pertencem ao casco).</summary>
    IReadOnlyList<ItemMenu> Menus { get; }

    /// <summary>Atalhos de teclado do módulo (listados na tela Sobre; os que têm chave abrem a tela).</summary>
    IReadOnlyList<AtalhoTela> Atalhos { get; }

    /// <summary>Declara as telas do módulo; chamado uma vez na criação da janela principal.</summary>
    void RegistrarTelas(CatalogoTelas catalogo);
}
