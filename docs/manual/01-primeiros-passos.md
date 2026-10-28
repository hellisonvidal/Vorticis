# Primeiros passos

## Instalação e módulos

**Para que serve:** instalar o Vorticis no Windows 10 ou 11 (64 bits).

1. Execute o arquivo `Setup-Vorticis-<versão>.exe`. Não precisa de senha de administrador do Windows.
2. Escolha os módulos: **Vorticis Contábil**, **Vorticis Fiscal** e **Vorticis Folha** (pelo menos um). Cada módulo vira um programa e um atalho próprios, com cor e ícone diferentes: a espiral da Vorticis na cor do módulo e um selo com a sigla — Contábil verde (**C**), Fiscal âmbar (**F**), Folha roxo (**FP**), Escritório azul (**E**), Gerencial ciano (**Gerencial**) e Condomínio vermelho (**CD**). O próprio instalador tem um ícone diferente (quadrado azul-marinho com a seta de instalação e as cores de todos os módulos).
3. Ao final, o instalador prepara o banco de dados sozinho. Abra o módulo pelo atalho.

Os módulos compartilham o mesmo banco, o mesmo login, as mesmas empresas e as mesmas configurações. Você pode abri-los ao mesmo tempo.

**Dicas:** para atualizar, basta rodar o instalador novo: os dados são preservados. Desinstalar remove o programa, mas **não** apaga os dados (pasta `%LOCALAPPDATA%\Vorticis`). Há também versões portáteis (descompacte e execute).

## Vorticis Suite Empresarial (todos os módulos numa janela)

**Para que serve:** trabalhar com todos os módulos instalados (Escritório, Contábil, Fiscal, Folha, Condomínio, Gerencial) numa única janela, como se fosse um só programa.
**Onde fica:** atalho **Vorticis Suite Empresarial** no menu Iniciar e na área de trabalho. É o programa aberto ao final da instalação.


1. Entre uma vez com o seu usuário: o login vale para todos os módulos. A Suite começa pelo Escritório, quando ele está instalado.
2. Troque de módulo pela navegação do layout escolhido ou pelas teclas **Ctrl+1**, **Ctrl+2**…. Os módulos aparecem em ordem alfabética em todos os layouts (Condomínio, Contábil, Escritório, Fiscal, Folha, Gerencial), e as teclas seguem essa ordem. A empresa escolhida continua a mesma quando também está liberada no outro módulo. As funções abertas de cada módulo ficam guardadas: ao voltar, elas reaparecem.
3. Para mudar o visual, clique no botão discreto **Layout** (ícone de quadradinhos), que fica em todos os layouts: na faixa de abas, na coluna de ícones à esquerda e no topo do painel. Abre uma janelinha com a **miniatura de cada layout**; clique na que você quiser e a troca é imediata, sem fechar o programa. Também dá para usar *Exibir › Layout da Suite* (ou *Configurações › Layout da Suite*), que tem a opção **Escolher com miniaturas…** e a lista dos layouts. A escolha fica gravada **para o seu usuário**: cada pessoa que entra no programa vê o layout que escolheu.

   São cinco opções:
   - **A – Clássico com abas:** uma aba para cada módulo no topo (com o ícone colorido do módulo) e, logo abaixo, os menus do módulo.
   - **B – Barra lateral (padrão):** uma coluna estreita à esquerda com os ícones dos módulos. Ao passar o mouse ou clicar, ela abre com os nomes; o módulo em uso mostra as suas funções agrupadas por categoria (Cadastros, Lançamentos, Relatórios…). Clique numa função para abri-la.
   - **C – Árvore com busca:** todos os módulos e funções numa árvore. No alto ficam o campo **Ir para…** (**Ctrl+K**; digite, por exemplo, "férias" ou "balancete" e clique no resultado), os seus **Favoritos** e os **Recentes**. Na árvore, a estrela ao lado de uma função a marca como favorita.
   - **D – Painel inicial:** um cartão grande por módulo, com o ícone, as funções que você mais usa e o botão **Abrir módulo**; embaixo, os seus favoritos e recentes e os **próximos vencimentos** das obrigações. Dentro de um módulo, a faixa de cima tem o botão **Voltar ao painel** e a caixa **Módulo** para trocar de módulo direto.
   - **E – Faixa de opções:** uma aba por módulo e, embaixo, a faixa com botões grandes agrupados por categoria (Cadastros, Lançamentos, Relatórios…). Botões com uma setinha abrem uma lista; **Mais** mostra as demais funções do grupo.

   No layout E a faixa de opções substitui a barra de menus: a empresa ativa, o seu nome e o botão **Exibir** ficam na linha das abas, e as setas nas pontas da faixa mostram os grupos que não cabem na tela. Com a navegação da esquerda **fixa** (B e C), a barra de cima mantém só **Exibir** e **Ajuda**, porque as demais funções já estão na lateral. A barra de funções abertas só aparece quando há alguma função aberta.

Nos demais layouts, os menus de cima (Cadastros, Lançamentos…) continuam à mostra, com o ícone de cada categoria, e todas as funções continuam acessíveis em qualquer layout.

   **Fixar e ocultar os menus.** No B e no C, a navegação da esquerda fica **oculta** por padrão: só a coluna de ícones aparece, para sobrar espaço para as telas. Ao passar o mouse ou clicar na coluna, a navegação abre **por cima** das telas e se fecha quando o mouse sai ou quando você escolhe uma função. O botão **Fixar** (alfinete) deixa a navegação sempre aberta, empurrando as telas para o lado; **Ocultar** volta a escondê-la. No A e no E existe o botão **Ocultar faixa / Fixar faixa**: oculta, a faixa mostra só os títulos das abas (no A, só os ícones) e a faixa de botões do E abre por cima ao passar o mouse ou clicar numa aba. Essas escolhas também ficam gravadas para o seu usuário e podem ser feitas em *Exibir › Layout da Suite*.
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
4. Em **Empresas que o usuário pode acessar**, marque as empresas (busque por nome, código ou CNPJ; *Marcar todas* e *Desmarcar todas* valem para as listadas). Usuário novo vem com todas marcadas e passa a receber também as empresas cadastradas depois; ao desmarcar alguma, isso deixa de valer. Dá para ter um perfil diferente por empresa.
5. **Salvar**.

Para esquecimentos use **Redefinir senha** (gera senha temporária). Excluir um usuário mantém o histórico de lançamentos, sem o vínculo com ele; ninguém exclui o próprio usuário.

### Perfis de acesso
Um perfil é um conjunto de funções liberadas (lançar, estornar, fechar períodos, relatórios, exportar, folha, fiscal etc.). Na aba *Perfis de acesso*: **Incluir** (ou **Duplicar perfil**), marque as funções e salve. Perfis padrão, que não se excluem: **Administrador** (tudo, inclusive empresas, usuários, conexão e backup), **Contador** (todas as funções, inclusive *Manter o orçamento*), **Assistente** (lança e estorna, não fecha períodos) e **Consulta** (relatórios).

### Segurança
- Após 5 tentativas de senha errada, o usuário fica bloqueado por 15 minutos.
- Toda tentativa de login fica na aba *Registro de acessos*.
- Para trocar sua senha: aba *Alterar minha senha*.

## Certificados digitais

Usados para assinar arquivos (ECD, ECF, SPED) e transmitir o eSocial. A tela fica em *Configurações › Certificados digitais* e é exclusiva do administrador. Veja [Certificados digitais](08-configuracoes.md#certificados-digitais).

## Filtros nas listas

Toda tela que mostra uma **lista de registros** (cadastros, consultas, lançamentos, notas, duplicatas, funcionários, eventos etc.) tem, logo acima da lista, a barra de localização. Ela funciona em todos os módulos do mesmo jeito e age só sobre as linhas que já estão na tela: não busca de novo no banco. Os filtros próprios da tela (período, empresa, situação) continuam valendo; a barra serve para refinar o resultado. Telas de digitação (itens da nota, partidas do lançamento, orçamento mês a mês) não têm a barra.

### Busca rápida
1. Clique na caixa **Buscar na lista** (ou aperte **Ctrl+F**, que leva o cursor direto para ela).
2. Digite parte do que procura. A lista mostra só as linhas que têm o texto em **qualquer coluna visível**. Não faz diferença usar maiúsculas ou acentos: *conceicao* encontra *Conceição*.
3. Com **vários termos separados por espaço**, a linha precisa ter todos: *maria souza* mostra só as linhas que têm *maria* e *souza*.
4. **Esc** dentro da caixa apaga a busca.

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
