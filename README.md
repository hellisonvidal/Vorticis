# Perseus

Sistema de Escrituração Fiscal Contábil para empresas brasileiras (Simples Nacional, Lucro Presumido e Lucro Real): contabilidade, documentos fiscais, conciliação bancária, demonstrações, SPED (ECD/ECF), folha de pagamento e eSocial.

Aplicativo desktop para Windows com PostgreSQL na própria máquina; arquitetura preparada para servidor/nuvem no futuro.

## Estado atual

| Fase | Situação |
|---|---|
| 0. Fundação | Solução .NET 10, migrações versionadas, multiempresa, auditoria com hash encadeado, CI, **login com perfis por empresa**, menu (Cadastros / Lançamentos / Integrações / Relatórios / Ajuda / Configurações), cadastro de empresa com **consulta à base pública do CNPJ**. **Pendente:** backup/restauração, instalador |
| 1. MVP contábil | Plano de contas modelo, lançamento com validação de partidas dobradas, estorno, fechamento/reabertura de período, Diário, Razão, Balancete (com exportação CSV). **Pendente:** CRUD de plano de contas e centros de custo, importação de saldos iniciais, Diário/Razão em PDF |

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

### Usuários e perfis

| Perfil | Pode |
|---|---|
| Administrador (global) | Tudo: empresas, usuários, conexão, e todas as operações em todas as empresas |
| Contador (por empresa) | Lançar, estornar, fechar/reabrir períodos, relatórios |
| Assistente (por empresa) | Lançar e estornar, relatórios |
| Consulta (por empresa) | Somente relatórios |

Senhas com PBKDF2-SHA256 (600 mil iterações); bloqueio de 15 minutos após 5 tentativas inválidas; usuário criado pelo administrador troca a senha no primeiro acesso; todas as tentativas de login ficam em `core.registro_acesso`.

### Consulta de CNPJ

Em **Cadastros › Empresas**, o botão *Buscar dados na Receita* preenche razão social, natureza jurídica, CNAE, endereço, município IBGE, início de atividade, situação cadastral e opção pelo Simples/MEI. A fonte é a **base pública do CNPJ divulgada pela Receita Federal** (dados abertos, atualização mensal), via [BrasilAPI](https://brasilapi.com.br) e, como reserva, [Minha Receita](https://minhareceita.org). O portal "Comprovante de Inscrição" da RFB tem captcha e não pode ser automatizado; a API oficial (Serpro "Consulta CNPJ") é paga e pode ser plugada como outra implementação de `IConsultaCnpj`.

### Executável para Windows

Arquivo único, sem exigir .NET instalado:

```bash
dotnet publish src/Perseus.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

O CI publica esse executável como artefato `perseus-win-x64` a cada push.

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
