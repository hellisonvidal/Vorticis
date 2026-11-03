# Contábil

Menus do módulo Vorticis Contábil. Permissões: cada função depende do seu perfil (veja [Usuários e perfis](01-primeiros-passos.md#usuarios-perfis-e-empresas-por-usuario)).

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Plano de contas

**Para que serve:** a lista de contas da empresa.
**Onde fica:** *Cadastros › Plano de contas*.

### Escolher um plano
1. Se a empresa ainda não tem plano, clique em **Escolher plano**, selecione um modelo e **Usar este plano**. A troca só é possível enquanto não há lançamentos.
2. Para importar de uma planilha, veja [Importar plano de contas](#importar-plano-de-contas).

### Usar um modelo em outra máscara
Na tela *Escolher o plano de contas*, depois de selecionar o modelo, digite em **Máscara dos códigos (opcional)** a máscara que você quer (ex.: `9.9.99.999`; a lista *Sugestões* ajuda) e clique em **Ver prévia**. A prévia mostra, conta a conta, o código do modelo e o código que ele terá na sua máscara. Cada nível é completado com zeros à esquerda (`1.1.1` vira `1.1.01` numa máscara `9.9.99`); se algum nível não couber, ou se dois códigos ficarem iguais, a prévia avisa o problema e **nada é convertido**. Estando tudo certo, **Usar este plano** grava na empresa a cópia convertida do modelo (o plano passa a ser "próprio", com o nome do modelo e a máscara) e as contas de encerramento acompanham os novos códigos.

### Criar novo plano
Na tela *Escolher o plano de contas*, clique em **Criar novo plano**, informe o **nome** do plano e a **máscara** (ex.: `9.9.99.999`, com pelo menos 2 níveis) e clique em **Criar plano**. O plano nasce vazio, só com os grupos principais (Ativo, Passivo, Patrimônio líquido, Receitas e Custos e despesas); use **Incluir** para cadastrar as demais contas, que seguem a máscara. Como substitui o plano da empresa, só é possível enquanto não há lançamentos.

### Converter máscara
**Onde fica:** botão **Converter máscara** na tela do plano (também serve para planos com lançamentos).
1. Digite a máscara desejada e clique em **Ver prévia**. A lista mostra cada conta com o **código atual**, o **código novo** e a situação (*Muda*, *Igual* ou *Atenção*, quando um nível perde zeros à esquerda).
2. Se o resumo apontar problemas (nível que não cabe na nova largura, mudança do número de níveis, códigos repetidos ou conta fora da sua superior), **nada é convertido**: corrija a máscara e veja a prévia de novo.
3. Sem problemas, clique em **Converter plano** e confirme. Todos os códigos são trocados de uma vez; se algo falhar, nada muda.

Os lançamentos, saldos, vínculos com o plano referencial, a hierarquia, o tipo (sintética/analítica) e a natureza das contas **não mudam**: eles apontam para a conta, não para o código. O sistema também atualiza os códigos guardados nas configurações (contas de encerramento, regras de contabilização, duplicatas, bancos e folha) e registra a conversão na Auditoria. Arquivos já entregues (ECD de anos anteriores) mantêm os códigos antigos.

**Mudou a máscara na empresa?** Em *Cadastros › Empresas › aba Contabilidade*, ao salvar com uma máscara nova e o plano já cadastrado, o sistema mostra quantas contas mudam (com exemplos) e pergunta se deseja converter o plano junto; ao confirmar, a máscara e os códigos são gravados na mesma operação. Se a conversão não for possível, nada é gravado e a mensagem diz o motivo.

### A tela do plano
A tela mostra **todas as contas** do plano numa lista (código, reduzido, descrição, tipo, natureza e situação), com o filtro e a barra de busca. A **ficha da conta** abre numa janela por cima da lista:
- **Incluir** abre a ficha de uma conta nova; com uma conta selecionada, o código já vem sugerido como subconta dela;
- **Alterar** abre a ficha da conta selecionada liberada para edição;
- **duplo clique** (ou **Enter**) numa conta, ou **Abrir ficha**, mostra a ficha só para consulta — nela, **Alterar** libera a edição e **Fechar** volta à lista;
- **Salvar** grava e fecha a ficha; **Cancelar** (ou **Esc**) desiste.

### Manter contas
1. **Incluir** e preencha o **código** (ex.: 1.1.01.004), a descrição, a natureza (devedora/credora) e se é **sintética** (grupo) ou **analítica** (recebe lançamentos).
2. O **código reduzido** das analíticas é gerado automaticamente (pode ser editado).
3. Controles: **Exige centro de custo**, **Exige participante** (e o grupo onde os participantes ficam) e **Ativa**.

### Importar contas do Excel
Para **acrescentar** contas ao plano a partir de uma planilha, sem trocar o plano:
1. Clique em **Importar contas do Excel**: abre uma janela por cima da lista.
2. **Procurar...** a planilha (.xlsx ou .csv) com o **código** e a **descrição** das contas (natureza, tipo e saldo são opcionais; sem eles, o sistema deduz pelo grupo).
3. A lista mostra as contas da planilha: as **novas** vêm marcadas e as que **já estão no plano** vêm desmarcadas. Marque ou desmarque as que quiser (**Marcar as novas** / **Desmarcar todas**) e confira a data **Incluídas em**.
4. **Acrescentar ao plano**: as marcadas entram na ordem certa (a conta superior antes das subcontas). Cada conta precisa da conta superior, no plano ou na própria planilha; as que não puderem entrar aparecem embaixo, com o motivo, sem impedir as outras.
5. **Fechar** volta à lista, já com as contas novas.

Para **substituir** o plano inteiro por uma planilha, use [Importar plano de contas](#importar-plano-de-contas).

**Dica:** filtre a lista por código, reduzido ou descrição. Em lançamentos, você pode digitar o reduzido em vez do código completo.

## Importar plano de contas

**Onde fica:** *Integrações › Importar › Plano de contas (planilha Excel/CSV)*.

1. **Procurar** o arquivo .xlsx ou .csv.
2. Em *Colunas*, diga qual coluna é o código, a descrição e, se houver, natureza, tipo (S/A) e saldo.
3. Para converter os códigos, marque *Converter os códigos para a máscara* e informe a máscara (ex.: `9.9.99.999`: 1101001 vira 1.1.01.001).
4. Confira a **pré-visualização** e clique em **Importar plano**. Só substitui o plano enquanto não houver lançamentos.
5. **Converter plano atual para a máscara** troca os códigos das contas existentes, mesmo com movimento, sem alterar lançamentos e hierarquia.

## Centros de custo

O cadastro é o mesmo do Fiscal (*Cadastros › Centros de custo* nos dois módulos): as notas fiscais com centro de custo são contabilizadas nele (veja [Centros de custo no Fiscal](05-fiscal.md#centros-de-custo)).

**Onde fica:** *Cadastros › Centros de custo*. Só é usado se a empresa ligou "Usar centros de custo" (aba Contabilidade); a máscara também é definida lá.

A tela mostra a **lista dos centros de custo**; a ficha abre numa janela por cima (**Incluir**, **Alterar**, duplo clique ou **Enter**).

1. **Incluir**: código (obrigatório nos níveis 2 ou mais, conforme a máscara), descrição e superior.
2. Marque **Sintético** para grupos que não recebem lançamentos. **Ativo** controla o uso.

## Históricos padrão

**Para que serve:** textos prontos para os lançamentos.
**Onde fica:** *Cadastros › Históricos*.

A tela mostra a lista dos históricos (com filtro por código ou texto); a ficha abre numa janela por cima.

1. **Incluir** (ou **Alterar**, ou duplo clique no histórico); o código é sequencial. Digite o texto fixo, por exemplo `Pagamento da NF {DOCUMENTO} de {PARTICIPANTE}`, e clique nas variáveis para inseri-las. **Salvar** fecha a janela.
2. No lançamento, digite o código do histórico: o texto entra e o cursor vai para o fim, para completar com o complemento livre.

**Variáveis disponíveis:** `{DOCUMENTO}`, `{PARTICIPANTE}`, `{DATA}`, `{MES}`, `{ANO}`, `{MES_ANTERIOR}`, `{COMPETENCIA}`, `{CONTA}`, `{VALOR}` e `{COMPLEMENTO}`. As que o sistema não consegue resolver ficam para você completar.

## Bloqueio de lançamentos

**Para que serve:** proteger os meses já conferidos ou entregues (ECD, balanço): nenhum lançamento com data até o dia informado pode ser incluído, alterado ou excluído.
**Onde fica:** *Cadastros › Bloqueio de lançamentos*.

1. Informe a data em **Bloquear lançamentos até** e clique em **Salvar**.
2. Para liberar, apague a data e salve.

A mesma data aparece na aba Contabilidade de *Cadastros › Empresas*. Para travar um mês inteiro com fechamento, use também o [Fechamento de períodos](#fechamento-de-periodos).

## Lançamentos

**Para que serve:** registrar os fatos contábeis (partidas dobradas: débitos = créditos).
**Onde fica:** *Lançamentos › Contábeis › Lançamento contábil* (atalho **Ctrl+L**).

A tela mostra os **lançamentos efetuados** do período, com os botões **Novo (Ctrl+N), Editar, Consultar, Excluir, Estornar** e Atualizar. Os estornados continuam na lista, marcados.

### Incluir um lançamento (pop-up)
1. Clique em **Novo (Ctrl+N)**. Abre o pop-up de digitação, com os campos **um embaixo do outro** (preenchimento na vertical). Embaixo ficam só os totais (**Débitos**, **Créditos** e **Diferença**) e os botões.
2. Informe a **data**.
3. Em **Conta a débito (D)** e **Conta a crédito (C)**, digite o **código reduzido** (no primeiro campo) ou o código/nome da conta (no segundo, com lista de sugestões). Para procurar no plano de contas, clique na **lupa** dentro do campo (ou tecle **F4**): abre uma lista só com as contas analíticas, com caixa para digitar parte do código ou do nome; **Enter** ou duplo clique escolhe. O **nome da conta** aparece ao lado, para conferir.
4. Informe o **valor** (formata como 1.500,00), o **documento** (NF, recibo, boleto), o **histórico padrão** (código) e/ou o **histórico**, o **centro de custo** (o campo só aparece se a empresa usa centros de custo: *Cadastros › Empresas › Contabilidade*) e o **participante**, quando exigidos.
5. Para um lançamento com **várias partidas**, clique em **+ Mais partidas**, logo abaixo da conta a crédito. Aparece uma linha compacta com a conta a débito (D) e/ou a conta a crédito (C), o valor e o histórico; repita para cada partida e use o **✕** para remover uma. O rodapé mostra a diferença: o lançamento só salva quando débitos e créditos são iguais.
6. **Salvar (Ctrl+S)**.

### Teclado
| Tecla | Efeito |
|---|---|
| Enter / Tab | Vai ao próximo campo (de cima para baixo) |
| Shift+Enter | Volta ao campo anterior |
| **Enter no último campo** | Salva, se o lançamento já fecha; senão segue para a próxima partida |
| Ctrl+Enter | Salva |
| F4 (ou a lupa) | Pesquisa a conta, o histórico ou o centro de custo |
| ↓ na última linha | Acrescenta uma partida (o mesmo que **+ Mais partidas**) |
| ↑ em partida nova e vazia | Cancela a partida |
| Ctrl+Delete | Exclui a partida |

### Uma partida ou várias
- Só o formulário, com conta débito **e** conta crédito: é um lançamento completo.
- Com **+ Mais partidas**, todas as linhas (o formulário e as partidas acrescentadas) formam **um único lançamento**, com a data, o documento e o centro de custo do formulário (a partida pode ter o seu próprio histórico). Débitos e créditos precisam fechar. Se faltar a contrapartida, o sistema pergunta e oferece incluir a linha.
- Exemplo: venda de 300,00 com 200,00 de mercadorias e 100,00 de serviços. No formulário, débito em Clientes e valor 300,00; em **+ Mais partidas**, uma linha com crédito em Vendas de mercadorias 200,00 e outra com crédito em Receita de serviços 100,00.

### Editar um lançamento
Dê **duplo clique** (ou Enter) no lançamento da lista, ou clique em **Editar**. O que acontece ao salvar depende da configuração **Usar estorno de lançamentos** da empresa (aba Contabilidade do cadastro da empresa):
- **Ligada (padrão):** o original é **estornado na data dele** e o corrigido é gravado, tudo numa única operação.
- **Desligada:** não há estorno. O próprio lançamento é alterado, **com o mesmo número**, e a alteração fica registrada na *Auditoria*.

**Consultar** abre só para leitura. Só lançamentos digitados à mão podem ser editados.

### Excluir um lançamento
1. Selecione o lançamento na lista e clique em **Excluir**.
2. O sistema pergunta: "Deseja realmente excluir o lançamento nº ... de ..., valor R$ ...?". Confirme para excluir.

O lançamento e suas partidas são removidos, os saldos das contas são atualizados e a exclusão fica registrada na *Auditoria* (o registro do que foi excluído permanece). Se o lançamento faz par com um estorno, os dois são excluídos juntos. A numeração dos demais lançamentos **não muda**; para eliminar a lacuna use *Lançamentos › Utilitários › Reorganizar lançamentos*.

O sistema **não exclui** (e explica o motivo na tela) quando:
- o período do lançamento está **fechado** (reabra-o em *Fechamento de períodos*) ou o exercício está **encerrado**;
- já existe **ECD gerada** para o ano;
- a data está antes da data de bloqueio da empresa, ou a partida está **conciliada** (desfaça a conciliação antes);
- o lançamento veio de outro módulo (**Fiscal**, **Folha**, **Imobilizado** ou **Encerramento do exercício**): desfaça a operação no módulo de origem, por exemplo em *Cálculos › Folha › Contabilização* ou *Integrações › Contabilizar notas fiscais*.

### Estornar
Disponível quando a empresa usa estorno de lançamentos (configuração acima).
1. Selecione o lançamento na lista.
2. Informe a **data** do estorno e o **motivo** (obrigatório).
3. Clique em **Estornar**. É criado um lançamento inverso; o original continua na lista, marcado como estornado.

### Regras e erros comuns
- "Período fechado": reabra a competência em *Fechamento de períodos*.
- "Data bloqueada": a empresa tem uma data em *Cadastros › Bloqueio de lançamentos*.
- Débitos diferentes de créditos: o lançamento não é salvo.
- Conta sintética não recebe lançamento; use a analítica.
- Para remover um lançamento já gravado use **Excluir** (veja acima); com estorno ligado, também é possível estornar.
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

### Extratos bancários (OFX e PDF)
**Onde fica:** *Integrações › Importar › Extratos bancários (OFX e PDF)*.

1. **Carregue o arquivo primeiro:** clique em **Procurar...** e escolha o extrato. Pode ser o **OFX** do internet banking ou o extrato em **PDF**, **TXT** ou **CSV**. O arquivo é lido na hora (o botão **Carregar** lê de novo).
2. **OFX:** os movimentos aparecem direto na aba **Movimentos**; siga para o passo 4.
3. **PDF, TXT ou CSV — layout personalizado conforme o documento:** aparece o quadro **2. Layout do extrato**, já com um layout **sugerido pelo próprio documento**. Abra a aba **Documento carregado** para ver cada linha do arquivo separada em colunas (Coluna 1, Coluna 2...) e, na coluna *Movimento*, as linhas que viraram movimento.
   - Ajuste, olhando o documento: **colunas separadas por** (espaços para PDF/TXT, ponto e vírgula para CSV), **coluna da data** e o **formato da data** (para datas sem ano, informe o **Ano**), **coluna do valor** (0 = automático: com valor e saldo na mesma linha, fica o valor), **da descrição** (0 = junta o texto das outras colunas) e **do documento** (0 = sem documento).
   - **Ignorar linhas com**: palavras que descartam a linha, separadas por vírgula (ex.: SALDO, TOTAL).
   - **Inverter sinal**: para extratos que mostram as saídas como positivas (ex.: fatura de cartão).
   - Cada ajuste é aplicado na hora: confira a aba **Movimentos**. Valores com **D** ou sinal de menos são saídas; com **C** ou sem sinal, entradas.
   - Dê um **Nome** (ex.: o nome do banco) e clique em **Salvar layout**. Da próxima vez, carregue o extrato desse banco e escolha o layout em **Layout salvo**. **Novo** volta à sugestão pelo documento; **Excluir layout** apaga o layout escolhido. Os layouts valem para todas as empresas.
4. Informe a **conta do banco** e as contrapartidas padrão de **entradas** e **saídas**; use *Preencher vazias* e ajuste linha a linha.
5. **Pré-visualizar** e **Importar**. Movimentos já importados não são duplicados (no OFX, pela identificação do banco; no PDF/TXT/CSV, pela data, valor, descrição e conta do banco); se o lançamento for estornado, pode ser importado de novo.

**Atenção:** PDF escaneado (imagem) não tem texto para ler; peça ao banco o OFX ou o PDF gerado pelo internet banking.

### Lançamentos contábeis (layout padrão)
**Onde fica:** *Integrações › Importar › Lançamentos contábeis (layout padrão)* (não está mais no menu Lançamentos).

Use **Gerar planilha modelo (.xlsx)**, preencha as colunas `DATA;DEBITO;CREDITO;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO`, escolha o arquivo, **Pré-visualizar** e **Importar**.
- **Débito e crédito em colunas separadas:** a linha com a conta a débito **e** a conta a crédito é um lançamento completo.
- **Só débito ou só crédito:** preencha apenas uma das duas colunas. As linhas seguintes com o **mesmo documento e a mesma data** formam um único lançamento, que se fecha quando os débitos igualam os créditos (no modelo, a venda de 06/03 tem um débito e dois créditos). Se o grupo não fechar, a pré-visualização mostra o erro de débitos diferentes de créditos.
- **Valor negativo** inverte o lado (débito vira crédito).
- Planilhas antigas, com as colunas `LANCAMENTO;DATA;CONTA;DC;VALOR`, continuam sendo aceitas.

### Importar SPED (ECD e ECF)
**Para que serve:** trazer para a empresa os dados de um arquivo SPED feito em outro sistema: a **ECD** (plano de contas, centros de custo, históricos, saldos e lançamentos) ou a **ECF** (e-Lalur/e-Lacs, Parte B, apurações do IRPJ e da CSLL e, se faltar, o plano de contas).
**Onde fica:** *Integrações › Importar › Importar SPED (ECD e ECF)*.
1. **1. Arquivo:** clique em **Procurar...** e escolha o arquivo .txt do SPED. O programa lê o arquivo na hora e mostra o **Tipo reconhecido**. Se o arquivo for da EFD (Fiscal), o aviso diz em qual módulo importar. O botão **Conferir arquivo** lê de novo.
2. **2. O que importar:** escolha a forma de importar (por exemplo, tudo, só o plano de contas e os saldos, ou só o plano). A explicação aparece logo abaixo. Marque as opções que quiser:
   - *Substituir o plano de contas atual pelo do arquivo*: só vale antes do primeiro lançamento. Desmarcado, o que já existe é mantido e o que for igual no arquivo é ignorado.
   - *Ajustar os códigos das contas à máscara do plano da empresa*.
   - *Cadastrar os signatários do arquivo* (ECD).
   - *Ignorar o que tiver erro e importar o restante*: sem esta opção, qualquer erro nos lançamentos impede a importação, e nada é gravado.
3. **Aba Prévia** (nada é gravado ainda): mostra o período, a empresa do arquivo, e para cada tipo de informação quantos registros são **novos**, quantos **já existem** (serão ignorados) e quantos têm **conflito**, com exemplos. Mais abaixo vêm os erros, avisos e informações, com a linha do arquivo. Se o CNPJ do arquivo não for o da empresa ativa, a importação é bloqueada: abra a empresa certa em *Cadastros*.
4. **3. Importar:** clique em **Importar** e confirme. A barra mostra o andamento e **Cancelar importação** desfaz tudo o que foi feito até ali. Tudo é gravado numa só vez: se algo falhar, nada fica pela metade. O mesmo arquivo não pode ser importado duas vezes; para repetir, desfaça a importação anterior.
5. **Aba Relatório:** mostra o que foi importado, o que já existia e o que teve erro. **Salvar relatório (CSV)** grava uma planilha que abre no Excel.
6. **Aba Histórico de importações:** lista as importações feitas (data, arquivo, período, usuário). Selecione uma e clique em **Desfazer importação** para remover o que ela criou. Se algo já foi usado, contabilizado ou fechado, nada é removido e o programa explica o motivo.

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

## Contabilização em lote

**Para que serve:** escolher *quando* os documentos dos outros módulos (notas fiscais, baixas de duplicatas e folha de pagamento) viram lançamentos contábeis. Com o tipo de contabilização **On-line** (o padrão, como sempre foi), cada nota, baixa ou folha gera o lançamento na hora. Com **Em lote**, os módulos só deixam uma *pendência de contabilização*, e você gera os lançamentos de uma vez, depois de conferir.

**Como ligar:** em *Cadastros › Parâmetros*, grupo *Tipo de contabilização*, escolha **Em lote** e clique em **Salvar**. A mudança vale para a empresa ativa e não altera os lançamentos que já existem. Para voltar ao normal, escolha **On-line**.

**Onde fica:** *Integrações › Exportar › Contabilização em lote* (o item só aparece quando a empresa está em lote).

**Como usar:**
1. Escolha a **origem** (Fiscal, baixas de duplicatas ou todas) e, se quiser, o período (*De* e *Até*). Clique em **Consultar**.
2. A tela mostra um resumo por mês e origem e a lista das pendências, com data, descrição e valor.
3. Marque as pendências (ou **Marcar todas**) e clique em **Ver prévia** para conferir as partidas do lançamento sem gravar nada.
4. Clique em **Contabilizar selecionadas**. O sistema gera os lançamentos pelas mesmas regras da contabilização on-line (regras de contabilização das notas, contas dos Parâmetros do Fiscal, regras da folha). As pendências que não puderem ser contabilizadas (por exemplo, sem regra ou período fechado) continuam na lista, com o motivo, e não impedem as outras.
5. Cada vez que você contabiliza, é criado um **lote**. Em *Lotes já contabilizados*, selecione um lote e clique em **Desfazer lote** para estornar os lançamentos dele: as pendências voltam para a lista e podem ser contabilizadas de novo.

**O que gera pendência no modo Em lote:** a gravação ou importação de notas fiscais, a baixa de duplicatas (a baixa é registrada e o saldo da duplicata diminui na hora; só o lançamento contábil espera), o botão **Contabilizar** da folha e das provisões da folha. As provisões de tributos (Simples, IRPJ/CSLL, PIS/COFINS) continuam sendo lançadas na hora. Se a baixa for estornada antes de entrar em um lote, a pendência dela é cancelada sozinha.

**Bens do imobilizado:** se alguma nota do lote tem item de ativo imobilizado, a tela mostra um aviso azul com o botão **Cadastrar bens das notas** (abre o Cadastro de bens já preenchido, um item de cada vez). Ao contabilizar notas com esses itens, o Cadastro de bens abre sozinho. Veja [Bens a cadastrar das notas](#bens-a-cadastrar-das-notas).

**Dica:** o programa que processa cada origem precisa estar instalado.

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
2. Confira a **conta de apuração do resultado** e as contas de **lucros** e **prejuízos acumulados** (vêm da aba Contabilidade da empresa). Cada campo tem uma **caixa de pesquisa**: digite parte do código, o número reduzido ou parte do nome e escolha a conta na lista; ou clique na **lupa** dentro do campo (ou tecle **F4**) para abrir a pesquisa em uma janela, com filtro por código ou nome, e escolha com **Enter** ou duplo clique. Só aparecem contas analíticas do plano desta empresa (de resultado para a apuração; de patrimônio líquido para lucros e prejuízos).
3. **Calcular prévia** para ver os valores. Antes de calcular (e antes de encerrar) o sistema confere as três contas: se alguma não existe no plano da empresa, não está na máscara do plano, é sintética (agrupadora) ou é de outro grupo (por exemplo, uma conta do Ativo), aparece o aviso *"Corrija as contas do encerramento antes de continuar"* com o motivo de cada conta, e nada é gravado. Corrija com a caixa de pesquisa e tente de novo.
4. **Encerrar exercício**. Os lançamentos saem como tipo "E" (exigido pela ECD).
5. Para ajustar algo depois, use **Desfazer o encerramento**, informe o motivo (obrigatório) e confirme: os dois lançamentos são estornados.

## Imobilizado

### Regras de depreciação (grupos)
**Onde fica:** *Imobilizado › Regras de depreciação (grupos)*. A tela mostra só a **lista das regras já cadastradas**; para incluir ou alterar, use **Incluir**, **Alterar**, duplo clique na linha ou **Enter**: a ficha da regra abre numa janela por cima da lista e **Salvar** a fecha (**Cancelar**, **Esc** ou o **X** fecham sem gravar). Já vêm cadastradas as taxas fiscais da RFB (edificações 4%, máquinas e móveis 10%, veículos de passageiros e computadores 20%, veículos de carga 25%, software 20%; terrenos não depreciam) com contas sugeridas. Edite taxa, vida útil, contas e marque *Deprecia* / *Intangível (amortização)*, ou crie novos grupos.

### Cadastro de bens
**Onde fica:** *Imobilizado › Cadastro de bens*. A tela mostra a lista dos bens (com filtro por código, descrição ou plaqueta); a ficha do bem abre numa janela por cima (**Incluir**, **Alterar**, duplo clique ou **Enter**).
1. **Incluir** e escolha o **grupo** (traz taxa e contas).
2. Informe plaqueta, local, nota fiscal e fornecedor, datas de aquisição e de início de uso.
3. Informe **custo**, **valor residual**, **vida útil (meses)** e **método** (linear ou soma dos dígitos), conforme o CPC 27.
4. **Turnos de uso**: 2 turnos acelera a depreciação fiscal em 1,5; 3 turnos em 2,0.
   - **Depreciar a partir da data de aquisição, proporcional aos dias** (opcional): a depreciação começa no dia da aquisição, e não no mês do início de uso. No primeiro mês entram só os dias desde a aquisição (inclusive). Exemplo: bem comprado em 21/04, quota de R$ 1.000,00 → em abril deprecia 10 de 30 dias = R$ 333,33. A parte que faltou é lançada no mês seguinte ao fim da vida útil. Na baixa, o mês da baixa deprecia os dias até a véspera. Sem a opção, vale a regra normal: quota cheia a partir do mês do início de uso, e o mês da baixa não deprecia.
5. Se o bem já vinha depreciado, preencha **Já depreciado** (depreciação acumulada antes do Vorticis).
6. Confirme as contas do bem, da depreciação acumulada e da despesa. **Salvar** (a janela fecha).
7. Para dar baixa: informe a **data** e o **motivo** (venda, perda, doação...).

**Bem que vem de uma nota fiscal:** quando o Cadastro de bens abre sozinho a partir de uma nota de compra (veja [Bens a cadastrar das notas](#bens-a-cadastrar-das-notas)), aparece uma faixa azul no alto e o bem já vem preenchido: descrição, valor, data da nota, início do uso (data de entrada), nota fiscal/série, fornecedor e, quando o sistema reconhece o tipo de bem, o **grupo** com a vida útil e as contas. Confira, escolha o grupo se estiver em branco e clique em **Salvar**: o bem fica ligado ao item da nota e o sistema abre o próximo item, se houver. **Cancelar** deixa o item esperando na lista *Bens a cadastrar das notas*.

### Bens a cadastrar das notas
**Para que serve:** não esquecer de cadastrar como bem o que foi comprado para o ativo imobilizado. Toda nota fiscal de **entrada** (digitada, importada por XML, por chave ou em lote) que tenha item de ativo imobilizado deixa esse item registrado aqui. Conta como ativo imobilizado o item com **CFOP 1.551, 2.551, 3.551, 1.406 ou 2.406**, o produto cadastrado com tipo **08 – Ativo imobilizado**, ou o item cuja regra fiscal ou regra de contabilização lança em conta de bens do imobilizado.
**Onde fica:** *Imobilizado › Bens a cadastrar das notas*.

**O que acontece ao gravar a nota:**
- Empresa com contabilização **On-line**: ao gravar ou importar a nota, o **Cadastro de bens** abre sozinho, já preenchido com o primeiro item (os outros vêm em seguida). Você confere e clica em **Salvar**, ou em **Cancelar** para deixar para depois.
- Empresa com contabilização **Em lote**: o item só fica registrado nesta lista. Ao usar a [Contabilização em lote](#contabilizacao-em-lote), a tela avisa que há bens esperando e abre o Cadastro de bens preenchido para as notas contabilizadas.
- Fiscal em programa separado, sem o Contábil aberto: o sistema só registra o item e avisa. Depois, abra o Contábil e use esta lista.

**Como usar a lista:**
1. A lista mostra nota, fornecedor, item, data, valor e o motivo de ser bem. Marque os itens (ou **Marcar todos**).
2. **Cadastrar** abre o Cadastro de bens preenchido, um item de cada vez.
3. **Descartar** tira da lista o que não é bem (pede confirmação).
4. **Mostrar também os já tratados** inclui os itens já cadastrados ou descartados.

Um item vira bem uma vez só: reprocessar ou regravar a nota não cria outro. Se você excluir o bem, o item volta para a lista. É preciso ter a permissão de lançamentos do Contábil para cadastrar ou descartar.

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

### Balanço Patrimonial
*Relatórios › Contábeis › Balanço Patrimonial*: escolha a **data** do balanço e o **nível** de detalhe e clique em **Gerar**. Antes do encerramento, o resultado do período aparece no patrimônio líquido para o balanço fechar. Use os botões de exportação para PDF, Excel ou CSV.

### Demonstração do Resultado (DRE)
*Relatórios › Contábeis › Demonstração do Resultado (DRE)*: informe o **período** e o **nível** de detalhe e clique em **Gerar**. A DRE ignora os lançamentos de encerramento. Exporte em PDF, Excel ou CSV. O Livro Contábil Completo continua incluindo o Balanço e a DRE.

### DMPL
*Relatórios › Contábeis › DMPL*: mutações do patrimônio líquido (CPC 26) por componente (capital, reservas, lucros ou prejuízos acumulados). Informe o período e **Gerar**. O saldo final confere com o balanço.

### DLPA
*Relatórios › Contábeis › DLPA*: demonstração de lucros ou prejuízos acumulados (Lei 6.404, art. 186). Informe o período e **Gerar**.

### DFC
*Relatórios › Contábeis › DFC*: Demonstração dos Fluxos de Caixa (CPC 03), que mostra de onde veio e para onde foi o dinheiro da empresa no período.

1. Informe o **período**, escolha o **método** (*Método indireto* ou *Método direto*) e o **nível** de detalhe, e clique em **Gerar**.
2. No **método indireto** a demonstração parte do lucro líquido do período, soma a depreciação e as demais despesas que não saíram do caixa e acerta as variações de ativos e passivos operacionais (por exemplo, clientes a receber e fornecedores a pagar). No **método direto** aparecem os recebimentos e pagamentos do período e, abaixo, a conciliação do lucro com o caixa das operações.
3. A demonstração tem três blocos: **Atividades operacionais**, **Atividades de investimento** (compra e venda de bens, participações) e **Atividades de financiamento** (empréstimos, capital, distribuição de lucros). No fim aparecem o aumento ou a redução de caixa, o caixa no início e o caixa no fim do período.
4. Embaixo da tela o programa avisa se o caixa **confere com o Balanço Patrimonial** nas duas datas. Se aparecer o aviso de que não confere, alguma conta está sem a classificação certa: ajuste em [Classificação da DFC e do EBITDA](#classificacao-da-dfc-e-do-ebitda) e gere de novo.
5. Exporte em PDF, Excel ou CSV. Para incluir a DFC no PDF do livro, marque **DFC (fluxos de caixa)** no [Livro Contábil Completo](#livro-contabil-completo). A DFC não vai no arquivo da ECD, porque o leiaute do SPED Contábil não tem registro próprio para ela.

Para comparar com outros períodos, marque **Comparativo** (veja [Comparativo](#comparativo)).

### EBITDA
*Relatórios › Contábeis › EBITDA* (também chamado LAJIDA: lucro antes de juros, impostos sobre o lucro, depreciação e amortização). Escolha a visão e clique em **Gerar**:

- **Demonstração do período**: parte da receita bruta, tira as deduções (receita líquida), tira custos e despesas e a depreciação (lucro operacional), soma a depreciação de volta e chega ao **EBITDA**. Em seguida mostra o **EBITDA ajustado**, que retira as receitas e despesas que você marcou como *não recorrentes*, e as **margens** (EBITDA dividido pela receita líquida). No fim há a conciliação com o lucro líquido da DRE.
- **Mensal**: um quadro com os doze meses do ano da data inicial e o total do ano (receita líquida, lucro operacional, EBITDA, margem e EBITDA ajustado).
- **Por centro de custo**: uma linha para cada centro de custo, com as partidas sem centro em *Sem centro de custo*, e o total.

Quais contas são depreciação, resultado financeiro, IR e CSLL ou itens não recorrentes se define em [Classificação da DFC e do EBITDA](#classificacao-da-dfc-e-do-ebitda). A demonstração do período aceita **Comparativo** e **Análise vertical** (cada linha como % da receita líquida).

### Classificação da DFC e do EBITDA
*Relatórios › Contábeis › Classificação da DFC e do EBITDA*: diz ao programa como tratar cada conta na DFC e no EBITDA. O programa já traz uma **sugestão automática**, feita pelo nome da conta, pela natureza e pelo plano referencial, então muitas vezes não é preciso mexer em nada.

1. Contas do **balanço**: escolha *Caixa e equivalentes*, *Atividade operacional*, *Atividade de investimento*, *Atividade de financiamento* ou *Contrapartida de ajuste (sem caixa)* (usada, por exemplo, na depreciação acumulada).
2. Contas de **resultado**: escolha *Receita*, *Dedução da receita*, *Custo ou despesa operacional*, *Depreciação, amortização e exaustão*, *Resultado financeiro* ou *IR e CSLL*.
3. Marque **Não recorrente** nas receitas e despesas eventuais (ganho na venda de um bem, multa, indenização) para que o EBITDA ajustado as desconsidere.
4. A coluna *Sugestão automática* mostra o que o programa sugeriu. **Restaurar sugestão automática** volta tudo para ela na tela. Clique em **Salvar classificação** para gravar; só as mudanças ficam registradas. Salvar exige a permissão de manter o plano de contas.

### Comparativo
Todos os relatórios contábeis (Balancete, Razão, Balanço, DRE, DMPL, DLPA, DFC e EBITDA) e os relatórios gerenciais *Contabilização por centro de custo*, *Receita por centro de custo* e *Orçado x Realizado* têm a opção **Comparativo**, logo acima da grade:

1. Marque **Comparativo**. Escolha **Período anterior** (o período de mesma duração logo antes; para um mês, o mês anterior) e/ou **Mesmo período do ano anterior**.
2. Se quiser, marque até **3 outros períodos**: em cada linha marque *Usar*, escolha *Mês*, *Trimestre*, *Ano* ou *Intervalo* e informe a data (em Mês, Trimestre e Ano, qualquer data dentro do período; no Intervalo, as datas inicial e final).
3. Escolha as colunas extras: **Variação em R$** (período atual menos o período comparado), **Variação em %** (variação dividida pelo valor comparado) e, nos relatórios que têm um total de referência (Balanço, DRE e EBITDA), a **Análise vertical (%)**, que mostra cada linha como % do total do ativo ou da receita.
4. Clique em **Gerar**. O relatório mostra uma coluna para o período atual e uma para cada período comparado; PDF, Excel e CSV saem exatamente como a tela (o PDF sai em paisagem). Desmarque **Comparativo** e gere de novo para voltar ao relatório normal.

Observações: no **Balanço** só importa a data final (as colunas são saldos em cada data). No **Balancete** e na **Contabilização por centro de custo** os valores são saldos (positivo = devedor, negativo = credor). Na **Receita por centro de custo** cada centro mostra três linhas por período: *Receita*, *Custos e despesas* e *Margem*. No **Orçado x Realizado** cada conta mostra o *Orçado* e o *Realizado* de cada período, somando os meses (a opção **Mês a mês** não tem comparativo; desmarque-a para comparar). No **Razão** o comparativo resume a conta escolhida em saldo anterior, débitos, créditos e saldo final. Na **DMPL** o comparativo usa a coluna *Total*. Quando uma linha não existe em um dos períodos, o valor aparece zerado e a variação % fica em branco.

## Relatórios gerenciais

O menu só aparece quando o parâmetro **Contabilidade gerencial** está ligado em *Cadastros › Parâmetros* (grupo *Centros de custo e contabilidade gerencial*). Ao ligar ou desligar e salvar, o menu muda na hora.

*Relatórios › Gerenciais* reúne relatórios para acompanhar custos, receitas e metas por centro de custo. Todos aparecem na tela e têm os botões **PDF**, **Excel (.xlsx)** e **CSV**, com as mesmas opções de assinatura dos relatórios contábeis (veja [Assinaturas](#assinaturas) e [PDF assinado](#pdf-assinado)). Os lançamentos de encerramento do exercício não entram. Os centros de custo vêm do cadastro [Centros de custo](#centros-de-custo): cada centro sintético mostra a soma dos centros analíticos abaixo dele.

### Contabilização por centro de custo
*Relatórios › Gerenciais › Contabilização por centro de custo*: mostra, para cada centro de custo, os débitos, créditos e o saldo do período e, abaixo, as contas com movimento. Marque **Comparativo** para comparar o saldo com outros períodos (veja [Comparativo](#comparativo)); nesse modo o detalhe dos lançamentos não é mostrado.
1. Informe o período (datas inicial e final).
2. Se quiser, escolha um **centro** (o relatório mostra ele e os centros abaixo dele), digite uma **conta ou grupo** (por exemplo, 4.2 mostra só as contas que começam com 4.2) e o **nível do centro** (1 mostra só os centros de topo, com as contas somadas; 0 mostra todos).
3. Marque **Detalhar lançamentos** para ver cada lançamento (data, número e histórico) abaixo da conta.
4. Clique em **Gerar**. Partidas sem centro de custo aparecem em *Sem centro de custo*.

### Orçamento
*Relatórios › Gerenciais › Orçamento*: onde se digita o orçamento do ano. Cada linha é uma conta de resultado (receita, custo ou despesa), um centro de custo (opcional) e o valor de cada mês, de Jan a Dez. Quem não tem a permissão *Manter o orçamento* só consulta.
1. Informe o **ano** e clique em **Abrir orçamento do ano**.
2. Clique em **Adicionar linha** e digite o código da conta (a descrição aparece sozinha), o centro e os valores dos 12 meses. **Remover linha** apaga a linha selecionada.
3. Para partir do que já existe, informe o **reajuste (%)** (por exemplo, 5 para mais 5%; negativo reduz) e use **Copiar orçamento do ano anterior** ou **Copiar realizado do ano anterior**. A grade é preenchida para você conferir e ajustar; nada é gravado ainda.
4. Para importar de uma planilha, escolha o arquivo (.xlsx ou .csv) em **Procurar...** e clique em **Importar planilha**. A primeira linha tem os títulos: *Conta*, *Centro de custo* (opcional) e *Jan* a *Dez* (ou, em vez dos meses, as colunas *Mês* de 1 a 12 e *Valor*). Linhas repetidas da mesma conta e centro são somadas. Se alguma linha tiver erro, o sistema informa o número da linha e não importa nada.
5. Clique em **Salvar orçamento**. Ele substitui o orçamento anterior do mesmo ano.

Digite os valores como positivos, na natureza da conta: a receita e a despesa orçadas são números positivos.

### Orçado x Realizado
*Relatórios › Gerenciais › Orçado x Realizado*: compara o orçamento com o que foi lançado na contabilidade.
1. Informe o **ano** e o **mês inicial e final** (1 a 12).
2. Se quiser, filtre por **centro** ou por **conta ou grupo**. Marque **Somar os centros** para ver uma linha por conta, sem separar por centro.
3. Marque **Mês a mês** para ver uma linha por mês com o acumulado; sem essa opção, o relatório mostra o total dos meses escolhidos.
4. Clique em **Gerar**. As colunas são *Orçado*, *Realizado*, *Diferença* (realizado − orçado) e *%* (realizado ÷ orçado). No total do período aparecem ainda as receitas, os custos e despesas e o resultado. Para comparar o orçado e o realizado de períodos diferentes, marque **Comparativo** (sem a opção *Mês a mês*; veja [Comparativo](#comparativo)).

### Relação de custos
*Relatórios › Gerenciais › Relação de custos*: lista os custos e despesas (contas de resultado de natureza devedora, inclusive deduções da receita) do período, com a **participação %** de cada linha no total.
1. Informe o período e, se quiser, centro, conta ou grupo e nível.
2. Em **agrupamento**, escolha *Por centro de custo (e contas)* ou *Por conta (e centros)*.
3. Em **comparação**, escolha *Período anterior (mesma duração)*, *Mesmo período do ano anterior* ou *Sem comparação*. Com comparação, o relatório mostra o valor do outro período, a variação e a variação %.
4. Clique em **Gerar**.

### Receita por centro de custo
*Relatórios › Gerenciais › Receita por centro de custo*: receita de cada centro mês a mês (até 24 meses), com o **% do total**, os custos e despesas do centro, a **margem** (receita − custos − despesas) e a **margem %** (margem ÷ receita). Informe o período, o centro e o nível (opcionais) e clique em **Gerar**. Para comparar com outros períodos, marque **Comparativo** (veja [Comparativo](#comparativo)).

## Relatórios BI

*Relatórios › BI* reúne as ferramentas de análise da empresa ativa: o painel com gráficos, a tabela de indicadores, o cubo (tabela dinâmica) e a exportação dos dados para o Power BI ou o Excel. Todas usam a mesma permissão dos relatórios de demonstrações; a exportação para arquivos pede também a permissão de exportar relatórios.

### Painel de indicadores
*Relatórios › BI › Painel de indicadores*: uma visão rápida do período.
1. Informe o **período** (data inicial e final) e clique em **Gerar**.
2. Os **cartões** mostram o valor de cada indicador e a variação em relação ao **período anterior** (o período de mesma duração logo antes). A variação aparece em verde quando melhorou e em vermelho quando piorou (por exemplo, despesa maior é ruim e lucro maior é bom); o sinal ▲ ou ▼ mostra se subiu ou caiu. Aparecem: receita líquida, custos e despesas, lucro líquido, EBITDA, margens, liquidez corrente, seca e geral, endividamento, ROE, ROA, giro do ativo e os prazos médios de recebimento, pagamento e estocagem (estes só quando a empresa tem contas de clientes, fornecedores e estoques com saldo).
3. Os **gráficos** mostram a receita e o lucro mês a mês nos últimos 12 meses (até o mês da data final), os custos e despesas por grupo de contas (as deduções da receita, como impostos sobre vendas, entram aqui como contas de resultado devedoras), a composição dos custos e despesas por centro de custo e a **cascata do resultado**, que parte da receita bruta e mostra, passo a passo, o que a diminui até chegar ao lucro líquido.
4. Use **PDF**, **Excel** ou **CSV** para levar a tabela de indicadores com o período e o período anterior.

Os indicadores de liquidez, endividamento, ROE, ROA e giro usam o Balanço na data final do período; o programa reconhece o ativo e o passivo circulante e não circulante pelos nomes dos grupos do plano de contas (por exemplo, "Ativo Circulante"). Se algum indicador aparecer como "—", falta informação para calcular (por exemplo, não há dívidas de curto prazo para dividir).

### Análise de indicadores
*Relatórios › BI › Análise de indicadores*: a tabela completa dos indicadores, com a **fórmula** e uma explicação curta de **como ler** cada um (liquidez, estrutura de capital, rentabilidade e prazos).
1. Informe o período e clique em **Gerar**.
2. Para comparar períodos, marque **Comparativo** e escolha o período anterior, o mesmo período do ano anterior ou até 3 outros períodos (veja [Comparativo](#comparativo)). A tabela ganha uma coluna por período e a variação em pontos.
3. Exporte em PDF, Excel ou CSV.

### Evolução patrimonial
*Relatórios › BI › Evolução patrimonial*: veja como o **Ativo**, o **Passivo**, o **Patrimônio líquido** e o **resultado** da empresa foram mudando mês a mês (ou ano a ano).
1. Em **Do mês de** e **até o mês de**, escolha os meses inicial e final (vale o mês da data escolhida; no máximo 10 anos de cada vez).
2. Se quiser ver só uma posição por ano, marque **Só o fim de cada ano** (o último ano mostra o mês final escolhido).
3. Clique em **Gerar**. O **gráfico** mostra as linhas de Ativo, Passivo, Patrimônio líquido e Resultado do período.
4. A **tabela** traz, para cada período, o Ativo, o Passivo (suas obrigações), o Patrimônio líquido dividido em **Capital social**, **Reservas** e **Lucros/prejuízos acumulados**, e o **Resultado do período** (receitas menos custos e despesas). Os valores são os do último dia de cada período.
5. A segunda tabela, **Variação sobre o período anterior**, mostra quanto o Ativo, o Passivo, o Patrimônio líquido e o Resultado subiram ou caíram em reais e em %. A última linha compara o primeiro período com o último. Aparece "—" no % quando o valor anterior era zero.
6. Use **PDF**, **Excel** ou **CSV** para levar as duas tabelas.

O lucro do ano que ainda não foi encerrado já entra em *Lucros/prejuízos acumulados*, por isso Capital, Reservas e Lucros somam o Patrimônio líquido. O programa reconhece capital, reservas e lucros pelos nomes das contas do patrimônio líquido no seu plano de contas.

### Cubo (tabela dinâmica)
*Relatórios › BI › Cubo (tabela dinâmica)*: monte você mesmo uma tabela de totais, como no Excel.
1. Informe o **período**.
2. Em **Linhas**, escolha o que fica de cima para baixo: *Conta*, *Grupo de contas*, *Centro de custo*, *Participante* ou *Mês*.
3. Em **Colunas**, escolha *Mês*, *Trimestre*, *Ano*, *Centro de custo* ou *(nenhuma)* (só o total).
4. Em **Somar**, escolha *Saldo (débitos − créditos)*, *Resultado (créditos − débitos)*, *Débitos* ou *Créditos*. Para ver receitas com valor positivo, use *Resultado*.
5. Se quiser, digite uma **conta ou grupo** (ex.: 4.2) ou marque **Só contas de resultado**. Clique em **Gerar**.
6. **Clique numa célula** para ver embaixo os lançamentos que formam aquele valor (data, número, conta, centro de custo, participante, débito/crédito, valor e histórico). Clicar no nome da linha lista todos os lançamentos da linha; clicar no *Total geral* lista todos.
7. **PDF**, **Excel** e **CSV** exportam o cubo; se houver lançamentos listados, eles vão junto no PDF e no Excel (em outra aba).

O cubo não considera rascunhos nem lançamentos de encerramento do exercício.

### Exportar dados para BI
*Relatórios › BI › Exportar dados para BI*: grava os dados da empresa em arquivos prontos para o Power BI, o Excel ou outra ferramenta de BI.
1. Informe o **período** (por padrão, desde o início do ano passado até hoje).
2. Escolha o **formato**: *CSV (um arquivo por tabela)*, *Excel (uma pasta de trabalho)* ou *CSV e Excel*.
3. Clique em **Escolher pasta...** (ou digite o caminho) e em **Exportar dados para BI**. Essa opção exige a permissão de exportar relatórios.

O programa grava na pasta: `fato_partidas` (uma linha por partida de lançamento, com o valor e o valor com sinal: débito positivo e crédito negativo), `dim_plano_contas`, `dim_centro_custo`, `dim_participante`, `dim_calendario` (um dia por linha, com ano, trimestre, mês e dia da semana) e um arquivo `leia-me.txt` com a lista de colunas. Os CSV usam vírgula entre as colunas, ponto nos decimais e datas no formato AAAA-MM-DD, para abrir igual em qualquer computador.

**Como importar no Power BI:**
1. No Power BI Desktop, clique em *Obter dados* › *Texto/CSV* (ou *Pasta*, para trazer todos de uma vez) e escolha os arquivos `.csv` (ou *Pasta de Trabalho do Excel*, se exportou o Excel).
2. Em *Transformar dados*, confira se as datas viraram *Data* e os valores *Número decimal*, e clique em *Fechar e aplicar*.
3. Na aba *Modelo*, ligue as tabelas (muitos para um): `fato_partidas[id_conta]` a `dim_plano_contas[id_conta]`, `fato_partidas[id_centro_custo]` a `dim_centro_custo[id_centro_custo]`, `fato_partidas[id_participante]` a `dim_participante[id_participante]` e `fato_partidas[data]` a `dim_calendario[data]`.
4. Crie os gráficos usando a soma de `valor_assinado` (para receitas, multiplique por −1) e os campos das tabelas de apoio (grupo da conta, centro de custo, mês, trimestre).

## Relatórios de depreciação

**Onde fica:** *Relatórios › Depreciação*. Todos aparecem na tela e exportam em PDF, Excel e CSV. Os valores são os da última depreciação lançada de cada bem (*Imobilizado › Cálculo e lançamento da depreciação*); a depreciação anterior à implantação entra no acumulado.

- **Relação de bens**: código, descrição, grupo, plaqueta, localização, nota fiscal, datas de aquisição e de início do uso, valor e situação (em uso ou baixado).
- **Valor depreciado**: para cada bem, o valor, a depreciação anterior à implantação, a lançada no sistema, a acumulada, o **% depreciado** e o último mês lançado.
- **Valor residual**: valor do bem, depreciação acumulada, **valor contábil líquido**, valor residual, **saldo a depreciar** e vida útil.
- **Resumo por grupo**: quantidade de bens, valor, depreciação acumulada e valor contábil líquido de cada grupo (regra de depreciação), com a taxa anual.

Escolha o grupo (ou *Todos os grupos*), marque **Incluir bens baixados** se quiser vê-los e clique em **Gerar**.

## Livro Contábil Completo

**Para que serve:** gerar em um único PDF o livro com termos, demonstrações e assinaturas, seguindo a ITG 2000.
**Onde fica:** *Relatórios › Contábeis › Livro Contábil Completo*.

1. Informe o **período** e o **número do livro**.
2. Marque o que entra: termo de abertura, Diário, Razão, Balancete, Balanço, DRE, DMPL, DLPA, DFC (desmarcada de início) e termo de encerramento.
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
