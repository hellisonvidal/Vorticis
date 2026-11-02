#!/usr/bin/env bash
# Publica uma versão do Vorticis no site: baixa o instalador e o portátil da Release do GitHub para site/downloads
# e gera o site/atualizacao.json (que o programa e a página de download leem).
# Uso (no servidor, dentro da pasta deploy):  ./publicar.sh 1.0.2
set -euo pipefail
cd "$(dirname "$0")"
versao="${1:?informe a versão, por exemplo: ./publicar.sh 1.0.2}"
repo="${REPO_GITHUB:-hellisonvidal/Vorticis}"
set -a; [ -f .env ] && . ./.env; set +a
: "${DOMINIO:?defina DOMINIO no .env}"

base="https://github.com/${repo}/releases/download/v${versao}"
mkdir -p site/downloads
for arquivo in "Setup-Vorticis-${versao}.exe" "VorticisSuiteEmpresarial-portatil-${versao}.zip"; do
  echo "baixando ${arquivo}..."
  curl -fL --retry 3 -o "site/downloads/${arquivo}.tmp" "${base}/${arquivo}"
  mv "site/downloads/${arquivo}.tmp" "site/downloads/${arquivo}"
done

# notas: a primeira seção de docs/notas-da-versao.md publicada junto com a Release
notas="$(curl -fsL "https://raw.githubusercontent.com/${repo}/v${versao}/docs/notas-da-versao.md" | awk 'NR>1 && /^# /{exit} NR>1{print}' || true)"
python3 - "$versao" "$DOMINIO" "$notas" > site/atualizacao.json.tmp <<'PY'
import json, sys, datetime
versao, dominio, notas = sys.argv[1], sys.argv[2], sys.argv[3]
site = "https://" + dominio
print(json.dumps({
    "versao": versao,
    "publicada": datetime.date.today().strftime("%d/%m/%Y"),
    "instalador": f"{site}/downloads/Setup-Vorticis-{versao}.exe",
    "portatil": f"{site}/downloads/VorticisSuiteEmpresarial-portatil-{versao}.zip",
    "notas": notas.strip(),
}, ensure_ascii=False, indent=2))
PY
mv site/atualizacao.json.tmp site/atualizacao.json
# guarda só as 3 versões mais recentes de cada arquivo
ls -1t site/downloads/Setup-Vorticis-*.exe 2>/dev/null | tail -n +4 | xargs -r rm -f
ls -1t site/downloads/VorticisSuiteEmpresarial-portatil-*.zip 2>/dev/null | tail -n +4 | xargs -r rm -f
echo "Versão ${versao} publicada em https://${DOMINIO} (os programas instalados vão avisar da versão nova)."
