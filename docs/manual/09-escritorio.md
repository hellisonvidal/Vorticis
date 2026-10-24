# Escritório

Módulo **Vorticis Escritório** (ícone azul): o gerenciador do escritório contábil. Ele tem **cadastros próprios**: os clientes e fornecedores do escritório ficam só aqui e **não aparecem** no Contábil, no Fiscal, na Folha, no ERP nem no Condomínio (e os cadastros desses módulos não aparecem aqui). A empresa escolhida no topo é o **próprio escritório**: cadastre-o em *Cadastros › Empresas* marcando o módulo **Escritório**.

## Clientes e fornecedores do escritório

**Para que serve:** cadastrar quem o escritório atende (clientes) e de quem compra (fornecedores).
**Onde fica:** *Cadastros › Clientes e fornecedores do escritório*.

1. Clique em **Incluir**; o código é sugerido.
2. Informe o **nome**, o **CNPJ/CPF** (conferido), marque **Cliente**, **Fornecedor** ou os dois, e, se quiser, regime, contato, e-mail, telefone, endereço, município e UF.
3. **Gravar**. Cliente ou fornecedor com tarefas, honorários, protocolos ou contratos não pode ser excluído: desmarque **Ativo**.

## Carteira de clientes

**Para que serve:** ver todos os clientes do escritório numa só lista.
**Onde fica:** *Escritório › Carteira de clientes*.

A lista mostra código, CNPJ/CPF, nome, regime, tarefas em aberto (e quantas estão atrasadas), honorário mensal, honorários em aberto e o contato. **Atualizar** relê os dados. Só aparecem os clientes ativos.

## Tarefas e prazos

**Para que serve:** controlar o que precisa ser feito para cada cliente, com prazo e responsável.
**Onde fica:** *Escritório › Tarefas e prazos* (atalho **Ctrl+T**).

1. **Nova tarefa**, escolha o **Cliente** (ou deixe *tarefa interna do escritório*), preencha **Tarefa**, **Vencimento**, **Responsável**, **Prioridade**, **Situação** e, se for o caso, **Repetir** (*Todo mês* ou *Todo ano*) e os **Detalhes**.
2. **Gravar**.
3. Feita a tarefa, selecione-a e clique em **Concluir**. Se ela se repete, a próxima é criada sozinha com o novo vencimento (mesmo dia do mês seguinte ou do ano seguinte; no mês mais curto, o último dia).
4. **Excluir** apaga a tarefa (pede confirmação).

Use a lista no topo para ver as tarefas de um cliente ou de todos, e **Só as em aberto** para esconder as concluídas e canceladas. Tarefas vencidas e não concluídas aparecem como **ATRASADA**.

## Protocolo de documentos

**Para que serve:** registrar a entrega de documentos ao cliente (guias, livros, relatórios) e o recebimento de documentos dele, com número sequencial.
**Onde fica:** *Escritório › Protocolo de documentos*.

1. Informe **Data**, **Tipo** (*Entrega ao cliente* ou *Recebido do cliente*), **Documento**, **Pessoa** (quem recebeu ou entregou), o **Cliente** e, se quiser, uma **Observação**.
2. **Registrar protocolo**: o sistema dá o próximo número do escritório e grava quem registrou.

## Gerador de contratos

**Para que serve:** montar o contrato de prestação de serviços contábeis com o cliente, ou o contrato de fornecimento com o fornecedor, já com os dados das partes, e imprimir para assinatura.
**Onde fica:** *Contratos › Gerador de contratos (clientes e fornecedores)*.

1. **Novo contrato** e escolha o **Tipo**: *Cliente* (o cliente é o contratante e o escritório, o contratado) ou *Fornecedor* (o escritório é o contratante).
2. Escolha a **Parte** (do cadastro próprio do escritório), confira o **Objeto**, informe o **Valor**, a periodicidade (*Mensal*, *Anual* ou *Parcela única*), o **dia** do vencimento, a **vigência** (sem fim = prazo indeterminado), o índice de **Reajuste** e o **Foro** (em branco: a cidade do escritório).
3. **Gerar texto**: o contrato sai com a qualificação das partes e as cláusulas de objeto, preço, reajuste, vigência, obrigações, rescisão e foro. Ajuste o texto se precisar.
4. **Gravar**: o contrato recebe o próximo número do escritório.
5. Selecione-o na lista e clique em **PDF para assinatura**: o arquivo traz o texto e as linhas de assinatura das duas partes.

Para encerrar um contrato sem perder o histórico, desmarque **Ativo** e grave. O valor dos honorários cobrados mês a mês fica em [Honorários e cobranças](#honorarios-e-cobrancas).

## Honorários e cobranças

**Onde fica:** *Contratos › Honorários e cobranças*.

### Honorário do cliente
1. Escolha o **cliente** na lista.
2. Informe **Valor mensal**, **Dia do vencimento** (1 a 31; nos meses mais curtos vale o último dia), **Início**, **Fim** (se houver) e o índice de **Reajuste**.
3. **Gravar honorário**. Cada cliente tem um honorário; gravar de novo altera o existente.

### Cobranças do mês
1. Escolha a **Competência** (mês/ano) e clique em **Gerar cobranças do mês**: o sistema cria uma cobrança para cada cliente com honorário vigente, com o vencimento no dia combinado. Rodar de novo não duplica.
2. Para registrar o pagamento, selecione a cobrança, confira **Recebido em** e **Valor** e clique em **Receber**.
3. **Reabrir** desfaz o recebimento; **Cancelar cobrança** cancela uma cobrança em aberto.

Marque **Todas em aberto** para ver todas as cobranças não recebidas, de qualquer mês. Cobranças vencidas aparecem como **VENCIDA**. A linha de totais mostra o total, o recebido e o que está em aberto.

## Relatórios de honorários

**Onde fica:** *Relatórios › Honorários*. Os dois aparecem na tela e exportam em PDF, Excel e CSV.

- **Relatório de contratos**: os contratos gravados no gerador, com número, tipo, cliente ou fornecedor, objeto, valor, periodicidade, vigência e situação; no fim, o total mensal dos contratos ativos de clientes e de fornecedores. Marque **Só os ativos** para esconder os encerrados.
- **Relatório de honorários**: as cobranças das competências escolhidas (mês/ano inicial e final), com cliente, vencimento, valor, situação e recebimento; no fim, o total, o que está em aberto e o recebido.

## Parâmetros

*Cadastros › Parâmetros* traz atalhos para o cadastro de clientes e fornecedores, os honorários, o gerador de contratos e as tarefas.
