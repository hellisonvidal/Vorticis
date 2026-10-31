namespace Vorticis.Desktop.Comum.Modulos;

/// <summary>Dica da tela Ajuda › Dicas: área (Geral, Contábil, Fiscal...), título, texto e, se houver, a tela que a dica ensina a usar.</summary>
public sealed record Dica(string Area, string Titulo, string Texto, string? Tela = null);

/// <summary>Dicas que valem em todos os módulos (o casco); cada módulo acrescenta as suas em <see cref="IModuloDesktop.Dicas"/>.</summary>
public static class DicasGerais
{
    public const string Area = "Geral";

    public static IReadOnlyList<Dica> Todas { get; } =
    [
        new(Area, "F1 abre o manual da tela", "Em qualquer função, a tecla F1 abre o manual do usuário já no tópico da tela em que você está.", ViewModels.TelasComuns.Manual),
        new(Area, "Filtros em todas as listas", "Toda lista tem busca rápida no topo e filtro por coluna: digite parte do nome, do código ou do valor para achar o registro sem rolar a tela."),
        new(Area, "Copie só a célula", "Ctrl+C numa grade copia apenas a célula selecionada, pronta para colar no Excel ou num e-mail."),
        new(Area, "Esconda o que você não usa", "Em Configurações › Menus do programa você desmarca os itens de menu que não usa; o administrador pode esconder itens para todos os usuários.", ViewModels.TelasComuns.Menus),
        new(Area, "Tema escuro e confirmação ao fechar", "Em Configurações › Configurações gerais › Preferências você escolhe o tema de cores, se o programa pergunta antes de fechar e quantos dias de vencimentos a tela inicial mostra.", ViewModels.TelasComuns.Preferencias),
        new(Area, "Todo relatório sai em PDF, Excel e CSV", "O relatório aparece na tela e, pelos botões de exportação, vira PDF (com assinatura do responsável e do contador, e até assinatura digital), Excel ou CSV com os mesmos números."),
        new(Area, "Backup fora do computador", "Faça backup em Configurações › Backup e restauração e copie o arquivo para outro disco ou para a nuvem: um backup no mesmo computador não protege contra defeito da máquina.", ViewModels.TelasComuns.Backup),
        new(Area, "Certificado digital padrão", "Cadastre o certificado A1 em Configurações › Certificados digitais e marque o padrão: ele assina os livros, os PDFs e as transmissões sem precisar escolher a cada vez.", ViewModels.TelasComuns.Certificados),
    ];
}
