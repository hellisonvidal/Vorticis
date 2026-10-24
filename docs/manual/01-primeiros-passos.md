# Primeiros passos

## Instalação e módulos

**Para que serve:** instalar o Vorticis no Windows 10 ou 11 (64 bits).

1. Execute o arquivo `Setup-Vorticis-<versão>.exe`. Não precisa de senha de administrador do Windows.
2. Na página **Licença de uso**, escolha o arquivo de licença (`licenca.vlic`) recebido do fornecedor: só os módulos da sua licença ficam disponíveis. Se ainda não tiver o arquivo, deixe em branco e instale-o depois (veja [Licença de uso](#licenca-de-uso)).
3. Escolha os módulos: **Vorticis Contábil**, **Vorticis Fiscal** e **Vorticis Folha de Pagamento** (pelo menos um). Cada módulo vira um programa e um atalho próprios, com cor e ícone diferentes: a espiral da Vorticis na cor do módulo e um selo com a sigla — Contábil verde (**C**), Fiscal âmbar (**F**), Folha roxo (**FP**), Escritório azul (**E**), ERP ciano (**ERP**) e Condomínio vermelho (**CD**). O próprio instalador tem um ícone diferente (quadrado azul-marinho com a seta de instalação e as cores de todos os módulos).
4. Ao final, o instalador prepara o banco de dados sozinho. Abra o módulo pelo atalho.

Os módulos compartilham o mesmo banco, o mesmo login, as mesmas empresas e as mesmas configurações. Você pode abri-los ao mesmo tempo.

**Dicas:** para atualizar, basta rodar o instalador novo: os dados são preservados. Desinstalar remove o programa, mas **não** apaga os dados (pasta `%LOCALAPPDATA%\Vorticis`). Há também versões portáteis (descompacte e execute).

## Licença de uso

**Para que serve:** cada módulo só abre se estiver na licença do cliente e dentro da validade. A licença é um arquivo (`licenca.vlic`) emitido e assinado pelo fornecedor do Vorticis; se o arquivo for alterado, ele deixa de valer.

**Como instalar:**
- No **instalador**, na página *Licença de uso* (o arquivo é copiado para a pasta do programa); ou
- Na **tela de entrada**: se faltar a licença, se ela estiver vencida ou se o módulo não estiver nela, aparece o motivo e o botão **Instalar arquivo de licença...**. Escolha o arquivo: ele é conferido e guardado em `%LOCALAPPDATA%\Vorticis`, e a entrada continua.

**Onde ver:** *Ajuda › Licença e registro* mostra o cliente, os módulos liberados, o número de usuários e a validade. Quando faltarem 30 dias ou menos para vencer, a barra inferior da janela avisa; peça a renovação ao fornecedor e instale o arquivo novo do mesmo jeito.

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

**Onde fica:** *Configurações › Usuários*, em quatro abas: *Usuários*, *Perfis de acesso*, *Alterar minha senha* e *Registro de acessos*. Usuários comuns veem apenas a troca de senha.

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

## Tela inicial e ajuda

Depois do login abre a **tela inicial**, com as próximas obrigações da empresa. *Ajuda › Tela inicial* volta a ela; *Ajuda › Licença e registro* mostra o titular da licença; *Ajuda › Sobre* mostra versão, atalhos de teclado e a conexão em uso. O seletor de empresa fica no topo da janela: escolha a empresa antes de trabalhar. Fechar o programa pede confirmação.

### Tecla Esc

A tecla **Esc** fecha a tela (janela de função) que está na frente, em qualquer módulo. Dentro de um pop-up (por exemplo, a digitação do lançamento contábil), Esc fecha só o pop-up. Se houver algo digitado que ainda não foi salvo, o sistema pergunta antes: *Fechar sem salvar* descarta o que foi digitado e *Continuar editando* volta para a tela. Quando uma lista de opções está aberta, o primeiro Esc fecha a lista.
