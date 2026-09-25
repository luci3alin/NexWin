#define MyAppName "NexWin"
#define MyAppVersion "1.0.85"
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

[Languages]
Name: "romanian"; MessagesFile: "compiler:Default.isl,Romanian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
romanian.GroupIcons=Scurtături și Pictograme:
english.GroupIcons=Shortcuts and Icons:
romanian.TaskDesktopIcon=Creează scurtătură pe Desktop (cu noua pictogramă NexWin)
english.TaskDesktopIcon=Create a Desktop shortcut (with the new NexWin icon)
romanian.GroupStartup=Integrare Sistem & Pornire Automată:
english.GroupStartup=System Integration & Auto-Start:
romanian.TaskAutoStartTray=Pornește automat NexWin în System Tray la aprinderea PC-ului (Fără fereastră UAC)
english.TaskAutoStartTray=Start NexWin automatically in System Tray at Windows login (Zero-UAC prompt)
romanian.GroupSafety=Siguranță & Mentenanță Inițială:
english.GroupSafety=Safety & Initial Maintenance:
romanian.TaskRestorePoint=Activează protecția System Restore pentru backup rapid înainte de optimizări
english.TaskRestorePoint=Enable System Restore protection for quick backup before optimizations
romanian.TaskCleanCache=Curăță cache-ul versiunilor anterioare și sincronizează limba selectată (RO)
english.TaskCleanCache=Clean legacy cache and synchronize selected installer language (EN)
romanian.LaunchApp=Pornește {#MyAppName} acum (în limba Română)
english.LaunchApp=Launch {#MyAppName} now (in English)

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; GroupDescription: "{cm:GroupIcons}"
Name: "autostarttray"; Description: "{cm:TaskAutoStartTray}"; GroupDescription: "{cm:GroupStartup}"
Name: "restorepoint"; Description: "{cm:TaskRestorePoint}"; GroupDescription: "{cm:GroupSafety}"
Name: "cleancache"; Description: "{cm:TaskCleanCache}"; GroupDescription: "{cm:GroupSafety}"

[Files]
Source: "..\native_stage\NexWin-v5\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\native\assets\logo.ico"; DestDir: "{app}"; DestName: "logo.ico"; Flags: ignoreversion
Source: "..\native\assets\logo.ico"; DestDir: "{app}\assets"; DestName: "logo.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\logo.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Run]
Filename: "schtasks.exe"; Parameters: "/Create /TN ""NexWinAutoStart"" /TR ""\""{app}\{#MyAppExeName}\"" --tray"" /SC ONLOGON /RL HIGHEST /F"; Flags: runhidden waituntilterminated; Tasks: autostarttray
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""NexWinAutoStart"" /F"; Flags: runhidden

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
