# Perseus

Sistema de Escrituração Fiscal Contábil para empresas brasileiras (Simples Nacional, Lucro Presumido e Lucro Real): contabilidade, documentos fiscais, conciliação bancária, demonstrações, SPED (ECD/ECF), folha de pagamento e eSocial.

Instalação local no Windows com PostgreSQL na própria máquina; arquitetura preparada para servidor/nuvem no futuro.

## Documentação

| Documento | Conteúdo |
|---|---|
| [01 — Arquitetura](docs/01-arquitetura.md) | Stack, camadas, modelo de dados, fluxos, segurança |
| [02 — Partidas dobradas](docs/02-partidas-dobradas.md) | Regras de validação no banco e na aplicação |
| [03 — Desafios fiscais](docs/03-desafios-fiscais.md) | Reforma Tributária, SPED, regimes, folha, eSocial, integrações |
| [04 — Roadmap](docs/04-roadmap.md) | Fases do MVP à versão completa, critérios de saída |

## Banco de dados

```bash
createdb perseus_dev
psql -v ON_ERROR_STOP=1 -d perseus_dev -f db/001_core_contabil.sql
# testes (em banco descartável):
psql -v ON_ERROR_STOP=1 -d perseus_test -f db/001_core_contabil.sql -f db/tests/001_partidas_dobradas_test.sql
```
