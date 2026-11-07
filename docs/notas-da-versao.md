# Vorticis 1.0.5

## Novidades desta versão

### Escritório
- **Seletor de clientes no topo:** em **Cliente:** aparecem os clientes de *Cadastros › Clientes* (o escritório, 0001, é o primeiro), com **Todos os clientes** no alto. O cliente escolhido filtra as tarefas, os protocolos, os contratos, a carteira e os honorários.
- **Novo menu Financeiro › Honorários:** lance o honorário com competência, valor, dia do vencimento (ajustado nos meses mais curtos) e periodicidade, que cria as parcelas seguintes, com a prévia antes de lançar; **cobranças avulsas** para serviços extras; **Gerar do contrato**; **baixas** com data, valor e observação; estornar, cancelar e excluir; filtros por competência, situação e tipo.
- **Cadastro de clientes:** a aba *Módulos do contrato* passou a se chamar **Contrato** e tem o honorário do cliente (valor, vencimento e periodicidade **mensal**, **anual** ou **especial**) e os módulos contratados. As cobranças ficam só no Financeiro.

### Contábil
- **Comparar com anos anteriores:** em cada relatório (Balanço, Balancete, DRE, DFC, EBITDA, DMPL, DLPA, Razão e os gerenciais) a comparação agora é uma caixa só: marque **Comparar com anos anteriores** e escolha quantos anos (1 a 5). O relatório sai com o período escolhido e o mesmo período de cada ano anterior, com a variação em R$ e em %. A comparação entre outros períodos saiu.

Para atualizar, rode o **Setup-Vorticis-1.0.5.exe** por cima da versão instalada: os dados são preservados e o banco é atualizado sozinho na primeira abertura.

---

# Vorticis 1.0.4

## Novidades desta versão

### Contábil
- **Lançamento contábil mais simples:** o botão **+ Mais partidas** (abaixo da conta a crédito) acrescenta linhas de débito e crédito; o rodapé mostra só os totais de débitos, créditos e a diferença. Com mais de uma linha, tudo é gravado como um único lançamento, que precisa fechar. O centro de custo só aparece quando a empresa usa centros de custo, e as contas têm lupa (F4) no lugar do plano de contas ao lado.
- **Encerramento do exercício:** lupa nas contas de apuração, lucros e prejuízos.
- **Importar lançamentos** fica só em *Integrações*; a planilha modelo tem as colunas DEBITO e CREDITO separadas e aceita linhas só com débito ou só com crédito.
- **Importar SPED (ECD e ECF)** em *Integrações › Importar*: prévia, importação, relatório e Desfazer.
- **Plano de contas:** criar um novo plano com a máscara que você definir, usar um modelo em outra máscara e converter o plano da empresa para outra máscara (também ao alterar a máscara em *Empresas › Contabilidade*), com prévia antes de gravar.
- **Conta automática de clientes e fornecedores:** escolha as contas título em *Empresas › Contabilidade* e marque *Criar conta no plano de contas* na ficha (vale também no Fiscal).
- **BI › Evolução patrimonial:** ativo, passivo, patrimônio líquido e resultado mês a mês ou por ano, com gráfico e variação.

### Fiscal
- **Importar SPED (EFD ICMS/IPI e EFD-Contribuições)** em *Integrações*.
- **Simples Nacional × IBS/CBS:** compara o imposto de hoje com o de 2026 a 2033, com IBS/CBS dentro ou fora do DAS. Os percentuais da transição estão nos parâmetros legais e devem ser conferidos com a legislação.
- Lupa (F4) nos campos de CFOP, NCM, código de ajuste e participante.

### Folha
- **Importar eSocial** em *Integrações › Importar*: eventos do portal (XML ou ZIP) ou baixados com o certificado; cadastra empregador, rubricas, funcionários, afastamentos e o histórico de remunerações para continuar a folha (médias de férias e 13º, saldos de férias).

### Em todos os módulos
- **Inclusão em janela:** as telas de cadastro mostram a lista e a ficha abre numa janela por cima (Incluir, Alterar, duplo clique ou Enter). Inclui as regras de depreciação.
- **Contadores:** a janela de inclusão foi reorganizada (um tipo por vez, campos alinhados).

Para atualizar, rode o **Setup-Vorticis-1.0.4.exe** por cima da versão instalada: os dados são preservados.

---

# Vorticis 1.0.3

## Novidades desta versão

### Suite
- **A Vorticis Suite Empresarial ganhou a aparência nova dos módulos:** barra de menus clara com a faixa na cor do módulo à frente, a empresa num seletor arredondado e o seu usuário com as iniciais num círculo, as mesmas etiquetas arredondadas das funções abertas e os mesmos botões, campos e listas, nos cinco visuais e nos temas claro e escuro.

Para atualizar, rode o **Setup-Vorticis-1.0.3.exe** por cima da versão instalada: os dados são preservados.

---

# Vorticis 1.0.2

## Novidades desta versão

### Escritório
- **O escritório é a empresa 0001 e já entra como o cliente 0001.** Em *Cadastros › Dados do escritório* (que não é mais o cadastro geral de empresas) o escritório recebe o código 0001 e é incluído sozinho no cadastro de clientes, em primeiro na lista; os clientes seguintes ficam com 0002, 0003... É do cadastro de clientes que o sistema todo é alimentado: os módulos marcados no cliente 0001 valem para a própria empresa do escritório.
- **Clientes, Tarefas e prazos e Protocolos** mostram a lista na tela e a ficha abre numa janela por cima (Incluir, Alterar, duplo clique ou Enter).
- Os parâmetros do Escritório ficam só em *Cadastros › Parâmetros*.

### Contábil
- **Plano de contas:** a tela mostra todas as contas e a ficha da conta abre em janela. **Importar contas do Excel** acrescenta ao plano só as contas que você marcar na planilha, sem trocar o plano.

### Usuários
- Dados pessoais e de contato do usuário: CPF, e-mail, telefone, celular, admissão, cargo e departamento.
- **Módulos que o usuário pode acessar:** desmarcado, o módulo fica bloqueado para ele.

### Em todos os módulos
- **Exportar a lista:** toda lista ganha o botão Exportar (Excel, CSV ou PDF) com o que está na tela.
- **Histórico de alterações** (*Configurações*, administrador): quem incluiu, alterou ou excluiu cada cadastro, quando e o que mudou.
- **Backup automático diário**, guardando os 7 últimos (*Configurações › Backup e restauração*).
- **Bloqueio por inatividade:** depois de 15 minutos sem uso a tela pede a senha (ajuste em *Preferências*; 0 desliga).
- **Ajuda › Verificar atualizações.**
- Aparência mais moderna nos módulos abertos sozinhos: cabeçalho com o selo do módulo, a empresa e o usuário.

### Suite
- **Cinco visuais:** Clássico renovado, Barra lateral, Faixa de opções, Início em mosaico e Busca primeiro. Quem usava um dos visuais retirados passa para o mais parecido.

Para atualizar, rode o **Setup-Vorticis-1.0.2.exe** por cima da versão instalada: os dados são preservados e o banco é atualizado sozinho na primeira abertura.

---

# Vorticis 1.0.1

## Novidades desta versão

- **Dez visuais para a Vorticis Suite Empresarial.** No botão **Visual** (ou em *Exibir › Visual da Suite*) cada pessoa escolhe, pela imagem, o visual que mais lhe agrada; a troca é imediata e fica gravada para o usuário:
  1. Clássico renovado (padrão, igual aos módulos separados);
  2. Barra lateral;
  3. Barra de comandos, com as funções mais usadas e o caminho da tela;
  4. Lista e ficha: nos cadastros, a lista à esquerda e a ficha à direita;
  5. Faixa de opções;
  6. Início em mosaico;
  7. Trilho compacto;
  8. Área em painéis, com as funções abertas, favoritos, recentes e a árvore;
  9. Busca primeiro: "O que você quer fazer?" no centro da tela;
  10. Alta densidade: tema escuro, mais linhas por tela e as teclas de atalho no rodapé.
- **Ctrl+K (Ir para…)** em todos os visuais.
- Quem já usava a Suite continua no visual equivalente ao layout escolhido antes.

Para atualizar, rode o **Setup-Vorticis-1.0.1.exe** por cima da versão instalada: os dados são preservados.

---

# Vorticis 1.0.0

Primeira versão para uso no dia a dia. A partir dela, cada atualização ganha um número novo (1.0.1 para correções, 1.1.0 para funções novas) e estas notas contam o que mudou.

## Como instalar ou atualizar

1. Baixe o **Setup-Vorticis-1.0.5.exe** desta página e execute. Não precisa de senha de administrador do Windows.
2. Escolha os módulos. O instalador prepara o banco de dados sozinho.
3. Na primeira vez, entre com **vorticis / vorticis** e cadastre o administrador do escritório: a partir daí esse acesso deixa de valer.
4. Para atualizar no futuro, rode o instalador da versão nova por cima: **os dados são preservados** e o banco é atualizado sozinho na primeira abertura.

Quem prefere não instalar pode usar o **VorticisSuiteEmpresarial-portatil-1.0.5.zip**: descompacte numa pasta e abra o `VorticisSuite.exe`.

**Antes de começar:** em *Configurações › Backup*, escolha a pasta das cópias de segurança (de preferência num disco externo ou numa pasta sincronizada com a nuvem) e faça uma cópia ao fim de cada dia de trabalho.

## O que vem nesta versão

- **Escritório**: clientes, contratos, honorários e cobranças, usuários e acessos centralizados. É o único lugar onde se escolhe em quais módulos cada empresa é usada.
- **Contábil**: plano de contas (com importação), lançamentos, conciliações, imobilizado e depreciação, balancete, razão, diário, DRE, balanço, DMPL, DLPA, fluxo de caixa, livros em PDF, ECD e ECF.
- **Fiscal**: notas de entrada e saída (digitação e importação de XML de NF-e, NFC-e, NFS-e e CT-e), regras fiscais e de contabilização, livros, apuração de ICMS, IPI, PIS/COFINS, Simples Nacional, Lucro Presumido e Real, DCTFWeb, planejamento tributário, EFD ICMS/IPI e a preparação para o IBS e a CBS.
- **Folha**: funcionários, sócios e pró-labore, folha mensal, férias, 13º, rescisões, contabilização, eSocial e FGTS Digital.
- **Gerencial** e **Condomínio**.
- **Vorticis Suite Empresarial**: todos os módulos numa só janela, com cinco layouts e tema claro ou escuro.

### Novidades desde a versão anterior (0.4.0)

- Os campos de valor mostram o número com ponto de milhar e duas casas (1.234,56) ao sair do campo.
- Lupa (ou F4) nos campos de conta, histórico, participante, centro de custo e produto para pesquisar o que informar; as listas de escolha longas ganham a caixa **Pesquisar na lista**.
- Fiscal: na janela da nota, os itens ficam na própria aba **Nota**; *Contabilizar notas fiscais*, *Importar NFS-e* e *Importar CT-e* passam para o menu **Integrações**.
- A escolha dos módulos de cada empresa fica só no Escritório.

O manual completo está em *Ajuda › Manual* (tecla **F1** abre o tópico da tela em uso).
