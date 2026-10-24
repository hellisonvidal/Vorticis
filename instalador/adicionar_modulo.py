"""Inclui um módulo da suíte no instalador (Vorticis.iss) e no CI (publicação e portátil).
Uso: python3 -I instalador/adicionar_modulo.py <componente> <Executavel> "<Nome do módulo>" <Projeto.Desktop>"""
import re, sys
from pathlib import Path

comp, exe, nome, projeto = sys.argv[1:5]
raiz = Path(__file__).resolve().parent.parent
iss = raiz / "instalador" / "Vorticis.iss"
s = iss.read_text(encoding="utf-8-sig")
if f'Name: "{comp}"' not in s:
    s = re.sub(r'(Name: "folha"; Description: [^\n]*\n(?:Name: "\w+"; Description: [^\n]*Types: completa personalizada\n)*)',
               lambda m: m.group(1) + f'Name: "{comp}"; Description: "{nome}"; Types: completa personalizada\n', s, count=1)
    s = re.sub(r'(Components: contabil fiscal folha[\w ]*)', lambda m: m.group(1) + f" {comp}", s)
    def apos(padrao, linha):
        global s
        ultimos = list(re.finditer(padrao, s))
        p = ultimos[-1].end()
        s = s[:p] + "\n" + linha + s[p:]
    apos(r'Source: "\.\.\\publish\\Vorticis\w+\.exe"; DestDir: "\{app\}"; Flags: ignoreversion; Components: \w+',
         f'Source: "..\\publish\\{exe}.exe"; DestDir: "{{app}}"; Flags: ignoreversion; Components: {comp}')
    apos(r'Source: "icone\\Vorticis\w+\.ico"; DestDir: "\{app\}"; Flags: ignoreversion; Components: \w+',
         f'Source: "icone\\{exe}.ico"; DestDir: "{{app}}"; Flags: ignoreversion; Components: {comp}')
    apos(r'Name: "\{autoprograms\}[^\n]*Components: \w+',
         f'Name: "{{autoprograms}}\\{nome}"; Filename: "{{app}}\\{exe}.exe"; IconFilename: "{{app}}\\{exe}.ico"; Components: {comp}')
    apos(r'Name: "\{autodesktop\}[^\n]*Components: \w+',
         f'Name: "{{autodesktop}}\\{nome}"; Filename: "{{app}}\\{exe}.exe"; IconFilename: "{{app}}\\{exe}.ico"; Tasks: desktopicon; Components: {comp}')
    apos(r'Filename: "\{app\}\\Vorticis\w+\.exe"; Description: "\{cm:LaunchProgram[^\n]*Components: \w+',
         f'Filename: "{{app}}\\{exe}.exe"; Description: "{{cm:LaunchProgram,{nome}}}"; Flags: nowait postinstall skipifsilent unchecked; Components: {comp}')
    s = s.replace("  else Result := 'VorticisFolha.exe';",
                  f"  else if WizardIsComponentSelected('{comp}') then Result := '{exe}.exe'\n  else Result := 'VorticisFolha.exe';"
                  if "else Result := 'VorticisFolha.exe'" in s and "WizardIsComponentSelected('folha')" not in s else "  else Result := 'VorticisFolha.exe';")
    if f"WizardIsComponentSelected('{comp}')" not in s:
        s = s.replace("  else Result := 'VorticisFolha.exe';",
                      f"  else if WizardIsComponentSelected('folha') then Result := 'VorticisFolha.exe'\n  else if WizardIsComponentSelected('{comp}') then Result := '{exe}.exe'\n  else Result := 'VorticisFolha.exe';")
    iss.write_text(s, encoding="utf-8-sig")

ci = raiz / ".github" / "workflows" / "ci.yml"
c = ci.read_text(encoding="utf-8")
if f'"{projeto}"' not in c:
    c = re.sub(r'(foreach \(\$projeto in "Vorticis\.Desktop"[^)]*)\)', lambda m: m.group(1) + f', "{projeto}")', c, count=1)
    curto = exe.replace("Vorticis", "")
    c = re.sub(r'(foreach \(\$modulo in "Contabil"[^)]*)\)', lambda m: m.group(1) + f', "{curto}")', c, count=1)
    c = c.rstrip() + f"""
      - uses: actions/upload-artifact@v4
        with:
          name: {exe}-portatil
          path: portatil/{exe}/
"""
    ci.write_text(c, encoding="utf-8")
print("ok", comp)
