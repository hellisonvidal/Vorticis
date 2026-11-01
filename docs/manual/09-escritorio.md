# Escritório

Módulo **Vorticis Escritório** (ícone azul): o gerenciador **do próprio escritório contábil**. O Escritório **não é o cadastro geral de empresas**: o cadastro de **clientes** serve para acompanhar as empresas contratadas, e é a partir dele que o trabalho nas empresas começa. As empresas usadas nos demais módulos são os **clientes** cadastrados aqui: o cliente é levado, já com os dados da Receita, para os módulos que ele contratou (Contabilidade, Fiscal, Folha, Condomínio, Gerencial). O escolhido no topo, em **Clientes:**, é o **próprio escritório**, dono das tarefas, protocolos, honorários e contratos. O Escritório não tem cadastro de fornecedores.

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Primeiro uso

Na instalação, o Escritório é o módulo aberto ao final. Depois de entrar com o administrador, se ainda não houver escritório cadastrado, a tela *Cadastros › Dados do escritório* abre sozinha: cadastre o seu escritório contábil e, em seguida, as empresas atendidas em *Cadastros › Clientes*, marcando os módulos de cada uma. **O próprio escritório pode ser o primeiro cliente**, para fazer nos módulos a contabilidade, a folha ou o fiscal dele mesmo.

O Escritório também administra a suíte: **usuários e controle de acesso** (*Configurações › Usuários e controle de acesso*) e o **banco de dados** (*Configurações › Banco de dados*: conexão, backup e restauração, senha do administrador e limpeza). Com o Escritório instalado, essas funções saem dos outros módulos.

## Dados do escritório

**Para que serve:** cadastrar o próprio escritório contábil (o contador), que é o escolhido no topo da tela e o dono das tarefas, protocolos, honorários e contratos.
**Onde fica:** *Cadastros › Dados do escritório* (primeiro item do menu).

Esta tela **não é o cadastro geral de empresas**: mostra só o escritório, sem plano de contas, sócios, contadores ou módulos. Digite o **CNPJ**, clique em **Buscar na Receita**, confira os dados e clique em **Salvar**. Só o escritório aparece na escolha do topo; as empresas atendidas ficam em *Cadastros › Clientes* e nos módulos que contrataram. O escritório recebe o código **9999** e não ocupa a numeração das empresas: o primeiro cliente levado aos módulos fica com o código **0001**, o segundo com 0002, e assim por diante.

### O escritório como cliente
Para usar os módulos no próprio escritório (a contabilidade, a folha ou o fiscal dele), cadastre-o também em *Cadastros › Clientes*, com o mesmo CNPJ, e marque os módulos na aba **Módulos do contrato**. Nos módulos ele vira uma empresa à parte, com código da numeração normal (por exemplo, **0001**), sem misturar com os dados do escritório desta tela.

### Empresas cadastradas direto nos módulos
Uma empresa incluída no cadastro de empresas do Contábil, do Fiscal ou da Folha fica ativa só naquele módulo. Para acompanhá-la no Escritório (tarefas, honorários) e escolher os outros módulos, cadastre-a como cliente com o **mesmo CNPJ**: o cliente fica ligado à empresa que já existe (nada é duplicado) e os módulos marcados no contrato passam a valer para ela.

## Clientes

**Para que serve:** cadastrar quem o escritório atende, levar o cliente para os módulos do contrato e controlar os honorários dele.
**Onde fica:** *Cadastros › Clientes*.

A tela mostra **todos os clientes cadastrados** numa lista (código, razão social, nome fantasia, CNPJ/CPF, regime, município, contato, módulos contratados e situação), com o filtro por código, nome ou documento. A **ficha do cliente** abre numa janela por cima da lista:
- **Incluir** abre a ficha em branco; **Alterar** abre a do cliente selecionado já liberada para edição;
- **duplo clique** (ou **Enter**) num cliente, ou o botão **Abrir ficha**, mostra a ficha só para consulta — nela, **Alterar** libera a edição e **Fechar** volta à lista;
- na edição, **Salvar** grava e fecha a ficha; **Cancelar** (ou fechar a janela, ou **Esc**) desiste do que foi digitado.

### Dados cadastrais
1. Clique em **Incluir**; o código é sugerido.
2. Digite o **CNPJ/CPF** e clique em **Buscar na Receita**: razão social, nome fantasia, natureza jurídica, CNAE, início das atividades, endereço, município (com o código IBGE), e-mail e telefone são preenchidos, como no cadastro de empresas. O regime vem sugerido quando o cliente é do Simples ou MEI; nos demais, escolha **Regime tributário**.
3. Complete o que faltar (contato, inscrição estadual, observação).
4. **Salvar**. Cliente com tarefas, honorários, protocolos ou contratos não pode ser excluído: desmarque **Ativo**.

### Módulos do contrato
1. Na aba **Módulos do contrato**, marque os módulos contratados: **Contabilidade**, **Fiscal**, **Folha de pagamento**, **Condomínio**, **Gerencial**.
2. **Gravar**. Se o cliente ainda não é empresa, o sistema avisa: *"Este cadastro será compartilhado com os demais módulos do Vorticis (Contábil, Fiscal, Folha, Gerencial...). Deseja continuar?"* — confirme em **Continuar**. O cliente é cadastrado como empresa nesses módulos, com o mesmo código (sequência) em todos eles — o código aparece na própria aba e na mensagem depois de gravar, com os dados da aba Dados cadastrais (com Contabilidade marcada, o plano de contas modelo é importado). Se já existir empresa com o mesmo CNPJ, ela é atualizada e passa a aparecer também nos módulos marcados.
3. Para isso, a aba Dados cadastrais precisa ter CNPJ, regime, natureza jurídica, CNAE, município IBGE, UF e início das atividades; o botão **Buscar na Receita** traz quase tudo. Só o **administrador** cadastra empresas nos módulos.

Nesta aba também fica o campo **Logo da empresa** (**Carregar logo...**, **Remover logo** e **Usar a logo nos relatórios**), igual ao de *Cadastros › Empresas*; a logo só é gravada quando o cliente já é empresa (marque um módulo e grave).

Esta aba é **o único lugar** em que se escolhe em quais módulos a empresa é usada: o Escritório gerencia tudo. Nos outros módulos o cadastro de empresas não mostra essa escolha (a empresa incluída lá fica ativa só naquele módulo e no Escritório), e eles também deixam de perguntar, ao entrar, se querem ativar empresas.

Desmarcar um módulo aqui tira a empresa da lista daquele módulo, mas não apaga nada do que já foi feito nele: basta marcar de novo para ela voltar.

### Honorários e cobranças
Aba **Honorários e cobranças** do cliente selecionado (veja [Honorários e cobranças](#honorarios-e-cobrancas)): o honorário mostrado e gravado é o do cliente da lista, e as cobranças listadas são só as dele, de todos os meses.

## Carteira de clientes

**Para que serve:** ver todos os clientes do escritório numa só lista.
**Onde fica:** *Relatórios › Carteira de clientes*.

A lista mostra código, CNPJ/CPF, nome, regime, tarefas em aberto (e quantas estão atrasadas), honorário mensal, honorários em aberto e o contato. **Atualizar** relê os dados. Só aparecem os clientes ativos.

## Tarefas e prazos

**Para que serve:** controlar o que precisa ser feito para cada cliente, com prazo e responsável.
**Onde fica:** *Organização › Tarefas e prazos* (atalho **Ctrl+T**).

A tela mostra a **lista das tarefas** (vencimento, cliente, tarefa, responsável, prioridade, situação e repetição); a **ficha da tarefa** abre numa janela por cima da lista.

1. **Nova tarefa** abre a ficha: escolha o **Cliente** (ou deixe *tarefa interna do escritório*), preencha **Tarefa**, **Vencimento**, **Responsável**, **Prioridade**, **Situação** e, se for o caso, **Repetir** (*Todo mês* ou *Todo ano*) e os **Detalhes**.
2. **Gravar**: a tarefa entra na lista e a ficha fecha.
3. Para ver ou mudar uma tarefa, dê **duplo clique** (ou **Enter**) nela, ou selecione-a e clique em **Abrir**. **Fechar** (ou **Esc**) sai sem gravar.
4. Feita a tarefa, selecione-a e clique em **Concluir** (na lista ou na ficha). Se ela se repete, a próxima é criada sozinha com o novo vencimento (mesmo dia do mês seguinte ou do ano seguinte; no mês mais curto, o último dia).
5. **Excluir** apaga a tarefa (pede confirmação).

Use a lista no topo para ver as tarefas de um cliente ou de todos, e **Só as em aberto** para esconder as concluídas e canceladas. Tarefas vencidas e não concluídas aparecem como **ATRASADA**.

## Protocolos de documentos

**Para que serve:** registrar a entrega de documentos ao cliente (guias, livros, relatórios) e o recebimento de documentos dele, com número sequencial.
**Onde fica:** *Organização › Protocolos de documentos*.

A tela mostra a **lista dos protocolos** registrados (número, data, cliente, tipo, documento, pessoa e quem registrou).

1. Clique em **Registrar protocolo**: a ficha abre numa janela por cima da lista.
2. Informe o **Cliente**, a **Data**, o **Tipo** (*Entrega ao cliente* ou *Recebido do cliente*), o **Documento**, a **Pessoa** (quem recebeu ou entregou) e, se quiser, uma **Observação**.
3. **Registrar**: o sistema dá o próximo número do escritório, grava quem registrou e fecha a ficha. **Cancelar** (ou **Esc**) fecha sem registrar.
4. Para apagar um protocolo, selecione-o na lista e clique em **Excluir**.

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

*Cadastros › Parâmetros* traz atalhos para o cadastro de clientes, os honorários, o gerador de contratos, as tarefas e os **Menus do programa** (que ficam em *Configurações › Menus do programa*; veja [Menus do programa](02-cadastros-comuns.md#menus-do-programa)).
