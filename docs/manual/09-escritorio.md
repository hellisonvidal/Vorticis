# Escritório

Módulo **Vorticis Escritório** (ícone azul): o gerenciador **do próprio escritório contábil**. As empresas usadas nos demais módulos são os **clientes** cadastrados aqui: o cliente é levado, já com os dados da Receita, para os módulos que ele contratou (Contabilidade, Fiscal, Folha, Condomínio, Gerencial). O escolhido no topo, em **Clientes:**, é o **próprio escritório**, dono das tarefas, protocolos, honorários e contratos. O Escritório não tem cadastro de fornecedores.

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Primeiro uso

Na instalação, o Escritório é o módulo aberto ao final. Depois de entrar com o administrador, se ainda não houver escritório cadastrado, a tela *Cadastros › Escritório* abre sozinha: cadastre o seu escritório contábil e, em seguida, os clientes em *Cadastros › Clientes*, marcando os módulos de cada um.

O Escritório também administra a suíte: **usuários e controle de acesso** (*Configurações › Usuários e controle de acesso*) e o **banco de dados** (*Configurações › Banco de dados*: conexão, backup e restauração, senha do administrador e limpeza). Com o Escritório instalado, essas funções saem dos outros módulos.

## Escritório

**Para que serve:** cadastrar o próprio escritório contábil (o contador), que é o escolhido no topo da tela.
**Onde fica:** *Cadastros › Escritório* (primeiro item do menu).

É o mesmo cadastro de empresas dos outros módulos: digite o **CNPJ**, clique em **Buscar na Receita**, confira os dados e grave com o módulo **Escritório** marcado. Só os escritórios aparecem na escolha do topo; os clientes ficam nos módulos que contrataram.

## Clientes

**Para que serve:** cadastrar quem o escritório atende, levar o cliente para os módulos do contrato e controlar os honorários dele.
**Onde fica:** *Cadastros › Clientes*.

### Dados cadastrais
1. Clique em **Incluir**; o código é sugerido.
2. Digite o **CNPJ/CPF** e clique em **Buscar na Receita**: razão social, nome fantasia, natureza jurídica, CNAE, início das atividades, endereço, município (com o código IBGE), e-mail e telefone são preenchidos, como no cadastro de empresas. O regime vem sugerido quando o cliente é do Simples ou MEI; nos demais, escolha **Regime tributário**.
3. Complete o que faltar (contato, inscrição estadual, observação).
4. **Gravar**. Cliente com tarefas, honorários, protocolos ou contratos não pode ser excluído: desmarque **Ativo**.

### Módulos do contrato
1. Na aba **Módulos do contrato**, marque os módulos contratados: **Contabilidade**, **Fiscal**, **Folha de pagamento**, **Condomínio**, **Gerencial**.
2. **Gravar**. Se o cliente ainda não é empresa, o sistema avisa: *"Este cadastro será compartilhado com os demais módulos do Vorticis (Contábil, Fiscal, Folha, Gerencial...). Deseja continuar?"* — confirme em **Continuar**. O cliente é cadastrado como empresa nesses módulos, com o mesmo código (sequência) em todos eles — o código aparece na própria aba e na mensagem depois de gravar, com os dados da aba Dados cadastrais (com Contabilidade marcada, o plano de contas modelo é importado). Se já existir empresa com o mesmo CNPJ, ela é atualizada e passa a aparecer também nos módulos marcados.
3. Para isso, a aba Dados cadastrais precisa ter CNPJ, regime, natureza jurídica, CNAE, município IBGE, UF e início das atividades; o botão **Buscar na Receita** traz quase tudo. Só o **administrador** cadastra empresas nos módulos.

Nesta aba também fica o campo **Logo da empresa** (**Carregar logo...**, **Remover logo** e **Usar a logo nos relatórios**), igual ao de *Cadastros › Empresas*; a logo só é gravada quando o cliente já é empresa (marque um módulo e grave).

Desmarcar um módulo aqui não apaga nada no módulo: para tirar a empresa de um módulo, use o cadastro de empresas.

### Honorários e cobranças
Aba **Honorários e cobranças** do cliente selecionado (veja [Honorários e cobranças](#honorarios-e-cobrancas)): o honorário mostrado e gravado é o do cliente da lista, e as cobranças listadas são só as dele, de todos os meses.

## Carteira de clientes

**Para que serve:** ver todos os clientes do escritório numa só lista.
**Onde fica:** *Relatórios › Carteira de clientes*.

A lista mostra código, CNPJ/CPF, nome, regime, tarefas em aberto (e quantas estão atrasadas), honorário mensal, honorários em aberto e o contato. **Atualizar** relê os dados. Só aparecem os clientes ativos.

## Tarefas e prazos

**Para que serve:** controlar o que precisa ser feito para cada cliente, com prazo e responsável.
**Onde fica:** *Organização › Tarefas e prazos* (atalho **Ctrl+T**).

1. **Nova tarefa**, escolha o **Cliente** (ou deixe *tarefa interna do escritório*), preencha **Tarefa**, **Vencimento**, **Responsável**, **Prioridade**, **Situação** e, se for o caso, **Repetir** (*Todo mês* ou *Todo ano*) e os **Detalhes**.
2. **Gravar**.
3. Feita a tarefa, selecione-a e clique em **Concluir**. Se ela se repete, a próxima é criada sozinha com o novo vencimento (mesmo dia do mês seguinte ou do ano seguinte; no mês mais curto, o último dia).
4. **Excluir** apaga a tarefa (pede confirmação).

Use a lista no topo para ver as tarefas de um cliente ou de todos, e **Só as em aberto** para esconder as concluídas e canceladas. Tarefas vencidas e não concluídas aparecem como **ATRASADA**.

## Protocolos de documentos

**Para que serve:** registrar a entrega de documentos ao cliente (guias, livros, relatórios) e o recebimento de documentos dele, com número sequencial.
**Onde fica:** *Organização › Protocolos de documentos*.

1. Informe **Data**, **Tipo** (*Entrega ao cliente* ou *Recebido do cliente*), **Documento**, **Pessoa** (quem recebeu ou entregou), o **Cliente** e, se quiser, uma **Observação**.
2. **Registrar protocolo**: o sistema dá o próximo número do escritório e grava quem registrou.

## Gerador de contratos

**Para que serve:** montar o contrato de prestação de serviços contábeis com o cliente, já com os dados das partes, e exportar em PDF (com assinatura digital, se quiser) ou em Word.
**Onde fica:** *Relatórios › Gerador de contratos*.

1. **Novo contrato**: o **Tipo** é *Cliente* (o cliente é o contratante e o escritório, o contratado).
2. Escolha a **Parte** (do cadastro próprio do escritório), confira o **Objeto**, informe o **Valor**, a periodicidade (*Mensal*, *Anual* ou *Parcela única*), o **dia** do vencimento, a **vigência** (sem fim = prazo indeterminado), o índice de **Reajuste** e o **Foro** (em branco: a cidade do escritório).
3. **Gerar texto**: o contrato sai com a qualificação das partes e as cláusulas de objeto, preço, reajuste, vigência, obrigações, rescisão e foro. Ajuste o texto se precisar.
4. **Gravar**: o contrato recebe o próximo número do escritório.
5. Selecione-o na lista e exporte:
   - **Exportar PDF**: o texto e as linhas de assinatura das duas partes. Marque antes **Assinar digitalmente** para assinar o PDF com o certificado digital padrão do contador (ou do responsável), definido em *Configurações › Certificados digitais*; com certificado A3, o leitor pede o PIN.
   - **Exportar Word**: o mesmo contrato em arquivo .docx, para editar no Word ou no LibreOffice.

A lista de contratos gravados fica nesta tela (não há mais o Relatório de contratos).

Para encerrar um contrato sem perder o histórico, desmarque **Ativo** e grave. O valor dos honorários cobrados mês a mês fica em [Honorários e cobranças](#honorarios-e-cobrancas).

## Honorários e cobranças

**Onde fica:** *Cadastros › Clientes*, aba **Honorários e cobranças** do cliente selecionado.

### Honorário do cliente
1. Selecione o **cliente** na lista do cadastro de clientes.
2. Informe **Valor mensal**, **Dia do vencimento** (1 a 31; nos meses mais curtos vale o último dia), **Início**, **Fim** (se houver) e o índice de **Reajuste**.
3. **Gravar honorário**. Cada cliente tem um honorário; gravar de novo altera o existente.

### Cobranças do mês

**Boleto:** o Vorticis ainda não emite boleto. O boleto registrado exige convênio de cobrança com o banco (arquivo CNAB de remessa e retorno ou a API do banco); enquanto isso, informe ao cliente a chave PIX ou os dados bancários do escritório e registre o recebimento aqui.

1. Escolha a **Competência** (mês/ano) e clique em **Gerar cobranças do mês**: o sistema cria uma cobrança para cada cliente com honorário vigente, com o vencimento no dia combinado. Rodar de novo não duplica.
2. Para registrar o pagamento, selecione a cobrança, confira **Recebido em** e **Valor** e clique em **Receber**.
3. **Reabrir** desfaz o recebimento; **Cancelar cobrança** cancela uma cobrança em aberto.

A lista mostra as cobranças do cliente de todos os meses; marque **Só em aberto** para ver só as não recebidas. **Gerar cobranças do mês** gera a cobrança da competência para todos os clientes com honorário, não só para o selecionado. Cobranças vencidas aparecem como **VENCIDA**. A linha de totais mostra o total, o recebido e o que está em aberto.

## Relatório de honorários

**Onde fica:** *Relatórios › Relatório de honorários*. Aparece na tela e exporta em PDF, Excel e CSV.

- **Relatório de honorários**: as cobranças das competências escolhidas (mês/ano inicial e final), com cliente, vencimento, valor, situação e recebimento; no fim, o total, o que está em aberto e o recebido.

## Parâmetros

*Cadastros › Parâmetros* é um submenu com **Parâmetros do Escritório** e **Menus do programa** (a escolha dos itens de menu que cada usuário vê; veja [Menus do programa](02-cadastros-comuns.md#menus-do-programa)). Os Parâmetros do Escritório trazem atalhos para o cadastro de clientes, os honorários, o gerador de contratos e as tarefas.
