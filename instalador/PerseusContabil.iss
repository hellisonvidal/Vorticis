; Instalador do Perseus Contábil (Inno Setup 6). Gerado pelo CI (job "instalador-windows").
; Entrada: ..\publish (PerseusContabil.exe + pasta pgsql com os binários do PostgreSQL).
; Compilar: iscc /DVersao=0.4.0 instalador\PerseusContabil.iss

#ifndef Versao
  #define Versao "0.0.0"
#endif

[Setup]
AppId={{6E0B7C3A-2F1D-4C55-9B7E-5A1F0D3C8E21}
AppName=Perseus Contábil
AppVersion={#Versao}
AppVerName=Perseus Contábil {#Versao}
AppPublisher=Perseus
DefaultDirName={autopf}\Perseus Contabil
DefaultGroupName=Perseus Contábil
DisableProgramGroupPage=yes
; Instala para o usuário atual, sem pedir senha de administrador (pode-se escolher "todos os usuários" na tela inicial).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\saida
OutputBaseFilename=Setup-PerseusContabil-{#Versao}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=icone\PerseusContabil.ico
UninstallDisplayIcon={app}\PerseusContabil.exe
UninstallDisplayName=Perseus Contábil
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.FinishedLabel=O Perseus Contábil foi instalado.%n%nNa primeira abertura o programa cria sozinho o seu banco de dados (na pasta AppData\Local\Perseus\banco do seu usuário); não é preciso instalar nem configurar o PostgreSQL.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Pasta padrão dos backups (dentro da instalação), gravável pelos usuários; não é apagada na desinstalação.
Name: "{app}\Backups"; Permissions: users-modify; Flags: uninsneveruninstall

[Icons]
Name: "{autoprograms}\Perseus Contábil"; Filename: "{app}\PerseusContabil.exe"
Name: "{autodesktop}\Perseus Contábil"; Filename: "{app}\PerseusContabil.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PerseusContabil.exe"; Description: "{cm:LaunchProgram,Perseus Contábil}"; Flags: nowait postinstall skipifsilent

[Code]
// O banco local roda a partir da pasta pgsql do programa; é preciso pará-lo antes de substituir ou remover
// esses arquivos. Os dados (%LOCALAPPDATA%\Perseus) NÃO são apagados na desinstalação.
procedure PararBancoLocal();
var
  Codigo: Integer;
  PgCtl, Dados: String;
begin
  PgCtl := ExpandConstant('{app}\pgsql\bin\pg_ctl.exe');
  Dados := ExpandConstant('{localappdata}\Perseus\banco');
  if FileExists(PgCtl) and FileExists(Dados + '\postmaster.pid') then
    Exec(PgCtl, 'stop -D "' + Dados + '" -m fast -w -t 60', '', SW_HIDE, ewWaitUntilTerminated, Codigo);
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
