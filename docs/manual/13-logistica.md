# Vorticis Logística

O **Vorticis Logística** (ícone verde-petróleo) é o programa da transportadora: **um programa só, com um menu só**, que reúne o controle da frota e da manutenção, o financeiro, o fiscal, a contabilidade e a folha de pagamento. As telas são as mesmas dos módulos do Vorticis (mesmas regras e o mesmo banco de dados), organizadas para o dia a dia da transportadora. Não é preciso trocar de módulo.

## Instalação e primeiro acesso

1. Rode o instalador **Setup-VorticisLogistica**. Ele instala o programa e configura o banco de dados sozinho.
2. Abra o atalho **Vorticis Logística** (menu Iniciar ou área de trabalho) e entre com o seu usuário.
3. Cadastre a transportadora em *Cadastros › Transportadoras*. Ela já fica ativa em todas as áreas do programa (frota, financeiro, fiscal, contábil e folha).
4. Escolha a transportadora no topo da tela e siga a ordem dos cadastros do capítulo [Frota](12-frota.md#ordem-dos-cadastros).

O Vorticis Logística é instalado numa pasta própria (*Vorticis Logistica*) e usa o mesmo banco de dados do Vorticis no computador.

## Menus do programa

| Menu | O que tem | Detalhes no manual |
|---|---|---|
| **Cadastros** | Transportadoras, contadores, clientes, fornecedores, veículos, motoristas, documentos e vencimentos; os cadastros do financeiro (produtos, formas de pagamento, contas bancárias), do contábil (plano de contas, históricos...), do fiscal e da folha (funcionários, cargos, rubricas...) em submenus; os **Parâmetros** | [Frota](12-frota.md), [Contábil](03-contabil.md), [Fiscal](05-fiscal.md), [Folha](06-folha.md) |
| **Operação** | Abastecimentos e viagens | [Frota](12-frota.md#abastecimentos) |
| **Manutenção** | Planos de manutenção, peças e serviços, ordens de serviço, autorização de serviços e notas de compra | [Frota](12-frota.md#ordens-de-servico) |
| **Emissão** | CT-e, MDF-e e NFS-e (em breve) | — |
| **Financeiro** | Contas a pagar, contas a receber e conciliação bancária; faturamento (pedidos de venda e notas de saída), compras, estoque e monitores | [Gerencial](10-gerencial.md) |
| **Fiscal** | Lançamentos e documentos fiscais, apuração, obrigações (SPED) e relatórios fiscais | [Fiscal](05-fiscal.md) |
| **Contábil** | Lançamentos, fechamentos, integrações e relatórios contábeis | [Contábil](03-contabil.md) |
| **Folha** | Cálculos da folha, pró-labore, eSocial e relatórios da folha | [Folha](06-folha.md) |
| **Relatórios** | Painel da frota, custos por veículo, consumo, viagens, vencimentos, ordens de serviço, aprovações e notas de compra; os relatórios gerenciais e financeiros num submenu | [Frota](12-frota.md#relatorios-da-frota) |
| **Obrigações** | Calendário de obrigações | [Obrigações](07-obrigacoes.md) |

Os atalhos de teclado do Frota valem aqui (**Ctrl+B** abastecimentos, **Ctrl+P** painel da frota, **Ctrl+O** ordens de serviço), além dos do Contábil, do Fiscal e da Folha que não repetem tecla (ex.: **Ctrl+L** lançamento contábil, **Ctrl+I** importação de XML, **Ctrl+F** folha mensal). A lista completa fica em *Ajuda › Sobre*.

## Parâmetros

*Cadastros › Parâmetros* abre os parâmetros da frota (avisos de vencimento, alerta de consumo e limite de autorização das ordens de serviço). Os parâmetros de cada área ficam em *Cadastros › Parâmetros dos módulos*: **Gerencial e financeiro**, **Contábil**, **Fiscal** e **Folha de pagamento**, iguais aos de cada módulo.

## Quem pode usar cada área

As permissões são as dos perfis de acesso (*Configurações › Usuários*, aba *Perfis de acesso*): cada tela continua exigindo a função da sua área (ex.: Frota › Manutenção, Fiscal › Contas a pagar, Contábil › Lançamentos). Assim, o motorista-chefe pode ver só a frota e o financeiro só o seu menu, no mesmo programa.

## O que vem a seguir

- **Integração automática:** o frete das viagens virando conta a receber e lançamento contábil; as notas de compra da manutenção virando conta a pagar e despesa do veículo.
- **Emissão de documentos eletrônicos:** CT-e e MDF-e (autorização na SEFAZ, DACTE e DAMDFE, cancelamento, carta de correção e encerramento) e NFS-e pelo Padrão Nacional.
