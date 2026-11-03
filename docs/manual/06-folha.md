# Folha de pagamento e eSocial

Menus do módulo Vorticis Folha. Os dados dos trabalhadores só aparecem a quem tem alguma permissão da folha (LGPD). Atalhos: **Ctrl+F** abre o cálculo da folha e **Ctrl+E** abre os eventos variáveis.

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

Os cadastros da folha (funcionários, cargos, departamentos, rubricas e tabelas legais) ficam em *Cadastros › Funcionários*. O menu *Cadastros* da Folha não mostra Clientes e Fornecedores, que não são usados na folha (eles continuam nos módulos Contábil e Fiscal).

## Competência de trabalho (aberta ou fechada)

Na faixa cinza logo abaixo da barra de menus fica, à direita, a **Competência** (à esquerda ficam as funções abertas) em que você está trabalhando (mês/ano) e a situação dela:

- **ABERTA** (verde): pode incluir, alterar e excluir eventos variáveis, cálculos (folha, férias, 13º, rescisões) e o pró-labore da competência.
- **FECHADA** (vermelho): tudo da competência fica só para consulta. O sistema recusa qualquer inclusão, alteração ou exclusão, mesmo que a tela seja aberta.

Passo a passo:

1. Escolha o mês na lista de **Competência**, na faixa cinza logo abaixo da barra de menus (as já fechadas aparecem como *(fechada)*; marque **Só fechadas** para listar apenas essas). As telas *Eventos variáveis*, *Cálculo da folha mensal* e *Pró-labore* passam a usar esse mês.
2. Para fechar, vá em *Cálculos › Folha › Cálculo da folha mensal* e clique em **Fechar competência**. A situação ao lado da empresa muda para **FECHADA**.
3. Precisou corrigir? **Reabrir** na mesma tela (só se a folha não estiver contabilizada) e a situação volta para **ABERTA**.

## Parâmetros e tabelas legais

**Para que serve:** informar os encargos da empresa e consultar as tabelas de INSS, IRRF, salário mínimo etc.
**Onde fica:** *Cadastros › Tabelas legais › Parâmetros e tabelas legais*.

1. Preencha **RAT**, **FAP**, **terceiros (%)**, **FPAS**, a opção pelo Simples Nacional e a **classificação tributária** (tabela 8 do eSocial; 99 = pessoas jurídicas em geral).
2. Informe o **responsável pelo fechamento (S-1299)**: CPF, e-mail e telefone.
3. **Gravar parâmetros**.

As *tabelas legais por vigência* são exibidas para conferência (valores de 2025 e 2026 já carregados).

## Tabelas da previdência social

**Para que serve:** consultar e imprimir as tabelas legais que a folha usa na competência: faixas do **INSS do empregado** e o **teto**, **salário-família** (limite de remuneração e cota), **salário mínimo**, **FGTS** (depósito mensal, aprendiz e multas) e **IRRF** mensal (faixas, dedução por dependente e desconto simplificado).
**Onde fica:** *Cadastros › Tabelas legais › Tabelas da previdência social*.

1. Escolha a **Competência** e clique em **Consultar**. Aparecem as tabelas vigentes no mês, com a data de início de cada uma e o fundamento legal no rodapé.
2. Use **PDF**, **Excel** ou **CSV** para guardar ou imprimir.

As tabelas vêm dos parâmetros legais do programa e são atualizadas com as novas versões; todos os cálculos (folha, férias, 13º, rescisões, pró-labore) usam a tabela vigente na competência.

## Cadastros da folha

O menu *Cadastros* da Folha reúne:
- **Funcionários**: cadastro de funcionários, cargos, departamentos e rubricas (eventos).
- **Sócios (pró-labore)**: os sócios da empresa (o mesmo cadastro de *Cadastros › Empresas*, aba Sócios), usados no [pró-labore](#pro-labore-dos-socios).
- **Contábeis**: [eventos contábeis](#contabilizacao-por-eventos) e centros de custo (o mesmo cadastro do Contábil e do Fiscal).
- **Tabelas legais**: as [tabelas da previdência social](#tabelas-da-previdencia-social) e os [parâmetros e tabelas legais](#parametros-e-tabelas-legais).
- **Contadores**: o cadastro comum de contadores (responsável pela escrituração e procurações).

## Cargos

*Cadastros › Funcionários › Cargos*: a tela mostra a lista dos cargos e a ficha abre numa janela (**Incluir**, **Alterar**, duplo clique ou **Enter**; **Salvar** fecha a janela). Informe código, nome do cargo, **CBO** (6 dígitos, ex.: 252105) e situação.

## Departamentos (lotações)

*Cadastros › Funcionários › Departamentos*: a tela mostra a lista e a ficha abre numa janela (**Incluir**, **Alterar**, duplo clique ou **Enter**). Informe código e nome. Viram as lotações do eSocial.

## Rubricas (eventos)

**Para que serve:** os códigos de proventos, descontos e informativas que compõem o holerite.
**Onde fica:** *Cadastros › Funcionários › Rubricas (eventos)*.

A tela mostra a lista das rubricas; a ficha abre numa janela.

1. **Incluir** (ou **Alterar**, ou duplo clique) código, descrição, tipo (provento, desconto ou informativa) e a **natureza** da tabela 3 do eSocial (ex.: 1000).
2. Marque as **incidências**: INSS, IRRF e FGTS.
3. Marque **Variável** se entra nas médias de férias, 13º e rescisão. **Salvar**.

## Cadastro de funcionários

**Onde fica:** *Cadastros › Funcionários › Cadastro de funcionários*.

A tela mostra a lista dos funcionários (com filtro por nome, matrícula ou CPF); a ficha, com as abas Pessoal, Contrato, Banco e Dependentes, abre numa janela por cima da lista.

1. **Incluir** (ou **Alterar**, duplo clique ou **Enter** na lista). A matrícula vazia assume a próxima.
2. **Pessoal**: nome, CPF e PIS/NIS (validados), nascimento, sexo, raça/cor, estado civil, contatos e endereço completo (o **tipo de logradouro** é exigido pelo eSocial: R, AV, TV...).
3. **Contrato**: categoria eSocial, tipo de contrato (e término, se determinado), admissão, CBO, **CNPJ do sindicato**, cargo e departamento.
4. **Salário**: tipo (mensal ou horista), valor, jornada (horas semanais e mensais; o divisor do valor-hora), centro de custo e insalubridade/periculosidade.
5. **Banco**: banco (COMPE), agência, conta, tipo e chave PIX.
6. **Dependentes**: **Adicionar dependente** (nascimento, parentesco, marque *Deduz IRRF*, *Salário-família*, *Incapaz*).
7. **Salvar** (a janela fecha).

**Regras:** alterar salário ou cargo grava o histórico contratual (evento S-2206). O desligamento é feito em *Rescisões*, não neste cadastro.

## Eventos variáveis

**Para que serve:** lançar o que muda todo mês antes de calcular a folha (horas extras, faltas, comissões, adiantamentos, vale-transporte, pensão alimentícia etc.).
**Onde fica:** *Cálculos › Folha › Eventos variáveis* (**Ctrl+E**).

A tela mostra os eventos já lançados na competência.

1. Escolha a **competência** e clique em **Novo lançamento**: a ficha abre numa janela por cima da lista.
2. Escolha o **funcionário** e a **rubrica**, informe a **quantidade** (horas, dias) ou o **valor** e uma observação.
3. **Lançar** grava e fecha a janela (**Cancelar**, **Esc** ou o **X** fecham sem gravar). Para corrigir, selecione na lista e **Excluir selecionado**.

## Cálculo da folha mensal

**Onde fica:** *Cálculos › Folha › Cálculo da folha mensal* (**Ctrl+F**).

1. Escolha a **competência**: o **mês** na primeira lista e o **ano** (com 4 dígitos) na lista ao lado. Não há dia a escolher.
2. **Calcular folha**. A lista mostra proventos, descontos, líquido, INSS, IRRF e FGTS de cada funcionário.
3. Selecione um funcionário para ver a **memória de cálculo** rubrica a rubrica.
4. **Recibo do selecionado (PDF)** gera o recibo do cálculo marcado na lista. Para os holerites, escolha na lista ao lado **Todos os funcionários** ou um funcionário e clique em **Holerites (PDF)**.
5. Conferido, **Fechar competência** (em ordem). **Reabrir** só é possível se a folha não estiver contabilizada.

O cálculo cobre horas extras, DSR, adicional noturno, insalubridade/periculosidade, INSS progressivo, IRRF (melhor entre deduções legais e desconto simplificado, com a redução de 2026), FGTS e encargos patronais.

## Férias

**Onde fica:** *Cálculos › Folha › Férias*.

1. Escolha o **funcionário**: aparecem os **períodos aquisitivos** e concessivos.
2. Selecione o período e informe o **início do gozo**, os **dias de gozo**, o **abono pecuniário** (dias vendidos) e as **faltas injustificadas** (reduzem os dias de direito), e a data de pagamento.
3. **Calcular férias**: gera 1/3 constitucional, abono sem incidências e médias das variáveis; mostra o líquido.
4. Em *Férias calculadas*: **Recibo (PDF)** ou **Cancelar férias**.

Fracionamento é aceito (calcule cada período de gozo).

## 13º salário

**Onde fica:** *Cálculos › Folha › 13º salário*.

1. Escolha o **ano** e a parcela: **1ª parcela (novembro)** ou **2ª parcela (dezembro)**. A 2ª calcula INSS e IRRF sobre o valor integral.
2. **Calcular**; confira a memória (avos).
3. **Recibo do selecionado (PDF)** ou **Recibos de todos (PDF)**.

## Rescisões

**Onde fica:** *Cálculos › Folha › Rescisões*.

1. Escolha o **funcionário** e o **motivo** (sem justa causa, pedido de demissão, justa causa, acordo do art. 484-A ou término de contrato).
2. Informe o tipo do **aviso prévio**, a data de **desligamento** e a de **pagamento**. O aviso proporcional segue a Lei 12.506.
3. Para a multa do FGTS, informe o **saldo do FGTS** ou use **Estimar pela folha**.
4. **Calcular rescisão**: saldo de salário, férias vencidas e proporcionais com 1/3, 13º proporcional, aviso e multa.
5. Em *Rescisões calculadas*: **TRCT simplificado (PDF)** ou **Cancelar rescisão**.

## Provisões de férias e 13º

**Onde fica:** *Cálculos › Folha › Provisões de férias e 13º* (e *Relatórios › Provisões*).
Escolha a competência e **Calcular provisões**: 1/12 de férias (mais 1/3) e 1/12 de 13º, com encargos. *Mostrar* alterna entre as visões.

## Contabilização da folha

Se a empresa usa o tipo de contabilização **Em lote** (*Cadastros › Parâmetros › Contábil*), o botão **Contabilizar** apenas registra a pendência; o lançamento sai em *Integrações › Exportar › Contabilização em lote* (veja [Contabilização em lote](03-contabil.md#contabilizacao-em-lote)).

**Onde fica:** *Cálculos › Folha › Contabilização*.

1. **Regras**: cada regra liga uma rubrica (ou `ENC_*` para encargos, `PROV_*` para provisões) a uma conta de débito e uma de crédito. **Gerar regras sugeridas** cria as regras com base no plano modelo; ajuste e **Gravar regra** (ou **Excluir**). Marque *Abrir por centro de custo do funcionário* se for o caso.
2. Escolha a **competência** e veja a **Prévia da folha**; **Contabilizar folha** gera o lançamento no último dia do mês (a competência precisa estar **fechada**).
3. Do mesmo modo, **Prévia das provisões** e **Contabilizar provisões**. A opção *Férias e 13º baixam a provisão* usa as provisões.
4. Para desfazer: selecione em *Contabilizações* e **Desfazer selecionada** (estorna o lançamento).

Exige também a permissão de incluir lançamentos (e de estornar, para desfazer).

**Modo de contabilização.** Em *Cadastros › Parâmetros › Folha*, grupo **Contabilização**, escolha **Resumida** (um lançamento por competência, como descrito acima) ou **Por eventos** (veja a seção seguinte). Com o modo *Por eventos*, a tela *Contabilização* deixa de contabilizar; use *Contabilização por eventos*. Não é possível contabilizar a mesma competência dos dois jeitos ao mesmo tempo: desfaça um antes de usar o outro.

## Contabilização por eventos

Em vez de um lançamento só para a competência, a folha gera **um lançamento para cada evento** (salários, horas extras, adicionais, férias, 13º, INSS, IRRF, FGTS, encargos patronais, provisões, adiantamentos, rescisões), cada um com o seu histórico, por exemplo *Salários 10/2026* ou *INSS retido 10/2026*.

**Onde ficam:** *Cálculos › Folha › Contabilização por eventos* e *Cadastros › Contábeis › Eventos contábeis*.

**Antes de começar**

1. Em *Cadastros › Parâmetros › Folha › Contabilização*, escolha o modo **Por eventos**. Marque **Detalhar por funcionário** se quiser uma partida para cada funcionário, com o nome e a matrícula no histórico; desmarcado, o lançamento traz só os totais do evento. Clique em **Gravar**.
2. Confira as regras de contas em *Cálculos › Folha › Contabilização*: as mesmas regras (rubrica → conta de débito e de crédito, com centro de custo) valem aqui. Quando a regra abre por centro de custo, o lançamento traz uma partida por centro.

**Eventos contábeis (opcional)**

1. Abra *Eventos contábeis*. Cada rubrica sem evento forma um evento próprio, com o nome da rubrica.
2. Para juntar rubricas num só lançamento, clique em **Novo** (a ficha do evento abre numa janela por cima da lista; **Abrir ficha** ou duplo clique alteram o selecionado), informe o **Código** (ex.: SAL), a **Descrição** (ela vai no histórico, ex.: *Salários*) e as **Rubricas** que somam nele, separadas por vírgula (ex.: `1000, 1001, 1002`; também vale `ENC_FGTS`, `PROV_13` etc.). Clique em **Gravar evento**.
3. Uma rubrica só pode somar em um evento. **Excluir** devolve as rubricas ao seu evento próprio.

**Contabilizar**

1. Abra *Contabilização por eventos*, escolha a **competência** e o **tipo de cálculo** (folha mensal, férias, 13º salário, rescisões ou provisões) e clique em **Prévia dos eventos**.
2. A grade mostra cada evento com o **valor**, as **contas** (débito e crédito), o **centro de custo** e a **situação**. Eventos **sem regra** aparecem com aviso (cadastre a regra em *Contabilização*) e não podem ser marcados; os já contabilizados mostram o número do lançamento. Clique num evento para ver as partidas dele embaixo.
3. Marque ou desmarque os eventos (**Marcar todos** / **Desmarcar todos**) e clique em **Contabilizar selecionados**. Cada evento vira um lançamento no último dia do mês. A competência da folha precisa estar **fechada** (as provisões não exigem). Se algum evento marcado tiver problema, nenhum é contabilizado e a mensagem diz qual.
4. A soma dos eventos é igual ao total da folha da competência.
5. Com a empresa em contabilização **Em lote**, o botão registra uma pendência por evento; os lançamentos saem em *Integrações › Exportar › Contabilização em lote*.

**Desfazer**

Marque os eventos já contabilizados e clique em **Desfazer selecionados**: o lançamento de cada um é estornado e o evento pode ser contabilizado de novo. Enquanto houver evento contabilizado, a folha (ou as provisões) da competência não pode ser reaberta nem recalculada.

## eSocial: assistente de configuração

**Para que serve:** preparar a empresa para o eSocial, conferindo etapa por etapa o que falta antes de gerar e transmitir os eventos. O assistente não grava nada: ele monta os arquivos em memória, confere com o leiaute oficial e mostra o que corrigir.
**Onde fica:** *Obrigações › eSocial › Assistente de configuração*.

1. Escolha a **competência** (mês e ano) e clique em **Verificar**. O resumo no topo mostra quantas etapas estão prontas.
2. Cada etapa aparece com um sinal: ✔ **Pronta** (verde), **!** **Atenção** (amarelo, pode seguir, mas leia o aviso) ou ✖ **Pendente** (vermelho, precisa corrigir).
3. Clique na etapa para ver a explicação e o que foi encontrado. O botão da etapa (por exemplo, **Abrir Rubricas** ou **Abrir Cadastro de funcionários**) abre a tela onde se corrige. Use **Próxima etapa ›** e **‹ Etapa anterior** para seguir em ordem.
4. Depois de corrigir, clique em **Verificar** de novo.

As etapas:
1. **Dados do empregador (S-1000 e S-1005):** CNPJ, classificação tributária, CNAE, município, RAT, FAP e terceiros.
2. **Responsável pelas informações:** nome, CPF e e-mail ou telefone (em *Parâmetros e tabelas legais*).
3. **Ambiente e quem transmite:** produção restrita (testes) ou produção, e se transmite o empregador, um procurador ou o escritório (em *Cadastros › Parâmetros*). Em produção, a etapa fica em **Atenção**, porque os envios são definitivos.
4. **Rubricas (S-1010):** natureza do eSocial e incidências de cada provento e desconto.
5. **Lotações (S-1020):** os departamentos (e a lotação GERAL).
6. **Trabalhadores (S-2200):** o cadastro de cada funcionário ativo na competência. Quem tiver dado faltando aparece com a matrícula, o nome e o campo a corrigir (por exemplo, o CNPJ do sindicato).
7. **Folha da competência (S-1200, S-1210 e S-1299):** a folha precisa estar calculada; o ideal é fechar a competência antes de transmitir.
8. **Certificado digital:** o certificado padrão de quem transmite, dentro da validade (em *Configurações › Certificados digitais*; só o administrador vê).
9. **Gerar e transmitir:** mostra os eventos já gerados ou enviados na competência. **Gerar todos os eventos** gera tudo de uma vez (os já enviados não são gerados de novo) e **Abrir Transmissão** leva à assinatura e ao envio. Se houver evento **rejeitado**, a etapa fica pendente: veja o motivo na Transmissão, corrija e gere de novo.

## eSocial: geração dos eventos

**Onde fica:** *Obrigações › eSocial › Geração dos eventos*.

1. Antes: parâmetros, rubricas, departamentos e funcionários completos; folha calculada (para S-1200/S-1210).
2. Escolha a **competência**, marque os **eventos a gerar** (S-1000, S-1005, S-1010, S-1020, S-2200, S-2206, S-2230, S-2299, S-1200, S-1210, S-1299) e clique em **Gerar XMLs**.
3. Resolva os avisos mostrados. Selecione um evento para ver o XML.
4. Para guardar os arquivos, informe a **pasta de exportação** e use **Exportar selecionado** ou **Exportar todos**.

## eSocial: transmissão

**Onde fica:** *Obrigações › eSocial › Transmissão*.

### Antes
- Certificado A1 ou A3 em *Configurações › Certificados digitais*, definido como **padrão** (responsável, procurador ou contador).
- Para transmitir como **procurador** ou **contador**: procuração eletrônica no e-CAC e o contador procurador cadastrado em *Cadastros › Contadores* (seção **Procuração eletrônica**), com vigência e abrangência **eSocial**.

### Passo a passo
1. Escolha a competência, o **ambiente** (comece pela **produção restrita**, sem efeito legal) e **Transmitir com** (empregador, procurador ou contador).
2. **Assinar e enviar pendentes** (ou **Assinar e enviar selecionado**). O sistema respeita a ordem: S-1000, tabelas (S-1005/1010/1020), não periódicos (S-2200/2206/2230/2299), periódicos (S-1200/S-1210) e por último S-1299; não envia um grupo enquanto o anterior aguarda retorno.
3. Use **Consultar retorno** para ver recibos e ocorrências. Corrija e gere de novo os eventos rejeitados.
4. Só depois de testar, repita em **produção** (o sistema pede confirmação) e guarde os recibos.

A tela traz um painel **Passo a passo** com o roteiro.

## Importar do eSocial

**Para que serve:** a empresa veio de **outro sistema de folha** e precisa continuar a folha de onde ele parou. O eSocial já tem tudo o que foi informado ao governo (funcionários, salários, rubricas, remuneração de cada mês, afastamentos, desligamentos); esta tela lê esses eventos e leva para o Vorticis o que a folha precisa para seguir: cadastro dos funcionários, rubricas, **histórico de holerites** (dele saem as médias de férias e 13º, as bases e o último salário), **dependentes** para o IRRF, **períodos de férias** já gozados e afastamentos.

**Onde fica:** *Integrações › Importar › Importar eSocial*. Precisa da permissão de manter cadastros da folha; para **Baixar do eSocial** precisa também da permissão do eSocial.

### Antes
- Cadastre e selecione a **empresa** certa: o sistema confere o CNPJ dos eventos e recusa arquivos de outro empregador.
- Tenha os eventos em XML. Há dois caminhos: **baixar do portal** ou **baixar aqui pelo certificado** (veja abaixo).
- Para a prévia mostrar tudo certo, inclua os eventos de **tabela** (S-1010 rubricas, S-1020 lotações) junto com os de remuneração (S-1200). Sem o S-1010 o sistema não sabe se uma rubrica é provento ou desconto.

### Passo a passo
1. Em **1. Arquivos do eSocial**, clique em **Escolher pasta...** (uma pasta com os XML), **Escolher arquivos...** (vários XML ou ZIP) ou digite o caminho e clique em **Conferir**. Aceita o XML do evento puro e o XML que veio do portal com o recibo do eSocial, soltos, em subpastas ou dentro de ZIP.
2. O sistema lê tudo, junta as versões e mostra na aba **Prévia**:
   - **Quantos eventos de cada tipo** (admissões, alterações contratuais, afastamentos, desligamentos, remunerações, pagamentos, rubricas, lotações), quantos são **novos**, quantos **já existem** no Vorticis (funcionário pelo CPF, rubrica pelo código, holerite pelo mês) e quantos têm **conflito**.
   - **Erros, avisos e informações** item a item (por exemplo: funcionário sem data de nascimento, matrícula que já pertence a outra pessoa, rubrica que não está nas tabelas).
   - As regras de leitura: se o mesmo fato foi enviado mais de uma vez, **vale a versão mais recente**; um evento **retificado** (retificação com o recibo anterior) substitui o anterior; um evento **excluído** pelo S-3000 não é importado; as alterações de salário ou cargo são aplicadas **por ordem de data**, mesmo que cheguem fora de ordem.
3. Na aba **Rubricas**, confira de onde cada rubrica do arquivo vai cair:
   - Se o Vorticis já tem a mesma rubrica (mesmo código ou mesmo nome), ela aparece como **Existente** e é usada.
   - Se o sistema anterior usava outro código para a mesma coisa (por exemplo, "Salário" com código 0001 e o Vorticis usa 1000), escolha a rubrica do Vorticis em **Usar a rubrica**. A prévia se atualiza.
   - Se o código já é de outra rubrica do Vorticis, aparece **Conflito** e a rubrica é criada com outro código (a letra E na frente); você pode trocar.
   - Rubricas **novas** nascem com o tipo, a natureza e as incidências do eSocial. Marque **Média** nas que são variáveis (horas extras, comissões, adicional noturno, DSR): elas entram na média de férias, 13º e rescisão.
4. Em **2. O que importar**, escolha:
   - **Histórico de holerites**: leva os meses do S-1200 (e a data de pagamento do S-1210) para o Vorticis como folhas já calculadas. É o que permite calcular **férias, 13º e rescisão** com a média correta logo no primeiro mês. Use **Importar o histórico só neste período** para limitar os meses.
   - **Férias gozadas**: grava os períodos aquisitivos já usados, para o Vorticis saber o saldo de férias de cada um.
   - **Fechar as competências importadas** (recomendado): o histórico não será recalculado por engano. Para continuar a folha, abra só o mês seguinte.
   - **Atualizar os parâmetros de encargos**: usa o RAT, o FAP, o FPAS e o código de terceiros do arquivo no lugar dos que estão em *Cadastros › Parâmetros*. Se a empresa ainda não tinha parâmetros, eles são criados sem marcar nada.
   - **Ignorar o que tiver erro e importar o restante**: sem marcar, qualquer item com erro impede a importação; marcando, o que tem erro fica de fora e aparece no relatório.
5. Clique em **Importar** e confirme. Tudo é gravado numa **única vez** com barra de andamento: se algo falhar, **nada fica pela metade**. Use **Cancelar importação** para parar.
6. Veja o resultado na aba **Relatório** e salve em **Salvar relatório (CSV)** (abre direto no Excel).

### O que é levado para a folha
- **Funcionários** (S-2200 e, para estagiários, S-2300): dados pessoais, endereço, contato, dependentes (para o IRRF e o salário-família), cargo, CBO, departamento (a lotação das folhas importadas), categoria, contrato, jornada, salário e situação (**ativo**, **afastado** ou **desligado**). Vale o **salário e o cargo mais recentes** (S-2206), e o histórico de reajustes é guardado junto com o funcionário. Quem recebia adicional de periculosidade vem com o adicional marcado; para quem recebia insalubridade o sistema avisa para informar o percentual.
- **Desligamentos** (S-2299): o funcionário vem como desligado, com a data e o motivo.
- **Afastamentos** (S-2230): licenças e afastamentos viram afastamentos do funcionário; os de motivo **férias** viram **períodos de férias gozados**, ligados ao período aquisitivo contado desde a admissão.
- **Rubricas** (S-1010), **lotações** (S-1020, viram departamentos) e **parâmetros de encargos** (S-1000, S-1005 e S-1020).
- **Histórico de holerites** (S-1200 e S-1210): cada mês vira um cálculo com seus itens, proventos, descontos, líquido, bases de INSS, IRRF e FGTS, INSS e IRRF retidos e o FGTS do mês (calculado pela alíquota da tabela legal). O 13º salário do ano (apuração anual) entra na competência de dezembro.
- O que **já existe** no Vorticis (funcionário com o mesmo CPF, holerite do mesmo mês) não é alterado: aparece como "já existe" e é ignorado.

### Cuidados
- **Não contabilize de novo** as competências importadas: a contabilidade desses meses foi feita no sistema anterior. O histórico serve para médias, bases e consulta.
- Os **encargos patronais** dos meses importados ficam zerados (não vêm no eSocial).
- As **verbas de rescisão** (S-2299) e os valores de períodos anteriores (infoPerAnt) **não** são importados como holerite.
- Funcionário com mais de um vínculo (readmissão) é importado pelo vínculo mais recente: o Vorticis guarda um vínculo por CPF.
- Sócios e diretores (categorias sem vínculo, como 721 e 722) não viram funcionários: cadastre-os em *Cadastros › Sócios (pró-labore)*.
- Confira os primeiros funcionários e um holerite importado (*Cálculos › Folha › Cálculo da folha mensal*, escolhendo a competência) antes de calcular o primeiro mês novo.

### Histórico de importações e desfazer
Na aba **Histórico de importações** ficam todas as importações (data, origem, período, quantidade e usuário). Para voltar atrás, selecione a importação e clique em **Desfazer importação**: saem os funcionários, as rubricas, o histórico de holerites, as férias e os afastamentos que ela criou, e as competências que ela fechou voltam a ficar abertas. Se algo já foi usado depois (um cálculo novo, uma rescisão, um evento variável, a contabilização), o sistema **não remove nada** e explica o motivo. O mesmo material só pode ser importado de novo depois de desfazer a importação anterior.

### Baixar do eSocial (pelo certificado)
Em vez de baixar os arquivos no portal, abra **Baixar do eSocial (em vez de usar arquivos)** e informe:
1. As **competências** inicial e final, o **ambiente** (produção ou produção restrita) e **Transmitir com** (empregador, procurador ou contador). Vale a mesma regra da Transmissão: certificado digital padrão do papel escolhido e, para procurador ou contador, procuração eletrônica com o perfil de consulta dos eventos.
2. O que baixar: tabelas, remunerações, pagamentos e os eventos dos trabalhadores (admissões, alterações, afastamentos, desligamentos). Em **CPFs a incluir**, digite os CPFs de quem não aparece nas remunerações do período.
3. Clique em **Baixar do eSocial**. O sistema consulta os identificadores dos eventos e baixa cada um, em lotes, mostrando o andamento; ao terminar, os eventos seguem para a mesma **Prévia** acima e você continua do passo 2.

**Atenção:** este serviço de consulta foi preparado conforme o manual do eSocial, mas **ainda não foi testado no ambiente real**. Se o eSocial recusar a consulta ou devolver algo diferente, a tela mostra o motivo; nesse caso baixe os eventos pelo portal do eSocial (*Download de eventos*), guarde os XML ou o ZIP numa pasta e use **Escolher pasta...**.

## FGTS Digital

**Para que serve:** conferir o FGTS da competência calculado pela folha e controlar a guia do **FGTS Digital** (GFD) e o pagamento.
**Onde fica:** *Obrigações › FGTS Digital*.

Como funciona o FGTS Digital: as remunerações e o FGTS de cada trabalhador chegam ao governo pelos eventos do **eSocial** (folha e fechamento, em *Obrigações › eSocial*). A guia é emitida no **portal do FGTS Digital** (com o certificado digital ou a conta gov.br, inclusive por procuração), não por este programa.

1. Calcule e confira a folha da competência e transmita os eventos do eSocial.
2. Em *Obrigações › FGTS Digital*, escolha a **Competência** e clique em **Consultar**: a tabela mostra a base e o FGTS de cada trabalhador (8%; 2% para aprendiz), o total e o **vencimento** (dia 20 do mês seguinte, antecipado para o dia útil anterior). Exporte em PDF, Excel ou CSV para conferir com a guia.
3. **Feche a competência** da folha. Antes disso a guia não pode ser registrada.
4. Emita a guia no portal do FGTS Digital, copie o identificador e clique em **Registrar guia** (o valor registrado é o total do demonstrativo).
5. Depois de pagar, informe **Pago em** e **Valor pago** e clique em **Registrar pagamento**.

**Excluir guia** apaga o registro de uma guia ainda não paga (para registrar outra). A linha de situação acima da tabela mostra o que falta fazer. Registrar e excluir pedem a permissão de calcular a folha.

## Procuração e procurador

O Vorticis não outorga procuração: ela é feita no **e-CAC** (ou módulo de procurações do eSocial) para o CPF/CNPJ do certificado, com os perfis dos eventos e prazo vigente. No sistema, marque o contador como procurador em *Cadastros › Contadores* (seção **Procuração eletrônica**, abrangência **eSocial**, com a vigência) e escolha **procurador** em *Transmitir com*.

## Pró-labore dos sócios

**Onde fica:** *Cálculos › Pró-labore › Sócios e cálculo do pró-labore*.

Os sócios vêm do cadastro da empresa (*Cadastros › Empresas › aba Sócios*, ou *Cadastros › Sócios (pró-labore)*). Só aparecem sócios pessoa física.

1. Selecione o sócio na lista.
2. Marque **Retira pró-labore** se ele recebe pró-labore (desmarque se não recebe).
3. Informe o **Valor mensal**, o **Início** e, se houver, o **Fim**.
4. Se o sócio tem dependentes para o imposto de renda, informe em **Dependentes (IRRF)**.
5. Se ele já tem INSS descontado em outro emprego ou empresa no mês, informe em **INSS em outras fontes**: o desconto fica limitado ao teto.
6. **Gravar**.
7. Com a competência **ABERTA**, clique em **Calcular pró-labore**. Para cada sócio que retira aparecem bruto, **INSS (11%)**, **IRRF**, **líquido** e **CPP patronal** (20%; no Simples Nacional, exceto anexo IV, a CPP já está no DAS e aparece zero), com a memória de cálculo.
8. **Excluir cálculo** apaga o cálculo da competência (só com a competência aberta).

As alíquotas vêm das tabelas legais (*Cadastros › Tabelas legais › Parâmetros e tabelas legais*, chave FOLHA_PRO_LABORE). Pró-labore não tem FGTS.

## Relatórios da folha

Com exportação para PDF, Excel e CSV:
- **Holerites e recibos** (*Relatórios › Holerites e recibos*): escolha na lista **Todos os funcionários** ou um funcionário e clique em **Holerites (PDF)**.
- **Resumo da folha**: totais por rubrica e funcionário na competência.
- **Encargos patronais**: INSS patronal, RAT, terceiros e FGTS.
- **Provisões**: veja [Provisões de férias e 13º](#provisoes-de-ferias-e-13o).

Em todos, escolha a **competência** (período de apuração: mês e ano, sem dia) e clique em **Gerar**. A tela já abre na competência de trabalho da faixa cinza. O mesmo seletor de mês e ano é usado em Eventos variáveis, Contabilização, eSocial e Transmissão.
