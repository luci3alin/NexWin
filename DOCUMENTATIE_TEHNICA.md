# NexWin - Documentatie Tehnica Completa

Acest document contine descrierea arhitecturii, tehnologiilor utilizate, structurii interne si a modificarilor aduse sistemului de operare Windows 11 de catre aplicatia NexWin.

> **Nota pentru versiunea livrata 2.0.0 (2026):** aplicatia distribuita este nativa C# WinForms pe .NET 10 LTS, self-contained pentru Windows x64. Descrierile istorice despre React, Node.js, Express si WebView2 se refera la prototipul web anterior si nu mai fac parte din installerul actual.

---

## 1. Arhitectura Generala si Stiva Tehnologica

Aplicatia NexWin este conceputa pe un model hibrid desktop performant, modular si offline-first. Functiile de baza nu necesita internet; benchmark-ul DNS este optional si contacteaza resolverele selectate de utilizator.

### 1.1. Interfata Utilizator Nativa
- **Framework principal**: Windows Forms pe .NET 10 LTS
- **Limbaj**: C# 14, compilat pentru `net10.0-windows`
- **Randare**: controale Windows native, fara DOM, browser incorporat sau JavaScript
- **Confirmari**: `MessageBox` native, fara alerte `window.confirm` greu de controlat
- **Design System**: tema Obsidian Dark cu accente violet, cyan, auriu si smarald
- **Accesibilitate UI**: controale native, navigabile cu mouse-ul si tastatura.

### 1.2. Aplicatia Desktop & Shell Windows
- **Limbaj & Runtime**: C# compilat pe .NET 10 LTS / Win32
- **Motor de randare**: Windows Forms nativ; nu este necesar WebView2
- **Integrare DWM Windows 11 (Desktop Window Manager)**:
  - Apel direct la functia nativa `DwmSetWindowAttribute` din `dwmapi.dll`.
  - Aplicare atribute `DWMWA_USE_IMMERSIVE_DARK_MODE` (atributele 19 si 20) pentru tema intunecata a controalelor ferestrei (Minimize, Maximize, Close).
  - Personalizare bara de titlu (`DWMWA_CAPTION_COLOR = 35`) setata pe `#07070A` (`0x000A0707` in format COLORREF Win32).
  - Personalizare text bara de titlu (`DWMWA_TEXT_COLOR = 36`) pe alb pur (`0x00FFFFFF`).
  - Personalizare contur fereastra (`DWMWA_BORDER_COLOR = 34`) pe `#1B1B28` (`0x00281B1B`).
- **Control Instanta Unica**: Mutex de sistem (`NexWin_SingleInstance_Mutex_2026`) ce impiedica lansarea duplicata a aplicatiei.
- **Management Ciclul de Viata**: o singura fereastra WinForms, cu anulare controlata la inchidere si fara proces web copil.
- **Privilegii Administrative**: Binarul contine un manifest de aplicatie Windows cu `requestedExecutionLevel level="requireAdministrator"`, garantand drepturile UAC necesare pentru modificarea registrilor de sistem si a serviciilor.

### 1.3. Orchestrare Locala
- **Executie**: `System.Diagnostics.Process` lanseaza `powershell.exe` cu redirectare stdout/stderr si timeout controlat.
- **Securitate**: lista alba de scripturi, validare de argumente si manifest `requireAdministrator` pentru operatiile privilegiate.
- **Scanare disc**: enumerare asincrona in thread pool, astfel incat interfața sa ramana responsiva.
- **Stocare**: fisierele de stare si snapshot-urile sunt scrise in `%LOCALAPPDATA%\NexWin`, nu langa executabilul instalat in Program Files.

### 1.4. Kit de Instalare si Distributie
- **Compilator Installer**: Inno Setup 6
- **Tip pachet**: installer Inno Setup cu aplicatie self-contained (`NexWin-Setup-v2.0.0-native.exe`)
- **Componente impachetate**:
  - Executabilul C#/.NET 10 self-contained pentru Windows x64
  - Scripturile PowerShell de sistem (`scripts/powershell/`)
  - Pictograma si documentatia locala
- **Capabilitate offline-first**: Zero apeluri HTTP externe si zero dependinte descarcate la instalare sau in timpul rularii. Singura functie dependenta de retea este benchmark-ul DNS.

---

## 2. Descrierea Detaliata a Modulelor si Modificarilor de Sistem

Mai jos sunt detaliate toate modulele aplicatiei, rolul functional, scripturile apelate, registrii modificati si serviciile afectate.

```
+-----------------------------------------------------------------------------------+
|                                 NEXWIN SUITE                                      |
+-----------------------------------------------------------------------------------+
| [Dashboard]        | Prezentare status, consum RAM/CPU si One-Click Boost         |
| [Disk Analyzer]    | WinDirStat Pro: Treemap, categorii, junk cleanup, TRIM       |
| [AI Removal]       | Eliminare Copilot, Recall, Edge AI, telemetrie tastare       |
| [Gaming Tweaks]    | VBS/HVCI off, HAGS on, Game Mode, Nagle off, Ultimate Power   |
| [Process Lasso]    | SmartTrim RAM cleaner, prioritati IFEO permanente, ProBalance|
| [Services Debloat] | Dezactivare telemetrie, DiagTrack, Edge workers, SvcHost 64GB |
| [Startup Apps]     | Monitorizare si activare/dezactivare aplicatii de pornire    |
| [Process Inspector]| Monitorizare consum procese active si oprire fortata         |
| [Classic Apps]     | Restaurare Windows Photo Viewer, Notepad clasic, Paint       |
| [Live Terminal]    | Streaming loguri PowerShell in timp real cu ETA si procentaj |
+-----------------------------------------------------------------------------------+
```

---

### Modulul 1: Dashboard si One-Click Boost Pipeline

**Rol:**
Ofera un sumar grafic al starii sistemului (numar de procese, utilizare RAM, incarcare CPU, versiune Windows) si permite optimizarea completa printr-un singur click.

**Script principal:** `scripts/powershell/Invoke-OneClickBoost.ps1`

**Etapele pipeline-ului de optimizare:**
1. `[1/7]` **Creare punct de restaurare sistem** (`Invoke-RestorePoint.ps1`)
   - Activeaza Volume Shadow Copy pe partitia C: (`Enable-ComputerRestore -Drive "C:\"`).
   - Creeaza punctul de restaurare cu numele `NexWin Boost` (`Checkpoint-Computer -RestorePointType "MODIFY_SETTINGS"`).
2. `[2/6]` **Eliminare componente AI** (`Invoke-AiRemoval.ps1 -All`)
   - Opreste si blocheaza complet Windows Copilot, Recall si telemetria aferenta.
3. `[3/6]` **Configurare profil gaming** (`Invoke-GamingTweaks.ps1 -All`)
   - Dezactiveaza VBS/HVCI, activeaza Game Mode, HAGS, Ultimate Performance si optimizeaza Nagle.
4. `[4/6]` **Debloat servicii si consolidare SvcHost** (`Invoke-DebloatServices.ps1 -All`)
   - Opreste serviciile redundante si seteaza pragul de comasare SvcHost la 64 GB.
5. `[5/6]` **Ajustare efecte vizuale** (`Invoke-VisualEffects.ps1 -Optimize`)
   - Opreste animatiile greoaie de ferestre si taskbar pastrand nealterata claritatea fonturilor (ClearType).
6. `[6/6]` **Curatare mentenanta si TRIM** (`Invoke-Maintenance.ps1 -CleanTemp -CleanShaderCache -RunTrim`)
   - Sterge folderele `%TEMP%`, curata shader cache GPU (NVIDIA/AMD/DirectX) si trimite semnalul TRIM catre SSD.
7. `[7/7]` **Raport final de stare** (`Get-SystemStatus.ps1`)
   - Recalculeaza toti parametrii si actualizeaza datele in interfata grafica.

---

### Modulul 2: Eliminare AI Windows (Copilot, Recall, Edge AI)

**Rol:**
Dezactiveaza complet toate tehnologiile de inteligenta artificiala nesolicitate integrate in Windows 11, recuperand resurse de calcul si protejand confidentialitatea utilizatorului.

**Script:** `scripts/powershell/Invoke-AiRemoval.ps1` (parametri: `-All`, `-DisableCopilot`, `-DisableRecall`, `-DisableEdgeAI`, `-DisablePaintAI`, `-DisableTasks`, `-Revert`)

**Modificari de Registri aplicate:**
- **Windows Copilot**:
  - `HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot` = `1` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot` = `1` (DWORD)
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\ShowCopilotButton` = `0` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search\AllowCortana` = `0` (DWORD)
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Search\SearchboxTaskbarMode` = `1` (DWORD)
- **Windows Recall si Analiza Date AI**:
  - `HKCU:\Software\Policies\Microsoft\Windows\WindowsAI\DisableAIDataAnalysis` = `1` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI\DisableAIDataAnalysis` = `1` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI\AllowRecallEnablement` = `0` (DWORD)
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\RecallEnabled` = `0` (DWORD)
- **Microsoft Edge AI si Hub-uri**:
  - `HKLM:\SOFTWARE\Policies\Microsoft\Edge\CopilotPageContext` = `0` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Edge\HubsSidebarEnabled` = `0` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Edge\StandaloneHubsSidebarEnabled` = `0` (DWORD)
- **Paint si Photos AI Experiments**:
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Paint\DisableAIExperiments` = `1` (DWORD)
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy\LetAppsAccessFaceTracker` = `2` (DWORD)
- **Telemetrie tastare, scris de mana si colectare contacte**:
  - `HKCU:\Software\Microsoft\InputPersonalization\RestrictImplicitInkCollection` = `1` (DWORD)
  - `HKCU:\Software\Microsoft\InputPersonalization\RestrictImplicitTextCollection` = `1` (DWORD)
  - `HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore\HarvestContacts` = `0` (DWORD)

**Servicii de sistem afectate:**
- `AIFabricService` - Oprit fortat si setat pe `Disabled` (StartupType 4).

**Task-uri planificate dezactivate (Scheduled Tasks):**
- `\Microsoft\Windows\WindowsAI\SnapshotTask`
- `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser`
- `\Microsoft\Windows\Application Experience\ProgramDataUpdater`
- `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator`
- `\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip`
- `\Microsoft\Windows\Autochk\Proxy`

---

### Modulul 3: Optimizari Gaming, GPU si Latenta Periferice

**Rol:**
Maximizeaza numarul de cadre pe secunda (FPS) si elimina stuttering-ul si input lag-ul in jocuri competitive.

**Scripturi:**
- `scripts/powershell/Invoke-GamingTweaks.ps1`
- `scripts/powershell/Invoke-UsbOptimization.ps1`
- `scripts/powershell/Invoke-GpuDriverProtection.ps1`

**Modificari de Sistem si Registri:**
- **VBS (Virtualization-Based Security) si HVCI (Memory Integrity)**:
  - `HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\EnableVirtualizationBasedSecurity` = `0`
  - `HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled` = `0`
   - *Efect:* Elimina stratul de virtualizare din kernel. Castigul depinde de hardware si joc; nu este garantat si reduce protectia de securitate Windows.
- **Windows 11 Game Mode**:
  - `HKCU:\Software\Microsoft\GameBar\AutoGameModeEnabled` = `1`
  - `HKCU:\Software\Microsoft\GameBar\AllowAutoGameMode` = `1`
- **HAGS (Hardware-Accelerated GPU Scheduling)**:
  - `HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode` = `2`
  - *Efect:* Permite placii video sa isi gestioneze direct memoria VRAM fara intermedierea continua a CPU-ului.
- **Plan de Energie Ultimate Performance**:
  - Aplica schema `e9a42b02-d5df-448d-aa00-03f14749eb61` prin utilitarul Windows `powercfg`.
  - In cazul in care schema nu este disponibila pe configuratia hardware, foloseste ca fallback profilul `High Performance` (`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`).
  - Impiedica nucleele procesorului sa intre in stari de park/throttling in timpul jocului.
- **Latenta de Retea (Dezactivare Algoritm Nagle)**:
  - Identifica placa de retea activa (ignorand interfetele virtuale/VPN/Loopback).
  - `HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AdapterGUID}\TcpAckFrequency` = `1`
  - `HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AdapterGUID}\TCPNoDelay` = `1`
   - *Efect:* Schimba comportamentul TCP pentru adaptorul selectat. Multe jocuri folosesc UDP, deci nu este un tratament universal pentru ping.
- **Configurare DNS de viteza ridicata**:
  - Seteaza adresele DNS pe placa activa: Cloudflare (`1.1.1.1`, `1.0.0.1`), Google (`8.8.8.8`, `8.8.4.4`) sau Quad9 (`9.9.9.9`, `149.112.112.112`).
  - Goleste cache-ul resolverului prin `Clear-DnsClientCache`.
- **Excludere Folder Jocuri in Windows Defender**:
  - Detecteaza automat calea de instalare Steam din registri (`HKCU:\Software\Valve\Steam` sau `HKLM:\SOFTWARE\Valve\Steam`) si folderul `steamapps\common` (sau `C:\Games`).
  - Adauga calea in lista de excluderi Windows Defender prin `Add-MpPreference -ExclusionPath`, oprind scanarea I/O a fisierelor de joc in timpul gameplay-ului.
- **Optimizare Periferice USB si Input Lag (`Invoke-UsbOptimization.ps1`)**:
  - Dezactiveaza optiunea `USB Selective Suspend` in schema de alimentare curenta prin `powercfg /SETACVALUEINDEX` si `powercfg /SETDCVALUEINDEX`.
  - Parcurge toate hub-urile si controlerele USB din sistem (`Win32_PnPEntity` cu servicii `usbhub`, `usbxhci`, `usbehci`) si seteaza:
    - `HKLM:\SYSTEM\CurrentControlSet\Enum\{DeviceID}\Device Parameters\EnhancedPowerManagementEnabled` = `0`
    - `HKLM:\SYSTEM\CurrentControlSet\Enum\{DeviceID}\Device Parameters\DeviceSelectiveSuspended` = `0`
    - `HKLM:\SYSTEM\CurrentControlSet\Enum\{DeviceID}\Device Parameters\SelectiveSuspendEnabled` = `0`
  - *Efect:* Elimina micro-deconectarea senzorului mouse-ului si garanteaza un polling rate constant (1000Hz, 4000Hz sau 8000Hz).
- **Protectie Suprascriere Drivere GPU (`Invoke-GpuDriverProtection.ps1`)**:
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\ExcludeWUDriversInQualityUpdate` = `1`
  - `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching\SearchOrderConfig` = `0`
  - *Efect:* Blocheaza Windows Update sa inlocuiasca driverele video oficiale NVIDIA/AMD cu versiuni generice Microsoft WHQL.

---

### Modulul 4: Debloat Servicii Windows si Consolidare SvcHost

**Rol:**
Reduce numarul total de procese active de la peste 270 la un nivel optimizat (< 170 - 200) prin oprirea serviciilor inutile de fundal si gruparea proceselor gazda.

**Scripturi:**
- `scripts/powershell/Invoke-DebloatServices.ps1`
- `scripts/powershell/Invoke-RestoreScreenshot.ps1`

**Modificari de Registri si Servicii:**
- **Telemetrie si Colectare Date**:
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry` = `0`
  - `HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection\MaxTelemetryAllowed` = `0`
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack\ShowDiagTrackNotice` = `0`
- **Servicii Oprite si Blocate Permanent (`Start = 4` in HKLM Services)**:
  - `DiagTrack`: Connected User Experiences and Telemetry
  - `dmwappushservice`: WAP Push Message Routing Service
  - `DPS`: Diagnostic Policy Service
  - `WdiServiceHost` si `WdiSystemHost`: Diagnostic Service Hosts
  - `InventorySvc`: Targeted Content and Application Inventory Service
  - `DsSvc`: Data Sharing Service
  - `DusmSvc`: Data Usage Service
  - `WerSvc`: Windows Error Reporting Service (plus cheia `HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\Disabled = 1`)
  - `MapsBroker`: Downloaded Maps Manager
  - `lfsvc`: Geolocation Service
  - `TrkWks`: Distributed Link Tracking Client
  - `RetailDemo`: Retail Demo Service
  - `RemoteRegistry`: Remote Registry
  - `shpamsvc`: Shared PC Account Manager
  - `PhoneSvc`: Telephony and Phone integration
  - `PcaSvc`: Program Compatibility Assistant
  - `DoSvc`: Delivery Optimization
  - `SSDPSRV`: SSDP Discovery
- **Microsoft Edge Background Workers si Startup Boost**:
  - `HKLM:\SOFTWARE\Policies\Microsoft\Edge\StartupBoostEnabled` = `0`
  - `HKLM:\SOFTWARE\Policies\Microsoft\Edge\BackgroundModeEnabled` = `0`
  - `HKCU:\Software\Policies\Microsoft\Edge\StartupBoostEnabled` = `0`
  - `HKCU:\Software\Policies\Microsoft\Edge\BackgroundModeEnabled` = `0`
  - *Efect:* Poate reduce procesele Edge de fundal; numarul exact depinde de versiunea Edge si de extensii.
- **Reclame si Continut Sugerat (ContentDeliveryManager)**:
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\SystemPaneSuggestionsEnabled` = `0`
  - `SubscribedContent-338388Enabled` = `0`
  - `SubscribedContent-338389Enabled` = `0`
  - `SubscribedContent-353696Enabled` = `0`
  - `SoftLandingEnabled` = `0`
- **Widgets si Feeds (Stiri si Interese)**:
  - `HKLM:\SOFTWARE\Policies\Microsoft\Dsh\AllowNewsAndInterests` = `0`
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDa` = `0`
  - Opreste procesele active `Widgets` si `WidgetService`.
- **Consolidare Procese SvcHost (SvcHost Split Threshold)**:
  - `HKLM:\SYSTEM\CurrentControlSet\Control\SvcHostSplitThresholdInKB` = `67108864` (DWORD, echivalentul a 64 GB).
  - *Explicatie:* Incepand cu Windows 10 Creators Update, daca un PC are peste 3.5 GB RAM, Windows atribuie fiecarui serviciu propriul proces `svchost.exe` (rezultand peste 80 de procese). Setarea pragului la 64 GB forteaza sistemul ca la urmatoarea repornire sa regrupeze serviciile compatibile in containere comune, scazand numarul de procese cu cateva zeci fara a pierde nicio functionalitate.
- **Optiuni Suplimentare**:
  - SysMain (Superfetch): optiune de oprire pentru SSD-uri rapide.
  - Windows Search (WSearch): optiune de dezactivare a indexarii continue pe disc.
- **Pastrare si Reparare Screenshot / Clipboard (`Invoke-RestoreScreenshot.ps1`)**:
  - Asigura functionarea tastelor `Win + Shift + S` si `PrintScreen`:
    - Serviciul `camsvc` (Capability Access Manager Service) este repornit si setat pe Manual.
    - `HKCU:\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled` = `1`.
    - Permisiune acordata pentru pachetele `ScreenSketch` in `BackgroundAccessApplications`.
    - Deblocare politici `NoScreenCapture` si `DisableSnippingTool`.
    - Repornire servicii de clipboard: `CDPSvc` (Connected Devices Platform) si `cbdhsvc` (Clipboard User Service).

---

### Modulul 5: Process Lasso Pro si Management Memorie RAM

**Rol:**
Gestioneaza prioritatile proceselor in timp real, curata memoria RAM inactiva si previne lag-ul cauzat de aplicatiile din fundal.

**Script:** `scripts/powershell/Invoke-ProcessLasso.ps1`

**Functionalitati si Implementare Tehnica:**
1. **SmartTrim RAM Cleaner (`-SmartTrim`)**:
   - Foloseste cod C# compilat dinamic in memorie via P/Invoke (`Add-Type`) cu apel la functia nativa `EmptyWorkingSet(IntPtr hwProc)` din biblioteca Windows `psapi.dll`.
   - Itereaza prin toate procesele active si elibereaza paginile de memorie nefolosite inapoi in rezerva sistemului, urmat de invocarea garbage collector-ului (`GC.Collect()`).
   - Poate reduce working set-ul raportat, dar nu garanteaza memorie libera sau performanta mai buna; poate creste page faults si stuttering. Se recomanda doar sub presiune reala de memorie.
2. **Prioritate Procese si Reguli Permanente IFEO (`-ProcessName`, `-Priority`, `-PersistentRule`)**:
   - Modifica prioritatea procesului activ (`High`, `BelowNormal`, `Idle`, `Normal`) prin clasa .NET `ProcessPriorityClass`.
   - **Regula persistenta IFEO:** Daca parametrul `-PersistentRule` este activ, creeaza cheia in registri:
     `HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{NumeProces}.exe\PerfOptions\CpuPriorityClass`
     (Valori: `3` = High, `5` = BelowNormal, `1` = Idle, `2` = Normal).
     *Efect:* De fiecare data cand Windows lanseaza acel executabil, ii va atribui automat prioritatea aleasa, chiar si dupa repornirea PC-ului.
3. **ProBalance Background Throttle (`-ProBalance`)**:
   - Detecteaza aplicatiile secundare active (Discord, Spotify, EpicGamesLauncher, SteamService, msedge, brave, chrome) si le scade prioritatea de la `Normal` la `BelowNormal`.
   - Jocul principal primeste acces preferential neintrerupt la ciclurile de ceas ale procesorului.
4. **Dezactivare CPU Power Throttling (`-DisablePowerThrottling`)**:
   - `HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling\PowerThrottlingOff` = `1` (DWORD).
   - Impiedica planificatorul Windows sa limiteze frecventa nucleelor CPU pentru procesele intensive.

---

### Modulul 6: WinDirStat Pro si Disk Analyzer

**Rol:**
Scanare ultra-rapida a discului, reprezentare vizuala a spatiului ocupat prin Treemap interactiv pe categorii de culori, detectare fisiere gigant si curatare fisiere temporare.

**Fisiere cheie:**
- `server/disk-scanner.js` (Scaner nativ Node.js/C++)
- `scripts/powershell/Invoke-DiskCleanup.ps1` (Curatare disc)
- `scripts/powershell/Invoke-Maintenance.ps1` (Curatare Shader Cache si SSD TRIM)
- `src/views/DiskAnalyzerView.tsx` (Interfata grafica Treemap)

**Caracteristici Tehnice:**
1. **Motor de Scanare Nativ Asincron**:
   - Implementat in JavaScript pe baza `fs.readdir` cu `withFileTypes: true` (apel direct la `FindFirstFileExW` din C++ prin `libuv`).
   - Filtreaza automat directoarele protejate de sistem (`System Volume Information`, `$Recycle.Bin`, foldere ce incep cu `$`).
   - Aloca un buget maxim de 1.5 secunde per subfolder pentru a livra rezultatele instantaneu, fara blocaje.
   - Viteza depinde de disc, permisiuni, antivirus si adancimea scanata; interfata trebuie sa afiseze cand rezultatul este partial sau limitat de bugetul de timp.
2. **Treemap Vizual Interactiv**:
   - Genereaza placi proportionale cu marimea fisierelor si folderelor.
   - Codare pe culori conform categoriilor:
     - Mov (`#8B5CF6`): Jocuri si Aplicatii (`.exe`, `.dll`, `.pak`, `.bin`, etc.)
     - Cyan (`#06B6D4`): Video si Media (`.mp4`, `.mkv`, `.avi`, `.mov`, etc.)
     - Galben-Auriu (`#F59E0B`): Arhive si Imagini Disc (`.zip`, `.rar`, `.7z`, `.iso`, `.img`)
     - Smarald (`#10B981`): Documente si Baze de Date (`.pdf`, `.docx`, `.xlsx`, `.sql`)
     - Roz (`#EC4899`): Fisiere Audio (`.mp3`, `.wav`, `.flac`)
     - Rosu (`#F43F5E`): Fisiere Temporare si Log-uri (`.tmp`, `.log`, `.dmp`, `.cache`)
   - Suporta navigare interactiva (click pe orice folder din Treemap navigheaza instant in interiorul sau).
3. **Bara Spectru de Stocare**:
   - Afiseaza ponderea procentuala a fiecarui tip de continut pe discul analizat.
4. **Presetari Rapide de Locatie**:
   - Butoane cu un singur click pentru: Descarcari (Downloads), Profil Utilizator, Desktop, AppData Local, Program Files si Partitia C:\.
5. **Integrare Windows Explorer**:
   - Fiecare element din lista are optiunea de a fi deschis direct in Explorer folosind comanda `explorer.exe /select,"<cale>"`, evidentiind fisierul respectiv.
6. **Detector Fisiere Gigant**:
   - Indexeaza si afiseaza rapid fisierele individuale cu dimensiuni de peste 10 MB, sortate descrescator.
7. **Curatare Inteligenta de Disc (`Invoke-DiskCleanup.ps1`)**:
   - Fisiere temporare utilizator (`%TEMP%`)
   - Fisiere temporare Windows (`C:\Windows\Temp`)
   - Cache descarcari Windows Update (`C:\Windows\SoftwareDistribution\Download`)
   - Rapoarte de crash si minidump-uri (`WER`, `Minidump`, `MEMORY.DMP`)
   - Golire cos de reciclare (`Clear-RecycleBin -Force`)
   - Cache Delivery Optimization
8. **Curatare Shader Cache si TRIM SSD (`Invoke-Maintenance.ps1`)**:
   - Curatare directoare shader cache GPU: `%LOCALAPPDATA%\D3DSCache`, `%LOCALAPPDATA%\NVIDIA\DXCache`, `%LOCALAPPDATA%\AMD\DxCache`.
   - Trimitere comanda de re-indexare si optimizare blocuri SSD: `Optimize-Volume -DriveLetter C -ReTrim`.

---

### Modulul 7: Manager Aplicatii Startup

**Rol:**
Identifica si permite pornirea sau oprirea aplicatiilor care ruleaza automat la pornirea sistemului Windows.

**Scripturi:**
- `scripts/powershell/Get-StartupApps.ps1`
- `scripts/powershell/Set-StartupApp.ps1`

**Mecanism Tehnic:**
- Scaneaza intrarile din registri:
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Run`
  - `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
  - `HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`
- Modifica starea de activare/dezactivare respectand formatul binar nativ din Windows Task Manager:
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run\{NumeAplicatie}`
  - `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run\{NumeAplicatie}`
  - Valoare binara: `02 00 00 00 00 00 00 00 00 00 00 00` = **Activ**
  - Valoare binara: `03 00 00 00 00 00 00 00 00 00 00 00` = **Inactiv**

---

### Modulul 8: Inspector Procese Active

**Rol:**
Ofera un manager de sarcini usor si direct, listand procesele ordonate descrescator dupa consumul de memorie RAM si incarcarea procesorului, cu posibilitatea de oprire fortata.

**Scripturi si Endpoints:**
- `scripts/powershell/Get-ProcessList.ps1`
- API `POST /api/kill-process`
- Executa comanda `Stop-Process -Id {PID} -Force` sau `Stop-Process -Name {Nume} -Force`.

---

### Modulul 9: Aplicatii Clasice Windows

**Rol:**
Restaureaza utilitarele clasice Win32 Windows care se deschid instantaneu, inlocuind versiunile moderne UWP lente.

**Script:** `scripts/powershell/Invoke-ClassicApps.ps1`

**Modificari:**
- **Windows Photo Viewer clasic**:
  - Configureaza asocierile de fisiere in `HKLM:\SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations`.
  - Mapeaza extensiile `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.tiff` la componenta nativa de sistem `PhotoViewer.FileAssoc.Tiff`.
- **Notepad Clasic**:
  - Elimina redirectarile UWP din cheia IFEO `HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe`.
- **Paint Clasic (mspaint)**:
  - Elimina redirectarile din cheia IFEO `HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\mspaint.exe`.

---

### Modulul 10: Optimizare Efecte Vizuale si Calitate Fonturi

**Rol:**
Elimina animatiile lente ale interfetei grafice Windows, garantand in acelasi timp pastrarea claritatii optime a fonturilor (ClearType).

**Scripturi:**
- `scripts/powershell/Invoke-VisualEffects.ps1`
- `scripts/powershell/Restore-FontSmoothing.ps1`

**Modificari de Registri:**
- **Viteza interfata:**
  - `HKCU:\Control Panel\Desktop\WindowMetrics\MinAnimate` = `0` (dezactiveaza animatiile la minimizare/maximizare)
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarAnimations` = `0` (dezactiveaza animatiile barei de activitati)
  - `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualFXSetting` = `3` (Profil personalizat de performanta)
- **Pastrare si Restaurare Netezire Fonturi (ClearType):**
  - `HKCU:\Control Panel\Desktop\FontSmoothing` = `"2"` (String obligatoriu, nu DWORD)
  - `HKCU:\Control Panel\Desktop\FontSmoothingType` = `2` (DWORD)
  - `HKCU:\Control Panel\Desktop\FontSmoothingGamma` = `0` (DWORD)
  - `HKCU:\Control Panel\Desktop\FontSmoothingOrientation` = `1` (DWORD)
  - Masca binara `UserPreferencesMask` este configurata cu semnatura implicita Windows 11:
    `9E 3E 07 80 12 00 00 00` (pastreaza bitul 1 activ pentru netezirea marginilor caracterelor).
  - **Notificare in timp real:** Apeleaza functia Win32 `SystemParametersInfo(SPI_SETFONTSMOOTHING)` din `user32.dll` via P/Invoke C# pentru a forta subsistemul grafic Windows sa aplice randarea ClearType imediat, fara a necesita deconectare sau repornire.

---

### Modulul 11: Benchmark si Configurare DNS

**Rol:**
Masoara latenta query-urilor DNS catre furnizorii majori si permite schimbarea sau resetarea configuratiei de retea.

**Script:** `scripts/powershell/Invoke-DnsBenchmark.ps1`

**Actiuni:**
- Executa query-uri DNS reale (`Resolve-DnsName -Server`) si raporteaza `QueryMs`, numarul de esantioane si ping-ul ICMP separat.
- Foloseste ca destinatii:
  - Cloudflare: `1.1.1.1`
  - Google: `8.8.8.8`
  - Quad9: `9.9.9.9`
- Calculeaza media query-urilor si returneaza datele in format JSON (`DNS_BENCHMARK_DATA`).
- Permite setarea adreselor primare si secundare pe placa activa prin `Set-DnsClientServerAddress` sau revenirea la configuratia furnizata de router prin parametrul `-ResetDns`.

---

### Modulul 12: Consola Live PowerShell si Terminal Drawer

**Rol:**
Ofera transparenta completa asupra comenzilor executate in fundal prin afisarea fluxului de date in timp real.

**Fisiere cheie:**
- `server/powershell-runner.js` (Proces copil PowerShell asincron)
- `src/components/TerminalDrawer.tsx` (Interfata consola)

**Functionalitati:**
- Spawneaza comanda `powershell.exe` cu flag-urile `-NoProfile -NonInteractive -ExecutionPolicy Bypass`.
- Receptioneaza fluxurile `stdout` si `stderr` pe bucati (chunks) si le transmite instantaneu prin Server-Sent Events (SSE) catre interfata React.
- **Detector automat de progres:** Parseaza tiparele de tip `[X/Y]` sau `[STEP X/Y]` din iesirea standard, calculand procentajul de completare si un timp estimat ramas (ETA dinamic) afisat sub forma unei bare de progres animate in partea superioara a consolei.
- Include functii de stergere a istoricului consolei, copiere a logurilor si autoscroll la linia curenta.

---

## 3. Matricea Completa a Modificarilor de Registri Windows

| Cale Registru (Hive si Cale) | Nume Valoare | Tip Date | Valoare Aplicata | Scopul Modificarii |
| :--- | :--- | :--- | :--- | :--- |
| `HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot` | `TurnOffWindowsCopilot` | REG_DWORD | `1` | Dezactiveaza Copilot pentru utilizatorul curent |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot` | `TurnOffWindowsCopilot` | REG_DWORD | `1` | Dezactiveaza Copilot la nivel de sistem |
| `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` | `ShowCopilotButton` | REG_DWORD | `0` | Ascunde butonul Copilot din taskbar |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search` | `AllowCortana` | REG_DWORD | `0` | Dezactiveaza asistentul Cortana |
| `HKCU:\Software\Policies\Microsoft\Windows\WindowsAI` | `DisableAIDataAnalysis` | REG_DWORD | `1` | Opreste colectarea datelor de catre Windows AI |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI` | `DisableAIDataAnalysis` | REG_DWORD | `1` | Opreste analiza AI la nivel de sistem |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI` | `AllowRecallEnablement` | REG_DWORD | `0` | Blocheaza activarea Windows Recall |
| `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` | `RecallEnabled` | REG_DWORD | `0` | Dezactiveaza Recall pentru profilul activ |
| `HKLM:\SOFTWARE\Policies\Microsoft\Edge` | `CopilotPageContext` | REG_DWORD | `0` | Blocheaza accesul Copilot la continutul paginilor in Edge |
| `HKLM:\SOFTWARE\Policies\Microsoft\Edge` | `HubsSidebarEnabled` | REG_DWORD | `0` | Dezactiveaza bara laterala si hub-urile AI din Edge |
| `HKLM:\SOFTWARE\Policies\Microsoft\Edge` | `StartupBoostEnabled` | REG_DWORD | `0` | Opreste pre-incarcarea Edge la pornirea Windows |
| `HKLM:\SOFTWARE\Policies\Microsoft\Edge` | `BackgroundModeEnabled` | REG_DWORD | `0` | Blocheaza rularea proceselor Edge in fundal |
| `HKCU:\Software\Microsoft\InputPersonalization` | `RestrictImplicitInkCollection` | REG_DWORD | `1` | Opreste telemetria pe scrisul de mana |
| `HKCU:\Software\Microsoft\InputPersonalization` | `RestrictImplicitTextCollection` | REG_DWORD | `1` | Opreste telemetria pe tastare (keylogging telemetric) |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection` | `AllowTelemetry` | REG_DWORD | `0` | Dezactiveaza telemetria standard Windows |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection` | `MaxTelemetryAllowed` | REG_DWORD | `0` | Limiteaza telemetria maxima la nivelul zero (Securitate) |
| `HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard` | `EnableVirtualizationBasedSecurity` | REG_DWORD | `0` | Dezactiveaza VBS pentru eliminarea penalizarii de FPS |
| `HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` | `Enabled` | REG_DWORD | `0` | Dezactiveaza Memory Integrity (HVCI) |
| `HKCU:\Software\Microsoft\GameBar` | `AutoGameModeEnabled` | REG_DWORD | `1` | Activeaza Windows 11 Game Mode |
| `HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers` | `HwSchMode` | REG_DWORD | `2` | Activeaza Hardware-Accelerated GPU Scheduling (HAGS) |
| `HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}` | `TcpAckFrequency` | REG_DWORD | `1` | Dezactiveaza intarzierea ACK (Nagle off) |
| `HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}` | `TCPNoDelay` | REG_DWORD | `1` | Trimite pachetele TCP imediat (latenta minima) |
| `HKLM:\SYSTEM\CurrentControlSet\Control` | `SvcHostSplitThresholdInKB` | REG_DWORD | `67108864` | Grupeaza procesele SvcHost (prag de 64 GB) |
| `HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` | `ExcludeWUDriversInQualityUpdate` | REG_DWORD | `1` | Blocheaza suprascrierea driverelor video de Windows Update |
| `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching` | `SearchOrderConfig` | REG_DWORD | `0` | Nu cauta drivere periferice pe Windows Update |
| `HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling` | `PowerThrottlingOff` | REG_DWORD | `1` | Opreste reducerea frecventei CPU prin Power Throttling |
| `HKCU:\Control Panel\Desktop` | `FontSmoothing` | REG_SZ | `"2"` | Mentine activ sistemul de netezire a fonturilor ClearType |
| `HKCU:\Control Panel\Desktop` | `FontSmoothingType` | REG_DWORD | `2` | Tip netezire fonturi CRT/LCD ClearType |
| `HKCU:\Control Panel\Desktop\WindowMetrics` | `MinAnimate` | REG_DWORD | `0` | Elimina animatiile de minimizare/maximizare ferestre |
| `HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` | `TaskbarAnimations` | REG_DWORD | `0` | Opreste animatiile barei de activitati |
| `HKCU:\Control Panel\Keyboard` | `PrintScreenKeyForSnippingEnabled` | REG_DWORD | `1` | Asigura deschiderea Snipping Tool la tasta PrintScreen |
| `HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting` | `Disabled` | REG_DWORD | `1` | Dezactiveaza trimiterea rapoartelor de eroare Windows |

---

## 4. Matricea Serviciilor Windows Gestionate

| Nume Serviciu | Denumire Completa | Stare Implicita Windows | Stare Aplicata de NexWin | Ratiune si Beneficiu |
| :--- | :--- | :--- | :--- | :--- |
| `DiagTrack` | Connected User Experiences and Telemetry | Automatic | Disabled / Stopped | Opreste monitorizarea utilizarii si consumul de disc/CPU |
| `dmwappushservice` | Device Management WAP Push Service | Automatic | Disabled / Stopped | Serviciu telemetric redundant |
| `DPS` | Diagnostic Policy Service | Automatic | Disabled / Stopped | Diagnosticare de fundal redundanta |
| `WdiServiceHost` | Diagnostic Service Host | Manual | Disabled / Stopped | Opreste analizele de performanta automate Windows |
| `WdiSystemHost` | Diagnostic System Host | Manual | Disabled / Stopped | Opreste scrierea permanenta a jurnalelor de diagnostic |
| `WerSvc` | Windows Error Reporting | Manual | Disabled / Stopped | Blocheaza generarea de rapoarte si trimiterea lor catre MS |
| `AIFabricService` | Windows AI Fabric Service | Automatic | Disabled / Stopped | Opreste sarcinile de fundal ale modelului AI local |
| `MapsBroker` | Downloaded Maps Manager | Automatic | Disabled / Stopped | Inutil pe PC-uri desktop si laptopuri de gaming |
| `lfsvc` | Geolocation Service | Manual | Disabled / Stopped | Opreste localizarea geografica si economiseste baterie/CPU |
| `TrkWks` | Distributed Link Tracking Client | Automatic | Disabled / Stopped | Urmarire legaturi fisiere NTFS intre retele, inutil acasa |
| `RetailDemo` | Retail Demo Service | Manual | Disabled / Stopped | Serviciu demonstrativ pentru magazine |
| `RemoteRegistry` | Remote Registry | Disabled | Disabled / Stopped | Intareste securitatea blocand accesul remote la registri |
| `shpamsvc` | Shared PC Account Manager | Disabled | Disabled / Stopped | Inutil pe calculatoare personale individuale |
| `PhoneSvc` | Phone Service | Manual | Disabled / Stopped | Integrare telefonica mobila redundanta |
| `PcaSvc` | Program Compatibility Assistant | Automatic | Disabled / Stopped | Opreste ferestrele pop-up de compatibilitate pentru jocuri |
| `DoSvc` | Delivery Optimization | Automatic | Disabled / Stopped | Previne utilizarea conexiunii de internet pentru upload P2P |
| `SysMain` | Superfetch / SysMain | Automatic | Optional Disabled | Recomandat pentru oprire pe SSD-uri NVMe/SATA rapide |
| `WSearch` | Windows Search Indexing | Automatic | Optional Disabled | Opreste indexarea continua I/O daca se doreste disc 0% load |
| `camsvc` | Capability Access Manager | Manual | Manual / Started | **Pastrat activ** pentru functionarea Snipping Tool / Screenshot |
| `CDPSvc` | Connected Devices Platform Service | Automatic | Automatic / Started | **Pastrat activ** pentru clipboard sincronizat si screenshot |

---

## 5. Structura actuala a Proiectului in Depozit (v2.0.0)

Structura folosita de build-ul livrat este:

```
NexWin/
|-- native/
|   |-- NexWin.Native.csproj       # C# WinForms net10.0-windows
|   `-- Program.cs                  # UI nativa si orchestrarea PowerShell
|-- scripts/powershell/             # Actiuni Windows validate
|-- installer/NexWin-Native.iss     # Configurarea Inno Setup
|-- native_stage/NexWin/            # Payload curat pentru installer
|-- dist-installer/                 # NexWin-Setup-v2.0.0-native.exe
|-- public/logo.ico                 # Pictograma aplicatiei
|-- README.md
`-- DOCUMENTATIE_TEHNICA.md
```

Payload-ul v2.0.0 nu include cod React/TypeScript, runtime Node.js, server Express, WebView2 sau dependinte npm.

### 5.1. Structura prototipului web (istoric)

Urmatorul inventar este pastrat doar ca istoric al primei implementari si nu este folosit de installerul v2.0.0.

```
NexWin/
|-- bin/
|   `-- node.exe                      # Runtime Node.js portabil (LTS)
|-- dist/                             # Build-ul static minificat al interfetei React
|-- dist-installer/
|   `-- NexWin-Setup.exe              # Kit-ul de instalare final complet (Inno Setup)
|-- dist-server/
|   `-- index.js                      # Serverul backend Express bundlat intr-un singur fisier
|-- public/
|   |-- logo.ico                      # Pictograma oficiala a aplicatiei
|   `-- logo.png
|-- scripts/
|   `-- powershell/                   # Scripturile de sistem ce aplica optimizarile
|       |-- Get-DiskAnalyzer.ps1
|       |-- Get-ProcessList.ps1
|       |-- Get-StartupApps.ps1
|       |-- Get-SystemStatus.ps1
|       |-- Invoke-AiRemoval.ps1
|       |-- Invoke-ClassicApps.ps1
|       |-- Invoke-DebloatServices.ps1
|       |-- Invoke-DiskCleanup.ps1
|       |-- Invoke-DnsBenchmark.ps1
|       |-- Invoke-GamingTweaks.ps1
|       |-- Invoke-GpuDriverProtection.ps1
|       |-- Invoke-LiveProcessDebloat.ps1
|       |-- Invoke-Maintenance.ps1
|       |-- Invoke-OneClickBoost.ps1
|       |-- Invoke-ProcessLasso.ps1
|       |-- Invoke-RestorePoint.ps1
|       |-- Invoke-RestoreScreenshot.ps1
|       |-- Invoke-UsbOptimization.ps1
|       |-- Invoke-VisualEffects.ps1
|       |-- Restore-FontSmoothing.ps1
|       |-- Stop-ProcessSafe.ps1
|       |-- Save-NexWinSnapshot.ps1
|       |-- Restore-NexWinSnapshot.ps1
|       `-- Set-StartupApp.ps1
|-- server/
|   |-- disk-scanner.js               # Motor nativ asincron de analiza spatiu disc (WinDirStat)
|   |-- operations.js                 # Lista alba si validare operatii/scripturi
|   |-- profile-store.js              # Profiluri si istoric profil activ
|   |-- benchmark-store.js             # Snapshot-uri de performanta
|   |-- audit-log.js                  # Jurnal local al actiunilor
|   |-- index.js                      # Router API Express si emitator Server-Sent Events (SSE)
|   `-- powershell-runner.js          # Executor asincron de scripturi PowerShell cu streaming
|-- src/                              # Codul sursa frontend (React + TypeScript)
|   |-- App.tsx                       # Componenta radacina, logica globala si SSE handler
|   |-- components/
|   |   |-- Header.tsx                # Bara superioara de stare si informatii sistem
|   |   |-- Sidebar.tsx               # Meniu navigare si badge numar procese
|   |   `-- TerminalDrawer.tsx        # Consola live cu bara de progres si ETA
|   |-- views/
|   |   |-- AiRemovalView.tsx         # Panou control granular eliminare Copilot/Recall
|   |   |-- ClassicAppsView.tsx       # Panou restaurare aplicatii clasice
|   |   |-- DashboardView.tsx         # Panou principal si buton One-Click Boost
|   |   |-- DiskAnalyzerView.tsx      # WinDirStat Pro: Treemap, categorii, junk cleaner
|   |   |-- GamingView.tsx            # Panou VBS, HAGS, Game Mode, DNS, USB, Nagle
|   |   |-- ProcessLassoView.tsx      # SmartTrim, prioritati IFEO, ProBalance
|   |   |-- ProcessesView.tsx         # Inspector procese active si kill process
|   |   |-- ServicesView.tsx          # Gestionare servicii, DiagTrack si SvcHost
|   |   |-- StartupView.tsx           # Manager programe la pornirea sistemului
|   |   |-- ProfilesView.tsx          # Profiluri Balanced, Gaming, Privacy, Laptop
|   |   `-- PerformanceView.tsx       # Snapshot-uri si comparatii stare sistem
|   `-- types.ts                      # Definitii TypeScript pentru starea aplicatiei
|-- DOCUMENTATIE_TEHNICA.md           # Acest document
|-- package.json                      # Manifest dependinte Node.js
|-- Start-NexWin.bat                  # Script lansare directa in mod dezvoltare
`-- vite.config.ts                    # Configurare bundler Vite
```

---

## 6. Siguranta, Reversibilitate si Rezilienta

1. **Reversibilitate**:
   - Scripturile `Invoke-AiRemoval.ps1`, `Invoke-DebloatServices.ps1`, `Invoke-GamingTweaks.ps1`, `Invoke-UsbOptimization.ps1` si `Invoke-GpuDriverProtection.ps1` includ parametrul `-Revert`.
   - Inaintea operatiilor cu risc ridicat este salvat un snapshot NexWin cu valorile cunoscute din registry, servicii, DNS, planul de energie si excluderile Defender. Restaurarea este best-effort: politicile GPO, valorile care lipsesc sau setarile care necesita restart pot necesita interventie manuala.
2. **Punct de Restaurare Preventiv**:
   - Inainte de a efectua orice modificare majora prin modulul One-Click Boost, aplicatia creeaza automat un punct de restaurare Windows (`System Restore Point: NexWin Boost`), permitand recuperarea rapida a starii anterioare in caz de necesitate.
3. **Izolare si Protectie Acces Utilizator**:
   - Toate modificarile de registru folosesc blocuri `try / catch` si fallback prin binarul de sistem `reg.exe` pentru a gestiona in siguranta permisiunile stricte sau politicile de grup administrate (GPO).
   - Serviciile critice precum `camsvc`, `CDPSvc` si componentele subsistemului de capturi de ecran sunt protejate si re-activate automat pentru a garanta ca utilitarele zilnice (Snipping Tool, Paint, Clipboard History) raman complet functionale.

4. **Control API local**:
   - API-ul asculta explicit pe `127.0.0.1`, limiteaza CORS la originile aplicatiei, limiteaza corpul requestului si accepta numai scripturi dintr-o lista alba.
   - Actiunile de oprire procese si stergere nu mai construiesc comenzi PowerShell din input arbitrar; procesele protejate si radacinile de sistem sunt respinse.
   - Operatiile importante sunt inregistrate in audit local, iar procesele PowerShell au limita de executie si raport de timeout.

## 7. Profiluri si Performance Lab

Aplicatia include profiluri controlate, cu nivel de risc vizibil:

- **Balanced**: doar modificari cu risc redus.
- **Gaming**: optimizari de energie, GPU si retea; poate modifica VBS/HVCI.
- **Privacy**: elimina componente AI si reduce telemetria.
- **Laptop / Battery**: evita optimizarile agresive de energie si USB.

Performance Lab salveaza snapshot-uri de stare in `%LOCALAPPDATA%\NexWin\native-benchmarks.json` pentru comparatii inainte/dupa. Aplicatia nativa nu scrie in folderul instalat `Program Files`, prevenind eroarea `EPERM` observata in prototipul web.

## 8. Decizia privind limbajul

C# pe .NET 10 LTS este alegerea pentru produsul livrat: are integrare directa cu Win32/Windows Forms, administrare simpla a proceselor si serviciilor, debugging matur si distribuire self-contained. Rust sau C++ pot fi utile pentru un scanner de disc foarte specializat, dar nu aduc un avantaj suficient pentru operatiile de registry si servicii din NexWin. PowerShell ramane stratul compatibil pentru scripturile deja verificate, iar interfata si orchestrarea sunt native.

### 8.1. Arhitectura nativa livrata
- `native/NexWin.Native.csproj`: proiect C# pe `net10.0-windows`.
- `native/Program.cs`: ferestrele WinForms, navigarea, statusul sistemului, confirmari native, log live si rularea controlata a scripturilor.
- `scripts/powershell/`: operatiile Windows existente, pastrate separat si executate prin `powershell.exe` cu lista alba.
- `installer/NexWin-Native.iss`: installerul Inno Setup pentru payload-ul nativ.
- Snapshot-urile Performance Lab folosesc `native-benchmarks.json` si scriere atomică in `%LOCALAPPDATA%\NexWin`, prevenind eroarea `EPERM` observata in prototipul web.
