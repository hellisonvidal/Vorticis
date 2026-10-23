# Vorticis — regras do projeto

C# .NET 10, Avalonia (MVVM), PostgreSQL. Módulos Contábil, Fiscal e Folha sobre o casco `src/Vorticis.Desktop.Comum` (`IModuloDesktop`).

## Regra principal

**Toda função nova ou alterada deve ter o manual atualizado em `docs/manual/` (e o mapa de F1, `docs/manual/mapa.txt`, se houver) no mesmo commit.**
O manual é embutido no casco; `ManualTests` falha se existir tela registrada sem tópico no mapa. Escreva para o contador (usuário final): passo a passo, linguagem simples, sem jargão técnico.

## Regras práticas

- **Migrações imutáveis:** nunca edite um `db/NNN_*.sql` já commitado; crie outro arquivo com o próximo número livre da faixa do módulo (core/contábil 001–029, fiscal 030–039 e 046–048, folha 040–045) e confira a última antes de numerar.
- **Testes obrigatórios:** `dotnet build Vorticis.slnx -c Release` e `dotnet test` em todos os projetos de `tests/` antes de commitar; toda regra nova ganha teste. Para rodar os de integração: `VORTICIS_TEST_PG="Host=localhost;Username=postgres;Password=postgres;Database=postgres"`.
- **Interface em português do Brasil:** textos, mensagens, menus e documentação; nomes de tela e botões iguais aos do manual.
- **Permissões nos serviços**, não só no menu; regras legais (alíquotas, tabelas) em `core.parametro_legal`, nunca no código.
- **Casco comum:** telas e cadastros usados por mais de um módulo ficam em `Vorticis.Desktop.Comum`; cada tela nova se registra em `RegistrarTelas` com chave única.
- **Economia de uso:** leia o mínimo necessário (README, `docs/`, definições de menu e views), use busca (`grep`) em vez de abrir arquivos grandes e evite reescrever o que já funciona.
