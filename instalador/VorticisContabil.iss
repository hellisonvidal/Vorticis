; Instalador do Vorticis Contábil (Inno Setup 6). Gerado pelo CI (job "instalador-windows").
; Entrada: ..\publish (VorticisContabil.exe + pasta pgsql com os binários do PostgreSQL).
; Compilar: iscc /DVersao=0.4.0 instalador\VorticisContabil.iss

#ifndef Versao
  #define Versao "0.0.0"
#endif

[Setup]
; Mesmo AppId da época em que o sistema se chamava Perseus: o instalador atualiza a instalação existente.
AppId={{6E0B7C3A-2F1D-4C55-9B7E-5A1F0D3C8E21}
AppName=Vorticis Contábil
AppVersion={#Versao}
AppVerName=Vorticis Contábil {#Versao}
AppPublisher=Vorticis Tecnologia Financeira
DefaultDirName={autopf}\Vorticis Contabil
DefaultGroupName=Vorticis Contábil
DisableProgramGroupPage=yes
; Instala para o usuário atual, sem pedir senha de administrador (pode-se escolher "todos os usuários" na tela inicial).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\saida
OutputBaseFilename=Setup-VorticisContabil-{#Versao}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=icone\VorticisContabil.ico
UninstallDisplayIcon={app}\VorticisContabil.exe
UninstallDisplayName=Vorticis Contábil
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.FinishedLabel=O Vorticis Contábil foi instalado.%n%nNa primeira abertura o programa cria sozinho o seu banco de dados (na pasta AppData\Local\Vorticis\banco do seu usuário); não é preciso instalar nem configurar o PostgreSQL.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Restos da instalação com o nome anterior (Perseus)
Type: files; Name: "{app}\PerseusContabil.exe"
Type: files; Name: "{autoprograms}\Perseus Contábil.lnk"
Type: files; Name: "{autodesktop}\Perseus Contábil.lnk"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Pasta padrão dos backups (dentro da instalação), gravável pelos usuários; não é apagada na desinstalação.
Name: "{app}\Backups"; Permissions: users-modify; Flags: uninsneveruninstall

[Icons]
Name: "{autoprograms}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"
Name: "{autodesktop}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VorticisContabil.exe"; Description: "{cm:LaunchProgram,Vorticis Contábil}"; Flags: nowait postinstall skipifsilent

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
