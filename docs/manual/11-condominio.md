# Condomínio

Módulo **Vorticis Condomínio** (ícone vermelho-rubi): administração de condomínios. A empresa escolhida no topo é o **condomínio** (cadastre-o em *Cadastros › Condomínios* com o CNPJ do condomínio e marque o módulo **Condomínio**). Proprietários, inquilinos e fornecedores são cadastrados em *Cadastros › Clientes* e *Fornecedores* (cadastro comum a todos os módulos).

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Ordem dos cadastros

Para as cobranças e os rateios funcionarem, cadastre nesta ordem:

1. **O condomínio:** *Cadastros › Condomínios* (é o cadastro de empresas, com o CNPJ do condomínio e o módulo **Condomínio** marcado). Escolha-o no topo da tela.
2. **Condôminos:** proprietários e inquilinos em *Cadastros › Clientes* (opcional, mas sem eles a cobrança sai sem responsável).
3. **Unidades:** *Cadastros › Unidades e condôminos*, com a **fração ideal** de cada uma (obrigatória e maior que zero) e o proprietário/inquilino.
4. **Parâmetros:** *Cadastros › Parâmetros*, com o dia de vencimento, a multa e os juros.
5. **Taxas:** *Financeiro › Taxas condominiais* (taxa ordinária, fundo de reserva...).
6. Depois: **Cobranças** do mês (*Financeiro › Cobranças e recebimentos*) e, quando houver um valor extra a dividir, o **Rateio entre unidades**.

## Unidades e condôminos

**Onde fica:** *Cadastros › Unidades e condôminos*.

1. **Nova unidade**: informe **Bloco** (se houver) e **Número**, o **Tipo** (apartamento, casa, sala, loja, vaga...), a **Fração ideal** (como está na convenção, ex.: 0,0125) e a área.
2. Escolha o **Proprietário** e, se houver, o **Inquilino**. Marque **Cobrar do inquilino** para as cobranças saírem no nome dele.
3. **Gravar**. Desmarque **Unidade ativa** para tirar a unidade das próximas cobranças.

A linha de totais mostra quantas unidades estão ativas e a soma das frações (o rateio funciona com qualquer soma, mas o normal é 1 ou 100).

## Taxas condominiais

**Onde fica:** *Financeiro › Taxas condominiais*.

Cadastre cada taxa (taxa ordinária, fundo de reserva, taxa extra, gás...) com o **Rateio**:
- **Fração ideal**: o **Valor** é o total a dividir; cada unidade paga pela sua fração (o centavo do arredondamento vai para a maior fração).
- **Partes iguais**: o total é dividido igualmente entre as unidades ativas.
- **Valor fixo por unidade**: cada unidade paga o valor informado.

Informe o **Início** e, se for temporária (ex.: taxa extra em 10 meses), o **Fim**. **Gravar**.

## Cobranças e recebimentos

**Onde fica:** *Financeiro › Cobranças e recebimentos* (atalho **Ctrl+B**).

1. Escolha a **Competência** e clique em **Gerar cobranças do mês**: cada unidade ativa recebe uma cobrança com a soma das taxas vigentes, vencendo no dia definido em *Cadastros › Parâmetros*. A coluna **Composição** mostra cada taxa. Rodar de novo não duplica.
2. Para registrar o pagamento, selecione a cobrança e informe **Pago em**: a **Multa** e os **Juros** de atraso são calculados sozinhos (multa de até 2% — limite do Código Civil, art. 1.336 — e juros por dia de atraso). Ajuste se precisar (a multa não passa do limite legal), informe um **Desconto** se houver, e clique em **Receber**.
3. **Estornar recebimento** volta a cobrança para em aberto; **Cancelar cobrança** cancela uma cobrança em aberto.

Marque **Todas em aberto** para ver todas as cobranças não pagas. As vencidas aparecem como **VENCIDA**.

## Despesas do condomínio

**Onde fica:** *Financeiro › Despesas do condomínio*.

Informe data, vencimento, **categoria** (pessoal, água, energia, manutenção, limpeza, segurança...), descrição, fornecedor e valor. Quando pagar, informe **Pago em** e grave de novo. Só as despesas pagas entram na prestação de contas do mês.

## Rateio entre unidades

**Onde fica:** *Financeiro › Rateio entre unidades*.

Use para dividir um valor avulso entre as unidades: uma obra, uma despesa extra, a recomposição do fundo de reserva.

1. Informe a **Descrição** (é o texto que aparece na cobrança, ex.: "Rateio da pintura da fachada"), o **Valor total**, o **Critério** (fração ideal ou partes iguais) e a **Competência** em que vai ser cobrado.
2. Clique em **Prévia**: a lista mostra quanto cabe a cada unidade ativa e o total rateado (os centavos de arredondamento vão para uma das unidades, para fechar o total exato).
3. Conferido, clique em **Lançar nas cobranças**. O valor de cada unidade entra como um item da cobrança da competência: se a unidade já tem cobrança em aberto, o item é somado a ela; se ainda não tem, a cobrança é criada com o vencimento padrão.

**Atenção:** cobranças da competência que já estão pagas ou canceladas não recebem o rateio; o sistema avisa quais unidades ficaram de fora. Confira o resultado em *Financeiro › Cobranças e recebimentos*.

## Áreas comuns e reservas

**Onde fica:** *Cadastros › Áreas comuns e reservas*.

1. Cadastre as áreas (salão de festas, churrasqueira...) com capacidade e taxa da reserva.
2. Selecione a área na lista, escolha a **Unidade** e a data e clique em **Reservar**. Cada área aceita uma reserva por dia; o sistema avisa se a data já estiver ocupada.
3. **Cancelar reserva** libera a data.

## Ocorrências e assembleias

**Onde fica:** *Administração › Ocorrências e assembleias*.

Registre reclamações, manutenções, advertências, multas, avisos e convocações de assembleia, ligados ou não a uma unidade. Marque **Resolvida / encerrada** quando concluir. A ata da assembleia é feita em *Relatórios › Atas de Assembleia*.

## Prestação de contas e inadimplência

**Onde fica:** *Relatórios › Prestação de contas e inadimplência*.

Escolha o **Mês**. O quadro mostra o que foi emitido, o arrecadado (cotas pagas no mês), as multas e juros recebidos, as despesas pagas por categoria, o **saldo do mês** e a inadimplência acumulada. Abaixo, a lista dos inadimplentes de hoje, com multa e juros atualizados.

## Atas de Assembleia

**Onde fica:** *Relatórios › Atas de Assembleia*.

1. Clique em **Nova**. O número da assembleia é dado automaticamente ao gravar (1, 2, 3... em cada condomínio).
2. Preencha a **data**, o **horário** (ex.: 19:30), o tipo (**Ordinária** ou **Extraordinária**), a convocação (**primeira** ou **segunda**), o **local**, o **presidente** e o **secretário(a)** da mesa e quantas **unidades** estavam presentes.
3. Escreva a **Ordem do dia** (pauta, um item por linha) e as **Deliberações** (o que foi decidido e o resultado das votações). Clique em **Gravar**.
4. Com a assembleia selecionada na lista, clique em **Gerar ata (PDF)**. A ata traz a abertura (data, local, convocação, mesa e presença), a ordem do dia, as deliberações, o encerramento e o **campo de assinaturas** do presidente, do secretário(a) e do síndico (o síndico vem de *Cadastros › Condomínios*, aba **Síndico**).
5. Marque **Incluir lista de presença** para sair, numa página à parte, a lista de todas as unidades com o condômino e uma linha para a assinatura de cada um.

O arquivo fica na pasta de arquivos gerados; o caminho aparece na mensagem abaixo do botão. Para corrigir, selecione a assembleia, altere e grave de novo; **Excluir** apaga o registro.

## Síndico

**Onde fica:** *Cadastros › Condomínios*, aba **Síndico** (só no módulo Condomínio, com o condomínio selecionado no topo).

Informe o nome, o CPF, o e-mail, o telefone e o período do mandato do síndico. Em **Assinatura dos livros contábeis e fiscais**:

- **Assina os livros contábeis e fiscais (assinatura normal):** o síndico assina os livros e relatórios do condomínio no lugar do sócio/responsável (o CPF passa a ser obrigatório).
- **Assinatura digital (certificado ICP-Brasil):** além de assinar, ele assina com certificado digital; a linha de assinatura sai com a indicação "assinado digitalmente".

Clique em **Gravar**. **Excluir** apaga o cadastro do síndico.

## Parâmetros

*Cadastros › Parâmetros*: **dia de vencimento** das cotas (1 a 28), **multa por atraso** (até 2%) e **juros de mora ao mês** (sem previsão na convenção, 1%).

O módulo Condomínio não tem o calendário de obrigações (menu Obrigações): as obrigações fiscais do condomínio ficam no Fiscal e na Folha.
