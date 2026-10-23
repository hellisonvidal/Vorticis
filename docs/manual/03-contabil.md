# Contábil

Menus do módulo Vorticis Contábil. Permissões: cada função depende do seu perfil (veja [Usuários e perfis](01-primeiros-passos.md#usuarios-perfis-e-empresas-por-usuario)).

## Plano de contas

**Para que serve:** a lista de contas da empresa.
**Onde fica:** *Cadastros › Plano de contas*.

### Escolher um plano
1. Se a empresa ainda não tem plano, clique em **Escolher plano**, selecione um modelo e **Usar este plano**. A troca só é possível enquanto não há lançamentos.
2. Para importar de uma planilha, veja [Importar plano de contas](#importar-plano-de-contas).

### Manter contas
1. **Incluir** e preencha o **código** (ex.: 1.1.01.004), a descrição, a natureza (devedora/credora) e se é **sintética** (grupo) ou **analítica** (recebe lançamentos).
2. O **código reduzido** das analíticas é gerado automaticamente (pode ser editado).
3. Controles: **Exige centro de custo**, **Exige participante** (e o grupo onde os participantes ficam) e **Ativa**.

**Dica:** filtre a lista por código, reduzido ou descrição. Em lançamentos, você pode digitar o reduzido em vez do código completo.

## Importar plano de contas

**Onde fica:** *Integrações › Importar › Plano de contas (planilha Excel/CSV)*.

1. **Procurar** o arquivo .xlsx ou .csv.
2. Em *Colunas*, diga qual coluna é o código, a descrição e, se houver, natureza, tipo (S/A) e saldo.
3. Para converter os códigos, marque *Converter os códigos para a máscara* e informe a máscara (ex.: `9.9.99.999`: 1101001 vira 1.1.01.001).
4. Confira a **pré-visualização** e clique em **Importar plano**. Só substitui o plano enquanto não houver lançamentos.
5. **Converter plano atual para a máscara** troca os códigos das contas existentes, mesmo com movimento, sem alterar lançamentos e hierarquia.

## Centros de custo

**Onde fica:** *Cadastros › Centros de custo*. Só é usado se a empresa ligou "Usar centros de custo" (aba Contabilidade); a máscara também é definida lá.

1. **Incluir**: código (obrigatório nos níveis 2 ou mais, conforme a máscara), descrição e superior.
2. Marque **Sintético** para grupos que não recebem lançamentos. **Ativo** controla o uso.

## Históricos padrão

**Para que serve:** textos prontos para os lançamentos.
**Onde fica:** *Cadastros › Históricos*.

1. **Incluir**; o código é sequencial. Digite o texto fixo, por exemplo `Pagamento da NF {DOCUMENTO} de {PARTICIPANTE}`.
2. No lançamento, digite o código do histórico: o texto entra e o cursor vai para o fim, para completar com o complemento livre.

**Variáveis disponíveis:** `{DOCUMENTO}`, `{PARTICIPANTE}`, `{DATA}`, `{MES}`, `{ANO}`, `{MES_ANTERIOR}`, `{COMPETENCIA}`, `{CONTA}`, `{VALOR}` e `{COMPLEMENTO}`. As que o sistema não consegue resolver ficam para você completar.

## Lançamentos

**Para que serve:** registrar os fatos contábeis (partidas dobradas: débitos = créditos).
**Onde fica:** *Lançamentos › Contábeis › Lançamento contábil* (atalho **Ctrl+L**).

A tela mostra os **lançamentos efetuados** do período, com os botões **Novo (Ctrl+N), Editar, Consultar, Excluir, Estornar** e Atualizar. Os estornados continuam na lista, marcados.

### Incluir um lançamento (pop-up)
1. Clique em **Novo (Ctrl+N)**. Abre o pop-up de digitação.
2. Informe a **data**.
3. Na linha, digite a **conta débito** e a **conta crédito**. Antes de cada conta há o campo do **código reduzido**. A conta também pode ser digitada pelo nome (surge uma lista de sugestões) ou clicada na lista *Contas analíticas* ao lado.
4. Informe o **valor** (formata como 1.500,00), o **documento** (NF, recibo, boleto), o **histórico** (pelo código do histórico padrão ou digitando), o centro de custo e o participante, quando exigidos.
5. **Salvar (Ctrl+S)**.

### Teclado
| Tecla | Efeito |
|---|---|
| Enter / Tab | Vai ao próximo campo |
| **Enter no último campo** | Salva, se o lançamento já fecha; senão segue para a próxima linha |
| Ctrl+Enter | Salva |
| Ctrl+N / ↓ na última linha | Nova linha |
| ↑ em linha nova e vazia | Cancela a linha |
| Ctrl+Delete | Exclui a linha |

### Uma linha ou várias
- Uma linha com conta débito **e** conta crédito é um lançamento completo.
- Linhas com **uma conta só** somam um único lançamento que precisa fechar (débitos = créditos). Se faltar a contrapartida, o sistema pergunta e oferece incluir a linha.

### Editar um lançamento
Dê **duplo clique** (ou Enter) no lançamento efetuado, ou clique em **Editar**. Ao salvar, o original é **estornado na data dele** e o corrigido é gravado, tudo numa única operação: lançamentos efetivados nunca são alterados no lugar. **Consultar** abre só para leitura.

### Estornar
1. Selecione o lançamento na lista.
2. Informe a **data** do estorno e o **motivo** (obrigatório).
3. Clique em **Estornar**. É criado um lançamento inverso.

### Regras e erros comuns
- "Período fechado": reabra a competência em *Fechamento de períodos*.
- "Data bloqueada": a empresa tem *Bloquear lançamentos até* na aba Contabilidade.
- Débitos diferentes de créditos: o lançamento não é salvo.
- Conta sintética não recebe lançamento; use a analítica.
- Só rascunhos podem ser excluídos; lançamento efetivado se estorna.
- Se a empresa exige o nº do documento, informe-o em todas as partidas.

## Saldos iniciais

**Para que serve:** carregar o saldo de cada conta ao implantar o sistema.
**Onde fica:** *Lançamentos › Contábeis › Saldos iniciais (implantação)*.

1. Escolha o **exercício**.
2. Digite o saldo de cada conta analítica e marque **D** ou **C** (use *Mostrar só contas preenchidas* para conferir).
3. Clique em **Lançar saldos iniciais**. A soma dos devedores precisa ser igual à dos credores.

## Importações

Todas ficam em *Integrações › Importar*. Em todas, **Pré-visualizar** valida cada lançamento (contas analíticas, período aberto, partidas dobradas) e **Importar** grava tudo numa única transação: se um lançamento falhar, nenhum é gravado.

### Planilhas (layouts personalizados)
1. **1. Planilha**: escolha o .xlsx ou .csv e clique em **Carregar** (mostra até 200 linhas; o cabeçalho de cada coluna indica o papel definido).
2. **2. Layout**: escolha um salvo ou clique em **Novo**, dê um nome e diga se *cada linha é um lançamento* (débito e crédito juntos) ou *uma partida* (conta + D/C, ou colunas de valor a débito e a crédito, agrupadas pelo nº do lançamento).
3. **3. Colunas**: indique qual coluna é data, conta débito/crédito (ou conta fixa), valor, histórico, documento e centro de custo. Informe a máscara das contas se quiser converter códigos (11010001 vira 1.1.01.0001).
4. **Salvar layout** (vale para todas as empresas), **Pré-visualizar** e **Importar**.

### Extratos bancários (OFX)
1. Escolha o arquivo OFX do internet banking e clique em **Ler**.
2. Informe a **conta do banco** e as contrapartidas padrão de **entradas** e **saídas**; use *Preencher vazias* e ajuste linha a linha.
3. **Pré-visualizar** e **Importar**. Movimentos já importados (banco/agência/conta/FITID) não são duplicados; se o lançamento for estornado, pode ser importado de novo.

### Lançamentos contábeis (layout padrão)
Use **Gerar planilha modelo (.xlsx)**, preencha (`LANCAMENTO;DATA;CONTA;DC;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO`), escolha o arquivo, **Pré-visualizar** e **Importar**.

### Plano referencial da RFB
**Onde fica:** *Integrações › Importar › Plano referencial da RFB (tabela oficial)* (administrador).
1. Escolha o **tipo de plano** (ex.: PJ em geral – Lucro Real) e o **ano**.
2. Escolha o arquivo oficial (.txt ou .csv, com cabeçalho contendo ao menos `CODIGO` e `DESCRICAO`), obtido no portal do SPED, e clique em **Importar tabela**. Reimportar atualiza sem perder vínculos.

### Vínculo ao plano referencial
**Onde fica:** *Integrações › Importar › Vínculo ao plano referencial (RFB)*.
1. Escolha o **exercício**, **Carregar** e **Usar este plano**.
2. Vincule cada conta analítica a uma referencial de mesma natureza: selecione a conta, busque a referencial e clique em **Vincular** (ou **Remover vínculo**).
3. **Sugerir vínculos** propõe pela semelhança das descrições (confira antes de **Aceitar sugestões**); **Copiar do ano anterior** reaproveita.
4. Marque *Mostrar só pendências* para ver as contas com movimento sem vínculo, que **impedem a ECD e a ECF**.

## Exportar lançamentos

**Onde fica:** *Integrações › Exportar › Lançamentos contábeis*.
Escolha o período, marque (ou não) *Incluir estornados e estornos*, clique em **Gerar** e exporte em Excel, PDF ou CSV.

## Conciliação automática

**Para que serve:** conferir, numa conta (fornecedores, clientes, bancos), o que já foi pago ou recebido.
**Onde fica:** *Lançamentos › Conciliações › Conciliação automática*.

1. Informe a conta (ou reduzido; vazio = todas) e o período; **Consultar**.
2. **Conciliar automaticamente (nº do documento)** concilia as partidas de débito e crédito do mesmo documento que se anulam (ex.: a NF na compra e o pagamento da mesma NF).
3. Ou marque partidas manualmente e clique em **Conciliar selecionadas**. **Desfazer conciliação** reabre.
4. *Mostrar conciliadas* e *Somente com documento* filtram a lista. O que fica em aberto compõe o saldo da conta.

## Fechamento de períodos

**Para que serve:** impedir alterações em meses já conferidos.
**Onde fica:** *Lançamentos › Fechamentos › Fechamento de períodos*.

1. **Carregar** a lista de competências.
2. Selecione o mês e clique em **Fechar competência** (em ordem) ou **Reabrir competência**.

## Reorganizar lançamentos

*Lançamentos › Contábeis › Reorganizar lançamentos*: refaz a numeração sequencial dos lançamentos (útil depois de exclusões). Clique em **Reorganizar numeração**. Exige permissão de fechar períodos.

## Reorganizar saldos

*Lançamentos › Fechamentos › Reorganizar saldos*: recalcula os saldos mensais a partir das partidas. Clique em **Recalcular saldos**; a tela mostra, por conta e mês, o saldo antes e depois. Use se a conferência apontar divergências (veja *Diferenças de saldos*).

## Encerramento do exercício

**Para que serve:** zerar as contas de resultado contra a apuração e transferir o lucro ou prejuízo ao patrimônio líquido, em 31/12.
**Onde fica:** *Lançamentos › Fechamentos › Encerramento do exercício*.

1. Escolha o **exercício**.
2. Confira a **conta de apuração do resultado** e as contas de **lucros** e **prejuízos acumulados** (vêm da aba Contabilidade da empresa).
3. **Calcular prévia** para ver os valores.
4. **Encerrar exercício**. Os lançamentos saem como tipo "E" (exigido pela ECD).
5. Para ajustar algo depois, use **Desfazer o encerramento**, informe o motivo (obrigatório) e confirme: os dois lançamentos são estornados.

## Imobilizado

### Regras de depreciação (grupos)
**Onde fica:** *Imobilizado › Regras de depreciação (grupos)*. Já vêm cadastradas as taxas fiscais da RFB (edificações 4%, máquinas e móveis 10%, veículos de passageiros e computadores 20%, veículos de carga 25%, software 20%; terrenos não depreciam) com contas sugeridas. Edite taxa, vida útil, contas e marque *Deprecia* / *Intangível (amortização)*, ou crie novos grupos.

### Cadastro de bens
**Onde fica:** *Imobilizado › Cadastro de bens*.
1. **Incluir** e escolha o **grupo** (traz taxa e contas).
2. Informe plaqueta, local, nota fiscal e fornecedor, datas de aquisição e de início de uso.
3. Informe **custo**, **valor residual**, **vida útil (meses)** e **método** (linear ou soma dos dígitos), conforme o CPC 27.
4. **Turnos de uso**: 2 turnos acelera a depreciação fiscal em 1,5; 3 turnos em 2,0.
5. Se o bem já vinha depreciado, preencha **Já depreciado** (depreciação acumulada antes do Vorticis).
6. Confirme as contas do bem, da depreciação acumulada e da despesa. **Salvar**.
7. Para dar baixa: informe a **data** e o **motivo** (venda, perda, doação...).

### Cálculo e lançamento da depreciação
**Onde fica:** *Imobilizado › Cálculo e lançamento da depreciação*.
1. Escolha a **competência** e clique em **Calcular**: aparecem a quota contábil e a fiscal e a diferença a controlar no LALUR.
2. **Lançar na contabilidade** gera o lançamento (despesa contra depreciação acumulada) no último dia do mês.
3. **Estornar competência** desfaz o mês mais recente.

## Consultas

Respostas rápidas, sem gerar relatório.

### Saldos de contas
*Consultas › Saldos de contas*: informe a data, clique em **Consultar** e filtre por código ou nome. Mostra saldo e movimento no ano.

### Lançamentos (consulta)
*Consultas › Lançamentos*: informe o período e filtre por conta, texto do histórico, valor ou número do lançamento.

### Diferenças de saldos
*Consultas › Diferenças de saldos*: informe o período e **Analisar**. Mostra o saldo inicial, o final e a diferença de cada conta, com alertas (débitos diferentes de créditos, saldo contrário à natureza, divergência entre partidas e saldos mensais). Se houver divergência interna, use *Reorganizar saldos*.

## Relatórios contábeis

Todos aparecem na tela e têm os botões **PDF**, **Excel (.xlsx)** e **CSV**. Exportar exige a permissão *Exportar relatórios*. Nos PDFs há as opções **Assinatura do sócio/responsável** e **Assinatura do contador**, que incluem o bloco de assinaturas ao final com os dados dos cadastros de sócios e contadores.

### Livro Diário
*Relatórios › Contábeis › Livro Diário*: informe o período e **Gerar**.

### Livro Razão
*Relatórios › Contábeis › Livro Razão*: clique numa conta da lista *Contas analíticas* (ou digite o reduzido) e **Gerar** para ver o razão do período com saldo corrente.

### Balancete de verificação
*Relatórios › Contábeis › Balancete de verificação* (atalho **Ctrl+B**): saldo anterior, débitos, créditos e saldo final. Opções: *Incluir contas zeradas* e *Detalhar participantes* (uma linha por cliente/fornecedor abaixo da conta).

### Balanço Patrimonial e DRE
*Relatórios › Contábeis › Balanço Patrimonial e DRE*: escolha o **nível** de detalhe e **Gerar**. Antes do encerramento, o resultado do período aparece no patrimônio líquido para o balanço fechar; a DRE ignora os lançamentos de encerramento.

### DMPL
*Relatórios › Contábeis › DMPL*: mutações do patrimônio líquido (CPC 26) por componente (capital, reservas, lucros ou prejuízos acumulados). Informe o período e **Gerar**. O saldo final confere com o balanço.

### DLPA
*Relatórios › Contábeis › DLPA*: demonstração de lucros ou prejuízos acumulados (Lei 6.404, art. 186). Informe o período e **Gerar**.

## Livro Contábil Completo

**Para que serve:** gerar em um único PDF o livro com termos, demonstrações e assinaturas, seguindo a ITG 2000.
**Onde fica:** *Relatórios › Contábeis › Livro Contábil Completo*.

1. Informe o **período** e o **número do livro**.
2. Marque o que entra: termo de abertura, Diário, Razão, Balancete, Balanço, DRE, DMPL, DLPA e termo de encerramento.
3. Marque as **assinaturas** (sócio/responsável e contador). O representante legal e o contador vêm dos cadastros; confira nome, CPF e CRC.
4. O **NIRE** vem do cadastro da empresa (somente leitura; aparece aviso se estiver vazio). Os dados de **arquivamento dos atos** são opcionais.
5. Para assinar o PDF com certificado digital, marque **Assinar digitalmente o PDF** (veja [PDF assinado](#pdf-assinado)).
6. Clique em **Gerar Livro Contábil Completo (PDF)**.

As folhas são numeradas, cada parte começa em página nova e o total de folhas do termo é calculado depois da montagem.

## Termos de abertura e encerramento

**Onde fica:** *Relatórios › Contábeis › Termos (abertura e encerramento)*.
1. Informe o período, o livro e o número do livro.
2. O **total de folhas** é opcional: em branco, o termo sai com linha para preencher à mão.
3. Confira representante legal (nome/CPF) e contador (nome/CRC), NIRE e arquivamento.
4. Clique em **Gerar Termo de Abertura (PDF)** ou **Gerar Termo de Encerramento (PDF)**; cada um sai em seu PDF.

## Assinaturas

Os blocos de assinatura dos PDFs usam o **representante legal** (cadastro de Sócios) e o **contador** (cadastro de Contadores, o signatário ECD/ECF ou o responsável técnico). Se faltar algum dado, cadastre em *Cadastros › Empresas › Sócios* e *Contadores*.

## PDF assinado

Nos livros e termos, marque **Assinar digitalmente o PDF** para aplicar a assinatura digital com o certificado padrão (veja [Certificados digitais](08-configuracoes.md#certificados-digitais)). O PDF é assinado (formato PAdES) com o certificado padrão do **contador** ou, na falta dele, do **responsável**. Se a assinatura foi pedida e não há certificado padrão utilizável, o sistema mostra o aviso na tela e **não gera** o PDF: cadastre o certificado ou desmarque a opção.
