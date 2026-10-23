# Fiscal

Menus do módulo Vorticis Fiscal. As escriturações usam os cadastros de clientes e fornecedores e o plano de contas do módulo Contábil, no mesmo banco.

## Produtos

**Para que serve:** cadastro das mercadorias e serviços, usado na digitação das notas e na EFD.
**Onde fica:** *Cadastros › Produtos*.

1. **Novo** e informe código, descrição, unidade, **NCM**, CEST, código de barras (GTIN/EAN), origem e tipo do item.
2. Informe os padrões fiscais: CFOP e CST de entrada e de saída, e as alíquotas de ICMS, IPI, PIS e COFINS.
3. **Salvar**. O filtro busca por descrição, código, GTIN ou NCM; *Somente ativos* esconde os inativos.

## Documentos de entrada e de saída (digitação)

**Para que serve:** lançar à mão notas que não têm XML.
**Onde fica:** *Lançamentos › Documentos fiscais › Entrada* e *Saída*.

1. Clique em **Nova nota**.
2. **Emissão**: data, modelo, série, número, chave de acesso (opcional) e natureza da operação. Digite o **CNPJ/CPF** do cliente/fornecedor (busca automática, ou **Buscar participante**). Se não existir, informe nome, UF e município para cadastrá-lo na hora.
3. **Itens**: digite o produto (código, descrição ou GTIN; **Buscar produto**), CFOP, quantidade e valor unitário. Em *Detalhes e tributos do item* confira NCM, CST, bases e alíquotas de ICMS, ICMS-ST, IPI, PIS, COFINS e ISS, desconto, frete, seguro e outras despesas. **Calcular tributos** preenche pelas alíquotas do produto.
4. **Adicionar item** (ou **Atualizar item selecionado** / **Remover item selecionado**).
5. Confira o **total informado**: precisa bater com a soma dos itens. **Salvar**.

A lista *Notas digitadas* mostra data, número, participante, valor, origem e se já foi contabilizada. Use **Editar** e **Excluir** (notas já contabilizadas devem ter a contabilização desfeita antes).

## Importação de XML (NF-e, NFC-e, NFS-e)

**Para que serve:** trazer as notas dos arquivos XML, sem digitar.
**Onde fica:** *Documentos fiscais › Importação de XML* (atalho **Ctrl+I**).

### Um ou mais arquivos
Clique em **Importar arquivos...** e escolha os .xml ou um .zip.

### Importação em lote (pasta)
1. Digite ou **Procurar...** a pasta (ex.: `C:\XML\NotasFiscais`).
2. Marque **Incluir subpastas** se for o caso e clique em **Importar pasta**. Também aceita .zip.
3. O painel mostra o resultado por arquivo (importado, duplicado, recusado e o motivo). **Exportar resumo (CSV)...** guarda o relatório; **Cancelar importação** interrompe o lote.

### Regras
- Aceita NF-e e NFC-e (modelos 55 e 65) e NFS-e no padrão nacional (ADN).
- O sentido é identificado pelo **CNPJ da empresa** (e dos estabelecimentos): é entrada se a empresa é o destinatário e saída se é o emitente. Notas de terceiros são recusadas.
- **Duplicidade:** a mesma chave de acesso não é importada duas vezes. Eventos de cancelamento são aplicados, mesmo que cheguem antes da nota.
- Clientes e fornecedores são criados ou vinculados automaticamente.

## Consulta de documentos

**Onde fica:** *Documentos fiscais › Consulta de documentos* (atalho **Ctrl+D**).
Filtre por período, CFOP e texto (número, nome, CNPJ ou chave) e clique em **Consultar**. Selecione uma nota para ver os itens e tributos. **Excluir selecionado** remove a nota (desfaça antes a contabilização, se houver).

## Regras de contabilização

**Para que serve:** ensinar o sistema em quais contas lançar cada tipo de nota.
**Onde fica:** *Documentos fiscais › Regras de contabilização*.

1. **Nova**. Informe o **CFOP** (completo, como 5102, só o prefixo, como 51, ou `NFSE` para notas de serviço; vazio vale para todos) e o **sentido** (entrada ou saída).
2. Informe a **conta de débito** e a **de crédito** e, se quiser, o **participante** e o histórico (aceita as variáveis dos históricos padrão).
3. **Salvar**.

Vale a regra **mais específica** (participante, CFOP completo, prefixo, geral); em empate, a de maior prioridade.

## Contabilizar notas fiscais

**Onde fica:** *Documentos fiscais › Contabilizar notas fiscais*.

1. Escolha o período e clique em **Pré-visualizar**: aparecem as notas pendentes, com o lançamento previsto.
2. Marque as notas e clique em **Contabilizar marcadas**. São gerados os lançamentos no Contábil (documento = número da nota).
3. Para desfazer, marque as notas em *Notas contabilizadas no período* e clique em **Desfazer contabilização das marcadas**: o lançamento é estornado e a nota volta a pendente.

**Erro comum:** nota sem regra aplicável ou período contábil fechado ficam sem contabilizar, com a mensagem na coluna *Situação*.

## Apuração do Simples Nacional (DAS)

**Onde fica:** *Apuração › Simples Nacional (DAS)*.

1. Escolha a **competência**.
2. **Sugerir pelas notas** preenche a receita do mês por atividade (ou digite em *Receitas do mês por atividade*). Use as opções de ICMS-ST, monofásico, ISS retido e exportação, quando houver.
3. O **RBT12** (receita dos 12 meses anteriores) vem do histórico; informe também a *folha 12 meses* para o Fator R (anexo III ou V). Se a empresa é nova, o sistema proporcionaliza.
4. **Calcular**: mostra alíquota efetiva, valor do DAS e a repartição por tributo, com memória de cálculo.
5. **Salvar apuração**. **Contabilizar provisão** lança a despesa e o passivo (precisa das [contas dos tributos](#contas-dos-tributos-provisoes)); **Desfazer provisão** estorna.

**Dica:** preencha antes o [Histórico de receita e folha](#historico-de-receita-e-folha) com os meses anteriores ao uso do sistema. Confira as tabelas de alíquotas com a legislação vigente.

## Lucro Presumido (IRPJ e CSLL)

**Onde fica:** *Apuração › Lucro Presumido (IRPJ e CSLL)*. Apuração **trimestral**.

1. Escolha o ano e o trimestre. **Sugerir pelas notas** preenche a receita por atividade (edite se precisar).
2. Informe outras receitas, IRRF a compensar, CSLL retida e outras deduções.
3. **Calcular**: presunção de 8%, 16% ou 32% (IRPJ) e 12% ou 32% (CSLL) por atividade; IRPJ de 15% mais adicional de 10% sobre o que passar de R$ 20 mil por mês; CSLL de 9%.
4. **Salvar apuração** e, se desejar, **Contabilizar provisão** (ou **Desfazer provisão**).

## Lucro Real (IRPJ, CSLL e LALUR)

**Onde fica:** *Apuração › Lucro Real (IRPJ, CSLL e LALUR)*. Apuração **trimestral**.

1. Escolha o trimestre e clique em **Preparar (contabilidade e LALUR)**: traz o lucro contábil das contas de resultado e os lançamentos do LALUR do ano.
2. Em *LALUR / LACS · Parte A*, inclua as **adições e exclusões** (descrição, valor, tributo, tipo e **código da ECF**) com **Incluir no LALUR/LACS**; **Excluir selecionado** remove.
3. Informe prejuízo fiscal e base negativa a compensar (limite de 30% do lucro), IRRF, CSLL retida e deduções. Os saldos iniciais da *Parte B* (prejuízos fiscais e base negativa) são gravados em **Gravar saldos iniciais**.
4. **Calcular**, **Salvar apuração** e, se desejar, **Contabilizar provisão**.

A *Parte B* mostra o razão dos prejuízos e da base negativa. O **código da ECF** de cada lançamento é obrigatório para gerar a ECF.

## PIS e COFINS

**Onde fica:** *Apuração › PIS e COFINS*.

1. Escolha a competência. **Sugerir pelas notas** preenche a receita da base e a base dos créditos (não cumulativo).
2. Marque **Excluir o ICMS da base (Tema 69/STF)**, se aplicável.
3. Informe PIS e COFINS retidos, **Calcular** e **Salvar apuração**. Cumulativo: 0,65% e 3%. Não cumulativo: 1,65% e 7,6%, com créditos sobre as entradas.
4. **Contabilizar provisão** / **Desfazer provisão** como nas demais apurações.

## Histórico de receita e folha

**Onde fica:** *Apuração › Histórico de receita e folha*.
Escolha o ano, **Carregar**, edite a receita bruta e a folha de cada mês anterior ao uso do sistema e **Salvar alterações**. Serve de base para o RBT12 do Simples e o Fator R.

## Contas dos tributos (provisões)

**Onde fica:** *Apuração › Contas dos tributos (provisões)*.
Para cada tributo (DAS, IRPJ, CSLL, PIS, COFINS...) informe a **conta de despesa** e a **conta de passivo (a recolher)** e **Salvar**. São usadas por **Contabilizar provisão** nas apurações.

## Retenções e guias

### Retenções (IRRF, CSRF, INSS, ISS)
**Onde fica:** *Obrigações › Retenções (IRRF, CSRF, INSS, ISS)*.
1. Escolha o período e **Consultar**.
2. **Gerar das notas** cria as retenções (sofridas e efetuadas) a partir dos documentos.
3. Para lançar à mão, use *Nova retenção (lançamento manual)*: tributo, sentido, base de cálculo, valor retido e código da receita, e **Incluir**.
4. **Excluir selecionada** apaga uma retenção.

### Guias de recolhimento
**Onde fica:** *Obrigações › Guias de recolhimento*.
1. Escolha as competências e **Consultar**.
2. **Gerar guias** cria DAS, DARF (com código de receita), DCTFWeb/GPS do INSS (informativo) e ISS a partir das apurações e retenções.
3. Selecione a guia e **Marcar como paga** (ou **Reabrir**). Guias manuais: *Nova guia (manual)* e **Excluir (manual)**.

Guias com vencimento próximo aparecem no calendário de obrigações e na tela inicial.

## Relatórios fiscais

Todos têm tela, filtro de período e exportação em PDF, Excel e CSV.

- **Livro de Registro de Entradas** e **de Saídas** (*Relatórios › Livros*): uma linha por nota e CFOP; notas canceladas aparecem sem valores. Informe o período e **Gerar**.
- **Resumo por CFOP**: totais por CFOP e sentido.
- **Produtos cadastrados** e **Itens das notas fiscais**: listagens com filtros.

## Livros de apuração do ICMS e do IPI

**Para que serve:** conferir e imprimir a apuração mensal do ICMS e do IPI, com os mesmos valores que vão para a EFD ICMS/IPI.
**Onde fica:** *Relatórios › Livros › Livro de Apuração do ICMS* e *Livro de Apuração do IPI*.

1. Escolha o **mês** e o **ano**. No livro do ICMS, informe também o **saldo credor anterior**, se houver.
2. Clique em **Gerar**: aparecem os débitos pelas saídas e os créditos pelas entradas, separados por CFOP, e o resultado do mês (imposto a recolher ou saldo credor a transportar).
3. Use **Exportar** para PDF, Excel ou CSV. O PDF sai com o cabeçalho da empresa, como os demais livros.

Notas canceladas, denegadas e NFS-e não entram. No livro do IPI só entram os itens com situação tributária do IPI preenchida. Outros débitos, outros créditos e estornos aparecem zerados, como na EFD.

## SPED EFD ICMS/IPI

**Para que serve:** gerar o arquivo da EFD ICMS/IPI (SPED Fiscal) do mês.
**Onde fica:** *Obrigações › SPED › EFD ICMS/IPI (SPED Fiscal)*.

1. **Passo 1**: empresa, mês/ano e, se for o caso, **escrituração retificadora**. Informe as opções: **perfil** (IND_PERFIL), **atividade**, **código de receita do ICMS** (ex.: 046-2 para o estado), do ICMS-ST (se houver), **dia de vencimento** e **saldo credor do mês anterior**. **Salvar estas opções como padrão da empresa** evita redigitar a cada mês.
2. **Passo 2**: pré-requisitos (cadastros incompletos, produtos sem NCM etc.).
3. **Passo 3**: validação prévia com o resumo da apuração (E110). Corrija os bloqueios.
4. **Passo 4**: assinatura. O padrão é **não assinar** (a assinatura oficial é feita no PVA, na transmissão).
5. **Passo 5**: **Gerar arquivo**, escolha a pasta e **Salvar arquivo** (`EFD_ICMS_IPI_<CNPJ>_<AAAAMM>.txt`).

No PVA: *Escrituração › Importar* e *Validar*; confira o resumo e então assine e transmita.
**Limitações:** sem ajustes do ICMS (E111), DIFAL, CIAP, inventário detalhado, produção e transporte.

## SPED EFD-Contribuições

**Onde fica:** *Obrigações › SPED › EFD-Contribuições (PIS/COFINS)*. Mesmo assistente de cinco passos.

1. No passo 1 informe a **natureza da pessoa jurídica**, a **atividade preponderante** e, para retificadora, o recibo da escrituração anterior. Salve as opções como padrão.
2. Siga os passos 2 a 5 como na EFD ICMS/IPI (arquivo `EFD_CONTRIBUICOES_<CNPJ>_<AAAAMM>.txt`).

Antes, faça a [apuração de PIS e COFINS](#pis-e-cofins) do mês. Ambas as telas têm a aba de **arquivos gerados**, com data, período e hash de cada geração.

## Contabilização

A integração das notas e das apurações com o Contábil é descrita em [Regras de contabilização](#regras-de-contabilizacao), [Contabilizar notas fiscais](#contabilizar-notas-fiscais) e [Contas dos tributos](#contas-dos-tributos-provisoes).
