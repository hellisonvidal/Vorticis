# ERP

Módulo **Vorticis ERP** (ícone verde-petróleo): as rotinas do dia a dia da empresa. Ele usa os mesmos dados do Fiscal: produtos, notas fiscais, contas a pagar e a receber (duplicatas) e conciliação são as mesmas telas e os mesmos registros. Clientes e fornecedores vêm de *Cadastros* (comuns a todos os módulos).

## Painel do empresário

**Para que serve:** ver a saúde do negócio numa tela só.
**Onde fica:** *Relatórios › Painel do empresário* (atalho **Ctrl+P**).

Escolha o **Mês de referência**. O painel mostra: vendas e compras do mês (notas fiscais), quanto há a receber e a pagar (e quanto já venceu ou vence em 7 dias), o saldo previsto, o valor do estoque pelo custo médio, os produtos abaixo do mínimo, os pedidos em aberto, os 5 maiores clientes, os 5 produtos mais vendidos e o fluxo previsto das próximas 8 semanas (a receber − a pagar). A frase no topo resume se as vendas superaram as compras.

## Pedidos de venda e de compra

**Onde fica:** *Vendas › Pedidos de venda* (atalho **Ctrl+V**) e *Compras › Pedidos de compra*.

1. **Novo pedido**. Escolha o **Cliente** (venda) ou o **Fornecedor** (compra), a **Data** e as **Parcelas** (quantidade, intervalo em dias e, se quiser, o **1º vencimento**).
2. Em **Itens**, escolha o **Produto** (o saldo em estoque aparece ao lado do nome), informe **Qtde**, **Unitário** (na venda, vem o preço de venda; na compra, o custo médio) e **Desconto**, e clique em **Incluir item**. **Remover** tira o item selecionado.
3. **Gravar pedido**. O número é dado pelo sistema. Enquanto estiver *Em aberto*, o pedido pode ser alterado ou cancelado (**Cancelar pedido**).
4. Para concluir, preencha **Nota fiscal** (número, série e, se tiver, a chave):
   - Venda: **Faturar pedido** grava a nota fiscal de saída no Fiscal, gera as contas a receber pelas parcelas e baixa o estoque.
   - Compra: **Receber mercadorias** grava a nota de entrada do fornecedor, gera as contas a pagar e dá entrada no estoque pelo custo do pedido.
5. Depois de faturado ou recebido, o pedido não muda mais. Para corrigir a nota, use *Notas fiscais de saída/entrada*.

**Importante:** o ERP ainda não transmite a NF-e para a SEFAZ (*Emissão de NF-e na SEFAZ — em breve*). Emita a nota no seu emissor e use o mesmo número aqui. Para notas de compra com XML, prefira *Compras › Importação de XML*.

## Estoque

**Onde fica:** *Estoque › Posição e movimentos*.

- A lista mostra, por produto, o **saldo**, o **mínimo**, o **custo médio** (média das entradas), o **valor** em estoque e o **preço de venda**. **REPOR** aparece quando o saldo está abaixo do mínimo.
- Selecione o produto para ver os **movimentos** (entradas das compras, saídas das vendas e ajustes).
- **Preço e estoque mínimo:** informe e clique em **Gravar**.
- **Ajuste de estoque (inventário):** informe a data, a quantidade (positiva para entrada, negativa para saída), o custo unitário e o motivo, e clique em **Registrar ajuste**.

## Financeiro

**Onde fica:** *Financeiro*.

- **Contas a pagar** e **Contas a receber** são telas separadas: cada uma mostra só as duplicatas do seu tipo, com inclusão, alteração, baixa e estorno de baixa (os mesmos passos da tela de Duplicatas do [Fiscal](05-fiscal.md#duplicatas-contas-a-pagar-e-a-receber)).
- **Conciliação bancária**: casa o extrato do banco com as baixas.

O ERP não mostra o calendário de obrigações (é rotina do escritório contábil, nos módulos Contábil, Fiscal e Folha).

## Relatórios

**Onde fica:** *Relatórios*. Todos aparecem na tela e exportam em PDF, Excel e CSV.

- **Relatório de vendas** e **Relatório de compras**: as notas autorizadas de saída ou de entrada do período, agrupadas por nota, por cliente/fornecedor ou por mês, com valor total e tributos.
- **Relatório de recebimentos**: os recebimentos (baixas das contas a receber) do período, com juros, multa, descontos e a conta usada.
- **Financeiro**: *Relatório de contas a pagar*, *Relatório de contas a receber*, *Posição por vencimento* e *Relatório de pagamentos*.

## Telas do Fiscal no ERP

*Produtos*, *Notas fiscais de saída e de entrada*, *Importação de XML*, *Contas a pagar*, *Contas a receber*, *Conciliação bancária* e os relatórios de vendas, compras, recebimentos e do *Financeiro* são as telas do módulo Fiscal; veja o [manual do Fiscal](05-fiscal.md).
