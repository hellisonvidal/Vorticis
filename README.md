# Perseus Contábil

Sistema de Escrituração Fiscal Contábil para empresas brasileiras (Simples Nacional, Lucro Presumido e Lucro Real): contabilidade, documentos fiscais, conciliação bancária, demonstrações, SPED (ECD/ECF), folha de pagamento e eSocial.

Aplicativo desktop para Windows com PostgreSQL na própria máquina; arquitetura preparada para servidor/nuvem no futuro.

## Estado atual

| Fase | Situação |
|---|---|
| 0. Fundação | Solução .NET 10, migrações versionadas, multiempresa, auditoria com hash encadeado, CI, login, **perfis de acesso configuráveis pelo administrador**, menu (Cadastros / Lançamentos / Integrações / Relatórios / Ajuda / Configurações), cadastro de empresa com consulta à base pública do CNPJ, **backup e restauração**, **instalador único com banco de dados embutido (sem configurar PostgreSQL)** ✔ |
| 1. MVP contábil | Plano de contas (modelo + manutenção), centros de custo, saldos iniciais, lançamento com validação de partidas dobradas, estorno, fechamento/reabertura de período, Diário, Razão, Balancete (CSV), **livros em PDF (Diário com termos de abertura e encerramento, Razão, Balancete)** ✔ |
| 3. Demonstrações e ECD | Encerramento do exercício (IND_LCTO = E) e desfazer; Balanço Patrimonial e DRE (tela e PDF); **plano referencial da RFB: importação da tabela oficial e vínculo das contas (sugestões automáticas, cópia do ano anterior, pendências para a ECD)**. **Pendente:** DMPL/DLPA, DFC, gerador do arquivo ECD |

Ver o [roadmap](docs/04-roadmap.md).

## Estrutura

```
db/                         Migrações SQL (fonte única; embutidas no Perseus.Infra) e testes SQL
src/Perseus.Domain          Regras puras: validação de lançamentos, estorno, balancete, rateio, CNPJ
src/Perseus.Application     Casos de uso (ServicoLancamento, ServicoPeriodo, ServicoRelatorios, ServicoEmpresa) e portas
src/Perseus.Infra           PostgreSQL (Npgsql), migrador, plano de contas modelo
src/Perseus.Desktop         Interface Avalonia (MVVM)
tests/                      Unitários, integração com PostgreSQL e testes de interface headless
docs/                       Arquitetura, regras, desafios fiscais, roadmap
```

## Instalar (usuário final, Windows 10/11 64 bits)

1. Baixe `Setup-PerseusContabil-<versão>.exe` (artefato **Setup-PerseusContabil** da execução mais recente do CI, na aba *Actions* do GitHub).
2. Execute. Não pede senha de administrador (instala para o usuário atual; a primeira tela permite escolher "todos os usuários").
3. Abra o Perseus Contábil. Na primeira abertura ele **cria sozinho o banco de dados** e vai direto à criação do administrador.

Não é preciso instalar nem configurar o PostgreSQL: o instalador traz os binários oficiais do PostgreSQL 16 (pasta `pgsql`), e o Perseus
cuida do resto:

| Item | Como funciona |
|---|---|
| Criação | `initdb` na primeira abertura, em `%LOCALAPPDATA%\Perseus\banco`, com senha aleatória (autenticação SCRAM) gravada cifrada (DPAPI) em `%APPDATA%\Perseus\perseus.json` |
| Execução | O servidor sobe quando o Perseus abre e para quando ele fecha; escuta só em `localhost`, na porta 54329 (ou outra livre, se ocupada) |
| Segurança | Nenhum serviço do Windows nem privilégio de administrador; outros usuários do Windows não leem a senha |
| Atualização | Basta rodar o instalador novo: ele para o banco, troca os arquivos e preserva os dados |
| Desinstalação | Remove o programa, **não** os dados (`%LOCALAPPDATA%\Perseus`) — apague a pasta manualmente se quiser |
| Backup | *Configurações › Backup e restauração* usa o `pg_dump`/`pg_restore` que acompanham o programa |
| Rede | Para vários computadores usarem o mesmo banco, *Configurações › Conexão* aceita um servidor PostgreSQL; o botão "Usar o banco local" volta ao banco embutido |

Também há o artefato **PerseusContabil-portatil** (pasta com `PerseusContabil.exe` + `pgsql`): descompacte e execute, sem instalar.

## Executar (desenvolvimento)

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e PostgreSQL 16+ (ou `PERSEUS_PG_EMBUTIDO_BIN` apontando para a pasta `bin` de um PostgreSQL, para usar o modo banco local).

```bash
# conexão (opcional; padrão: Host=localhost;Database=perseus;Username=perseus;Password=perseus)
set PERSEUS_DB=Host=localhost;Database=perseus;Username=postgres;Password=suasenha     # Windows (cmd)
export PERSEUS_DB="Host=localhost;Database=perseus;Username=postgres;Password=suasenha" # Linux/macOS

dotnet run --project src/Perseus.Desktop
```

Sem `PERSEUS_DB` e sem binários do PostgreSQL ao lado do programa, usa o servidor padrão acima. Na primeira execução o banco é criado (se o usuário do PostgreSQL tiver permissão), as migrações são aplicadas e a tela de **primeiro acesso** pede a criação do usuário administrador. Em seguida, a **configuração dos perfis de acesso é obrigatória** (as demais funções ficam bloqueadas até o administrador clicar em *Concluir configuração dos perfis*); o cadastro de empresas não é exigido no primeiro uso. A janela sempre abre maximizada. Depois do login o sistema abre a **tela inicial** (nome do programa ao fundo; *Ajuda › Tela inicial* volta a ela). Se a conexão falhar, o próprio Perseus abre a tela de conexão; a configuração fica em `%APPDATA%\Perseus\perseus.json`, com a senha cifrada por DPAPI no Windows.

### Usuários e perfis de acesso

Tudo sobre usuários fica em **Configurações › Usuários**, em abas: *Usuários* (cadastro), *Perfis de acesso*, *Alterar minha senha* e *Registro de acessos* (usuários comuns veem só a troca de senha).

O administrador cria **perfis** marcando função a função o que cada um libera. No cadastro do usuário o **perfil é obrigatório**: ele vale para todas as empresas, inclusive as cadastradas depois, e admite exceções por empresa (outro perfil ou sem acesso). Funções configuráveis:

| Grupo | Funções |
|---|---|
| Lançamentos | Incluir lançamentos · Estornar · Saldos iniciais · Fechar períodos · Reabrir períodos |
| Cadastros | Manter plano de contas · Manter centros de custo |
| Relatórios | Balancete · Livro Razão · Livro Diário · Exportar (CSV) |

Perfis padrão (não excluíveis): **Administrador** (todas as funções, sempre, e a administração do sistema: empresas, usuários, perfis, conexão e backup), **Contador** (todas as funções contábeis), **Assistente** (lança e estorna, não fecha nem reabre períodos), **Consulta** (relatórios). As permissões são verificadas **nos serviços** (não só no menu): mesmo que a tela seja contornada, a operação é recusada.

Senhas com PBKDF2-SHA256 (600 mil iterações); bloqueio de 15 minutos após 5 tentativas inválidas; usuário criado pelo administrador troca a senha no primeiro acesso; todas as tentativas de login ficam em `core.registro_acesso` (aba *Registro de acessos*). Na tela de entrada, **Salvar login neste computador** guarda usuário e senha em `%APPDATA%\Perseus\login.json`, com a senha cifrada pelo Windows (DPAPI, só o mesmo usuário do Windows consegue ler); não use em computadores compartilhados.

### Livros, demonstrações e encerramento

- *Relatórios › Livros e relatórios em PDF*: Livro Diário com termos de abertura e encerramento (ITG 2000: folhas numeradas, identificação da entidade, assinaturas do representante legal e do contador — o número de folhas do termo é calculado após a montagem), Livro Razão de todas as contas com movimento, Balancete e Balanço/DRE. Gerados com PDFsharp/MigraDoc (MIT) e fonte Liberation Sans embutida (SIL OFL 1.1, ver `src/Perseus.Infra/Dados/Fontes/OFL.txt`).
- *Relatórios › Balanço Patrimonial e DRE*: estrutura do plano da empresa até o nível escolhido; antes do encerramento, o resultado do período aparece destacado no PL para o balanço fechar; a DRE ignora os lançamentos de encerramento.
- *Lançamentos › Encerramento do exercício*: zera as contas de resultado contra a Apuração do Resultado e transfere o lucro/prejuízo ao PL, em 31/12, com IND_LCTO = E (ECD). "Desfazer" estorna os dois lançamentos para permitir ajustes.

### Uso: janelas e cadastros

- Cada função aberta pelo menu abre em uma **janela flutuante** própria (pode ser movida, redimensionada e fechada no ✕). A barra abaixo do menu lista as funções abertas: clique para trazer à frente, ✕ para fechar. Reabrir a mesma função traz a janela existente.
- Todos os cadastros (empresas, sócios, contadores, plano de contas, centros de custo, usuários e perfis) têm a mesma barra: **Incluir · Alterar · Excluir · Salvar · Cancelar**. Em consulta o formulário mostra o registro selecionado; Incluir/Alterar liberam a edição; **Excluir sempre pede confirmação**.
- Excluir uma empresa a retira da lista e bloqueia o acesso, mas mantém a escrituração no banco (guarda legal). Excluir um usuário que já entrou no sistema apenas o inativa (preserva o histórico).
- **Integrações** está dividido em *Importar* (plano referencial da RFB; XML de NF-e, NFS-e, OFX e Excel em breve) e *Exportar* (ECD, ECF e eSocial em breve).

### Sócios e contadores

- **Cadastros › Empresas, aba Sócios** (da empresa selecionada): nome, CPF/CNPJ, qualificação, participação no capital (soma dos ativos ≤ 100%), entrada/saída e representante legal. *Importar quadro societário da Receita* traz o QSA da base pública (o CPF vem mascarado — complete-o). Ao cadastrar uma empresa pela consulta de CNPJ, os sócios já são incluídos.
- **Cadastros › Contadores** (também como aba *Contadores* em Cadastros › Empresas): contadores (CPF, CRC/UF) e escritórios (CNPJ, CRC) são cadastros únicos reaproveitados entre empresas; cada empresa tem seus vínculos com início/fim e **um responsável técnico vigente** (garantido também no banco). Troca de contabilidade: encerre o vínculo, não exclua.
- Os termos do **Livro Diário** já vêm preenchidos com o representante legal e o responsável técnico — os mesmos signatários da ECD (registro J930).

### Plano referencial da RFB

1. **Configurações › Plano referencial da RFB** (administrador): importe a tabela oficial do plano referencial para o tipo de plano (ex.: *PJ em geral – Lucro Real*) e o ano. Obtenha o arquivo no portal do SPED (tabelas da ECD/ECF) ou no programa validador; ele precisa de uma primeira linha de cabeçalho com ao menos `CODIGO` e `DESCRICAO` (`TIPO`, `COD_SUP`, `NIVEL`, `COD_NAT`, `DT_INI`, `DT_FIM` são usados quando existem; separador `|`, `;`, tab ou vírgula; UTF-8 ou ISO-8859-1). Reimportar atualiza a tabela sem perder vínculos.
2. **Cadastros › Vínculo ao plano referencial**: escolha o plano do exercício e vincule cada conta analítica a uma referencial analítica de mesma natureza (ECD I051). *Sugerir vínculos* propõe pela semelhança das descrições (o contador confirma); *Copiar do ano anterior* reaproveita os vínculos. Contas com movimento ou saldo sem vínculo aparecem como pendências que impedem a ECD.

O Perseus não traz a tabela embutida: ela é oficial, muda a cada ano e deve vir da RFB. Os testes usam uma tabela **fictícia**.

### Backup e restauração

*Configurações › Backup e restauração* gera cópias com `pg_dump` (formato compactado) em `Documentos\Perseus Contabil\Backups`. A restauração exige digitar `RESTAURAR`, gera antes um backup de segurança do estado atual e encerra todas as sessões. Usa as ferramentas do PostgreSQL que acompanham o instalador (ou as de um PostgreSQL instalado na máquina, ou a pasta indicada em `PERSEUS_PG_BIN`).

### Consulta de CNPJ

Em **Cadastros › Empresas**, o botão *Buscar dados na Receita* preenche razão social, natureza jurídica, CNAE, endereço, município IBGE, início de atividade, situação cadastral e opção pelo Simples/MEI. A fonte é a **base pública do CNPJ divulgada pela Receita Federal** (dados abertos, atualização mensal), via [BrasilAPI](https://brasilapi.com.br) e, como reserva, [Minha Receita](https://minhareceita.org). O portal "Comprovante de Inscrição" da RFB tem captcha e não pode ser automatizado; a API oficial (Serpro "Consulta CNPJ") é paga e pode ser plugada como outra implementação de `IConsultaCnpj`.

**Inscrição estadual:** *Buscar IE (SINTEGRA)* (também executado logo após a consulta do CNPJ) procura as inscrições do CNPJ na UF da empresa — a UF informada ou, na falta dela, a deduzida do código IBGE do município. A fonte é a API pública do [CNPJ.ws](https://www.cnpj.ws), que reúne as inscrições dos cadastros estaduais (os portais SINTEGRA de cada estado exigem captcha); limite de 3 consultas por minuto. O sistema **sempre pergunta** antes de usar o número: em Incluir/Alterar ele vai para o formulário (gravado ao Salvar); em consulta, grava na empresa só se o usuário confirmar. *Abrir SINTEGRA* abre o portal para conferência manual.

### Gerar o instalador

O job `instalador-windows` do CI faz tudo em um Windows limpo:

1. `instalador/baixar-postgresql.ps1` baixa os binários oficiais do PostgreSQL 16 para Windows (EDB) e monta `pgsql` (só `bin`, `lib`, `share` + runtime do Visual C++);
2. roda o teste do banco embutido no Windows (`ServidorEmbutidoTests`: initdb, start, migrações, reinício, stop);
3. publica `PerseusContabil.exe` (arquivo único, sem exigir .NET instalado) com a pasta `pgsql` ao lado;
4. compila `instalador/PerseusContabil.iss` com o Inno Setup → `Setup-PerseusContabil-<versão>.exe`;
5. `instalador/teste-instalacao.ps1` instala em modo silencioso, abre o programa e confere que o banco foi criado, migrado e parado ao fechar.

Localmente (Windows, com Inno Setup 6):

```powershell
./instalador/baixar-postgresql.ps1 -Destino pgsql
dotnet publish src/Perseus.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
Move-Item pgsql publish/pgsql
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DVersao=0.4.0 instalador/PerseusContabil.iss   # gera saida\Setup-PerseusContabil-0.4.0.exe
```

## Testes

```bash
dotnet test                                    # unitários (sempre) + integração/UI (se houver banco)
export PERSEUS_TEST_PG="Host=localhost;Username=postgres;Password=postgres;Database=postgres"
dotnet test                                    # agora inclui integração e interface
export PERSEUS_SCREENSHOTS=/caminho/pasta      # opcional: salva PNG de cada aba no teste de interface
```

Os testes de integração criam e apagam um banco temporário por classe de teste.

## Documentação

| Documento | Conteúdo |
|---|---|
| [01 — Arquitetura](docs/01-arquitetura.md) | Stack, camadas, modelo de dados, fluxos, segurança |
| [02 — Partidas dobradas](docs/02-partidas-dobradas.md) | Regras de validação no banco e na aplicação |
| [03 — Desafios fiscais](docs/03-desafios-fiscais.md) | Reforma Tributária, SPED, regimes, folha, eSocial, integrações |
| [04 — Roadmap](docs/04-roadmap.md) | Fases do MVP à versão completa, critérios de saída |
