; Instalador da suíte Vorticis (Inno Setup 6): Contábil, Fiscal e Folha de Pagamento. Gerado pelo CI (job "instalador-windows").
; Entrada: ..\publish (VorticisContabil.exe, VorticisFiscal.exe, VorticisFolha.exe + pasta pgsql com os binários do PostgreSQL).
; Compilar: iscc /DVersao=0.4.0 instalador\Vorticis.iss

#ifndef Versao
  #define Versao "0.0.0"
#endif

[Setup]
; Mesmo AppId da época em que o sistema se chamava Perseus (e depois Vorticis Contábil): o instalador atualiza a instalação existente.
AppId={{6E0B7C3A-2F1D-4C55-9B7E-5A1F0D3C8E21}
AppName=Vorticis
AppVersion={#Versao}
AppVerName=Vorticis {#Versao}
AppPublisher=Vorticis Tecnologia Financeira
DefaultDirName={autopf}\Vorticis
DefaultGroupName=Vorticis
DisableProgramGroupPage=yes
; Instala para o usuário atual, sem pedir senha de administrador (pode-se escolher "todos os usuários" na tela inicial).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\saida
OutputBaseFilename=Setup-Vorticis-{#Versao}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=icone\VorticisContabil.ico
; O ícone é copiado para a pasta do programa (não depende de qual módulo foi instalado)
UninstallDisplayIcon={app}\Vorticis.ico
UninstallDisplayName=Vorticis
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.FinishedLabel=O Vorticis foi instalado.%n%nNa primeira abertura o programa cria sozinho o seu banco de dados (na pasta AppData\Local\Vorticis\banco do seu usuário), compartilhado por todos os módulos; não é preciso instalar nem configurar o PostgreSQL.

[Types]
Name: "completa"; Description: "Instalação completa (Contábil, Fiscal e Folha de Pagamento)"
Name: "personalizada"; Description: "Instalação personalizada (escolher os módulos)"; Flags: iscustom

[Components]
; Todos marcados por padrão; é preciso manter pelo menos um (ver NextButtonClick). Banco de dados, login, empresas e
; configurações são comuns: o PostgreSQL embutido (pasta pgsql) é instalado com qualquer módulo.
Name: "contabil"; Description: "Vorticis Contábil"; Types: completa personalizada
Name: "fiscal"; Description: "Vorticis Fiscal"; Types: completa personalizada
Name: "folha"; Description: "Vorticis Folha de Pagamento"; Types: completa personalizada

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Restos da instalação com o nome anterior (Perseus)
Type: files; Name: "{app}\PerseusContabil.exe"
Type: files; Name: "{autoprograms}\Perseus Contábil.lnk"
Type: files; Name: "{autodesktop}\Perseus Contábil.lnk"

[Files]
; Comuns a todos os módulos: banco de dados local e ícone
Source: "..\publish\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: contabil fiscal folha
Source: "icone\VorticisContabil.ico"; DestDir: "{app}"; DestName: "Vorticis.ico"; Flags: ignoreversion; Components: contabil fiscal folha
; Executáveis (arquivo único, sem exigir .NET instalado)
Source: "..\publish\VorticisContabil.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: contabil
Source: "..\publish\VorticisFiscal.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: fiscal
Source: "..\publish\VorticisFolha.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: folha
; Ícone de cada módulo (atalhos): espiral verde (Contábil), laranja (Fiscal) e roxa (Folha)
Source: "icone\VorticisContabil.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: contabil
Source: "icone\VorticisFiscal.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: fiscal
Source: "icone\VorticisFolha.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: folha

[Dirs]
; Pasta padrão dos backups (dentro da instalação), gravável pelos usuários; não é apagada na desinstalação.
Name: "{app}\Backups"; Permissions: users-modify; Flags: uninsneveruninstall

[Icons]
Name: "{autoprograms}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"; IconFilename: "{app}\VorticisContabil.ico"; Components: contabil
Name: "{autoprograms}\Vorticis Fiscal"; Filename: "{app}\VorticisFiscal.exe"; IconFilename: "{app}\VorticisFiscal.ico"; Components: fiscal
Name: "{autoprograms}\Vorticis Folha de Pagamento"; Filename: "{app}\VorticisFolha.exe"; IconFilename: "{app}\VorticisFolha.ico"; Components: folha
Name: "{autodesktop}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"; IconFilename: "{app}\VorticisContabil.ico"; Tasks: desktopicon; Components: contabil
Name: "{autodesktop}\Vorticis Fiscal"; Filename: "{app}\VorticisFiscal.exe"; IconFilename: "{app}\VorticisFiscal.ico"; Tasks: desktopicon; Components: fiscal
Name: "{autodesktop}\Vorticis Folha de Pagamento"; Filename: "{app}\VorticisFolha.exe"; IconFilename: "{app}\VorticisFolha.ico"; Tasks: desktopicon; Components: folha

[Run]
; Ao final, só o primeiro módulo instalado é aberto por padrão; os demais podem ser marcados.
Filename: "{app}\VorticisContabil.exe"; Description: "{cm:LaunchProgram,Vorticis Contábil}"; Flags: nowait postinstall skipifsilent; Components: contabil
Filename: "{app}\VorticisFiscal.exe"; Description: "{cm:LaunchProgram,Vorticis Fiscal}"; Flags: nowait postinstall skipifsilent unchecked; Components: fiscal
Filename: "{app}\VorticisFolha.exe"; Description: "{cm:LaunchProgram,Vorticis Folha de Pagamento}"; Flags: nowait postinstall skipifsilent unchecked; Components: folha

[Code]
// O banco local roda a partir da pasta pgsql do programa; é preciso pará-lo antes de substituir ou remover
// esses arquivos. Os dados (%LOCALAPPDATA%\Vorticis) NÃO são apagados na desinstalação.
procedure PararBancoLocal();
var
  Codigo, I: Integer;
  PgCtl, Dados: String;
begin
  PgCtl := ExpandConstant('{app}\pgsql\bin\pg_ctl.exe');
  // Perseus: pasta de dados das versões anteriores à troca de nome (o programa a renomeia ao abrir).
  for I := 0 to 1 do
  begin
    if I = 0 then Dados := ExpandConstant('{localappdata}\Vorticis\banco')
    else Dados := ExpandConstant('{localappdata}\Perseus\banco');
    if FileExists(PgCtl) and FileExists(Dados + '\postmaster.pid') then
      Exec(PgCtl, 'stop -D "' + Dados + '" -m fast -w -t 60', '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  end;
end;

// Pelo menos um módulo precisa ficar marcado.
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectComponents) and (WizardSelectedComponents(False) = '') then
  begin
    MsgBox('Escolha pelo menos um módulo do Vorticis para instalar.', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  PararBancoLocal();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  PararBancoLocal();
  Result := True;
end;
