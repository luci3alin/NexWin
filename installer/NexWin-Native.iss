#define MyAppName "NexWin"
#define MyAppVersion "1.0.87"
#define MyAppPublisher "luci3alin"
#define MyAppURL "https://github.com/luci3alin/NexWin"
#define MyAppExeName "NexWin.exe"

[Setup]
AppId={{D6F98B22-8D3E-4B07-920E-A86292E47B2E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} - Windows 11 Tuning Suite
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist-installer
OutputBaseFilename=NexWin-Setup-v{#MyAppVersion}-native
SetupIconFile=..\native\assets\logo.ico
WizardImageFile=assets\wizard-banner.bmp
WizardSmallImageFile=assets\wizard-small.bmp
WizardImageStretch=yes
UninstallDisplayIcon={app}\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName} {#MyAppVersion}
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
ShowLanguageDialog=yes
UsePreviousLanguage=no
ChangesAssociations=yes

[Languages]
Name: "romanian"; MessagesFile: "compiler:Default.isl,Romanian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
romanian.SelectTasksDesc=Opțiuni suplimentare
romanian.SelectTasksLabel2=Alege opțiunile dorite pentru instalare:
romanian.FinishedHeadingLabel=Instalare completă {#MyAppName}
romanian.FinishedLabelNoIcons={#MyAppName} a fost instalat cu succes pe calculatorul tău.
romanian.FinishedLabel={#MyAppName} a fost instalat cu succes pe calculatorul tău.
romanian.ClickFinish=Apasă pe Finalizare pentru a închide instalatorul.
english.SelectTasksDesc=Additional options
english.SelectTasksLabel2=Choose the options you want to apply:
english.FinishedHeadingLabel={#MyAppName} Installation Complete
english.FinishedLabelNoIcons={#MyAppName} has been successfully installed on your computer.
english.FinishedLabel={#MyAppName} has been successfully installed on your computer.
english.ClickFinish=Click Finish to close the installer.

[CustomMessages]
romanian.GroupIcons=Scurtături:
english.GroupIcons=Shortcuts:
romanian.TaskDesktopIcon=Creează scurtătură pe Desktop
english.TaskDesktopIcon=Create a Desktop shortcut
romanian.GroupStartup=Pornire automată:
english.GroupStartup=Startup:
romanian.TaskAutoStartTray=Pornește automat odată cu Windows
english.TaskAutoStartTray=Start automatically with Windows
romanian.GroupSafety=Siguranță și mentenanță:
english.GroupSafety=Safety and maintenance:
romanian.TaskRestorePoint=Activează System Restore pentru backup
english.TaskRestorePoint=Enable System Restore for backup
romanian.TaskCleanCache=Curăță fișierele temporare vechi
english.TaskCleanCache=Clean old temporary files
romanian.LaunchApp=Pornește {#MyAppName}
english.LaunchApp=Launch {#MyAppName}

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; GroupDescription: "{cm:GroupIcons}"
Name: "autostarttray"; Description: "{cm:TaskAutoStartTray}"; GroupDescription: "{cm:GroupStartup}"
Name: "restorepoint"; Description: "{cm:TaskRestorePoint}"; GroupDescription: "{cm:GroupSafety}"
Name: "cleancache"; Description: "{cm:TaskCleanCache}"; GroupDescription: "{cm:GroupSafety}"

[Files]
Source: "..\native_stage\NexWin-v1087\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\native\assets\logo.ico"; DestDir: "{app}"; DestName: "logo.ico"; Flags: ignoreversion
Source: "..\native\assets\nexwin_ribbon.ico"; DestDir: "{app}"; DestName: "nexwin_ribbon.ico"; Flags: ignoreversion
Source: "..\native\assets\logo.ico"; DestDir: "{app}\assets"; DestName: "logo.ico"; Flags: ignoreversion

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NexWin"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Flags: uninsdeletevalue; Tasks: autostarttray

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\nexwin_ribbon.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\nexwin_ribbon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\nexwin_ribbon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
procedure SyncLanguageAndSettings();
var
  LangCode: string;
  LocalAppDataDir: string;
  JsonContent: string;
begin
  if ActiveLanguage = 'english' then
    LangCode := 'en'
  else
    LangCode := 'ro';

  // 1. Write selected language to %LocalAppData%\NexWin\settings.json so NexWin & System Tray open in this exact language
  LocalAppDataDir := ExpandConstant('{localappdata}\NexWin');
  if not DirExists(LocalAppDataDir) then
    ForceDirectories(LocalAppDataDir);

  JsonContent := '{' + #13#10 +
                 '  "language": "' + LangCode + '",' + #13#10 +
                 '  "syncedFromInstaller": true' + #13#10 +
                 '}';
  SaveStringToFile(LocalAppDataDir + '\settings.json', JsonContent, False);

  // 2. Also save to HKCU\Software\NexWin for instant native lookup
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\NexWin', 'Language', LangCode);

  // 3. Purge any old unverified donation cache if cleancache task is checked
  if WizardIsTaskSelected('cleancache') then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\NexWin\CommunityGoal', 'LastSyncedJson');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SyncLanguageAndSettings();
  end;
end;
