# Fiscal

Menus do módulo Vorticis Fiscal. As escriturações usam os cadastros de clientes e fornecedores e o plano de contas do módulo Contábil, no mesmo banco.

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Produtos

**Para que serve:** cadastro das mercadorias e serviços, usado na digitação das notas e na EFD.
**Onde fica:** *Cadastros › Produtos*.

1. **Novo** e informe código, descrição, unidade, **NCM**, CEST, código de barras (GTIN/EAN), origem e tipo do item.
2. Informe os padrões fiscais: CFOP e CST de entrada e de saída, e as alíquotas de ICMS, IPI, PIS e COFINS.
3. **Salvar**. O filtro busca por descrição, código, GTIN ou NCM; *Somente ativos* esconde os inativos.

## Regras fiscais

**Para que serve:** ensinar o sistema a escriturar as notas de **entrada** importadas do XML do fornecedor. O fornecedor emite a nota com o CFOP de saída dele (5.102, 6.102, 5.405...); na sua escrituração o CFOP é de entrada (1.102, 2.102, 1.403...). Aqui você define essa troca, o CST e se a empresa tem direito a crédito de ICMS, IPI, PIS e COFINS.
**Onde fica:** *Cadastros › Regras fiscais*.

1. Clique em **Gerar regras padrão** para criar a troca usual (5.xxx vira 1.xxx, 6.xxx vira 2.xxx, com as correspondências conhecidas, como 5.405 para 1.403 e 5.933 para 1.933). Só entram correspondências seguras; as demais ficam sem regra. As regras que você já cadastrou não são alteradas.
2. Para uma regra própria, clique em **Nova**, escolha o tipo **Entrada** e informe o **CFOP do emitente** e o **CFOP a usar**. Se quiser, restrinja a regra a um fornecedor, a um NCM (ou ao começo dele, para valer para um grupo), a um produto ou a um CST/CSOSN do XML.
3. Em **Então**, informe, se precisar, o CST/CSOSN a usar e desmarque os impostos que **não geram crédito** (os valores de crédito do item ficam zerados na escrituração, e o XML original não muda). Também pode indicar as contas de débito e de crédito (código da conta analítica) para a contabilização.
4. **Salvar**. A regra vale nas próximas importações; para notas já importadas use **Reaplicar regras** (veja abaixo).

**Qual regra vale:** a mais específica. Produto vence NCM, que vence fornecedor, que vence a regra só com o CFOP. Se duas forem igualmente específicas, vale a de menor **Ordem**.

**Sem regra:** se nenhuma regra servir, o item recebe a troca padrão (5 vira 1, 6 vira 2, 7 vira 3) e fica marcado como **Sem regra**, para você conferir. Na tela de entrada marque *Só notas com itens sem regra fiscal* para listar essas notas.

**Notas de saída:** crie uma regra do tipo **Saída** para o sistema sugerir o CFOP e o CST ao digitar um item, conforme o produto, o NCM, o cliente e o destino (*Dentro do estado* ou *Outro estado*). A sugestão aparece quando você busca o produto no item e pode ser alterada.

## Documentos de entrada e de saída (digitação)

**Para que serve:** consultar as notas já lançadas e lançar à mão as que não têm XML (ou conferir um XML antes de gravar).
**Onde fica:** *Lançamentos › Documentos fiscais › Entrada* e *Saída*.

### A tela de consulta
Ao abrir, a tela mostra a **grade das notas lançadas** (digitadas e importadas), em tela cheia:

- **Filtros:** período (de/até), cliente ou fornecedor (nome ou CNPJ/CPF), número, **Origem** (todas, digitadas ou importadas por XML) e **Situação** (todas, autorizadas, canceladas, contabilizadas ou não contabilizadas). Clique em **Filtrar**; **Limpar filtros** volta ao padrão.
- **Rodapé:** quantidade de notas e totais de valor, ICMS, IPI, PIS, COFINS e ISS do que está na grade (notas canceladas não entram nos valores).
- **Novo lançamento:** abre a digitação de uma nota em branco numa janela à parte (pop-up).
- **Editar** (ou dois cliques na nota, ou Enter): abre a nota para alterar.
- **Consultar:** abre a nota só para leitura.
- **Excluir:** apaga a nota e as duplicatas dela (notas contabilizadas devem ter a contabilização desfeita antes; nota com duplicata já baixada só pode ser excluída depois de estornar as baixas).
- **Importar XML:** importa arquivos, pastas ou .zip em lote (veja [Importação de XML](#importacao-de-xml-nf-e-nfc-e-nfs-e)); as notas aparecem na grade.

### A janela da nota
A nota tem abas na mesma janela: **Nota**, **Itens**, **Tributos**, **Parcelas** e **Escrituração**. **Enter** passa para o próximo campo; **Esc** (ou **Fechar**) fecha a janela. Ao **Gravar nota**, a janela fecha e a grade é atualizada.

1. Aba **Nota**: modelo, série, número, **Emissão**, data de entrada/saída, chave de acesso (opcional) e natureza da operação. Digite o **CNPJ/CPF** do cliente/fornecedor (busca automática, ou **Buscar participante**). Se não existir, informe nome, UF e município para cadastrá-lo na hora. A data de entrada/saída acompanha a emissão.
2. Aba **Itens**: produto (código, descrição ou GTIN; **Buscar produto**), CFOP, quantidade, valor unitário, NCM, unidade, desconto, frete, seguro e outras despesas. **Adicionar item**, **Atualizar item selecionado** ou **Remover item selecionado**.
3. Aba **Tributos**: escolha um item na lista e confira **CFOP, CST/CSOSN, base, alíquota e valor de ICMS**, ICMS-ST, IPI, PIS, COFINS e, para serviço, ISS. Ao alterar a **base** ou a **alíquota**, o valor do tributo é recalculado (base × alíquota); você pode corrigir o valor à mão depois. **Calcular tributos** preenche pelas alíquotas do produto o que estiver em branco. Clique em **Aplicar ao item** para guardar; os **totais da nota** (rodapé da janela) são recalculados. Nas notas importadas de XML os tributos são só de leitura.
4. Aba **Parcelas**: escolha a **Forma de pagamento** ([cadastro](02-cadastros-comuns.md#formas-de-pagamento)) e as duplicatas são geradas pelos prazos dela, contados da emissão, sobre o total da nota. Se mudar os itens depois, clique em **Gerar pela forma** para refazer. Para parcelar à mão, informe o **número de parcelas**, o **primeiro vencimento** e o **intervalo em dias** e clique em **Gerar parcelas** (a última recebe a diferença de centavos; a soma tem de bater com o valor da nota). **Limpar parcelas** apaga a lista. Sem parcelas, a nota nova gera uma duplicata única *à vista* (veja [Duplicatas](#duplicatas-contas-a-pagar-e-a-receber)).
5. Aba **Escrituração**: centro de custo, contas de débito e crédito e observações (veja abaixo, nas notas importadas).
6. Confira o **total informado** (aba Nota): precisa bater com a soma dos itens; em branco, vale a soma. Clique em **Gravar nota**.
7. Nota de entrada com itens de **ativo imobilizado** (CFOP 1.551, 2.551, 3.551, 1.406 ou 2.406): ao gravar a nota nova, o sistema pergunta se quer cadastrar os bens. Respondendo **Cadastrar os bens**, abre a tela [Bens do imobilizado da nota](#bens-do-imobilizado-da-nota). Depois, o botão **Cadastrar bens do imobilizado** abre a mesma tela.

**Campos bloqueados?** Uma faixa no topo da janela explica o motivo:
- *Seu perfil só permite consultar*: peça ao administrador para marcar, no seu perfil, a permissão **Importar XML e digitar notas fiscais** (*Configurações › Usuários › Perfis*).
- *Nota contabilizada*: desfaça a contabilização para alterar, ou use **Novo lançamento** para lançar outra.
- *Nota aberta para consulta*: você abriu pelo botão **Consultar**; feche e use **Editar**.
- *Nota importada de XML* (faixa amarela): os dados do XML não mudam; só a escrituração (CFOP de entrada, CST, créditos de ICMS, IPI, PIS e COFINS, centro de custo, produto vinculado, contas e observações). Escolha o item na aba **Itens**, ajuste na aba **Escrituração**, clique em **Aplicar ao item** e depois em **Gravar escrituração**. A coluna *Regra fiscal* mostra *Sem regra* nos itens que não encontraram regra; ao gravar a escrituração a marca sai.

### Reaplicar regras (notas de entrada)
Na tela de *Entrada*, **Reaplicar regras** refaz o CFOP, o CST e os créditos das notas **importadas e ainda não contabilizadas** do período filtrado, pelas regras de *Cadastros › Regras fiscais*. Itens sem regra que você já conferiu à mão são mantidos. Notas contabilizadas, ou de período fiscal fechado, não são alteradas.

### Importar uma nota (XML ou chave de acesso)
No alto da janela da nota há dois atalhos. Eles **gravam a nota como importada** (com o XML original e a chave de acesso, como na importação em lote) e abrem a nota para você conferir a escrituração. Os valores do XML não mudam; só a escrituração.

**Importar por XML:** clique em **Escolher arquivo...** e escolha o XML de uma nota. O fornecedor/cliente é cadastrado a partir do XML, se ainda não existir. Em notas de entrada, o CFOP de entrada, o CST e os créditos vêm das *Regras fiscais*; itens sem regra são avisados. A nota precisa ser da empresa selecionada, e da operação da tela (uma nota de saída do seu cliente abre em *Saída*). Se a nota (mesma chave de acesso) já estiver lançada, o sistema avisa e abre a que já existe, sem duplicar.

**Importar por chave de acesso:** cole a chave de 44 dígitos (pode ter espaços ou pontos) e clique em **Importar por chave**. O sistema confere o dígito verificador e então:
1. **procura a nota entre as já lançadas e os XMLs importados da empresa**; se achar, mostra a nota que já está no sistema (não duplica);
2. se não achar, e a empresa tiver **certificado digital** (e-CNPJ A1 ou A3, cadastrado em *Configurações › Certificados digitais* e marcado como padrão do responsável), **baixa o XML na SEFAZ** (Ambiente Nacional, a mesma consulta do Portal da NF-e). Se o certificado for A1, o sistema pede a senha;
3. sem certificado, o sistema avisa que a nota não está entre as importadas e que é preciso o certificado digital da empresa.

Se a SEFAZ devolver só o **resumo** da nota, ela ainda não foi manifestada: aparece o botão **Manifestar ciência**, que registra a *Ciência da operação* em nome da empresa e libera o XML completo. Aguarde alguns minutos e clique de novo em **Importar por chave**. Mensagens da SEFAZ (ex.: *137 – Nenhum documento localizado*, *656 – Consumo indevido*) aparecem como vieram; a SEFAZ limita o número de consultas por hora (se aparecer *656*, espere uma hora).

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
- Clientes e fornecedores são criados ou vinculados automaticamente. Com **Completar o cadastro de fornecedores e clientes novos pela consulta do CNPJ (Receita)** marcado (vem marcado), o cadastro criado a partir do XML é completado como no botão *Buscar dados na Receita* do cadastro de clientes e fornecedores: nome fantasia, endereço completo, e-mail, telefone e inscrição estadual que faltarem. Os dados que vieram no XML são mantidos. Na janela da nota (Importar por XML ou por chave), o fornecedor novo aparece com os dados do XML para conferência e é cadastrado só ao gravar a nota.
- **Nota de outra empresa:** se o XML não for da empresa selecionada (o CNPJ dela não é o do emitente nem o do destinatário), a nota não é importada e aparece na lista como *Outra empresa*, com os CNPJs do emitente e do destinatário. Escolha a empresa certa no topo e importe de novo.
- Se acontecer um erro inesperado, o programa mostra o aviso, grava os detalhes em `%LOCALAPPDATA%\VorticisEmpresarial\logs\erros.log` e continua aberto.
- **Regras fiscais:** nas notas de **entrada**, cada item recebe o CFOP de entrada, o CST e os créditos conforme [Regras fiscais](#regras-fiscais); sem regra, vale a troca padrão (5 para 1, 6 para 2, 7 para 3) e o item fica marcado como *Sem regra* para conferência.
- **Duplicatas:** a cobrança da NF-e (parcelas com número, vencimento e valor) vira duplicatas a pagar (nota de entrada) ou a receber (nota de saída). Nota sem cobrança gera uma duplicata *à vista*, vencendo na emissão (os dias são configuráveis, e é possível desligar em *Cadastros › Parâmetros*). NFC-e só gera duplicata se trouxer cobrança. Se a nota for cancelada, as duplicatas ainda abertas também são canceladas.

## Consulta de documentos

**Onde fica:** *Documentos fiscais › Consulta de documentos* (atalho **Ctrl+D**).
Filtre por período, CFOP e texto (número, nome, CNPJ ou chave) e clique em **Consultar**. Selecione uma nota para ver os itens e tributos. **Excluir selecionado** remove a nota e as suas duplicatas (desfaça antes a contabilização, se houver). Se alguma duplicata da nota já foi baixada, o sistema recusa: estorne as baixas em *Lançamentos › Duplicatas* e exclua depois.

## Regras de contabilização

**Para que serve:** ensinar o sistema em quais contas lançar cada tipo de nota.
**Onde fica:** *Cadastros › Regras de contabilização*.

1. **Nova**. Informe o **CFOP** (completo, como 5102, só o prefixo, como 51, ou `NFSE` para notas de serviço; vazio vale para todos) e o **sentido** (entrada ou saída).
2. Informe a **conta de débito** e a **de crédito** e, se quiser, o **participante** e o histórico (aceita as variáveis dos históricos padrão).
3. **Salvar**.

Vale a regra **mais específica** (participante, CFOP completo, prefixo, geral); em empate, a de maior prioridade.

## Contabilizar notas fiscais

**Onde fica:** *Documentos fiscais › Contabilizar notas fiscais* (só aparece com a contabilização **em lote**).

### On-line ou em lote
Em *Cadastros › Parâmetros*, grupo *Lançamento das notas*, escolha como as notas vão para o Contábil:
- **Em lote** (*Contabilizar as notas on-line* desmarcado, o padrão): as notas ficam pendentes e você contabiliza depois, por período, nesta tela.
- Se a empresa usa o tipo de contabilização **Em lote** do Contábil (veja [Contabilização em lote](03-contabil.md#contabilizacao-em-lote)), as notas gravadas ou importadas e as baixas de duplicatas viram pendências nessa tela de lote, em vez de lançamentos na hora.
- **On-line** (marcado): cada nota é contabilizada assim que é gravada (digitação) ou importada (XML ou chave de acesso), pelas regras de contabilização. O menu *Contabilizar notas fiscais* fica escondido. Nota sem regra ou com período contábil fechado continua pendente, e o motivo aparece na mensagem da gravação ou no resumo da importação; corrija a regra e grave a nota de novo, ou volte para **em lote** para contabilizar as pendentes.

1. Escolha o período e clique em **Pré-visualizar**: aparecem as notas pendentes, com o lançamento previsto.
2. Marque as notas e clique em **Contabilizar marcadas**. São gerados os lançamentos no Contábil (documento = número da nota).
3. Para desfazer, marque as notas em *Notas contabilizadas no período* e clique em **Desfazer contabilização das marcadas**: o lançamento é estornado e a nota volta a pendente.

Quando a nota tem duplicatas e a empresa informou a **conta de fornecedores** (entrada) ou de **clientes** (saída) em *Cadastros › Parâmetros*, essa conta passa a ser a contrapartida da nota: o fornecedor é creditado na compra e o cliente debitado na venda, com o código do fornecedor/cliente na partida. Uma regra específica daquele participante, ou as contas digitadas na própria nota, têm preferência.

**Erro comum:** nota sem regra aplicável ou período contábil fechado ficam sem contabilizar, com a mensagem na coluna *Situação*.

## Bens do imobilizado da nota

**Para que serve:** levar as compras de ativo imobilizado da nota de entrada direto para o cadastro de bens do Contábil (*Imobilizado › Cadastro de bens*), sem digitar de novo.
**Onde fica:** abre sozinha ao gravar a nota de entrada com CFOP de imobilizado (ou pelo botão **Cadastrar bens do imobilizado** na digitação).

1. Cada item da nota vira uma linha, com a **descrição** e o **valor** do item. A nota, o fornecedor, a **data de aquisição** (emissão) e o **início do uso** (data de entrada) já vêm preenchidos.
2. Escolha o **grupo** de cada bem (regra de depreciação). A **vida útil** fiscal e as **contas** (do bem, da depreciação acumulada e da despesa) vêm do grupo e podem ser trocadas.
3. Informe, se quiser, a **plaqueta** e a **localização**. Desmarque **Cadastrar** nos itens que não são bens.
4. Clique em **Cadastrar no imobilizado**. A coluna **Situação** mostra *Cadastrado* ou o que falta corrigir.

O cadastro exige a permissão de lançamentos do Contábil. Os bens aparecem no Contábil e entram na depreciação mensal.

## Fechamento do período fiscal

**Para que serve:** travar o mês depois de escriturar as notas e apurar os impostos, para que nada mude depois de entregue a apuração (empresas do **regime normal**: Lucro Presumido ou Real).
**Onde fica:** *Apuração › Fechamento do período fiscal*.

1. Escolha o **Ano**. A lista mostra os 12 meses: situação (aberto/fechado), quantas notas há, quantas ainda **não foram contabilizadas** e quando e por quem o mês foi fechado.
2. Selecione o mês e clique em **Fechar mês**. Se houver notas não contabilizadas, o aviso aparece antes de confirmar.
3. Com o mês fechado, as notas com data de entrada/saída no mês **não podem ser incluídas, alteradas nem excluídas** (digitação, importação de XML ou pela chave). A contabilização das notas continua permitida.
4. Para corrigir algo, selecione o mês e clique em **Reabrir mês**.

**Atenção:** empresa do Simples Nacional não usa este fechamento (o período é encerrado pela apuração do DAS). O regime vem do cadastro da empresa.

## Duplicatas (contas a pagar e a receber)

**Para que serve:** controlar o que a empresa tem a pagar aos fornecedores e a receber dos clientes, parcela por parcela, e gerar a contabilização do pagamento e do recebimento. As duplicatas nascem das notas (importadas ou digitadas) e também podem ser lançadas à mão.
**Onde fica:** *Lançamentos › Duplicatas (contas a pagar e a receber)*.

**Antes de usar:** em *Cadastros › Parâmetros*, grupo *Duplicatas e baixas*, informe os códigos das contas contábeis analíticas: fornecedores, clientes, juros pagos, multa paga, descontos obtidos, juros recebidos, multa recebida e descontos concedidos. As de juros, multa e desconto só são exigidas quando você usar esses valores numa baixa. Também ali você escolhe se a nota sem cobrança gera duplicata à vista e o texto do histórico dos lançamentos (aceita {DOCUMENTO}, {PARTICIPANTE}, {DATA} e {VALOR}).

### Consultar
1. Escolha *A pagar e a receber*, *A pagar* ou *A receber*, o período de **vencimento**, o fornecedor/cliente (nome, código ou CNPJ) e a **situação**. Marque **Somente vencidas** para ver só as que passaram do vencimento.
2. Clique em **Consultar**. Cada linha mostra vencimento, documento (número da nota/parcela), valor, saldo, situação e dias de atraso. O resumo acima da lista traz o saldo em aberto a pagar e a receber.

Situações: **Aberta** (sem baixa), **Parcial** (parte já paga ou recebida), **Liquidada** (saldo zero) e **Cancelada**.

### Incluir e alterar
1. Clique em **Nova duplicata**, escolha *A pagar* ou *A receber*, informe o fornecedor/cliente (como cadastrado), o número do documento, a parcela, a emissão, o vencimento e o valor. **Gravar**.
2. Para alterar, selecione a duplicata, mude os campos e **Gravar**. O valor só muda enquanto não há baixas. O vencimento não pode ser anterior à emissão.
3. **Cancelar duplicata** vale para a que está aberta e sem baixas. **Excluir** vale para as lançadas à mão e sem baixas; as que vieram de nota são canceladas ou somem junto com a nota.

### Baixar (pagar ou receber)
1. Selecione a duplicata. Na área *Baixa*, confira a **data**, o **valor principal** (já vem com o saldo; digite menos para baixa parcial), os **juros**, a **multa** e o **desconto** (se houver) e o código da **conta do banco ou caixa**.
2. Clique em **Baixar selecionada**. O valor pago é principal + juros + multa - desconto.
3. O sistema gera o lançamento no Contábil. **Pagamento:** débito no fornecedor (principal), nos juros e na multa; crédito no desconto obtido e no banco. **Recebimento:** débito no banco e no desconto concedido; crédito no cliente (principal), nos juros e na multa recebidos. O documento do lançamento é o número da nota/parcela (ex.: 1234/001).
4. **Baixar várias:** marque as duplicatas na primeira coluna, informe a data e a conta do banco e clique em **Baixar marcadas**. Cada uma é baixada pelo saldo total; as que tiverem problema ficam indicadas e as outras são baixadas.

### Estornar uma baixa
Selecione a duplicata, escolha a baixa na lista *Baixas da duplicata selecionada* e clique em **Estornar baixa selecionada**. O lançamento contábil é estornado e o valor volta ao saldo. Se o período contábil estiver fechado, o estorno é recusado: reabra o período e tente de novo.

**Dicas e erros comuns:**
- "Informe a conta de fornecedores em Cadastros › Parâmetros": falta configurar a conta antes da primeira baixa.
- O valor da baixa não pode passar do saldo; duplicata liquidada ou cancelada não recebe baixa.
- Perfis: quem tem a função de *manter duplicatas* inclui e altera; só quem tem a de *baixar duplicatas* baixa, estorna e concilia (Administrador e Contador; o Assistente só mantém).

## Conciliação de duplicatas

**Para que serve:** baixar duplicatas a partir do extrato bancário, sem digitar, e deixar a partida do banco marcada como conciliada.
**Onde fica:** *Lançamentos › Conciliação de duplicatas*.

**Antes:** importe o extrato do banco (OFX ou PDF) no Contábil, em *Integrações › Importar › Extratos bancários (OFX e PDF)*.

1. Informe o código da **conta contábil do banco** do extrato, o período e a **tolerância em dias** (quantos dias de diferença entre o vencimento e a data do banco ainda contam como a mesma operação).
2. Clique em **Sugerir**. A lista mostra pares *duplicata em aberto x movimento do extrato* que têm o mesmo valor do saldo, data próxima do vencimento e/ou o número do documento no histórico, com o motivo da sugestão. Saída do banco só combina com duplicata a pagar; entrada, com a receber.
3. Desmarque os pares que não quiser e clique em **Baixar marcadas**. A duplicata é baixada pelo saldo total, na data do extrato.

Como fica a contabilidade: se o extrato foi importado contra a própria conta de fornecedores/clientes, a baixa só registra o vínculo com o movimento (o lançamento do extrato já baixa a parcela). Se foi importado contra outra conta (por exemplo, "pagamentos a classificar"), a baixa lança contra essa conta e as duas partidas são conciliadas em *Lançamentos › Conciliações*. Estornar a baixa desfaz a conciliação e o movimento volta a ser sugerido.

## Relatórios de duplicatas

**Onde ficam:** *Relatórios › Duplicatas*. Todos aparecem na tela e exportam em PDF, Excel e CSV.

- **Duplicatas a pagar** e **Duplicatas a receber**: uma linha por parcela, com vencimento, documento, valor, valor baixado, saldo, situação e dias de atraso na **data-base**. Filtre por vencimento, fornecedor/cliente e situação (*Em aberto* ou *Todas*) e clique em **Gerar**.
- **Posição por vencimento**: saldo em aberto por fornecedor/cliente nas faixas *a vencer*, *vencidas de 1 a 30 dias*, *de 31 a 60*, *de 61 a 90* e *há mais de 90 dias*, na data-base escolhida. Escolha *Contas a pagar* ou *Contas a receber*.
- **Relatório de recebimentos** e **Relatório de pagamentos**: cada baixa feita no período (data, cliente/fornecedor, documento e parcela, principal, juros e multa, desconto, valor líquido e conta usada), com o total no fim. Informe o período das baixas e clique em **Gerar**.

## Apuração do Simples Nacional (DAS)

**Onde fica:** *Apuração › Simples Nacional (DAS)*.

1. Escolha a **competência**.
2. Informe a receita do mês por atividade, de um destes jeitos:
   - **Sugerir pelas notas**: preenche pelas notas de saída lançadas no sistema.
   - **Incluir receita manualmente** (quadro acima da lista): escolha a atividade (anexo), digite o **Valor**, marque ICMS-ST, monofásico, ISS retido ou exportação quando houver e clique em **Incluir receita**. Repita para cada atividade. Serve para empresas sem notas no sistema ou para completar o que veio das notas.
   - Para corrigir, clique no valor na lista e digite o novo; **Remover selecionada** tira uma linha e **Limpar receitas** apaga todas.
3. O **RBT12** (receita dos 12 meses anteriores) vem do histórico; informe também a *folha 12 meses* para o Fator R (anexo III ou V). Se a empresa é nova, o sistema proporcionaliza.
4. **Calcular**: mostra alíquota efetiva, valor do DAS e a repartição por tributo, com memória de cálculo.
5. **Salvar apuração**. **Contabilizar provisão** lança a despesa e o passivo (precisa das [contas dos tributos](#contas-dos-tributos-provisoes)); **Desfazer provisão** estorna.

**PGDAS-D:** o Vorticis calcula o DAS, mas a transmissão da declaração continua no portal do Simples Nacional (PGDAS-D). A integração direta exige o contrato do *Integra Contador* do SERPRO (chaves de acesso pagas) e procuração eletrônica; ela ainda não está disponível.

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
- **Resumos** (*Relatórios › Resumos*):
  - **Resumo de vendas** e **Resumo de compras**: soma das notas autorizadas de saída (vendas) ou de entrada (compras) do período, com quantidade de notas, valor total, ICMS, IPI, PIS+COFINS e ISS. Escolha como agrupar: *Por nota*, *Por cliente/fornecedor* ou *Por mês*. Notas canceladas ou denegadas ficam de fora.
  - **Resumo por CFOP**: totais por CFOP e sentido.
- **Produtos cadastrados** e **Itens das notas fiscais**: listagens com filtros.
- **Duplicatas a pagar**, **Duplicatas a receber** e **Posição por vencimento**: veja [Relatórios de duplicatas](#relatorios-de-duplicatas).

## Livros de apuração do ICMS e do IPI

**Para que serve:** conferir e imprimir a apuração mensal do ICMS e do IPI, com os mesmos valores que vão para a EFD ICMS/IPI.
**Onde fica:** *Relatórios › Livros › Livro de Apuração do ICMS* e *Livro de Apuração do IPI*.

1. Escolha o **mês** e o **ano**. No livro do ICMS, informe também o **saldo credor anterior**, se houver.
2. Clique em **Gerar**: aparecem os débitos pelas saídas e os créditos pelas entradas, separados por CFOP, e o resultado do mês (imposto a recolher ou saldo credor a transportar).
3. Use **Exportar** para PDF, Excel ou CSV. O PDF sai com o cabeçalho da empresa, como os demais livros.

Notas canceladas, denegadas e NFS-e não entram. No livro do IPI só entram os itens com situação tributária do IPI preenchida. Os outros débitos, outros créditos e estornos lançados em *Lançamentos › SPED Fiscal › Ajustes da apuração do ICMS* entram como na EFD.

## SPED EFD ICMS/IPI

**Para que serve:** gerar o arquivo da EFD ICMS/IPI (SPED Fiscal) do mês.
**Onde fica:** *Obrigações › SPED › EFD ICMS/IPI (SPED Fiscal)*, ou *Lançamentos › SPED Fiscal › Gerar EFD ICMS/IPI (SPED Fiscal)*.

**Antes de gerar (configuração):** deixe o padrão da empresa em *Cadastros › Parâmetros*, grupo *EFD ICMS/IPI* (perfil, atividade, códigos de receita e dia de vencimento do ICMS): o assistente já abre preenchido. Lance também, se houver, os [outros lançamentos do SPED Fiscal](#outros-lancamentos-do-sped-fiscal).

1. **Passo 1**: empresa, mês/ano e, se for o caso, **escrituração retificadora**. Informe as opções: **perfil** (IND_PERFIL), **atividade**, **código de receita do ICMS** (ex.: 046-2 para o estado), do ICMS-ST (se houver), **dia de vencimento** e **saldo credor do mês anterior**. **Salvar estas opções como padrão da empresa** evita redigitar a cada mês.
2. **Passo 2**: pré-requisitos (cadastros incompletos, produtos sem NCM etc.).
3. **Passo 3**: validação prévia com o resumo da apuração (E110). Corrija os bloqueios.
4. **Passo 4**: assinatura. O padrão é **não assinar** (a assinatura oficial é feita no PVA, na transmissão).
5. **Passo 5**: **Gerar arquivo**, escolha a pasta e **Salvar arquivo** (`EFD_ICMS_IPI_<CNPJ>_<AAAAMM>.txt`).

No PVA: *Escrituração › Importar* e *Validar*; confira o resumo e então assine e transmita.
O arquivo traz as notas (bloco C), as contas de consumo (C500 e D500), os ajustes da apuração (E111) e o inventário cadastrado (bloco H) da competência.
**Limitações:** sem DIFAL, CIAP (bloco G), produção (bloco K) e conhecimentos de transporte (D100).

## Outros lançamentos do SPED Fiscal

**Para que serve:** lançar o que a EFD ICMS/IPI pede além das notas de mercadoria.
**Onde fica:** *Lançamentos › SPED Fiscal*.

### Contas de energia, gás, água e comunicação
**Onde fica:** *Lançamentos › SPED Fiscal › Contas de energia, gás, água e comunicação*.
Para as contas recebidas de energia elétrica (modelo 06), gás canalizado (28), água (29) e de comunicação/telecomunicação (21 e 22), que não vêm em XML de NF-e:
1. Cadastre a concessionária ou operadora em *Cadastros › Fornecedores*.
2. Clique em **Nova**, escolha o **Modelo**, o **Fornecedor** e informe número, série, emissão, entrada e o **valor da nota**. O CFOP vem sugerido (energia 1253, comunicação 1303, água e gás 1556; altere se a atividade for outra).
3. Para aproveitar o crédito, informe o **CST** (ex.: 000), a **base** e a **alíquota** do ICMS: o valor do ICMS é calculado. Informe PIS e COFINS, se destacados. **Gravar**.
A nota sai no C500/C590 (energia, gás, água) ou no D500/D590 (comunicação) da EFD do mês da entrada, e o ICMS entra nos créditos da apuração (E110).

### Ajustes da apuração do ICMS
**Onde fica:** *Lançamentos › SPED Fiscal › Ajustes da apuração do ICMS*.
1. Escolha a **Competência**.
2. Informe o **código do ajuste** da tabela 5.1.1 da sua UF (8 posições, ex.: SP020099). A 4ª posição diz o tipo, mostrado ao lado: 0 outros débitos, 1 estorno de créditos, 2 outros créditos, 3 estorno de débitos, 4 deduções, 5 débitos especiais.
3. Informe a **descrição** e o **valor** e clique em **Gravar**.
Cada ajuste vira um E111 e soma no campo certo do E110; o ICMS a recolher (E116) já sai ajustado. As deduções só abatem até o saldo devedor do mês.

### Inventário de estoque (bloco H)
**Onde fica:** *Lançamentos › SPED Fiscal › Inventário de estoque (bloco H)*.
1. Clique em **Novo**, informe a **data do inventário** (ex.: 31/12) e a **conta contábil do estoque**. O campo **Informar na EFD de** vem sugerido: o inventário de 31/12 vai na EFD de **fevereiro** do ano seguinte.
2. Escolha cada **produto** (do cadastro de produtos), clique em **Adicionar item** e informe na grade a **quantidade** e o **valor unitário** (o valor do item é calculado).
3. **Gravar inventário**. Na EFD da competência escolhida, o bloco H sai com o H005 (data e total) e um H010 por produto; o cadastro do produto entra no 0200.

## SPED EFD-Contribuições

**Onde fica:** *Obrigações › SPED › EFD-Contribuições (PIS/COFINS)*. Mesmo assistente de cinco passos.

1. No passo 1 informe a **natureza da pessoa jurídica**, a **atividade preponderante** e, para retificadora, o recibo da escrituração anterior. Salve as opções como padrão.
2. Siga os passos 2 a 5 como na EFD ICMS/IPI (arquivo `EFD_CONTRIBUICOES_<CNPJ>_<AAAAMM>.txt`).

Antes, faça a [apuração de PIS e COFINS](#pis-e-cofins) do mês. Ambas as telas têm a aba de **arquivos gerados**, com data, período e hash de cada geração.

## Contabilização

A integração das notas e das apurações com o Contábil é descrita em [Regras de contabilização](#regras-de-contabilizacao), [Contabilizar notas fiscais](#contabilizar-notas-fiscais) e [Contas dos tributos](#contas-dos-tributos-provisoes).
