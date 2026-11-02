# Configurações

Menu *Configurações*. As telas de backup, senha do administrador e limpeza do banco e conexão são exclusivas do administrador. Em todos os módulos, o menu termina com o submenu **Configurações gerais** (veja abaixo).

**Com o Vorticis Escritório instalado**, os usuários, os perfis de acesso e as funções do banco de dados (conexão, backup, restauração, senha do administrador e limpeza) ficam **só no Escritório**: *Configurações › Usuários e controle de acesso* e *Configurações › Banco de dados*. Nos outros módulos, o menu Configurações mostra apenas **Certificados digitais**, **Configurações gerais** e **Sair**. Sem o Escritório, cada módulo continua com essas funções no menu Configurações, como descrito abaixo.

## Backup e restauração

**Para que serve:** guardar cópias de segurança de todo o banco de dados e voltar a elas se for preciso.
**Onde fica:** *Configurações › Backup e restauração* (no Escritório: *Configurações › Banco de dados › Backup e restauração*).

### Fazer backup
1. Veja a **pasta** de destino. O padrão é a pasta *Backups* dentro da instalação (ou *Documentos\Vorticis Empresarial\Backups*, se não for possível gravar). **Escolher pasta...** muda o destino dos próximos backups, inclusive os automáticos.
2. Clique em **Fazer backup agora**. O arquivo aparece na lista (nome, data e tamanho).

### Backup automático
O sistema faz sozinho **um backup por dia**: no primeiro acesso do dia, em qualquer computador, ele grava a cópia em segundo plano (dá para continuar trabalhando) e avisa na barra de baixo onde ela ficou.
1. Na mesma tela, o quadro **Backup automático todo dia** vem **ligado**, guardando os **7 últimos**.
2. Para mudar, marque ou desmarque a opção, informe em **Guardar os últimos** quantos backups automáticos manter (de 1 a 90) e clique em **Gravar**. Só o administrador altera.
3. Os automáticos têm o nome começando por *automatico_*; os mais antigos que passarem do limite são apagados. Os backups feitos com **Fazer backup agora** nunca são apagados sozinhos.
4. Embaixo aparece a data do **último backup automático**.

### Restaurar
1. Selecione o arquivo e clique em **Restaurar backup selecionado...**.
2. Informe a **senha de administrador** (padrão `VORTICIS`, alterável em *Senha do administrador e limpeza do banco*).
3. O sistema faz antes um **backup de segurança** do estado atual e encerra todas as sessões.

**Excluir backup selecionado** apaga o arquivo (com confirmação). **Dica:** copie os backups também para outro disco ou nuvem; um backup na mesma máquina não protege contra defeito do computador.

## Configurações gerais

**Onde fica:** *Configurações › Configurações gerais*, em todos os módulos. Reúne:
- **Preferências**: o jeito de trabalhar neste computador (veja abaixo).
- **Menus do programa**: os itens de menu que cada usuário vê (veja [Menus do programa](02-cadastros-comuns.md#menus-do-programa)).
- **Parâmetros do módulo** (*Parâmetros do Contábil*, *do Fiscal*...): a mesma tela de *Cadastros › Parâmetros*. No **Escritório** os parâmetros ficam só em *Cadastros › Parâmetros*.

### Preferências
1. **Tema de cores**: *Claro*, *Escuro* ou *Automático (igual ao sistema)*. Vale na hora, na Suite e nos módulos abertos sozinhos.
2. **Perguntar antes de fechar o programa**: desmarcado, o programa fecha sem a pergunta *Deseja realmente fechar o sistema?*.
3. **Vencimentos na tela inicial (próximos dias)**: quantos dias à frente a tela inicial mostra as obrigações a vencer (de 1 a 90; o padrão é 15).
4. Clique em **Gravar**. As preferências valem para este computador, em todos os módulos.

### Bloqueio por inatividade
Para ninguém usar o sistema com o seu usuário quando você sai de perto do computador:
1. Em *Configurações › Configurações gerais › Preferências*, informe **Bloquear a tela depois de (minutos sem uso)** — o padrão é **15**; **0** desliga.
2. Depois desse tempo sem mexer no teclado ou no mouse, a tela fica escura com a mensagem **Tela bloqueada** e as telas abertas somem de vista.
3. Digite a **sua senha** e clique em **Desbloquear**: tudo volta exatamente como estava (nada do que estava digitado se perde). Senha errada conta para o bloqueio de 15 minutos depois de 5 tentativas, como no login.
4. **Sair (trocar de usuário)** volta à tela de entrada para outra pessoa entrar.

## Senha do administrador e limpeza do banco

**Onde fica:** *Configurações › Senha do administrador e limpeza do banco* (no Escritório: *Configurações › Banco de dados › Senha do administrador e limpeza do banco*).

### Senha de administrador
Usada para confirmar restauração de backup e limpeza do banco. Informe a senha atual, a nova e a confirmação e clique em **Alterar senha**. Troque a senha padrão (`VORTICIS`) logo no início.

### Limpar banco de dados (não recomendado)
Recomeça o sistema **do zero**: apaga **todas** as empresas com tudo o que é delas (notas, duplicatas, mesmo as baixadas, lançamentos, folha, pedidos, condomínio, escritório, mesmo nos períodos fechados), os contadores, os escritórios, os certificados, todos os usuários, os perfis personalizados, as preferências de menu e as configurações gravadas. Só a pasta de backup continua a mesma. Gera um backup antes.
1. Digite **LIMPAR** e confirme.
2. Informe a senha de administrador.
3. O programa volta sozinho à tela de entrada, como numa instalação nova, **sem precisar fechar e abrir de novo**: todas as telas abertas são fechadas, o usuário, a empresa ativa e o login salvo neste computador são esquecidos. Só entra `vorticis` / `vorticis`, que abre o cadastro do administrador; depois de cadastrá-lo, entre com ele: o sistema pede de novo todos os cadastros, como no primeiro uso (perfis de acesso, o escritório, as empresas ou clientes e os demais cadastros de cada módulo).

## Histórico de alterações

**Para que serve:** saber **quem** incluiu, alterou ou excluiu um cadastro ou lançamento, **quando** e **o que mudou** (por exemplo, quem mudou o valor de um honorário ou excluiu um funcionário).
**Onde fica:** *Configurações › Histórico de alterações*, em todos os módulos. Só o **administrador** abre.

1. Escolha o período (**De** e **até**; o padrão são os últimos 30 dias).
2. Se quiser, filtre pelo **usuário** (parte do login), pelo **cadastro** (Empresas, Lançamentos contábeis, Funcionários, Clientes do escritório...) ou marque **Só a empresa ativa**.
3. Clique em **Consultar**. A lista mostra data e hora, usuário, operação (*Inclusão*, *Alteração* ou *Exclusão*), cadastro, número do registro, empresa e os campos alterados.
4. Clique numa linha: embaixo aparece **O que mudou**, campo a campo, com o valor **Antes** e **Depois**.
5. **Conferir a trilha** recalcula a cadeia de segurança da trilha inteira e diz se ela está **íntegra** ou se alguém mexeu nela direto no banco de dados (por fora do sistema).

A trilha é gravada pelo próprio banco de dados e não pode ser apagada pelo sistema. A lista também tem a barra de busca, os filtros e o botão **Exportar**.

## Verificar atualizações

**Para que serve:** saber se já saiu uma versão nova do Vorticis e baixá-la.
**Onde fica:** *Ajuda › Verificar atualizações*, em todos os módulos.

1. A tela mostra a **versão instalada** e consulta o site do Vorticis: aparece **Você já usa a versão mais nova** ou **Há uma versão nova**, com o que mudou.
2. Com versão nova, clique em **Baixar a versão nova**: o navegador baixa o instalador.
3. Feche o Vorticis em todos os computadores e rode o instalador por cima da instalação: os dados são preservados e o banco é atualizado sozinho na primeira abertura.

O programa também consulta o site uma vez a cada abertura e, se houver versão nova, avisa na barra de baixo. Sem internet, ou sem o endereço do site configurado na instalação, nada acontece.

## Conexão com o banco de dados

**Onde fica:** *Configurações › Conexão com o banco de dados* (no Escritório: *Configurações › Banco de dados*; abre sozinha se o programa não consegue conectar).

- **Configuração automática** (recomendado): preenche o usuário e a senha do PostgreSQL só se for usar um servidor local já instalado; clique em **Configurar automaticamente**. O sistema mostra cada passo.
- **Banco de dados local do Vorticis**: porta, pasta dos dados e troca da senha do banco (deixe em branco para manter). **Recriar o banco local** cria um novo e guarda o antigo como `banco.backup-<data>`.
- **Servidor PostgreSQL (rede ou nuvem)**: informe servidor, porta, banco, usuário e senha (*Mostrar senhas* ajuda a conferir), **Testar conexão** e **Salvar e reconectar**. É assim que vários computadores usam o mesmo banco. O botão para usar o banco local volta ao embutido.

### Banco de dados na nuvem
O Vorticis funciona com um PostgreSQL contratado num provedor de nuvem (serviço de banco de dados gerenciado), para o escritório e os computadores de fora usarem os mesmos dados:
1. No provedor, crie o banco (PostgreSQL 14 ou mais novo), um usuário com senha forte e **libere o acesso** (firewall) para os endereços de internet dos computadores que vão usar o sistema.
2. Em *Servidor PostgreSQL (rede ou nuvem)*, informe o endereço do servidor, a porta (normalmente 5432), o banco, o usuário e a senha.
3. Em **Conexão segura (SSL)**, escolha **Exigir conexão segura (nuvem)**; se o provedor fornecer o certificado, use **Exigir e conferir o certificado do servidor**. Com servidor fora da rede interna e a conexão segura desligada, a tela avisa.
4. Se a conexão demorar, aumente o **Tempo limite**. Se ficar parada e cair, informe **Manter conexão viva** (sugestão: 60 segundos).
5. **Testar conexão** e **Salvar e reconectar**. Na primeira conexão o sistema cria as tabelas.

Os backups (*Backup e restauração*) também funcionam com o banco na nuvem; guarde as cópias fora do provedor. A velocidade depende da internet: com conexão lenta, telas de muitos lançamentos demoram mais.

## Certificados digitais

**Para que serve:** guardar os certificados usados para assinar ECD, ECF, SPED, PDFs e para transmitir o eSocial.
**Onde fica:** *Configurações › Certificados digitais* (administrador).

### Certificado A1 (arquivo .pfx ou .p12)
1. Em *Importar certificado A1*, escolha o **arquivo** e digite a **senha**.
2. **Conferir** mostra titular, CNPJ/CPF e validade.
3. Informe uma descrição (ex.: e-CNPJ da empresa) e clique em **Importar**. O arquivo é guardado como está, protegido pela própria senha; **a senha não é gravada** e será pedida no uso (ou cadastrada para uso do sistema, quando permitido).

### Certificado A3 (token ou cartão)
Clique em **Ler certificados instalados no Windows**, selecione e **Registrar A3**. Só o registro é guardado; a chave fica no token. Certificados A1 instalados no Windows (com chave exportável) podem ser importados para o Vorticis.

### Certificado padrão
Selecione o certificado e marque **Padrão do contador**, **do responsável** ou **do procurador** para que os assistentes (ECD, ECF, EFD, PDF assinado, eSocial) o escolham automaticamente. **Retirar padrão** desfaz; **Excluir do repositório** remove.

A lista avisa os certificados **vencidos ou que vencem em até 30 dias**.

## Solução de problemas

### O programa abriu na tela de conexão
1. Clique em **Configurar automaticamente**. Ele localiza o PostgreSQL embutido, confere o Visual C++, remove travas antigas, cria e inicia o banco, aplica as atualizações e testa a conexão.
2. Se falhar, clique em **Copiar diagnóstico** e envie ao suporte. Os logs ficam em `%LOCALAPPDATA%\VorticisEmpresarial\logs`.
3. Se aparecer a mensagem "Este banco de dados foi usado por outro programa Vorticis (ex.: Suite Logística) e tem atualizações que este programa não reconhece", o banco configurado pertence a outro programa. Vá em **Configurações › Conexão** e clique em **Configurar automaticamente** para usar o banco próprio deste programa.

**Onde ficam os seus dados:** a configuração e o login salvo ficam em `%APPDATA%\VorticisEmpresarial`; o banco local, os logs e as senhas de certificados ficam em `%LOCALAPPDATA%\VorticisEmpresarial`. O Vorticis Suite Logística usa pasta e banco próprios, por isso os dois programas podem ficar instalados no mesmo computador sem se misturar.

### Problemas frequentes
| Situação | O que fazer |
|---|---|
| "vorticis/vorticis" não entra | Só vale em instalação sem nenhum usuário. Use o login do administrador cadastrado. |
| Existe banco local, mas a senha não é conhecida | Use **Recriar o banco local** (a pasta antiga é preservada como `banco.backup-<data>`). |
| Faltam DLLs do Visual C++ | Instale o `vc_redist.x64.exe` (vem no instalador e nos portáteis). |
| Usuário bloqueado | Aguarde 15 minutos ou peça ao administrador para redefinir a senha. |
| Não vejo uma opção do menu | O perfil do usuário não libera a função, ou não há empresa selecionada. |
| Dois módulos abertos | É normal: compartilham o mesmo banco. O banco só para quando o último fecha. |
| Lançamento recusado por período | Reabra a competência em *Fechamento de períodos* ou revise a data de bloqueio da empresa. |

Sem o PostgreSQL embutido ao lado do programa, a configuração tenta um servidor em `localhost:5432` e pede apenas usuário e senha.
