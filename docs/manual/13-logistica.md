# Vorticis Suite Logística

O **Vorticis Suite Logística** (ícone verde-petróleo) é o programa da transportadora: **um programa só, com um menu só**, que reúne o controle da frota e da manutenção, o financeiro, a contabilidade e a folha de pagamento (estas duas no menu **Gerencial**). As telas são as mesmas dos módulos do Vorticis (mesmas regras e o mesmo banco de dados), organizadas para o dia a dia da transportadora. Não é preciso trocar de módulo.

## Instalação e primeiro acesso

1. Rode o instalador **Setup-VorticisSuiteLogistica**. Ele instala o programa e configura o banco de dados sozinho.
2. Abra o atalho **Vorticis Suite Logística** (menu Iniciar ou área de trabalho) e entre com o seu usuário.
3. Cadastre a transportadora em *Cadastros › Transportadoras*. Ela já fica ativa em todas as áreas do programa (frota, financeiro, contábil e folha).
4. Escolha a transportadora no topo da tela e siga a ordem dos cadastros do capítulo [Frota](12-frota.md#ordem-dos-cadastros).

O Vorticis Suite Logística é instalado numa pasta própria (*Vorticis Logistica*) e usa o mesmo banco de dados do Vorticis no computador. Quem já tinha o programa com o nome antigo (Vorticis Logística) só precisa rodar o instalador novo: ele atualiza a instalação, troca os atalhos para o nome **Vorticis Suite Logística** e mantém os dados.

## Menus do programa

| Menu | O que tem | Detalhes no manual |
|---|---|---|
| **Cadastros** | Transportadoras, contadores, clientes, fornecedores, veículos, motoristas, documentos e vencimentos; os cadastros do financeiro (produtos, formas de pagamento, contas bancárias), do contábil (plano de contas, históricos...) e da folha (funcionários, cargos, rubricas...) em submenus; os **Parâmetros** | [Frota](12-frota.md), [Contábil](03-contabil.md), [Folha](06-folha.md) |
| **Operação** | Abastecimentos e viagens; submenu **Coletas** (*Ordens de coleta*, em breve) | [Frota](12-frota.md#abastecimentos) |
| **Manutenção** | Planos de manutenção, peças e serviços, ordens de serviço, autorização de serviços e notas de compra | [Frota](12-frota.md#ordens-de-servico) |
| **Emissão** | CT-e, MDF-e e NFS-e (em breve) | — |
| **Financeiro** | Contas a pagar, contas a receber e conciliação bancária; faturamento (pedidos de venda e notas de saída), compras, estoque e monitores | [Gerencial](10-gerencial.md) |
| **Gerencial** | Submenus **Contábil** (lançamentos, fechamentos, imobilizado, consultas e integrações) e **Folha** (cálculos, pró-labore e eSocial) | [Contábil](03-contabil.md), [Folha](06-folha.md) |
| **Relatórios** | Todos os relatórios do programa, em submenus: **Frota e operação**, **Manutenção**, **Financeiro e gerencial**, **Contábil** e **Folha** | [Frota](12-frota.md#relatorios-da-frota) |

Neste programa **não** há o menu Fiscal, o calendário de obrigações nem o *Painel do empresário*: use o menu **Financeiro** para as contas a pagar e a receber. A tela inicial também não mostra o quadro de próximas obrigações.

Os atalhos de teclado do Frota valem aqui (**Ctrl+B** abastecimentos, **Ctrl+P** painel da frota, **Ctrl+O** ordens de serviço), além dos do Contábil, do Gerencial e da Folha que não repetem tecla (ex.: **Ctrl+L** lançamento contábil, **Ctrl+I** importação de XML, **Ctrl+F** folha mensal). A lista completa fica em *Ajuda › Sobre*.

## Parâmetros

*Cadastros › Parâmetros* abre os parâmetros da frota (avisos de vencimento, alerta de consumo e limite de autorização das ordens de serviço). Os parâmetros de cada área ficam em *Cadastros › Parâmetros dos módulos*: **Gerencial e financeiro**, **Contábil**, **Fiscal** (usado pelo financeiro) e **Folha de pagamento**, iguais aos de cada módulo.

## Quem pode usar cada área

As permissões são as dos perfis de acesso (*Configurações › Usuários*, aba *Perfis de acesso*): cada tela continua exigindo a função da sua área (ex.: Frota › Manutenção, Financeiro › Contas a pagar, Contábil › Lançamentos). Assim, o motorista-chefe pode ver só a frota e o financeiro só o seu menu, no mesmo programa.

## O que vem a seguir

- **Integração automática:** o frete das viagens virando conta a receber e lançamento contábil; as notas de compra da manutenção virando conta a pagar e despesa do veículo.
- **Emissão de documentos eletrônicos:** CT-e e MDF-e (autorização na SEFAZ, DACTE e DAMDFE, cancelamento, carta de correção e encerramento) e NFS-e pelo Padrão Nacional.
