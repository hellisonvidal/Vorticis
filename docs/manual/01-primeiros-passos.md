# Primeiros passos

## Instalação e módulos

**Para que serve:** instalar o Vorticis no Windows 10 ou 11 (64 bits).

1. Execute o arquivo `Setup-Vorticis-<versão>.exe`. Não precisa de senha de administrador do Windows.
2. Escolha os módulos: **Vorticis Contábil**, **Vorticis Fiscal** e **Vorticis Folha** (pelo menos um). Cada módulo vira um programa e um atalho próprios, com cor e ícone diferentes: a espiral da Vorticis na cor do módulo e um selo com a sigla — Contábil verde (**C**), Fiscal âmbar (**F**), Folha roxo (**FP**), Escritório azul (**E**), Gerencial ciano (**Gerencial**) e Condomínio vermelho (**CD**). O próprio instalador tem um ícone diferente (quadrado azul-marinho com a seta de instalação e as cores de todos os módulos).
3. Ao final, o instalador prepara o banco de dados sozinho. Abra o módulo pelo atalho.

Os módulos compartilham o mesmo banco, o mesmo login, as mesmas empresas e as mesmas configurações. Você pode abri-los ao mesmo tempo.

**Versões e atualizações:** o Vorticis começa na versão **1.0.0**. Cada versão nova é publicada no GitHub, na página *Releases* do projeto, com o instalador e as **notas da versão** (o que mudou). Para atualizar, baixe e rode o instalador novo: os dados são preservados e o banco é atualizado sozinho na primeira abertura. A versão em uso aparece em *Ajuda › Sobre* e na tela inicial.

**Dicas:** Desinstalar remove o programa, mas **não** apaga os dados (pasta `%LOCALAPPDATA%\Vorticis`). Há também versões portáteis (descompacte e execute).

## A janela de cada módulo

Cada módulo aberto sozinho (Contábil, Fiscal, Folha, Escritório, Gerencial, Condomínio) tem a mesma aparência:
- **Cabeçalho** no alto, com o selo do módulo na cor dele, o nome e a descrição do módulo; à direita, a **empresa** em uso (clique para trocar) e o **seu usuário**, com as iniciais num círculo.
- **Barra de menus** clara logo abaixo (Cadastros, Lançamentos, Relatórios…), com uma faixa na cor do módulo embaixo, para saber sempre em que módulo você está.
- **Funções abertas** em etiquetas arredondadas na faixa seguinte: clique para trazer a tela para a frente e no **✕** para fechar.
- Botões, campos e listas com cantos arredondados e o cabeçalho das listas em destaque. O tema claro ou escuro segue *Configurações › Configurações gerais › Preferências*.

Na Suite a moldura é a do visual escolhido (veja abaixo).

## Vorticis Suite Empresarial (todos os módulos numa janela)

**Para que serve:** trabalhar com todos os módulos instalados (Escritório, Contábil, Fiscal, Folha, Condomínio, Gerencial) numa única janela, como se fosse um só programa.
**Onde fica:** atalho **Vorticis Suite Empresarial** no menu Iniciar e na área de trabalho. É o programa aberto ao final da instalação.


1. Entre uma vez com o seu usuário: o login vale para todos os módulos. A Suite começa pelo Escritório, quando ele está instalado.
2. Troque de módulo pela navegação do visual escolhido ou pelas teclas **Ctrl+1**, **Ctrl+2**…. Os módulos aparecem em ordem alfabética em todos os visuais (Condomínio, Contábil, Escritório, Fiscal, Folha, Gerencial), e as teclas seguem essa ordem. A empresa escolhida continua a mesma quando também está liberada no outro módulo. As funções abertas de cada módulo ficam guardadas: ao voltar, elas reaparecem.
3. Para mudar o visual, clique no botão discreto **Visual** (ícone de quadradinhos), que fica em todos os visuais: na faixa de abas, na coluna de ícones à esquerda, na barra de cima do mosaico e da busca. Abre uma janelinha com a **imagem de cada um dos 5 visuais**; clique no que você quiser e a troca é imediata, sem fechar o programa. Também dá para usar *Exibir › Visual da Suite* (ou *Configurações › Visual da Suite*), com a lista numerada. A escolha fica gravada **para o seu usuário**: cada pessoa que entra no programa vê o visual que escolheu. Em todos eles as funções, os nomes dos menus e os atalhos são os mesmos.

   Os cinco visuais:
   1. **Clássico renovado (padrão):** igual ao Vorticis de cada módulo: as abas dos módulos no topo, o menu (Cadastros, Lançamentos…) logo abaixo e as funções abertas numa faixa embaixo do menu.
   2. **Barra lateral:** uma coluna à esquerda com os módulos e, no módulo em uso, as funções agrupadas por categoria (Cadastros, Lançamentos, Relatórios…). Clique numa função para abri-la.
   3. **Faixa de opções:** uma aba por módulo e, embaixo, botões grandes agrupados por categoria, como no Word e no Excel. Botões com uma setinha abrem uma lista; **Mais** mostra as demais funções do grupo. Botões apagados não podem ser abertos agora (em geral falta escolher a empresa ou o seu usuário não tem permissão); ao clicar, a barra de baixo explica o motivo. A empresa ativa, o seu nome e o botão **Exibir** ficam na linha das abas.
   4. **Início em mosaico:** um cartão grande por módulo, com o ícone, as funções que você mais usa e o botão **Abrir módulo**; embaixo, os seus favoritos e recentes e os **próximos vencimentos**. Dentro de um módulo, a faixa de cima tem **Voltar ao painel** e a caixa **Módulo**.
   5. **Busca primeiro:** a moldura fica mínima (marca, módulo, empresa, **Menus** e **Visual**) e o centro da tela pergunta **"O que você quer fazer?"**. Digite parte do nome (por exemplo, "férias" ou "balancete") e clique no resultado, ou tecle **Enter** para abrir o primeiro. Os recentes, favoritos e atalhos do módulo ficam embaixo. O botão **Menus** mostra ou esconde o menu do módulo.

   **Ctrl+K (Ir para…)** funciona em todos os visuais: no 5 vai para a busca do centro; nos demais, a mesma busca abre por cima da tela (**Esc** ou **Fechar** volta).

   Quem usava um dos visuais que saíram (a Suite tinha dez) passa para o mais parecido: *Barra de comandos*, *Lista e ficha* e *Alta densidade* viram o **1 – Clássico renovado**; *Trilho compacto* e *Área em painéis* viram o **2 – Barra lateral**. O tema escuro, se estava em uso, continua.

   **Tema de cores (claro, escuro ou automático).** Na mesma janelinha do botão **Visual**, na linha **Tema de cores**, escolha **Claro**, **Escuro** ou **Automático**. O escuro descansa a vista em ambientes com pouca luz e vale para todos os visuais, para os menus, as telas de cadastro, as listas, os gráficos e o manual; o **Automático** acompanha o tema do Windows. A troca é imediata e fica gravada para o seu usuário. Também dá para trocar em *Exibir › Visual da Suite › Tema de cores*.

   **Fixar e ocultar os menus.** No 2 a navegação da esquerda começa **fixa**; com **Ocultar** ela fica escondida e só a coluna de ícones aparece, para sobrar espaço para as telas. Ao passar o mouse ou clicar na coluna, a navegação abre **por cima** das telas e se fecha quando o mouse sai ou quando você escolhe uma função. O botão **Fixar** (alfinete) deixa a navegação sempre aberta, empurrando as telas para o lado; **Ocultar** volta a escondê-la. Nos visuais com abas (1 e 3) existe o botão **Ocultar faixa / Fixar faixa**: oculta, a faixa mostra só os ícones das abas e a faixa de botões do 3 abre por cima ao passar o mouse ou clicar numa aba. Essas escolhas também ficam gravadas para o seu usuário e podem ser feitas em *Exibir › Visual da Suite*.
4. O número vermelho no ícone ou na aba mostra quantas funções estão abertas naquele módulo. As janelas das funções ficam sempre na frente da janela principal: para alternar entre elas, clique no nome da função na barra de funções abertas.
5. **Sair (trocar de usuário)**, em qualquer módulo, encerra a sessão em todos.

Os executáveis de cada módulo (Vorticis Contábil, Vorticis Fiscal…) continuam disponíveis para quem prefere um módulo por janela.

## Configuração automática do banco de dados

**Para que serve:** você não precisa instalar nem configurar o PostgreSQL. O Vorticis traz um banco embutido que é criado na primeira abertura, escuta só no seu computador e para quando o último módulo é fechado.

Se o programa abrir direto na tela **Conexão com o banco de dados**, veja [Conexão com o banco](08-configuracoes.md#conexao-com-o-banco-de-dados) e [Solução de problemas](08-configuracoes.md#solucao-de-problemas).

## Primeiro acesso (vorticis / vorticis)

Em instalação nova não existe nenhum usuário. Por isso:

1. Na tela de entrada, informe usuário **vorticis** e senha **vorticis**. Esse é o login de implantação do programa (não é senha do banco).
2. O cadastro de usuários abre sozinho. Inclua o primeiro usuário com perfil **Administrador** (login, nome e senha).
3. Ao salvar, o sistema volta à tela de login. A combinação vorticis/vorticis **deixa de funcionar**. Entre com o administrador.
4. Defina os **perfis de acesso** (obrigatório na primeira vez) e clique em **Concluir configuração dos perfis**. Só então as demais funções são liberadas.
5. Cadastre a primeira empresa em *Cadastros › Empresas*.

**Dica:** na tela de entrada, **Salvar login neste computador** guarda usuário e senha (cifrados pelo Windows). Não use em computador compartilhado.

## Usuários, perfis e empresas por usuário

**Onde fica:** *Configurações › Usuários* (com o Vorticis Escritório instalado, só nele: *Configurações › Usuários e controle de acesso*), em quatro abas: *Usuários*, *Perfis de acesso*, *Alterar minha senha* e *Registro de acessos*. Usuários comuns veem apenas a troca de senha.

### Cadastrar um usuário (administrador)
1. Aba *Usuários* › **Incluir**.
2. Informe login, nome e **senha inicial** (o usuário troca no primeiro acesso).
3. Escolha o **perfil** (obrigatório).
4. Em **Dados pessoais e contato** (opcionais), informe **CPF**, **e-mail**, **telefone** e **celular**, a data de **admissão** no escritório, o **cargo**, o **departamento** e uma **observação**. O sistema confere se o CPF é válido, se o e-mail tem o formato nome@dominio e se a admissão não é uma data futura. Cargo, e-mail e telefone aparecem na lista de usuários.
5. Em **Módulos que o usuário pode acessar**, desmarque os módulos que ele não deve usar (Contábil, Fiscal, Folha de Pagamento, Escritório, Gerencial, Condomínio). Num módulo bloqueado o usuário consegue entrar, mas não vê nenhuma empresa nem função: só *Ajuda*, *Alterar minha senha* e *Sair*, com o aviso de que o acesso não foi liberado. Usuário novo vem com todos marcados; administradores acessam sempre todos os módulos.
6. Em **Empresas que o usuário pode acessar**, marque as empresas (busque por nome, código ou CNPJ; *Marcar todas* e *Desmarcar todas* valem para as listadas). Usuário novo vem com todas marcadas e passa a receber também as empresas cadastradas depois; ao desmarcar alguma, isso deixa de valer. Dá para ter um perfil diferente por empresa.
7. **Salvar**.

Para esquecimentos use **Redefinir senha** (gera senha temporária). Excluir um usuário mantém o histórico de lançamentos, sem o vínculo com ele; ninguém exclui o próprio usuário.

### Perfis de acesso
Um perfil é um conjunto de funções liberadas (lançar, estornar, fechar períodos, relatórios, exportar, folha, fiscal etc.). Na aba *Perfis de acesso*: **Incluir** (ou **Duplicar perfil**), marque as funções e salve. Perfis padrão, que não se excluem: **Administrador** (tudo, inclusive empresas, usuários, conexão e backup), **Contador** (todas as funções, inclusive *Manter o orçamento*), **Assistente** (lança e estorna, não fecha períodos) e **Consulta** (relatórios).

### Segurança
- Após 5 tentativas de senha errada, o usuário fica bloqueado por 15 minutos.
- Toda tentativa de login fica na aba *Registro de acessos*.
- Para trocar sua senha: aba *Alterar minha senha*.

## Certificados digitais

Usados para assinar arquivos (ECD, ECF, SPED) e transmitir o eSocial. A tela fica em *Configurações › Certificados digitais* e é exclusiva do administrador. Veja [Certificados digitais](08-configuracoes.md#certificados-digitais).

## Dicas

**Para que serve:** um auxiliar dentro do sistema, com dicas práticas da área do módulo aberto: dicas contábeis no Contábil, fiscais no Fiscal, de folha na Folha, e assim por diante, mais as dicas gerais do programa.
**Onde fica:** *Ajuda › Dicas*, em todos os módulos.

1. No alto aparece a **Dica do dia** (muda a cada dia); **Outra dica** passa para a seguinte.
2. Abaixo estão todas as dicas. Digite em **Procurar nas dicas** para filtrar pelo texto ou escolha a área (*Geral*, *Contábil*, *Fiscal*...).
3. Quando a dica fala de uma função, o botão **Abrir a função** leva direto à tela.

## Campos de valor
Em todos os módulos, os campos em que você digita **valores** (valor, preço, base, alíquota, desconto, frete, saldo, salário, multa, juros...) ficam alinhados à direita e aceitam só números, vírgula, ponto e sinal de menos.
- Digite como preferir: *1500*, *1500,5* ou *1.500,50* (também aceita colar *1500.50* de uma planilha).
- Ao sair do campo (Tab, Enter ou clique em outro campo), o valor aparece com **ponto de milhar e duas casas depois da vírgula**: *1.500,50*.
- Campo em branco continua em branco.

## Pesquisa dentro dos campos
Quando você não sabe o código a informar, pesquise sem sair da tela:
- **Campos de conta, histórico, participante (cliente/fornecedor), centro de custo e produto** têm uma **lupa** dentro do próprio campo. Clique nela (ou tecle **F4** com o campo selecionado) e digite parte do código ou da descrição na caixa que abre: a lista mostra só o que combina (sem diferença de acentos ou maiúsculas; pode digitar várias palavras, como *banco itau*). Use as setas e **Enter** (ou duplo clique) para escolher; o código entra no campo. **Esc** fecha sem escolher.
- **Listas de escolha com muitas opções** (aquelas que abrem com uma setinha, como rubricas, CFOP, contas bancárias, formas de pagamento) mostram no alto a caixa **Pesquisar na lista**: digite parte do nome e só ficam as opções que combinam; **Enter** escolhe a primeira.
- A lista da lupa é atualizada a cada minuto; o que você acabou de cadastrar aparece na pesquisa seguinte.

## Filtros nas listas

Toda tela que mostra uma **lista de registros** (cadastros, consultas, lançamentos, notas, duplicatas, funcionários, eventos etc.) tem, logo acima da lista, a barra de localização. Ela funciona em todos os módulos do mesmo jeito e age só sobre as linhas que já estão na tela: não busca de novo no banco. Os filtros próprios da tela (período, empresa, situação) continuam valendo; a barra serve para refinar o resultado. Telas de digitação (itens da nota, partidas do lançamento, orçamento mês a mês) não têm a barra.

### Busca rápida
1. Clique na caixa **Buscar na lista** (ou aperte **Ctrl+F**, que leva o cursor direto para ela).
2. Digite parte do que procura. A lista mostra só as linhas que têm o texto em **qualquer coluna visível**. Não faz diferença usar maiúsculas ou acentos: *conceicao* encontra *Conceição*.
3. Com **vários termos separados por espaço**, a linha precisa ter todos: *maria souza* mostra só as linhas que têm *maria* e *souza*.
4. **Esc** dentro da caixa apaga a busca.

### Exportar a lista
O botão **Exportar**, na mesma barra, grava o que está na lista naquele momento: as **colunas visíveis**, só as **linhas que passaram pela busca e pelos filtros** e na **ordem** em que estão (se você clicou num cabeçalho para ordenar, vale essa ordem).
1. Clique em **Exportar** e escolha **Excel (.xlsx)**, **CSV (.csv)** ou **PDF (.pdf)**.
2. O arquivo é salvo na pasta de relatórios (*Documentos › Vorticis Contabil › Relatorios*) e abre sozinho. Ao lado da barra aparece onde ele ficou.
3. O PDF sai com a empresa, a data e quem emitiu no cabeçalho, e o total de registros no fim.

Excel e CSV seguem a permissão **Exportar relatórios para planilha** do perfil do usuário, como nos relatórios.

### Filtros por coluna
1. Clique em **Filtros**. Abre um painel com um quadro para cada coluna.
2. Colunas de texto: escolha **Contém**, **Começa com** ou **É igual a** e digite o texto.
3. Colunas de número ou valor: preencha **de** e **até** (qualquer um dos dois pode ficar vazio).
4. Colunas de data: escolha a data inicial (**De**) e a final (**Até**).
5. Colunas de sim/não: escolha **Todos**, **Sim** ou **Não**.
6. Quando a coluna tem poucos valores diferentes, aparece **Escolher valores**: marque só os que quer ver.
7. Os filtros se somam: a linha precisa passar por todos. O botão passa a mostrar quantos filtros estão ligados (por exemplo, **Filtros (2)**). Clique em **Filtros** de novo para fechar o painel.

### Quantas linhas aparecem
À direita fica o contador, por exemplo **12 de 340 registros**. Com a lista inteira ele mostra só o total. Para voltar a ver tudo, clique em **Limpar filtros** (o botão só aparece quando há filtro ligado).

### Bom saber
- Selecionar uma linha, abrir, alterar, excluir ou usar os botões da tela funciona normalmente sobre as linhas que estão à mostra.
- Linhas incluídas depois também passam pelo filtro ligado.
- O programa **lembra os filtros de cada tela** enquanto estiver aberto: se você fechar a lista e abrir de novo, a busca continua lá (use **Limpar filtros** para zerar). Ao fechar o programa, os filtros são esquecidos.
- Na **Folha**, o atalho Ctrl+F continua abrindo o cálculo da folha quando o cursor não está numa lista; dentro de uma lista, Ctrl+F vai para a busca.

## Tela inicial e ajuda

Depois do login abre a **tela inicial**, com as próximas obrigações da empresa. *Ajuda › Tela inicial* volta a ela; *Ajuda › Licença e registro* mostra o titular da licença; *Ajuda › Sobre* mostra versão, atalhos de teclado e a conexão em uso. O seletor de empresa fica no topo da janela: escolha a empresa antes de trabalhar. Fechar o programa pede confirmação.

Logo abaixo dos menus fica, fixa, a **barra de funções abertas**: cada função aberta (que abre na sua própria janela) aparece ali com o nome; clique no nome para trazê-la para a frente e no **✕** para fechá-la. As janelas das funções nunca cobrem essa barra. Na Folha, a competência de trabalho fica na mesma linha, à direita.

### Tecla Esc

A tecla **Esc** fecha a tela (janela de função) que está na frente, em qualquer módulo. Dentro de um pop-up (por exemplo, a digitação do lançamento contábil), Esc fecha só o pop-up. Se houver algo digitado que ainda não foi salvo, o sistema pergunta antes: *Fechar sem salvar* descarta o que foi digitado e *Continuar editando* volta para a tela. Quando uma lista de opções está aberta, o primeiro Esc fecha a lista.
