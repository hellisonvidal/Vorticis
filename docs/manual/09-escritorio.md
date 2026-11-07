# Escritório

Módulo **Vorticis Escritório** (ícone azul): o gerenciador **do próprio escritório contábil**. O Escritório **não é o cadastro geral de empresas**: o cadastro de **clientes** serve para acompanhar as empresas contratadas, e é a partir dele que o trabalho nas empresas começa. As empresas usadas nos demais módulos são os **clientes** cadastrados aqui: o cliente é levado, já com os dados da Receita, para os módulos que ele contratou (Contabilidade, Fiscal, Folha, Condomínio, Gerencial). O Escritório não tem cadastro de fornecedores. O dono das tarefas, protocolos, honorários e contratos é sempre o **próprio escritório**.

### Escolher o cliente no topo da tela

No topo da tela, em **Cliente:**, ficam os clientes de *Cadastros › Clientes* (o escritório, 0001, é o primeiro), com **Todos os clientes** no alto da lista:
- Escolha um cliente para que as telas do Escritório mostrem **só o que é dele**: as tarefas (*Tarefas e prazos*), os protocolos, os contratos, os honorários e a carteira. Em *Cadastros › Clientes*, a ficha dele já vem selecionada.
- Escolha **Todos os clientes** para voltar a ver tudo.
- Cliente incluído, alterado ou excluído em *Cadastros › Clientes* já aparece (ou sai) da lista na hora.

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Primeiro uso

Na instalação, o Escritório é o módulo aberto ao final. Depois de entrar com o administrador, se ainda não houver escritório cadastrado, a tela *Cadastros › Dados do escritório* abre sozinha: cadastre o seu escritório contábil e, em seguida, as empresas atendidas em *Cadastros › Clientes*, marcando os módulos de cada uma. O próprio escritório já entra como o **cliente 0001**, o primeiro da lista; os clientes seguintes ficam com 0002, 0003...

O Escritório também administra a suíte: **usuários e controle de acesso** (*Configurações › Usuários e controle de acesso*) e o **banco de dados** (*Configurações › Banco de dados*: conexão, backup e restauração, senha do administrador e limpeza). Com o Escritório instalado, essas funções saem dos outros módulos.

## Dados do escritório

**Para que serve:** cadastrar o próprio escritório contábil (o contador), que é o escolhido no topo da tela e o dono das tarefas, protocolos, honorários e contratos.
**Onde fica:** *Cadastros › Dados do escritório* (primeiro item do menu).

Esta tela **não é o cadastro geral de empresas**: mostra só o escritório, sem plano de contas, sócios, contadores, módulos ou primeiro exercício. Digite o **CNPJ**, clique em **Buscar dados na Receita**, confira os dados e clique em **Salvar**.

- O escritório recebe o **código 0001** (o primeiro da numeração das empresas) e entra **sozinho no cadastro de clientes como o cliente 0001**, o primeiro da lista, com os mesmos dados.
- É do **cadastro de clientes** que o sistema todo é alimentado: os próximos clientes ficam com **0002**, **0003** e assim por diante, e cada um vai para os módulos que contratou.
- Para fazer nos módulos a contabilidade, a folha ou o fiscal do próprio escritório, abra o cliente **0001** e marque os módulos na aba **Contrato**: vale para a própria empresa do escritório, sem criar outra.
- O cliente 0001 não pode ser excluído (para não usá-lo, desmarque **Ativo**).

### Empresas cadastradas direto nos módulos
Uma empresa incluída no cadastro de empresas do Contábil, do Fiscal ou da Folha fica ativa só naquele módulo. Para acompanhá-la no Escritório (tarefas, honorários) e escolher os outros módulos, cadastre-a como cliente com o **mesmo CNPJ**: o cliente fica ligado à empresa que já existe (nada é duplicado) e os módulos marcados no contrato passam a valer para ela.

## Clientes

**Para que serve:** cadastrar quem o escritório atende, levar o cliente para os módulos do contrato e controlar os honorários dele.
**Onde fica:** *Cadastros › Clientes*.

A tela mostra **todos os clientes cadastrados**, em ordem de código — o primeiro é o próprio escritório, **0001** — numa lista (código, razão social, nome fantasia, CNPJ/CPF, regime, município, contato, módulos contratados e situação), com o filtro por código, nome ou documento. A **ficha do cliente** abre numa janela por cima da lista:
- **Incluir** abre a ficha em branco; **Alterar** abre a do cliente selecionado já liberada para edição;
- **duplo clique** (ou **Enter**) num cliente, ou o botão **Abrir ficha**, mostra a ficha só para consulta — nela, **Alterar** libera a edição e **Fechar** volta à lista;
- na edição, **Salvar** grava e fecha a ficha; **Cancelar** (ou fechar a janela, ou **Esc**) desiste do que foi digitado.

### Dados cadastrais
1. Clique em **Incluir**; o código é sugerido.
2. Digite o **CNPJ/CPF** e clique em **Buscar na Receita**: razão social, nome fantasia, natureza jurídica, CNAE, início das atividades, endereço, município (com o código IBGE), e-mail e telefone são preenchidos, como no cadastro de empresas. O regime vem sugerido quando o cliente é do Simples ou MEI; nos demais, escolha **Regime tributário**.
3. Complete o que faltar (contato, inscrição estadual, observação).
4. **Salvar**. Cliente com tarefas, honorários, protocolos ou contratos não pode ser excluído: desmarque **Ativo**.

### Contrato

A aba **Contrato** tem o honorário do cliente e os módulos contratados.

**Honorário:**
1. Informe o **Valor do honorário** (em reais, com os centavos), o **Dia do vencimento** (1 a 31; nos meses mais curtos vale o último dia) e a **Periodicidade**:
   - **Mensal**: cobrado todo mês;
   - **Anual**: cobrado uma vez por ano, no mês em que o honorário começou;
   - **Especial**: sem cobrança automática; as parcelas são lançadas em *Financeiro › Honorários*, no botão **Novo honorário**.
2. **Salvar** grava o honorário junto com o cadastro do cliente. Para tirar o honorário do contrato, apague o valor e salve (as cobranças já lançadas continuam).

As cobranças, as baixas e as cobranças avulsas ficam em [Financeiro › Honorários](#honorarios-e-cobrancas).

**Módulos contratados:**
1. Marque os módulos contratados: **Contabilidade**, **Fiscal**, **Folha de pagamento**, **Condomínio**, **Gerencial**.
2. **Gravar**. Se o cliente ainda não é empresa, o sistema avisa: *"Este cadastro será compartilhado com os demais módulos do Vorticis (Contábil, Fiscal, Folha, Gerencial...). Deseja continuar?"* — confirme em **Continuar**. O cliente é cadastrado como empresa nesses módulos, com o mesmo código (sequência) em todos eles — o código aparece na própria aba e na mensagem depois de gravar, com os dados da aba Dados cadastrais (com Contabilidade marcada, o plano de contas modelo é importado). Se já existir empresa com o mesmo CNPJ, ela é atualizada e passa a aparecer também nos módulos marcados.
3. Para isso, a aba Dados cadastrais precisa ter CNPJ, regime, natureza jurídica, CNAE, município IBGE, UF e início das atividades; o botão **Buscar na Receita** traz quase tudo. Só o **administrador** cadastra empresas nos módulos.

Nesta aba também fica o campo **Logo da empresa** (**Carregar logo...**, **Remover logo** e **Usar a logo nos relatórios**), igual ao de *Cadastros › Empresas*; a logo só é gravada quando o cliente já é empresa (marque um módulo e grave).

Esta aba é **o único lugar** em que se escolhe em quais módulos a empresa é usada: o Escritório gerencia tudo. Nos outros módulos o cadastro de empresas não mostra essa escolha (a empresa incluída lá fica ativa só naquele módulo e no Escritório), e eles também deixam de perguntar, ao entrar, se querem ativar empresas.

Desmarcar um módulo aqui tira a empresa da lista daquele módulo, mas não apaga nada do que já foi feito nele: basta marcar de novo para ela voltar.

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

A tela mostra a **lista dos contratos gravados**. A **ficha do contrato** abre numa janela por cima da lista: **Novo contrato**, **Abrir ficha**, duplo clique ou **Enter** a abrem; **Gravar** grava e fecha a janela, e **Cancelar**, **Esc** ou o **X** fecham sem gravar.

1. **Novo contrato**: o **Tipo** é *Cliente* (o cliente é o contratante e o escritório, o contratado).
2. Escolha a **Parte** (do cadastro próprio do escritório), confira o **Objeto**, informe o **Valor**, a periodicidade (*Mensal*, *Anual* ou *Parcela única*), o **dia** do vencimento, a **vigência** (sem fim = prazo indeterminado), o índice de **Reajuste** e o **Foro** (em branco: a cidade do escritório).
3. **Gerar texto** (botão na ficha): o contrato sai com a qualificação das partes e as cláusulas de objeto, preço, reajuste, vigência, obrigações, rescisão e foro. Ajuste o texto se precisar.
4. **Gravar**: o contrato recebe o próximo número do escritório e a janela fecha.
5. Selecione-o na lista da tela e exporte:
   - **Exportar PDF**: o texto e as linhas de assinatura das duas partes. Marque antes **Assinar digitalmente** para assinar o PDF com o certificado digital padrão do contador (ou do responsável), definido em *Configurações › Certificados digitais*; com certificado A3, o leitor pede o PIN.
   - **Exportar Word**: o mesmo contrato em arquivo .docx, para editar no Word ou no LibreOffice.

A lista de contratos gravados fica nesta tela (não há mais o Relatório de contratos).

Para encerrar um contrato sem perder o histórico, desmarque **Ativo** e grave. O valor dos honorários cobrados fica em [Financeiro › Honorários](#honorarios-e-cobrancas).

## Honorários e cobranças

**Para que serve:** organizar os honorários do escritório: lançar os honorários com as parcelas, as cobranças avulsas de serviços extras e registrar as baixas (recebimentos).
**Onde fica:** *Financeiro › Honorários*.

A tela mostra as cobranças das **competências** escolhidas (*de* e *até*, mês/ano), de todos os clientes ou só do cliente escolhido no topo, em **Cliente:**. Filtre também pela **Situação** (todas, em aberto, vencidas, recebidas, canceladas) e pelo **Tipo** (honorários ou avulsas). A lista mostra o cliente, o tipo, a descrição, a parcela (1/12, 2/12...), a competência, o vencimento, o valor, a situação e a baixa; cobranças vencidas e não pagas aparecem como **VENCIDA**. A linha de totais mostra o total, o recebido, o que está em aberto e o vencido.

### Novo honorário
1. Clique em **Novo honorário**. Na janela, escolha o **Cliente** (o escolhido no topo já vem marcado; se ele tem honorário na aba Contrato, o valor e o dia vêm preenchidos).
2. Informe a **Competência** (mês/ano) da primeira parcela, o **Valor** (em reais) e o **Dia do vencimento**. Nos meses seguintes o vencimento fica no mesmo dia; nos meses mais curtos, no último dia (31 vira 28 ou 29 em fevereiro, 30 em abril...).
3. Escolha a **Periodicidade** (parcela única, mensal, bimestral, trimestral, semestral ou anual) e quantas **parcelas**: o sistema cria as parcelas seguintes. A **prévia** mostra cada parcela com a competência, o vencimento e o valor antes de lançar.
4. **Lançar**. Se o cliente já tem honorário em alguma das competências, nada é lançado e o aviso mostra os meses: escolha outra competência, menos parcelas, ou lance como cobrança avulsa.

### Cobrança avulsa
Para cobrar um serviço extra (abertura de empresa, declaração de IRPF, consultoria...): clique em **Cobrança avulsa**, escolha o cliente, descreva o serviço em **Descrição** (obrigatória), informe a competência, o valor e o dia do vencimento e, se for parcelado, a periodicidade e as parcelas. **Lançar**. O mesmo cliente pode ter várias cobranças avulsas no mesmo mês.

### Gerar do contrato
**Gerar do contrato** cria, na competência *de*, a cobrança do honorário informado na aba **Contrato** de cada cliente: os mensais todo mês, os anuais só no mês em que o honorário começou (os especiais não). Rodar de novo não duplica.

### Baixas
1. Selecione a cobrança e clique em **Baixar** (ou dê um duplo clique nela).
2. Confira **Recebido em** e o **Valor recebido** (pode ser diferente do valor da cobrança, com juros, multa ou desconto) e, se quiser, escreva uma **Observação** (Pix, boleto, com desconto...).
3. **Confirmar baixa**.

**Estornar baixa** desfaz o recebimento (a cobrança volta a ficar em aberto); **Cancelar cobrança** cancela uma cobrança em aberto; **Excluir** apaga uma cobrança em aberto ou cancelada (a recebida precisa ter a baixa estornada antes).

**Boleto:** o Vorticis ainda não emite boleto. O boleto registrado exige convênio de cobrança com o banco (arquivo CNAB de remessa e retorno ou a API do banco); enquanto isso, informe ao cliente a chave PIX ou os dados bancários do escritório e registre a baixa aqui.

## Relatório de honorários

**Onde fica:** *Relatórios › Relatório de honorários*. Aparece na tela e exporta em PDF, Excel e CSV.

- **Relatório de honorários**: as cobranças das competências escolhidas (mês/ano inicial e final), com cliente, vencimento, valor, situação e recebimento; no fim, o total, o que está em aberto e o recebido.

## Parâmetros

*Cadastros › Parâmetros* traz atalhos para o cadastro de clientes, os honorários, o gerador de contratos, as tarefas e os **Menus do programa** (que ficam em *Configurações › Menus do programa*; veja [Menus do programa](02-cadastros-comuns.md#menus-do-programa)).
