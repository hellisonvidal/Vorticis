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

## Veículos e motoristas

**Onde fica:** *Cadastros › Veículos e motoristas*.

Veículos: **Placa** (ABC1234 ou ABC1D23), RENAVAM, tipo, UF, tara e capacidade em kg; **Gravar veículo**. Motoristas: nome, **CPF** (conferido), CNH e validade; **Gravar motorista**. Selecione na lista para alterar.

## Conhecimentos e manifestos (CT-e e MDF-e)

**Onde fica:** *Transporte › Conhecimentos e manifestos*.

Registre os CT-e e MDF-e emitidos para controlar fretes e viagens: **Tipo**, número e série, emissão, chave, **Remetente** e **Destinatário** (CT-e), UFs de início e fim, **Veículo** (obrigatório no MDF-e), **Motorista**, valores do frete e da carga, peso, as chaves das NF-e transportadas (uma por linha) e a **Situação** (digitado, autorizado, encerrado, cancelado). **Gravar**. A transmissão à SEFAZ ainda não é feita pelo Vorticis (*em breve*).

## Telas do Fiscal no ERP

*Produtos*, *Notas fiscais de saída e de entrada*, *Importação de XML*, *Contas a pagar e a receber*, *Conciliação bancária* e os relatórios do *Financeiro* são as telas do módulo Fiscal; veja o [manual do Fiscal](05-fiscal.md).
