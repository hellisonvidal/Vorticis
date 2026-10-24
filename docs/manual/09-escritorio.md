# Escritório

Módulo **Vorticis Escritório** (ícone azul): organiza o escritório contábil. Os cadastros (*Cadastros › Empresas, Contadores, Sócios, Clientes e Fornecedores*) são os mesmos de todos os módulos: o que você cadastra aqui aparece no Contábil, no Fiscal, na Folha, no ERP e no Condomínio, e vice-versa. Em *Cadastros › Empresas*, marque em **Módulos** onde cada empresa deve aparecer (marque **Escritório** para ela entrar na carteira).

## Carteira de clientes

**Para que serve:** ver todas as empresas do escritório numa só lista.
**Onde fica:** *Escritório › Carteira de clientes*.

A lista mostra CNPJ, razão social, regime, tarefas em aberto (e quantas estão atrasadas), honorário mensal, honorários em aberto, o vencimento do certificado digital mais próximo e os módulos em que a empresa está ativa. **Atualizar** relê os dados. Só aparecem as empresas a que você tem acesso.

## Tarefas e prazos

**Para que serve:** controlar o que precisa ser feito para cada cliente, com prazo e responsável.
**Onde fica:** *Escritório › Tarefas e prazos* (atalho **Ctrl+T**).

1. Escolha a empresa no topo (a tarefa nova vai para ela).
2. **Nova tarefa**, preencha **Tarefa**, **Vencimento**, **Responsável**, **Prioridade**, **Situação** e, se for o caso, **Repetir** (*Todo mês* ou *Todo ano*) e os **Detalhes**.
3. **Gravar**.
4. Feita a tarefa, selecione-a e clique em **Concluir**. Se ela se repete, a próxima é criada sozinha com o novo vencimento (mesmo dia do mês seguinte ou do ano seguinte; no mês mais curto, o último dia).
5. **Excluir** apaga a tarefa (pede confirmação).

Marque **Todas as empresas** para ver as tarefas de todos os clientes e **Só as em aberto** para esconder as concluídas e canceladas. Tarefas vencidas e não concluídas aparecem como **ATRASADA**.

## Protocolo de documentos

**Para que serve:** registrar a entrega de documentos ao cliente (guias, livros, relatórios) e o recebimento de documentos dele, com número sequencial.
**Onde fica:** *Escritório › Protocolo de documentos*.

1. Escolha a empresa no topo.
2. Informe **Data**, **Tipo** (*Entrega ao cliente* ou *Recebido do cliente*), **Documento**, **Pessoa** (quem recebeu ou entregou) e, se quiser, uma **Observação**.
3. **Registrar protocolo**: o sistema dá o próximo número da empresa e grava quem registrou.

## Honorários: contratos e cobranças

**Onde fica:** *Honorários › Contratos e cobranças*.

### Contrato da empresa
1. Escolha a empresa no topo.
2. Informe **Valor mensal**, **Dia do vencimento** (1 a 31; nos meses mais curtos vale o último dia), **Início**, **Fim** (se houver) e o índice de **Reajuste**.
3. **Gravar contrato**. Cada empresa tem um contrato; gravar de novo altera o existente.

### Cobranças do mês
1. Escolha a **Competência** (mês/ano) e clique em **Gerar cobranças do mês**: o sistema cria uma cobrança para cada empresa com contrato vigente, com o vencimento no dia do contrato. Rodar de novo não duplica.
2. Para registrar o pagamento, selecione a cobrança, confira **Recebido em** e **Valor** e clique em **Receber**.
3. **Reabrir** desfaz o recebimento; **Cancelar cobrança** cancela uma cobrança em aberto.

Marque **Todas em aberto** para ver todas as cobranças não recebidas, de qualquer mês. Cobranças vencidas aparecem como **VENCIDA**. A linha de totais mostra o total, o recebido e o que está em aberto.

## Parâmetros

*Cadastros › Parâmetros* traz atalhos para os contratos de honorários, as tarefas e o cadastro de empresas.
