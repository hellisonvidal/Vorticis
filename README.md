# Vorticis Contábil

Sistema de Escrituração Fiscal Contábil para empresas brasileiras (Simples Nacional, Lucro Presumido e Lucro Real): contabilidade, documentos fiscais, conciliação bancária, demonstrações, SPED (ECD/ECF), folha de pagamento e eSocial.

Aplicativo desktop para Windows com PostgreSQL na própria máquina; arquitetura preparada para servidor/nuvem no futuro.

## Estado atual

| Fase | Situação |
|---|---|
| 0. Fundação | Solução .NET 10, migrações versionadas, multiempresa, auditoria com hash encadeado, CI, login, **perfis de acesso configuráveis pelo administrador**, menu (Cadastros / Lançamentos / Imobilizado / Consultas / Integrações / Relatórios / Configurações / Ajuda), cadastro de empresa com consulta à base pública do CNPJ, **backup e restauração**, **instalador único com banco de dados embutido (sem configurar PostgreSQL)** ✔ |
| 1. MVP contábil | Plano de contas (modelo + manutenção), centros de custo, saldos iniciais, lançamento com validação de partidas dobradas, estorno, fechamento/reabertura de período, Diário, Razão, Balancete, **relatórios em tela com exportação para PDF, Excel (.xlsx) e CSV**, **Consultas rápidas de saldos e lançamentos**, **importação de lançamentos (planilhas com layouts personalizados, extratos OFX, layout padrão)**, **importação do plano de contas de planilha com conversão de máscara**, **livros em PDF (Diário com termos de abertura e encerramento, Razão, Balancete)** ✔ |
| 3. Demonstrações e ECD | Encerramento do exercício (IND_LCTO = E) e desfazer; Balanço Patrimonial e DRE (tela e PDF); **plano referencial da RFB: importação da tabela oficial e vínculo das contas (sugestões automáticas, cópia do ano anterior, pendências para a ECD)**. **Pendente:** DMPL/DLPA, DFC, gerador do arquivo ECD |

Ver o [roadmap](docs/04-roadmap.md).

## Estrutura

```
db/                         Migrações SQL (fonte única; embutidas no Vorticis.Infra) e testes SQL
src/Vorticis.Domain          Regras puras: validação de lançamentos, estorno, balancete, rateio, CNPJ
src/Vorticis.Application     Casos de uso (ServicoLancamento, ServicoPeriodo, ServicoRelatorios, ServicoEmpresa) e portas
src/Vorticis.Infra           PostgreSQL (Npgsql), migrador, plano de contas modelo
src/Vorticis.Desktop.Comum   Casco comum dos executáveis (Avalonia/MVVM): bootstrap, janela principal com menu do módulo, login, banco, empresas, usuários, perfis, certificados, backup, configurações
src/Vorticis.Desktop         Executável do módulo Contábil (VorticisContabil.exe)
src/Vorticis.Fiscal(.Desktop)  Regras/serviços e executável do módulo Fiscal (VorticisFiscal.exe) — estrutura pronta, funções em breve
src/Vorticis.Folha(.Desktop)   Regras/serviços e executável do módulo Folha de Pagamento (VorticisFolha.exe) — estrutura pronta, funções em breve
tests/                      Unitários, integração com PostgreSQL e testes de interface headless
docs/                       Arquitetura, regras, desafios fiscais, roadmap
```

## Instalar (usuário final, Windows 10/11 64 bits)

1. Baixe `Setup-Vorticis-<versão>.exe` (artefato **Setup-Vorticis** da execução mais recente do CI, na aba *Actions* do GitHub).
2. Execute. Não pede senha de administrador (instala para o usuário atual; a primeira tela permite escolher "todos os usuários").
3. Escolha os módulos: **Vorticis Contábil**, **Vorticis Fiscal** e **Vorticis Folha de Pagamento** (todos marcados; pelo menos um é obrigatório). Cada um vira um executável e um atalho próprios; o PostgreSQL embutido é instalado com qualquer módulo.
4. Abra o módulo desejado. Na primeira abertura ele **cria sozinho o banco de dados** e vai direto à criação do administrador. Os módulos compartilham o mesmo banco, login, empresas e configurações; o banco sobe com o primeiro módulo aberto e só para quando o último fecha.

Não é preciso instalar nem configurar o PostgreSQL: o instalador traz os binários oficiais do PostgreSQL 16 (pasta `pgsql`), e o Vorticis
cuida do resto:

| Item | Como funciona |
|---|---|
| Criação | `initdb` na primeira abertura, em `%LOCALAPPDATA%\Vorticis\banco`, com senha aleatória (autenticação SCRAM) gravada cifrada (DPAPI) em `%APPDATA%\Vorticis\vorticis.json` |
| Execução | O servidor sobe quando o primeiro módulo do Vorticis abre e para quando o último fecha; escuta só em `localhost`, na porta 54329 (ou outra livre, se ocupada) |
| Segurança | Nenhum serviço do Windows nem privilégio de administrador; outros usuários do Windows não leem a senha |
| Atualização | Basta rodar o instalador novo: ele para o banco, troca os arquivos e preserva os dados |
| Desinstalação | Remove o programa, **não** os dados (`%LOCALAPPDATA%\Vorticis`) — apague a pasta manualmente se quiser |
| Backup | *Configurações › Backup e restauração* usa o `pg_dump`/`pg_restore` que acompanham o programa |
| Rede | Para vários computadores usarem o mesmo banco, *Configurações › Conexão* aceita um servidor PostgreSQL; o botão "Usar o banco local" volta ao banco embutido |

Também há os artefatos portáteis **VorticisContabil-portatil**, **VorticisFiscal-portatil** e **VorticisFolha-portatil** (pasta com o `.exe` do módulo + `pgsql`): descompacte e execute, sem instalar.

## Executar (desenvolvimento)

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e PostgreSQL 16+ (ou `VORTICIS_PG_EMBUTIDO_BIN` apontando para a pasta `bin` de um PostgreSQL, para usar o modo banco local).

```bash
# conexão (opcional; padrão: Host=localhost;Database=vorticis;Username=vorticis;Password=vorticis)
set VORTICIS_DB=Host=localhost;Database=vorticis;Username=postgres;Password=suasenha     # Windows (cmd)
export VORTICIS_DB="Host=localhost;Database=vorticis;Username=postgres;Password=suasenha" # Linux/macOS

dotnet run --project src/Vorticis.Desktop          # Contábil (ou src/Vorticis.Fiscal.Desktop, src/Vorticis.Folha.Desktop)
```

Sem `VORTICIS_DB` e sem binários do PostgreSQL ao lado do programa, usa o servidor padrão acima. Na primeira execução o banco é criado (se o usuário do PostgreSQL tiver permissão), as migrações são aplicadas e a tela de **primeiro acesso** pede a criação do usuário administrador. Em seguida, a **configuração dos perfis de acesso é obrigatória** (as demais funções ficam bloqueadas até o administrador clicar em *Concluir configuração dos perfis*); o cadastro de empresas não é exigido no primeiro uso. A janela sempre abre maximizada. Depois do login o sistema abre a **tela inicial** (nome do programa ao fundo; *Ajuda › Tela inicial* volta a ela). Se a conexão falhar, o próprio Vorticis abre a tela de conexão; a configuração fica em `%APPDATA%\Vorticis\vorticis.json`, com a senha cifrada por DPAPI no Windows.

### Usuários e perfis de acesso

Tudo sobre usuários fica em **Configurações › Usuários**, em abas: *Usuários* (cadastro), *Perfis de acesso*, *Alterar minha senha* e *Registro de acessos* (usuários comuns veem só a troca de senha).

O administrador cria **perfis** marcando função a função o que cada um libera. No cadastro do usuário o **perfil é obrigatório**: ele vale para todas as empresas, inclusive as cadastradas depois, e admite exceções por empresa (outro perfil ou sem acesso). Funções configuráveis:

| Grupo | Funções |
|---|---|
| Lançamentos | Incluir lançamentos · Estornar · Saldos iniciais · Fechar períodos · Reabrir períodos |
| Cadastros | Manter plano de contas · Manter centros de custo |
| Relatórios | Balancete · Livro Razão · Livro Diário · Exportar (PDF, Excel, CSV) |

Perfis padrão (não excluíveis): **Administrador** (todas as funções, sempre, e a administração do sistema: empresas, usuários, perfis, conexão e backup), **Contador** (todas as funções contábeis), **Assistente** (lança e estorna, não fecha nem reabre períodos), **Consulta** (relatórios). As permissões são verificadas **nos serviços** (não só no menu): mesmo que a tela seja contornada, a operação é recusada.

Senhas com PBKDF2-SHA256 (600 mil iterações); bloqueio de 15 minutos após 5 tentativas inválidas; usuário criado pelo administrador troca a senha no primeiro acesso; todas as tentativas de login ficam em `core.registro_acesso` (aba *Registro de acessos*). Na tela de entrada, **Salvar login neste computador** guarda usuário e senha em `%APPDATA%\Vorticis\login.json`, com a senha cifrada pelo Windows (DPAPI, só o mesmo usuário do Windows consegue ler); não use em computadores compartilhados.

### Livros, demonstrações e encerramento

- *Relatórios › Contábeis* agrupa todos os relatórios contábeis. *Livro Contábil Completo* (antes "Livros e relatórios em PDF"): o usuário marca o que entra no livro (termo de abertura, Diário, Razão, Balancete, Balanço, DRE, DMPL, DLPA, termo de encerramento e assinaturas) e informa o número do livro; o NIRE vem do cadastro da empresa (somente leitura, com aviso se vazio). Segue a ITG 2000: folhas numeradas, identificação da entidade, assinaturas do representante legal e do contador — o número de folhas do termo é calculado após a montagem; cada parte começa em página nova. *Termos* gera o Termo de Abertura e o de Encerramento isoladamente, cada um em seu PDF. Em todos os relatórios exportados para PDF há as opções *Assinatura do sócio/responsável* e *Assinatura do contador*, que incluem o bloco de assinaturas ao final (nome, qualificação/CRC e CPF dos cadastros de Sócios e Contadores). Gerados com PDFsharp/MigraDoc (MIT) e fonte Liberation Sans embutida (SIL OFL 1.1, ver `src/Vorticis.Infra/Dados/Fontes/OFL.txt`).
- *Relatórios › Balanço Patrimonial e DRE*: estrutura do plano da empresa até o nível escolhido; antes do encerramento, o resultado do período aparece destacado no PL para o balanço fechar; a DRE ignora os lançamentos de encerramento.
- *Relatórios › Contábeis › DMPL* (CPC 26 / NBC TG 26): colunas por componente do PL (capital, reservas de capital, reservas de lucros, ajustes de avaliação patrimonial, lucros ou prejuízos acumulados e total; ações em tesouraria só se houver) e linhas de saldo inicial, ajustes de exercícios anteriores, aumento de capital, resultado do exercício, constituição de reservas, dividendos e saldo final. As contas do PL são enquadradas nas colunas pela descrição; a implantação de saldos compõe a abertura; as mutações do período são classificadas pela contrapartida; o resultado é o da DRE, de modo que o saldo final confere com o Balanço.
- *Relatórios › Contábeis › DLPA* (Lei 6.404, art. 186): saldo inicial, ajustes de exercícios anteriores, reversões de reservas, lucro líquido, saldo à disposição, transferências para reservas, dividendos e saldo final. DMPL e DLPA têm tela e PDF próprios e exportam também para Excel e CSV.
- *Lançamentos › Encerramento do exercício*: zera as contas de resultado contra a Apuração do Resultado e transfere o lucro/prejuízo ao PL, em 31/12, com IND_LCTO = E (ECD). "Desfazer" estorna os dois lançamentos para permitir ajustes.

### Uso: janelas e cadastros

- Cada função aberta pelo menu abre em **tela cheia, na área de trabalho abaixo do menu** (sem cobrir a barra de título do programa), com o botão **✕ Fechar** no cabeçalho. A barra abaixo do menu lista as funções abertas: clique para trazer à frente, ✕ para fechar. Reabrir a mesma função traz a janela existente.
- Todos os cadastros (empresas, sócios, contadores, plano de contas, centros de custo, usuários e perfis) têm a mesma barra: **Incluir · Alterar · Excluir · Salvar · Cancelar** e mostram o **Nº** sequencial de cada registro. Em consulta o formulário mostra o registro selecionado; Incluir/Alterar liberam a edição; **Excluir sempre pede confirmação**.
- **Exclusão física:** excluir apaga o registro do banco de dados, para que o mesmo CNPJ, login ou código possa ser cadastrado de novo. Excluir uma **empresa** apaga também toda a sua escrituração (lançamentos, plano, períodos, sócios, acessos): o sistema avisa, **gera um backup automático antes** e informa onde ele ficou. Excluir um **usuário** mantém o histórico (lançamentos, registro de acessos) sem o vínculo com ele; ninguém exclui o próprio usuário.
- **Fechar uma função e abri-la de novo recomeça do zero** (nada do que foi digitado ou consultado permanece).
- **Lançamento contábil pelo teclado:** todos os campos ficam abertos para digitação (um clique já edita); Enter/Tab avança, ↓ na última linha cria outra, ↑ numa linha recém-criada e ainda vazia a cancela, Ctrl+Delete exclui a linha, **Enter no último campo salva** (quando o lançamento já fecha; senão segue para a próxima linha), Ctrl+Enter também salva. Antes da conta há o **código reduzido**; a conta também pode ser digitada pelo nome (lista de sugestões). O valor é formatado com milhar e duas casas (1.500,00). O código do **histórico padrão** preenche o texto fixo e deixa o cursor no fim para completar.
- **Configurações › Configurações gerais** (administrador): senha de administrador e limpeza do banco. O tipo de lançamento não é mais configurado: uma linha com conta débito e crédito é um lançamento; linhas com uma conta só somam um único lançamento que precisa fechar (se faltar a contrapartida, o sistema pergunta e oferece incluir a linha).
- **Históricos padrão com variáveis**: o texto aceita `{DOCUMENTO}`, `{PARTICIPANTE}`, `{DATA}`, `{MES}`, `{ANO}`, `{MES_ANTERIOR}`, `{COMPETENCIA}`, `{CONTA}`, `{VALOR}` e `{COMPLEMENTO}`, preenchidas no lançamento com os dados da linha (as não resolvidas ficam para o usuário completar).
- **Grades**: Ctrl+C copia só a célula atual; Ctrl+V cola na célula editável.
- **Código reduzido:** as contas analíticas recebem uma sequência numérica automática (editável no plano de contas); históricos, clientes, fornecedores e bens também têm código sequencial.
- **Cadastros › Históricos** (texto fixo + complemento livre no lançamento), **Cadastros › Clientes** e **Cadastros › Fornecedores** (menus separados; a mesma pessoa pode ser os dois; CPF/CNPJ validado, conta contábil opcional). Seguem o padrão do cadastro de empresas: formulário em seções (identificação, endereço completo com CEP, contabilidade) e a lista de cadastrados à direita; *Buscar dados na Receita* preenche tudo pelo CNPJ (e a inscrição estadual, ou ISENTO).
- **Cadastros › Plano de contas** no padrão do cadastro de empresas: formulário em seções à esquerda (identificação, classificação, controles) e as contas cadastradas à direita, com o código alinhado à esquerda (o recuo do nível fica na descrição).
- **Plano de contas com participantes** (opção em *Cadastros › Empresas › Escrituração*): clientes e fornecedores ficam alocados dentro das contas contábeis — no cadastro do cliente/fornecedor, informe a conta (ex.: Clientes) e marque **Participante**; a conta passa a exigir o participante, a tela de lançamento ganha a coluna *Part.* e o balancete mostra só a soma na conta, com a opção **Detalhar participantes** (uma linha por cliente/fornecedor abaixo da conta, inclusive na exportação). Desligar a opção libera as contas.
- **Código da empresa** (nº do cadastro, 0001) aparece no seletor de empresas do topo e na lista *Empresas cadastradas*.
- **Máscaras de digitação**: todo campo de CNPJ (inclusive alfanumérico), CPF, CEP e data é formatado enquanto se digita — 00.000.000/0000-00, 000.000.000-00, 00000-000 e dd/mm/aaaa.
- **Nº do documento no lançamento** (NF, recibo, boleto): fica na partida (ECD I250.NUM_ARQ) e é a base da conciliação automática. A tela de lançamento mostra os **lançamentos efetuados** do período — os estornados continuam na lista, marcados — e o **estorno** é feito ali mesmo: selecione o lançamento, informe a data e o motivo e clique em *Estornar*. **Duplo clique** num lançamento efetuado (manual, não estornado) o abre para **alteração**: ao salvar, o original é estornado na data dele e o lançamento corrigido é gravado, numa única transação (lançamentos efetivados nunca são editados no lugar).
- **Lançamentos › Conciliação automática:** concilia as partidas de uma conta (fornecedores, clientes, bancos...) **automaticamente pelo nº do documento** quando débitos e créditos do mesmo documento se anulam (ex.: a NF na compra e o pagamento da mesma NF), ou manualmente marcando as partidas; *Desfazer* reabre. O que fica em aberto compõe o saldo da conta.
- **Consultas › Diferenças de saldos:** saldo de cada conta no início e no fim do período e a diferença, com alertas de débitos ≠ créditos, saldo contrário à natureza e divergências internas (partidas × saldos mensais).
- **Livro Razão:** painel com as contas analíticas — clique numa conta para ver o razão no período (aceita também o código reduzido).
- **Fechar o sistema pede confirmação.**
- **Configurações gerais › Limpar banco de dados** (NÃO recomendado, só administrador): exige digitar LIMPAR e, em seguida, a **senha de administrador** (padrão `VORTICIS`, alterável em Configurações gerais); gera um backup antes, apaga empresas, cadastros, usuários e perfis personalizados e **volta à tela de login**, onde se entra com o administrador padrão **ADMIN / VORTICIS** (troca de senha obrigatória no primeiro acesso).
- **Configurações › Certificados digitais** (só administrador): repositório dos certificados para comunicação com órgãos públicos. **A1** (.pfx/.p12): *Conferir* mostra titular, CNPJ/CPF e validade; *Importar* guarda o arquivo no banco como está, protegido pela própria senha (a senha não é gravada — é pedida no uso). **A3** (token/cartão): *Ler certificados instalados no Windows* e *Registrar A3*. A lista avisa os certificados vencidos ou que vencem em até 30 dias.
- **Inscrição estadual:** se a consulta não encontrar inscrição para o CNPJ na UF, o campo é preenchido com **ISENTO** automaticamente.
- **Consultas** (menu próprio): *Saldos de contas* (saldo de cada conta numa data, com filtro por código/nome) e *Lançamentos* (por período, filtrando por conta, texto do histórico, valor ou número) — respostas rápidas sem gerar relatório.
- **Relatórios na tela:** Balancete, Razão, Diário e Balanço/DRE são sempre exibidos na tela, com os botões **PDF**, **Excel (.xlsx)** e **CSV** (abre no Excel em pt-BR). A exportação exige a permissão *Exportar relatórios*.
- **Ajuda › Licença e registro:** para quem o Vorticis está registrado (titular, CPF/CNPJ, e-mail — o administrador registra) e as licenças dos componentes de terceiros.
- **Configurações › Conexão com o banco de dados:** permite editar a conexão a qualquer momento — banco local (porta, troca da senha, pasta dos dados) ou servidor PostgreSQL (servidor, porta, banco, usuário, senha, com *Mostrar senhas* e *Testar conexão*).

### Imobilizado

- **Regras de depreciação (grupos):** taxas fiscais da RFB (IN RFB 1.700/2017, Anexo III; RIR/2018 arts. 317–330) já cadastradas — edificações 4%, instalações, máquinas e móveis 10%, veículos de passageiros e computadores 20%, veículos de carga 25%, softwares (amortização) 20%, terrenos não depreciam — com as contas sugeridas; podem ser alteradas e ampliadas.
- **Cadastro de bens:** grupo, plaqueta, local, NF, fornecedor, aquisição, início de uso, custo, **valor residual, vida útil e método (linear ou soma dos dígitos)** conforme a NBC TG 27 / CPC 27, turnos de uso (aceleração fiscal ×1,5 ou ×2,0 — RIR/2018 art. 323), depreciação já existente na implantação, contas do bem, da depreciação acumulada e da despesa, e baixa.
- **Cálculo e lançamento da depreciação:** por competência, mostra a quota contábil (CPC 27) e a fiscal (RFB) e a diferença a controlar no LALUR; *Lançar* gera o lançamento (despesa × depreciação/amortização acumulada) no último dia do mês; *Estornar competência* desfaz o mês mais recente.

### Integrações › Exportar

- **Lançamentos contábeis**: as partidas do período (nº, data, conta, D/C, valor, histórico, documento, centro de custo, participante e situação) em **Excel (.xlsx), PDF ou CSV**, com opção de deixar de fora os estornados e seus estornos.

### Integrações › Importar

Cada importação é uma função própria no menu Integrações › Importar.

- **Planilhas** (Excel .xlsx ou CSV): o usuário cria **layouts personalizados** dizendo qual coluna da planilha é a data, a conta débito/crédito (ou conta fixa), o valor, o histórico, o documento e o centro de custo. Dois modelos: *uma linha por lançamento* (débito e crédito na mesma linha; valor negativo inverte) ou *uma linha por partida* (conta + D/C + valor, ou colunas de valor a débito e a crédito, agrupadas pelo nº do lançamento ou até débitos = créditos). Opcionalmente converte os códigos de conta por uma máscara (11010001 → 1.1.01.0001). Os layouts ficam salvos no banco e servem a todas as empresas.
- **Extratos bancários (OFX)**: lê o extrato do internet banking; cada movimento vira um lançamento contra a conta contábil do banco, com contrapartida padrão para entradas e saídas (editável linha a linha). Movimentos já importados (banco/agência/conta/FITID) não são duplicados; se o lançamento for estornado, pode ser importado de novo.
- Na importação de planilhas, **Carregar** mostra a planilha como está no arquivo (até 200 linhas), ao lado do layout: o cabeçalho de cada coluna indica o papel definido (Data, Conta débito, Valor...), para conferir antes de importar.
- **Lançamentos contábeis**: layout padrão do Vorticis (`LANCAMENTO;DATA;CONTA;DC;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO`), com botão para gerar a planilha modelo.
- Em todas, **Pré-visualizar** valida cada lançamento (contas analíticas, período aberto, partidas dobradas) e **Importar** grava tudo numa única transação — se um falhar, nenhum é gravado.
- **Modelos de plano de contas e aba Contabilidade da empresa**: 13 modelos embutidos (básico; comércio, indústria e serviços nos regimes Simples, Presumido e Real; ITG 1000 ME/EPP; entidade sem fins lucrativos/ITG 2002; completo geral/CPC), escolhidos no cadastro da empresa (*Cadastros › Empresas › Contabilidade*) ou em *Plano de contas › Escolher plano* (a grade só aparece com o plano escolhido; troca só sem lançamentos). A aba guarda também máscara, início da escrituração, centro de custo, grande porte, apuração do IRPJ, regime de caixa, uso de participantes e as contas de apuração/lucros/prejuízos que o Encerramento usa como padrão. O NIRE (11 dígitos) fica nos dados cadastrais. Conta que exige participante indica o **grupo** (conta sintética) onde as contas dos participantes são alocadas.
- **Plano de contas**: importa um plano modelo de planilha (código e descrição obrigatórios; natureza, tipo S/A e saldo D/C lidos ou deduzidos), convertendo os códigos para a **máscara escolhida** (ex.: `9.9.99.999`: 1.1.1.1 → 1.1.01.001; 1101001 → 1.1.01.001). Substitui o plano só enquanto não há lançamentos. *Converter plano atual para a máscara* troca os códigos das contas existentes, mesmo com movimento (lançamentos e hierarquia não mudam).
- **Vínculo ao plano referencial** também fica em Integrações › Importar.
- *Exportar*: ECD, ECF e eSocial em breve.

### Sócios e contadores

- **Cadastros › Empresas, aba Sócios** (da empresa selecionada): nome, CPF/CNPJ, qualificação, participação no capital (soma dos ativos ≤ 100%), entrada/saída e representante legal. *Importar quadro societário da Receita* traz o QSA da base pública (o CPF vem mascarado — complete-o). Ao cadastrar uma empresa pela consulta de CNPJ, os sócios já são incluídos.
- **Cadastros › Contadores** (também como aba *Contadores* em Cadastros › Empresas): contadores (CPF, CRC/UF) e escritórios (CNPJ, CRC) são cadastros únicos reaproveitados entre empresas; cada empresa tem seus vínculos com início/fim e **um responsável técnico vigente** (garantido também no banco). Troca de contabilidade: encerre o vínculo, não exclua. A flag **Assina ECD/ECF** indica o contador signatário da ECD e da ECF (e dos termos do Diário); sem marcação, assina o responsável técnico.
- Os termos do **Livro Diário** já vêm preenchidos com o representante legal e o responsável técnico — os mesmos signatários da ECD (registro J930).

### Plano referencial da RFB

1. **Integrações › Importar › Plano referencial da RFB** (administrador): importe a tabela oficial do plano referencial para o tipo de plano (ex.: *PJ em geral – Lucro Real*) e o ano. Obtenha o arquivo no portal do SPED (tabelas da ECD/ECF) ou no programa validador; ele precisa de uma primeira linha de cabeçalho com ao menos `CODIGO` e `DESCRICAO` (`TIPO`, `COD_SUP`, `NIVEL`, `COD_NAT`, `DT_INI`, `DT_FIM` são usados quando existem; separador `|`, `;`, tab ou vírgula; UTF-8 ou ISO-8859-1). Reimportar atualiza a tabela sem perder vínculos.
2. **Integrações › Importar › Vínculo ao plano referencial**: escolha o plano do exercício e vincule cada conta analítica a uma referencial analítica de mesma natureza (ECD I051). *Sugerir vínculos* propõe pela semelhança das descrições (o contador confirma); *Copiar do ano anterior* reaproveita os vínculos. Contas com movimento ou saldo sem vínculo aparecem como pendências que impedem a ECD.

O Vorticis não traz a tabela embutida: ela é oficial, muda a cada ano e deve vir da RFB. Os testes usam uma tabela **fictícia**.

### Backup e restauração

*Configurações › Backup e restauração* gera cópias com `pg_dump` (formato compactado). Por padrão elas ficam na pasta **Backups dentro do diretório de instalação** do programa (criada pelo instalador; se não puder ser gravada, `Documentos\Vorticis Contabil\Backups`); *Escolher pasta...* grava outra pasta para os próximos backups, inclusive os automáticos. *Excluir backup selecionado* apaga um arquivo de backup (com confirmação). A restauração pede confirmação com a **senha de administrador** (padrão `VORTICIS`), gera antes um backup de segurança do estado atual e encerra todas as sessões. Usa as ferramentas do PostgreSQL que acompanham o instalador (ou as de um PostgreSQL instalado na máquina, ou a pasta indicada em `VORTICIS_PG_BIN`).

### Consulta de CNPJ

Em **Cadastros › Empresas**, o botão *Buscar dados na Receita* preenche razão social, natureza jurídica, CNAE, endereço, município IBGE, início de atividade, situação cadastral e opção pelo Simples/MEI. A fonte é a **base pública do CNPJ divulgada pela Receita Federal** (dados abertos, atualização mensal), via [BrasilAPI](https://brasilapi.com.br) e, como reserva, [Minha Receita](https://minhareceita.org). O portal "Comprovante de Inscrição" da RFB tem captcha e não pode ser automatizado; a API oficial (Serpro "Consulta CNPJ") é paga e pode ser plugada como outra implementação de `IConsultaCnpj`.

**Inscrição estadual:** *Buscar IE (SINTEGRA)* (também executado logo após a consulta do CNPJ) procura as inscrições do CNPJ na UF da empresa — a UF informada ou, na falta dela, a deduzida do código IBGE do município. A fonte é a API pública do [CNPJ.ws](https://www.cnpj.ws), que reúne as inscrições dos cadastros estaduais (os portais SINTEGRA de cada estado exigem captcha); limite de 3 consultas por minuto. O sistema **sempre pergunta** antes de usar o número: em Incluir/Alterar ele vai para o formulário (gravado ao Salvar); em consulta, grava na empresa só se o usuário confirmar. *Abrir SINTEGRA* abre o portal para conferência manual.

### Gerar o instalador

O job `instalador-windows` do CI faz tudo em um Windows limpo (o ícone do programa e do instalador vem de `instalador/icone/`, gerado por `gerar_icone.py`):

1. `instalador/baixar-postgresql.ps1` baixa os binários oficiais do PostgreSQL 16 para Windows (EDB) e monta `pgsql` (só `bin`, `lib`, `share` + runtime do Visual C++);
2. roda o teste do banco embutido no Windows (`ServidorEmbutidoTests`: initdb, start, migrações, reinício, stop);
3. publica `VorticisContabil.exe`, `VorticisFiscal.exe` e `VorticisFolha.exe` (arquivo único, sem exigir .NET instalado) na pasta `publish`, com a pasta `pgsql` ao lado, e monta os portáteis (exe + `pgsql` de cada módulo);
4. compila `instalador/Vorticis.iss` com o Inno Setup → `Setup-Vorticis-<versão>.exe` (seleção de módulos);
5. `instalador/teste-instalacao.ps1` instala em modo silencioso (todos os módulos), abre o Contábil e confere que o banco foi criado, migrado e parado ao fechar.

Artefatos do CI: **Setup-Vorticis**, **VorticisContabil-portatil**, **VorticisFiscal-portatil**, **VorticisFolha-portatil**.

Localmente (Windows, com Inno Setup 6):

```powershell
./instalador/baixar-postgresql.ps1 -Destino pgsql
foreach ($p in "Vorticis.Desktop", "Vorticis.Fiscal.Desktop", "Vorticis.Folha.Desktop") {
  dotnet publish "src/$p" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
}
Move-Item pgsql publish/pgsql
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DVersao=0.4.0 instalador/Vorticis.iss   # gera saida\Setup-Vorticis-0.4.0.exe
```

## Testes

```bash
dotnet test                                    # unitários (sempre) + integração/UI (se houver banco)
export VORTICIS_TEST_PG="Host=localhost;Username=postgres;Password=postgres;Database=postgres"
dotnet test                                    # agora inclui integração e interface
export VORTICIS_SCREENSHOTS=/caminho/pasta      # opcional: salva PNG de cada aba no teste de interface
```

Os testes de integração criam e apagam um banco temporário por classe de teste.

## Documentação

| Documento | Conteúdo |
|---|---|
| [01 — Arquitetura](docs/01-arquitetura.md) | Stack, camadas, modelo de dados, fluxos, segurança |
| [02 — Partidas dobradas](docs/02-partidas-dobradas.md) | Regras de validação no banco e na aplicação |
| [03 — Desafios fiscais](docs/03-desafios-fiscais.md) | Reforma Tributária, SPED, regimes, folha, eSocial, integrações |
| [04 — Roadmap](docs/04-roadmap.md) | Fases do MVP à versão completa, critérios de saída |
