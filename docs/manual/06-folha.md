# Folha de pagamento e eSocial

Menus do módulo Vorticis Folha. Os dados dos trabalhadores só aparecem a quem tem alguma permissão da folha (LGPD). Atalhos: **Ctrl+F** abre o cálculo da folha e **Ctrl+E** abre os eventos variáveis.

Os cadastros da folha (funcionários, cargos, departamentos, rubricas e tabelas legais) ficam em *Cadastros › Funcionários*. O menu *Cadastros* da Folha não mostra Clientes e Fornecedores, que não são usados na folha (eles continuam nos módulos Contábil e Fiscal).

## Competência de trabalho (aberta ou fechada)

Na barra de cima, ao lado do seletor de empresas, fica a **Competência** em que você está trabalhando (mês/ano) e a situação dela:

- **ABERTA** (verde): pode incluir, alterar e excluir eventos variáveis, cálculos (folha, férias, 13º, rescisões) e o pró-labore da competência.
- **FECHADA** (vermelho): tudo da competência fica só para consulta. O sistema recusa qualquer inclusão, alteração ou exclusão, mesmo que a tela seja aberta.

Passo a passo:

1. Escolha o mês na lista de **Competência**, na faixa cinza logo abaixo da barra de menus (as já fechadas aparecem como *(fechada)*; marque **Só fechadas** para listar apenas essas). As telas *Eventos variáveis*, *Cálculo da folha mensal* e *Pró-labore* passam a usar esse mês.
2. Para fechar, vá em *Cálculos › Folha › Cálculo da folha mensal* e clique em **Fechar competência**. A situação ao lado da empresa muda para **FECHADA**.
3. Precisou corrigir? **Reabrir** na mesma tela (só se a folha não estiver contabilizada) e a situação volta para **ABERTA**.

## Parâmetros e tabelas legais

**Para que serve:** informar os encargos da empresa e consultar as tabelas de INSS, IRRF, salário mínimo etc.
**Onde fica:** *Cadastros › Funcionários › Parâmetros e tabelas legais*.

1. Preencha **RAT**, **FAP**, **terceiros (%)**, **FPAS**, a opção pelo Simples Nacional e a **classificação tributária** (tabela 8 do eSocial; 99 = pessoas jurídicas em geral).
2. Informe o **responsável pelo fechamento (S-1299)**: CPF, e-mail e telefone.
3. **Gravar parâmetros**.

As *tabelas legais por vigência* são exibidas para conferência (valores de 2025 e 2026 já carregados).

## Cargos

*Cadastros › Funcionários › Cargos*: **Incluir**, código, nome do cargo, **CBO** (6 dígitos, ex.: 252105) e situação.

## Departamentos (lotações)

*Cadastros › Funcionários › Departamentos*: **Incluir**, código e nome. Viram as lotações do eSocial.

## Rubricas (eventos)

**Para que serve:** os códigos de proventos, descontos e informativas que compõem o holerite.
**Onde fica:** *Cadastros › Funcionários › Rubricas (eventos)*.

1. **Incluir** código, descrição, tipo (provento, desconto ou informativa) e a **natureza** da tabela 3 do eSocial (ex.: 1000).
2. Marque as **incidências**: INSS, IRRF e FGTS.
3. Marque **Variável** se entra nas médias de férias, 13º e rescisão. **Salvar**.

## Cadastro de funcionários

**Onde fica:** *Cadastros › Funcionários › Cadastro de funcionários*.

1. **Incluir**. A matrícula vazia assume a próxima.
2. **Pessoal**: nome, CPF e PIS/NIS (validados), nascimento, sexo, raça/cor, estado civil, contatos e endereço completo (o **tipo de logradouro** é exigido pelo eSocial: R, AV, TV...).
3. **Contrato**: categoria eSocial, tipo de contrato (e término, se determinado), admissão, CBO, **CNPJ do sindicato**, cargo e departamento.
4. **Salário**: tipo (mensal ou horista), valor, jornada (horas semanais e mensais; o divisor do valor-hora), centro de custo e insalubridade/periculosidade.
5. **Banco**: banco (COMPE), agência, conta, tipo e chave PIX.
6. **Dependentes**: **Adicionar dependente** (nascimento, parentesco, marque *Deduz IRRF*, *Salário-família*, *Incapaz*).
7. **Salvar**.

**Regras:** alterar salário ou cargo grava o histórico contratual (evento S-2206). O desligamento é feito em *Rescisões*, não neste cadastro.

## Eventos variáveis

**Para que serve:** lançar o que muda todo mês antes de calcular a folha (horas extras, faltas, comissões, adiantamentos, vale-transporte, pensão alimentícia etc.).
**Onde fica:** *Cálculos › Folha › Eventos variáveis* (**Ctrl+E**).

1. Escolha a **competência** e o **funcionário**.
2. Escolha a **rubrica**, informe a **quantidade** (horas, dias) ou o **valor** e uma observação.
3. **Lançar**. Para corrigir, selecione e **Excluir selecionado**.

## Cálculo da folha mensal

**Onde fica:** *Cálculos › Folha › Cálculo da folha mensal* (**Ctrl+F**).

1. Escolha a **competência**: o **mês** na lista e o **ano** no campo ao lado (não há dia a escolher).
2. **Calcular folha**. A lista mostra proventos, descontos, líquido, INSS, IRRF e FGTS de cada funcionário.
3. Selecione um funcionário para ver a **memória de cálculo** rubrica a rubrica.
4. **Recibo do selecionado (PDF)** ou **Holerites da competência (PDF)**.
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

**Onde fica:** *Cálculos › Folha › Contabilização*.

1. **Regras**: cada regra liga uma rubrica (ou `ENC_*` para encargos, `PROV_*` para provisões) a uma conta de débito e uma de crédito. **Gerar regras sugeridas** cria as regras com base no plano modelo; ajuste e **Gravar regra** (ou **Excluir**). Marque *Abrir por centro de custo do funcionário* se for o caso.
2. Escolha a **competência** e veja a **Prévia da folha**; **Contabilizar folha** gera o lançamento no último dia do mês (a competência precisa estar **fechada**).
3. Do mesmo modo, **Prévia das provisões** e **Contabilizar provisões**. A opção *Férias e 13º baixam a provisão* usa as provisões.
4. Para desfazer: selecione em *Contabilizações* e **Desfazer selecionada** (estorna o lançamento).

Exige também a permissão de incluir lançamentos (e de estornar, para desfazer).

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

## Procuração e procurador

O Vorticis não outorga procuração: ela é feita no **e-CAC** (ou módulo de procurações do eSocial) para o CPF/CNPJ do certificado, com os perfis dos eventos e prazo vigente. No sistema, marque o contador como procurador em *Cadastros › Contadores* (seção **Procuração eletrônica**, abrangência **eSocial**, com a vigência) e escolha **procurador** em *Transmitir com*.

## Pró-labore dos sócios

**Onde fica:** *Cálculos › Pró-labore › Sócios e cálculo do pró-labore*.

Os sócios vêm do cadastro da empresa (*Cadastros › Empresas › aba Sócios*, ou *Cálculos › Pró-labore › Sócios (cadastro da empresa)*). Só aparecem sócios pessoa física.

1. Selecione o sócio na lista.
2. Marque **Retira pró-labore** se ele recebe pró-labore (desmarque se não recebe).
3. Informe o **Valor mensal**, o **Início** e, se houver, o **Fim**.
4. Se o sócio tem dependentes para o imposto de renda, informe em **Dependentes (IRRF)**.
5. Se ele já tem INSS descontado em outro emprego ou empresa no mês, informe em **INSS em outras fontes**: o desconto fica limitado ao teto.
6. **Gravar**.
7. Com a competência **ABERTA**, clique em **Calcular pró-labore**. Para cada sócio que retira aparecem bruto, **INSS (11%)**, **IRRF**, **líquido** e **CPP patronal** (20%; no Simples Nacional, exceto anexo IV, a CPP já está no DAS e aparece zero), com a memória de cálculo.
8. **Excluir cálculo** apaga o cálculo da competência (só com a competência aberta).

As alíquotas vêm das tabelas legais (*Cadastros › Funcionários › Parâmetros e tabelas legais*, chave FOLHA_PRO_LABORE). Pró-labore não tem FGTS.

## Relatórios da folha

Com exportação para PDF, Excel e CSV:
- **Holerites e recibos** (*Relatórios › Holerites e recibos*): holerite individual ou de todos da competência.
- **Resumo da folha**: totais por rubrica e funcionário na competência.
- **Encargos patronais**: INSS patronal, RAT, terceiros e FGTS.
- **Provisões**: veja [Provisões de férias e 13º](#provisoes-de-ferias-e-13o).

Em todos, informe a competência e clique em **Gerar**.
