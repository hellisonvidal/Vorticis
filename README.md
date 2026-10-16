# Perseus Contábil

Sistema de Escrituração Fiscal Contábil para empresas brasileiras (Simples Nacional, Lucro Presumido e Lucro Real): contabilidade, documentos fiscais, conciliação bancária, demonstrações, SPED (ECD/ECF), folha de pagamento e eSocial.

Aplicativo desktop para Windows com PostgreSQL na própria máquina; arquitetura preparada para servidor/nuvem no futuro.

## Estado atual

| Fase | Situação |
|---|---|
| 0. Fundação | Solução .NET 10, migrações versionadas, multiempresa, auditoria com hash encadeado, CI, login, **perfis de acesso configuráveis pelo administrador**, menu (Cadastros / Lançamentos / Integrações / Relatórios / Ajuda / Configurações), cadastro de empresa com consulta à base pública do CNPJ, **backup e restauração**. **Pendente:** instalador |
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

## Executar

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e PostgreSQL 16+.

```bash
# conexão (opcional; padrão: Host=localhost;Database=perseus;Username=perseus;Password=perseus)
set PERSEUS_DB=Host=localhost;Database=perseus;Username=postgres;Password=suasenha     # Windows (cmd)
export PERSEUS_DB="Host=localhost;Database=perseus;Username=postgres;Password=suasenha" # Linux/macOS

dotnet run --project src/Perseus.Desktop
```

Na primeira execução o banco é criado (se o usuário do PostgreSQL tiver permissão), as migrações são aplicadas e a tela de **primeiro acesso** pede a criação do usuário administrador. Se a conexão falhar, o próprio Perseus abre a tela de conexão; a configuração fica em `%APPDATA%\Perseus\perseus.json`, com a senha cifrada por DPAPI no Windows.

### Usuários e perfis de acesso

O administrador cria **perfis** em *Configurações › Perfis de acesso*, marcando função a função o que cada um libera, e atribui a cada usuário um perfil **por empresa** em *Configurações › Usuários*. Funções configuráveis:

| Grupo | Funções |
|---|---|
| Lançamentos | Incluir lançamentos · Estornar · Saldos iniciais · Fechar períodos · Reabrir períodos |
| Cadastros | Manter plano de contas · Manter centros de custo |
| Relatórios | Balancete · Livro Razão · Livro Diário · Exportar (CSV) |

Perfis padrão (editáveis, não excluíveis): **Contador** (todas), **Assistente** (lança e estorna, não fecha nem reabre períodos), **Consulta** (relatórios). Cadastro de empresas, usuários, perfis, conexão e backup são exclusivos de usuários marcados como **administrador**. As permissões são verificadas **nos serviços** (não só no menu): mesmo que a tela seja contornada, a operação é recusada.

Senhas com PBKDF2-SHA256 (600 mil iterações); bloqueio de 15 minutos após 5 tentativas inválidas; usuário criado pelo administrador troca a senha no primeiro acesso; todas as tentativas de login ficam em `core.registro_acesso`.

### Livros, demonstrações e encerramento

- *Relatórios › Livros e relatórios em PDF*: Livro Diário com termos de abertura e encerramento (ITG 2000: folhas numeradas, identificação da entidade, assinaturas do representante legal e do contador — o número de folhas do termo é calculado após a montagem), Livro Razão de todas as contas com movimento, Balancete e Balanço/DRE. Gerados com PDFsharp/MigraDoc (MIT) e fonte Liberation Sans embutida (SIL OFL 1.1, ver `src/Perseus.Infra/Dados/Fontes/OFL.txt`).
- *Relatórios › Balanço Patrimonial e DRE*: estrutura do plano da empresa até o nível escolhido; antes do encerramento, o resultado do período aparece destacado no PL para o balanço fechar; a DRE ignora os lançamentos de encerramento.
- *Lançamentos › Encerramento do exercício*: zera as contas de resultado contra a Apuração do Resultado e transfere o lucro/prejuízo ao PL, em 31/12, com IND_LCTO = E (ECD). "Desfazer" estorna os dois lançamentos para permitir ajustes.

### Sócios, contadores e escritório contábil

- **Cadastros › Sócios** (da empresa selecionada): nome, CPF/CNPJ, qualificação, participação no capital (soma dos ativos ≤ 100%), entrada/saída e representante legal. *Importar quadro societário da Receita* traz o QSA da base pública (o CPF vem mascarado — complete-o). Ao cadastrar uma empresa pela consulta de CNPJ, os sócios já são incluídos.
- **Cadastros › Contadores e escritório contábil**: contadores (CPF, CRC/UF) e escritórios (CNPJ, CRC) são cadastros únicos reaproveitados entre empresas; cada empresa tem seus vínculos com início/fim e **um responsável técnico vigente** (garantido também no banco). Troca de contabilidade: encerre o vínculo, não exclua.
- Os termos do **Livro Diário** já vêm preenchidos com o representante legal e o responsável técnico — os mesmos signatários da ECD (registro J930).

### Plano referencial da RFB

1. **Configurações › Plano referencial da RFB** (administrador): importe a tabela oficial do plano referencial para o tipo de plano (ex.: *PJ em geral – Lucro Real*) e o ano. Obtenha o arquivo no portal do SPED (tabelas da ECD/ECF) ou no programa validador; ele precisa de uma primeira linha de cabeçalho com ao menos `CODIGO` e `DESCRICAO` (`TIPO`, `COD_SUP`, `NIVEL`, `COD_NAT`, `DT_INI`, `DT_FIM` são usados quando existem; separador `|`, `;`, tab ou vírgula; UTF-8 ou ISO-8859-1). Reimportar atualiza a tabela sem perder vínculos.
2. **Cadastros › Vínculo ao plano referencial**: escolha o plano do exercício e vincule cada conta analítica a uma referencial analítica de mesma natureza (ECD I051). *Sugerir vínculos* propõe pela semelhança das descrições (o contador confirma); *Copiar do ano anterior* reaproveita os vínculos. Contas com movimento ou saldo sem vínculo aparecem como pendências que impedem a ECD.

O Perseus não traz a tabela embutida: ela é oficial, muda a cada ano e deve vir da RFB. Os testes usam uma tabela **fictícia**.

### Backup e restauração

*Configurações › Backup e restauração* gera cópias com `pg_dump` (formato compactado) em `Documentos\Perseus Contabil\Backups`. A restauração exige digitar `RESTAURAR`, gera antes um backup de segurança do estado atual e encerra todas as sessões. Requer as ferramentas de linha de comando do PostgreSQL (instaladas junto com ele; ou informe a pasta em `PERSEUS_PG_BIN`).

### Consulta de CNPJ

Em **Cadastros › Empresas**, o botão *Buscar dados na Receita* preenche razão social, natureza jurídica, CNAE, endereço, município IBGE, início de atividade, situação cadastral e opção pelo Simples/MEI. A fonte é a **base pública do CNPJ divulgada pela Receita Federal** (dados abertos, atualização mensal), via [BrasilAPI](https://brasilapi.com.br) e, como reserva, [Minha Receita](https://minhareceita.org). O portal "Comprovante de Inscrição" da RFB tem captcha e não pode ser automatizado; a API oficial (Serpro "Consulta CNPJ") é paga e pode ser plugada como outra implementação de `IConsultaCnpj`.

### Executável para Windows

Arquivo único, sem exigir .NET instalado:

```bash
dotnet publish src/Perseus.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

O executável gerado é `PerseusContabil.exe`; o CI o publica como artefato `perseus-contabil-win-x64` a cada push.

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
