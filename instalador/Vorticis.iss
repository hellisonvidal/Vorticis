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
SetupIconFile=icone\VorticisInstalador.ico
; O ícone é copiado para a pasta do programa (não depende de qual módulo foi instalado)
UninstallDisplayIcon={app}\Vorticis.ico
UninstallDisplayName=Vorticis
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.FinishedLabel=O Vorticis foi instalado e o banco de dados foi configurado automaticamente (na pasta AppData\Local\Vorticis\banco do seu usuário), compartilhado por todos os módulos; não é preciso instalar nem configurar o PostgreSQL.%n%nSe o banco não estiver pronto, ao abrir o programa a tela de Conexão traz o botão "Configurar automaticamente".

[Types]
Name: "completa"; Description: "Instalação completa (todos os módulos da suíte)"
Name: "personalizada"; Description: "Instalação personalizada (escolher os módulos)"; Flags: iscustom

[Components]
; Todos marcados por padrão; é preciso manter pelo menos um (ver NextButtonClick). Banco de dados, login, empresas e
; configurações são comuns: o PostgreSQL embutido (pasta pgsql) é instalado com qualquer módulo.
Name: "contabil"; Description: "Vorticis Contábil"; Types: completa personalizada
Name: "fiscal"; Description: "Vorticis Fiscal"; Types: completa personalizada
Name: "folha"; Description: "Vorticis Folha"; Types: completa personalizada
Name: "escritorio"; Description: "Vorticis Escritório"; Types: completa personalizada
Name: "erp"; Description: "Vorticis Gerencial"; Types: completa personalizada
Name: "condominio"; Description: "Vorticis Condomínio"; Types: completa personalizada

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Restos da instalação com o nome anterior (Perseus)
Type: files; Name: "{app}\PerseusContabil.exe"
Type: files; Name: "{autoprograms}\Perseus Contábil.lnk"
Type: files; Name: "{autodesktop}\Perseus Contábil.lnk"
; Atalhos da Folha com o nome antigo (agora "Vorticis Folha")
Type: files; Name: "{autoprograms}\Vorticis Folha de Pagamento.lnk"
Type: files; Name: "{autodesktop}\Vorticis Folha de Pagamento.lnk"
; Módulo ERP renomeado para Vorticis Gerencial
Type: files; Name: "{app}\VorticisErp.exe"
Type: files; Name: "{app}\VorticisErp.ico"
Type: files; Name: "{autoprograms}\Vorticis ERP.lnk"
Type: files; Name: "{autodesktop}\Vorticis ERP.lnk"

[Files]
; Comuns a todos os módulos: banco de dados local e ícone
Source: "..\publish\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: contabil fiscal folha escritorio erp condominio
Source: "icone\VorticisContabil.ico"; DestDir: "{app}"; DestName: "Vorticis.ico"; Flags: ignoreversion; Components: contabil fiscal folha escritorio erp condominio
; Executáveis (arquivo único, sem exigir .NET instalado)
Source: "..\publish\VorticisContabil.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: contabil
Source: "..\publish\VorticisFiscal.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: fiscal
Source: "..\publish\VorticisFolha.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: folha
Source: "..\publish\VorticisEscritorio.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: escritorio
Source: "..\publish\VorticisGerencial.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: erp
Source: "..\publish\VorticisCondominio.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: condominio
; Vorticis Suite Empresarial: os módulos instalados numa janela só (mostra os módulos cujo executável está na pasta)
Source: "..\publish\VorticisSuite.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: contabil fiscal folha escritorio erp condominio
; Ícone de cada módulo (atalhos): espiral verde (Contábil), laranja (Fiscal) e roxa (Folha)
Source: "icone\VorticisContabil.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: contabil
Source: "icone\VorticisFiscal.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: fiscal
Source: "icone\VorticisFolha.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: folha
Source: "icone\VorticisEscritorio.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: escritorio
Source: "icone\VorticisGerencial.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: erp
Source: "icone\VorticisCondominio.ico"; DestDir: "{app}"; Flags: ignoreversion; Components: condominio
Source: "icone\VorticisInstalador.ico"; DestDir: "{app}"; DestName: "VorticisSuite.ico"; Flags: ignoreversion; Components: contabil fiscal folha escritorio erp condominio

#ifexist "..\publish\vc_redist.x64.exe"
; Microsoft Visual C++ Redistributable 2015-2022 (x64), instalado em silêncio só se ainda não existir (ver InstalarVcRuntime)
Source: "..\publish\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not VcRuntimeInstalado
#endif

[Dirs]
; Pasta padrão dos backups (dentro da instalação), gravável pelos usuários; não é apagada na desinstalação.
Name: "{app}\Backups"; Permissions: users-modify; Flags: uninsneveruninstall

[InstallDelete]
; Atalhos do nome antigo (Vorticis Suite), trocados pelos de Vorticis Suite Empresarial ao atualizar
Type: files; Name: "{autoprograms}\Vorticis Suite.lnk"
Type: files; Name: "{autodesktop}\Vorticis Suite.lnk"

[Icons]
Name: "{autoprograms}\Vorticis Suite Empresarial"; Filename: "{app}\VorticisSuite.exe"; IconFilename: "{app}\VorticisSuite.ico"; Components: contabil fiscal folha escritorio erp condominio
Name: "{autodesktop}\Vorticis Suite Empresarial"; Filename: "{app}\VorticisSuite.exe"; IconFilename: "{app}\VorticisSuite.ico"; Tasks: desktopicon; Components: contabil fiscal folha escritorio erp condominio
Name: "{autoprograms}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"; IconFilename: "{app}\VorticisContabil.ico"; Components: contabil
Name: "{autoprograms}\Vorticis Fiscal"; Filename: "{app}\VorticisFiscal.exe"; IconFilename: "{app}\VorticisFiscal.ico"; Components: fiscal
Name: "{autoprograms}\Vorticis Folha"; Filename: "{app}\VorticisFolha.exe"; IconFilename: "{app}\VorticisFolha.ico"; Components: folha
Name: "{autoprograms}\Vorticis Escritório"; Filename: "{app}\VorticisEscritorio.exe"; IconFilename: "{app}\VorticisEscritorio.ico"; Components: escritorio
Name: "{autoprograms}\Vorticis Gerencial"; Filename: "{app}\VorticisGerencial.exe"; IconFilename: "{app}\VorticisGerencial.ico"; Components: erp
Name: "{autoprograms}\Vorticis Condomínio"; Filename: "{app}\VorticisCondominio.exe"; IconFilename: "{app}\VorticisCondominio.ico"; Components: condominio
Name: "{autodesktop}\Vorticis Contábil"; Filename: "{app}\VorticisContabil.exe"; IconFilename: "{app}\VorticisContabil.ico"; Tasks: desktopicon; Components: contabil
Name: "{autodesktop}\Vorticis Fiscal"; Filename: "{app}\VorticisFiscal.exe"; IconFilename: "{app}\VorticisFiscal.ico"; Tasks: desktopicon; Components: fiscal
Name: "{autodesktop}\Vorticis Folha"; Filename: "{app}\VorticisFolha.exe"; IconFilename: "{app}\VorticisFolha.ico"; Tasks: desktopicon; Components: folha
Name: "{autodesktop}\Vorticis Escritório"; Filename: "{app}\VorticisEscritorio.exe"; IconFilename: "{app}\VorticisEscritorio.ico"; Tasks: desktopicon; Components: escritorio
Name: "{autodesktop}\Vorticis Gerencial"; Filename: "{app}\VorticisGerencial.exe"; IconFilename: "{app}\VorticisGerencial.ico"; Tasks: desktopicon; Components: erp
Name: "{autodesktop}\Vorticis Condomínio"; Filename: "{app}\VorticisCondominio.exe"; IconFilename: "{app}\VorticisCondominio.ico"; Tasks: desktopicon; Components: condominio

[Run]
; Ao final abre a Vorticis Suite Empresarial (todos os módulos instalados numa janela; começa pelo Escritório, para cadastrar o escritório
; e os clientes). Os módulos avulsos podem ser marcados.
Filename: "{app}\VorticisSuite.exe"; Description: "{cm:LaunchProgram,Vorticis Suite Empresarial}"; Flags: nowait postinstall skipifsilent; Components: contabil fiscal folha escritorio erp condominio
Filename: "{app}\VorticisContabil.exe"; Description: "{cm:LaunchProgram,Vorticis Contábil}"; Flags: nowait postinstall skipifsilent unchecked; Components: contabil
Filename: "{app}\VorticisFiscal.exe"; Description: "{cm:LaunchProgram,Vorticis Fiscal}"; Flags: nowait postinstall skipifsilent unchecked; Components: fiscal
Filename: "{app}\VorticisFolha.exe"; Description: "{cm:LaunchProgram,Vorticis Folha}"; Flags: nowait postinstall skipifsilent unchecked; Components: folha
Filename: "{app}\VorticisEscritorio.exe"; Description: "{cm:LaunchProgram,Vorticis Escritório}"; Flags: nowait postinstall skipifsilent unchecked; Components: escritorio
Filename: "{app}\VorticisGerencial.exe"; Description: "{cm:LaunchProgram,Vorticis Gerencial}"; Flags: nowait postinstall skipifsilent unchecked; Components: erp
Filename: "{app}\VorticisCondominio.exe"; Description: "{cm:LaunchProgram,Vorticis Condomínio}"; Flags: nowait postinstall skipifsilent unchecked; Components: condominio

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

var
  Progresso: TOutputProgressWizardPage;

// Runtime do Visual C++ 2015-2022 (x64): exigido pelos binários do PostgreSQL e pelo próprio programa.
function VcRuntimeInstalado(): Boolean;
var
  Instalado: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Instalado) and (Instalado = 1);
  if not Result then
    Result := FileExists(ExpandConstant('{sys}\vcruntime140_1.dll')) and FileExists(ExpandConstant('{sys}\msvcp140.dll'));
end;

// Instala o redistribuível em silêncio (o instalador do Visual C++ pede elevação por conta própria: "runas").
procedure InstalarVcRuntime();
var
  Codigo: Integer;
  Arquivo: String;
begin
  Arquivo := ExpandConstant('{tmp}\vc_redist.x64.exe');
  if (not FileExists(Arquivo)) or VcRuntimeInstalado() then Exit;
  Progresso.SetText('Instalando o Microsoft Visual C++ Redistributable (x64)...', '');
  // Se o usuário recusar a elevação, segue: as DLLs também vão em pgsql\bin, e a tela de Conexão explica o que faltar.
  ShellExec('runas', Arquivo, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, Codigo);
end;

function ModuloParaConfigurar(): String;
begin
  if WizardIsComponentSelected('contabil') then Result := 'VorticisContabil.exe'
  else if WizardIsComponentSelected('fiscal') then Result := 'VorticisFiscal.exe'
  else if WizardIsComponentSelected('escritorio') then Result := 'VorticisEscritorio.exe'
  else if WizardIsComponentSelected('erp') then Result := 'VorticisGerencial.exe'
  else if WizardIsComponentSelected('condominio') then Result := 'VorticisCondominio.exe'
  else Result := 'VorticisFolha.exe';
end;

// Configuração automática do banco: o primeiro módulo selecionado roda com --configurar-banco (sem janela; código 0 = pronto).
procedure ConfigurarBanco();
var
  Codigo: Integer;
  ArquivoLog: String;
begin
  ArquivoLog := ExpandConstant('{localappdata}\Vorticis\logs\configuracao-banco.log');
  Progresso.SetText('Configurando o banco de dados do Vorticis (pode levar até um minuto)...', '');
  Progresso.SetProgress(0, 0);
  if (not Exec(ExpandConstant('{app}\' + ModuloParaConfigurar()), '--configurar-banco', ExpandConstant('{app}'), SW_HIDE,
               ewWaitUntilTerminated, Codigo)) then
    Codigo := -1;
  Log('Configuração automática do banco: código de saída ' + IntToStr(Codigo));
  if Codigo = 0 then
    Progresso.SetText('Banco de dados configurado com sucesso.', '')
  else
  begin
    Progresso.SetText('O banco de dados não pôde ser configurado agora.', '');
    MsgBox('Não foi possível configurar o banco de dados automaticamente (código ' + IntToStr(Codigo) + ').' + #13#10#13#10 +
           'A instalação foi concluída. Ao abrir o Vorticis, a tela de Conexão mostrará o botão "Configurar automaticamente" ' +
           'para tentar de novo.' + #13#10#13#10 + 'Detalhes no registro: ' + ArquivoLog, mbInformation, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then Exit;
  Progresso := CreateOutputProgressPage('Configurando o Vorticis', 'Preparando o runtime e o banco de dados');
  Progresso.Show;
  try
    InstalarVcRuntime();
    ConfigurarBanco();
  finally
    Progresso.Hide;
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
