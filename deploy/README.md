# Vorticis on-line — guia de publicação

Este pacote coloca o Vorticis no **seu domínio**:

| Endereço | O que é |
|---|---|
| `https://www.seudominio.com.br` | Site do Vorticis: página de download, instaladores e o arquivo de atualizações (`atualizacao.json`). HTTPS gratuito e automático. |
| `db.seudominio.com.br:5432` | Banco de dados (PostgreSQL 16) do escritório, para todos os computadores trabalharem nos mesmos dados pela internet. Só aceita **conexão segura (SSL)** e senha. |
| `./backups` no servidor | Backup diário do banco às 2h, guardando os últimos 30 dias. |

O Vorticis continua sendo um **programa instalado no Windows** (rápido, funciona com certificado digital A1/A3, impressão e arquivos locais). O que fica on-line é o banco de dados e o site de download/atualização.

## O que você precisa

1. **Um servidor** com Linux (Ubuntu 22.04 ou 24.04), 2 GB de memória ou mais e 40 GB de disco — por exemplo, uma VPS em provedor nacional (Locaweb, Hostinger, KingHost) ou internacional (DigitalOcean, AWS Lightsail, Hetzner). Com **Docker** instalado (`curl -fsSL https://get.docker.com | sh`).
2. **O domínio** e acesso ao painel de DNS dele.

## Passo a passo

1. **DNS:** no painel do domínio, crie dois registros do tipo **A** apontando para o IP do servidor:
   - `www` (ou o nome que você quiser para o site);
   - `db` (o banco de dados).
2. **Firewall do servidor/provedor:** libere as portas **80** e **443** (site) e **5432** (banco). Se possível, libere a 5432 só para os endereços de internet do escritório.
3. **Copie a pasta `deploy`** deste repositório para o servidor (por exemplo, `scp -r deploy usuario@servidor:~/vorticis`).
4. No servidor, dentro da pasta:
   ```bash
   cp .env.exemplo .env
   nano .env            # preencha DOMINIO, EMAIL_CERTIFICADO, DOMINIO_BANCO e uma POSTGRES_PASSWORD forte
   docker compose up -d
   docker compose ps    # site, banco e backup devem ficar "running"; o banco, "healthy"
   ```
5. **Publique a versão atual** (baixa o instalador e o portátil da Release do GitHub e gera o `atualizacao.json`):
   ```bash
   ./publicar.sh 1.0.1
   ```
   Abra `https://www.seudominio.com.br`: a página mostra a versão e o botão de download.
6. **Ligue o aviso de atualizações no programa:** no arquivo `Directory.Build.props` do repositório, preencha
   `<VorticisSite>https://www.seudominio.com.br</VorticisSite>` e gere a próxima versão (o CI cria a Release). A partir dela, todos os
   programas instalados consultam o site ao abrir e mostram *Ajuda › Verificar atualizações*. (Para uma instalação já existente,
   basta criar o arquivo `site.txt` com o endereço na pasta do programa.)
7. **Em cada computador do escritório**, na tela *Conexão com o banco de dados* (Escritório: *Configurações › Banco de dados*):
   - Servidor: `db.seudominio.com.br` — Porta: `5432` — Banco: `vorticis` — Usuário: `vorticis` — Senha: a do `.env`;
   - **Conexão segura (SSL): Exigir conexão segura (nuvem)**;
   - **Testar conexão** e **Salvar e reconectar**. Na primeira conexão o Vorticis cria as tabelas sozinho.

## A cada versão nova

1. Mude a versão no `Directory.Build.props` e envie: o CI testa, gera o instalador e cria a Release `v<versão>` no GitHub.
2. No servidor: `./publicar.sh <versão>`. Os programas instalados avisam da versão nova na próxima abertura.

## Segurança e cuidados

- O `.env` tem a senha do banco: não o envie para o Git (já está no `.gitignore`).
- O banco recusa qualquer conexão de rede sem SSL e exige senha (scram-sha-256). O certificado do banco é gerado no servidor na primeira subida (vale 10 anos).
- Backups do servidor em `./backups` (formato do Vorticis: dá para restaurar pela tela *Backup e restauração*). Copie-os para fora do servidor de vez em quando (outro provedor, nuvem ou disco).
- Além dos backups do servidor, o Vorticis faz o **backup automático diário** em cada computador que entra primeiro no dia (*Configurações › Backup e restauração*).
- Para ver os logs: `docker compose logs -f banco` (ou `site`, `backup`). Para atualizar as imagens: `docker compose pull && docker compose up -d`.

## E se eu quiser usar pelo navegador?

O Vorticis é um programa de Windows que fala direto com o banco. Para abrir **pelo navegador**, as opções são:
1. **Acesso remoto** (recomendado para começar): o programa roda num servidor Windows (ou num computador do escritório) e as pessoas
   entram por Área de Trabalho Remota / RemoteApp, inclusive pelo navegador (Remote Desktop Web Client). Não exige mudar o programa.
2. **Versão web própria**: exige um servidor de aplicação (API) entre o navegador e o banco e telas novas para web — é um projeto à parte.
