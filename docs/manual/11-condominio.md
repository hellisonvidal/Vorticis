# Condomínio

Módulo **Vorticis Condomínio** (ícone vermelho-rubi): administração de condomínios. A empresa escolhida no topo é o **condomínio** (cadastre-o em *Cadastros › Empresas* com o CNPJ do condomínio e marque o módulo **Condomínio**). Proprietários, inquilinos e fornecedores são cadastrados em *Cadastros › Clientes* e *Fornecedores* (cadastro comum a todos os módulos).

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

## Áreas comuns e reservas

**Onde fica:** *Cadastros › Áreas comuns e reservas*.

1. Cadastre as áreas (salão de festas, churrasqueira...) com capacidade e taxa da reserva.
2. Selecione a área na lista, escolha a **Unidade** e a data e clique em **Reservar**. Cada área aceita uma reserva por dia; o sistema avisa se a data já estiver ocupada.
3. **Cancelar reserva** libera a data.

## Ocorrências e assembleias

**Onde fica:** *Administração › Ocorrências e assembleias*.

Registre reclamações, manutenções, advertências, multas, avisos e assembleias (com a pauta ou a ata na descrição), ligados ou não a uma unidade. Marque **Resolvida / encerrada** quando concluir.

## Prestação de contas e inadimplência

**Onde fica:** *Relatórios › Prestação de contas e inadimplência*.

Escolha o **Mês**. O quadro mostra o que foi emitido, o arrecadado (cotas pagas no mês), as multas e juros recebidos, as despesas pagas por categoria, o **saldo do mês** e a inadimplência acumulada. Abaixo, a lista dos inadimplentes de hoje, com multa e juros atualizados.

## Parâmetros

*Cadastros › Parâmetros*: **dia de vencimento** das cotas (1 a 28), **multa por atraso** (até 2%) e **juros de mora ao mês** (sem previsão na convenção, 1%).
