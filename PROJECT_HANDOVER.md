# NexWin - Project Technical Handover & Architecture Map

> **Purpose:** Acest fisier serveste drept documentatie completa a arhitecturii, componentelor implementate si starii curente a proiectului **NexWin**. Orice AI sau dezvoltator care preia proiectul poate continua implementarea direct din acest punct, fara a fi nevoie sa analizeze de la zero toate fisierele din solutie.

---

## 1. Structura Generala a Proiectului

- **Tehnologie:** .NET 10.0 Windows Desktop (WPF nativ C#, Win32 P/Invoke, WMI, Windows Registry).
- **Tip Executabil:** `WinExe` (aplicatie GUI complet nativa, fara Electron sau WebViews).
- **Directoare cheie:**
  - `native/` - Contine tot codul sursa nativ C#/WPF:
    - `NexWin.Native.csproj` - Fisierul de proiect .NET 10 (target: `net10.0-windows`, `<UseWPF>true</UseWPF>`).
    - `MainWindow.xaml` - Resurse globale de design: tematica Dark Slate, pensule de culoare, template-uri de butoane (`BlueGradientButtonStyle`, `DarkDangerOutlineButtonStyle`, `DarkTableFooterButtonStyle`, `SecondaryButtonStyle`), stil de CheckBox rotunjit custom, stil de meniu contextual intunecat (`DarkContextMenuStyle`, `DarkMenuItemStyle`), stil invizibil pentru ScrollBar (latime 0).
    - `MainWindow.xaml.cs` - Controlerul principal al interfetei, gestionarea navigarii, randarea sincrona STA a fiecarei pagini, logica evenimentelor UI.
    - `NativeTuning.cs` - Motorul backend nativ: interactiunea cu Windows Registry (HKCU / HKLM), P/Invoke Win32 (`SetPriorityClass`, `EmptyWorkingSet`, `GlobalMemoryStatusEx`, `CreateProcess`), WMI (detectie CPU/GPU/RAM/Disk/SMART), scanare dinamica jocuri (Steam, Riot, Roblox, Epic), Snapshots, Windows Customizer, Network Lab, Speedtest CLI Ookla, Steam Lite launcher.
    - `Bootstrap.cs` - Punctul de intrare `Main()`, instantierea aplicatiei si suportul pentru capturi de ecran automate fara fereastra (`--screenshot-page <PageName> <OutPath>`).
    - `assets/` - Resurse grafice (`logo_ribbon.png`, `speedtest.exe`, iconite extrase din executabile).

---

## 2. Standarde Vizuale & Reguli de Design (Stricte)

1. **Regula Fara Em-Dashes:**
   - Nu se folosesc caractere em-dash sau en-dash. Se utilizeaza exclusiv cratime / liniute obisnuite ASCII (`-`).
2. **Date Reale (Fara Placeholders):**
   - Telemetria hardware afiseaza date reale din sistem: procesor i9-13900HX, placa grafica RTX 4070 Laptop (interogata prin `nvidia-smi`), 31.7 GB RAM DDR5 Micron Dual-Channel, SSD Samsung 970 EVO Plus 2TB cu telemetrie SMART si utilizare disc reala.
3. **Paleta de Culori (Dark Modern Minimalist):**
   - Fundal Principal: `#070D18` / `#091120`
   - Fundal Carduri: `#0C1526` / `#0F1A2E` (cu bordura `#1E293B`)
   - Accent Principal: `#2563EB` / `#3B82F6` (Gradient: `#3B82F6` -> `#1D4ED8`)
   - Accent Gaming / Selectat: `#A855F7` (Violet neon cu stralucire)
   - Accent Secundar / Text Activ: `#38BDF8` (Cyan)
   - Succes / Activ: `#10B981` (Verde Smarald)
   - Avertisment / Diagnoza: `#F59E0B` (Chihlimbar)
   - Pericol / Oprire: `#DC2626` / `#EF4444` (Rosu)
   - Text Principal: `#F1F5F9` (Alb curat)
   - Text Muted / Secundar: `#64748B` / `#94A3B8` (Slate Muted)
   - **Interzis roz/magenta pe butoane:** Butoanele primare folosesc gradientul albastru regal (`BlueGradientButtonStyle`) sau violetul de gaming (`#A855F7`).
4. **Scrollbar-uri:**
   - **Complet invizibile:** ScrollBar are latime 0 in `MainWindow.xaml`, iar derularea se face exclusiv prin `ScrollViewer` cu rotita mouse-ului (`VerticalScrollBarVisibility="Hidden"`).
5. **Emoji-uri:**
   - **Complet interzise:** Toate iconitele sunt cai vectoriale SVG (`CreateVectorIcon`) randate curat prin geometrie WPF sau iconite reale extrase din executabile Windows.
6. **Regula WPF STA Threading:**
   - Metodele de afisare a paginilor (`ShowPage()`) trebuie sa ramana sincrone (`void ShowPage()`), invocand initial starea rapida sincrona, iar orice operatie de background grea (retea, scanare fisiere, speedtest) se ruleaza pe `Task.Run` cu actualizare ulterioara pe `Dispatcher.Invoke(...)`.

---

## 3. Harta Rutei & Modulul Curent

Ruta este gestionata in `MainWindow.xaml.cs` prin metoda `NavigateTo(string page)`:

| Nume Ruta | Pagina Corespunzatoare | Fisier & Metoda | Stare Implementare |
| :--- | :--- | :--- | :--- |
| `Dashboard` | Dashboard General | `MainWindow.xaml.cs` -> `ShowDashboard()` | Complet, metrici CPU/RAM/GPU live in header. |
| `Profiles` | Profiluri & Presets | `MainWindow.xaml.cs` -> `ShowProfiles()` | Profiluri: Gamer, Minimalist, Baterie/Work. |
| `Performance` / `Hardware` | Hardware & Thermals | `MainWindow.xaml.cs` -> `ShowPerformance()` | Complet pixel-perfect conform designului: telemetrie CPU reala din Thermal Zone (~76°C), GPU RTX 4070, RAM DDR5 4800 MT/s, SSD NVMe Samsung 970 EVO Plus 2TB, 5 bare temperaturi, 6 status pills, 6 actiuni rapide. Eliminat badge-ul live de sus si bara de footer de jos. |
| `Gaming` | Gaming Optimizations | `MainWindow.xaml.cs` -> `ShowGaming()` | Complet: Filtrare stricta doar pe jocurile fizic instalate pe disc, buton direct de lansare joc `[ ▶ Lansează jocul ]`, buton cautare jocuri pe PC, card joc selectat cu metadate reale (Cale, Prioritate nuclee, GPU Dedicat RTX 4070), buton toggle violet, Steam Gaming Lite RAM optimizer. Fara sub-tabs redundante, fara benchmark DNS redundant. |
| `AI` | AI & Recall Removal | `MainWindow.xaml.cs` -> `ShowAi()` | Complet: Copilot, Recall, Edge AI, Telemetrie Ink/Typing blocate. Culori corectate (albastru pentru activare). |
| `Services` | Services & Debloat | `MainWindow.xaml.cs` -> `ShowServices()` | Complet: Dezactivare servicii inutile (DiagTrack, SysMain etc.). |
| `Disk` / `DiskTree` | Disk Analyzer & Storage | `MainWindow.xaml.cs` -> `ShowDisk()` | Complet: TreeSize asincron non-blocking cu caching, Donut 33%, tab-uri, File Manager cu footer fixat. Mesaj curat de incarcare fara jargon tehnic. |
| `Processes` | Process Inspector | `MainWindow.xaml.cs` -> `ShowProcesses()` | Complet: Butoane disc eliminate, RAM Working Sets button, dark context menu, cautare live in timp real, checkbox select all / deselect all sincronizat. |
| `Customizer` | Windows Customizer | `MainWindow.xaml.cs` -> `ShowCustomizer()` | Complet: Tweak-uri Taskbar, File Explorer, Start Menu, UI animatii + Restart Explorer. |
| `Network` | Network Lab & Speedtest | `MainWindow.xaml.cs` -> `ShowNetwork()` | Complet: Adaptor stats, ping live servere competitive gaming, card integrat Speedtest Rețea (Download, Upload, Ping, Jitter, ISP, Server) cu progres în 3 etape și link raport online, unelte 1-click reparare retea. Titlu curat fără referințe Ookla/CLI. |
| `Snapshot` | NexWin System Snapshot | `MainWindow.xaml.cs` -> `ShowSnapshot()` | Complet: Banner UNDO EVERYTHING, creare snapshots, restaurare/stergere. |
| `Startup` | Startup Manager | `MainWindow.xaml.cs` -> `ShowStartup()` | Complet: Detectare aplicatii pornire, masurare impact, optiune adaugare aplicatie custom. |
| `Apps` | Software Installer | `MainWindow.xaml.cs` -> `ShowApps()` | Complet: Catalog aplicatii stil Ninite cu iconite reale si verificare versiuni instalate. |

---

## 4. Metode & Clase Cheie in `NativeTuning.cs`

- **Hardware Telemetry**:
  - `HardwareTelemetry GetHardwareTelemetry(double currentCpuLoad = -1.0)` - Returneaza date reale CPU (clock dinamic, temp live din `\Thermal Zone Information(*)\High Precision Temperature`, load, power, voltage), GPU (RTX 4070 prin nvidia-smi: temp, load, VRAM, clock, power), RAM (GlobalMemoryStatusEx, canale, viteza), SSD (Samsung 970 EVO Plus, SMART, ore, GB liberi/ocupati).
- **Ethernet & Internet Speedtest**:
  - `RunSpeedtestAsync(Action<string>? onProgress)` - Ruleaza binarul oficial Speedtest (`assets/speedtest.exe`) cu `--format=json --accept-license --accept-gdpr`, parseaza debitele reale de Download, Upload, Ping latency, Jitter, ISP, Server si link raport online.
- **Steam Gaming Lite & RAM Optimizer**:
  - `LaunchSteamLiteAsync()` - Porneste Steam in Small Mode securizat (`+open steam://open/minigameslist -nointro -nobigpicture -vrdisable -silent`).
  - `TrimSteamWorkingSet()` - Win32 `EmptyWorkingSet` dedicat proceselor `steam` si `steamwebhelper`.
- **Windows Customizer**:
  - `WindowsCustomizerState GetCustomizerItems()` - Verifica starea cheilor de registry Windows.
  - `SetCustomizerItem(string id, bool enable)` - Modifica registri pentru Taskbar, Explorer, Start, UI.
  - `RestartExplorerAsync()` - Opreste si reporneste `explorer.exe`.
- **System Snapshots & Undo Everything**:
  - `CreateSnapshot(string name, string desc)` - Salveaza starea curenta a serviciilor si registrilor cheie.
  - `List<SystemSnapshot> GetSnapshots()` - Listeaza punctele de salvare salvate pe disc in JSON.
  - `RestoreSnapshot(string id)` - Aplica starea salvata.
  - `UndoEverythingAsync()` - Reseteaza toate optimizarile la valorile default din fabrica Windows.
- **Network Lab**:
  - `NetworkLabStatus GetNetworkLabStatus()` & `GetNetworkLabStatusAsync()` - Informatii placa de retea.
  - `List<GamingServerPing> GetDefaultGamingServers()` & `PingGamingServersAsync()` - Masurare ping servere gaming.
  - `FlushDnsAsync()`, `ResetWinsockAsync()`, `ResetTcpIpAsync()`, `RenewIpAsync()` - Reparatii retea cu 1 click.

---

## 5. Ghid de Compilare si Rulare

Pentru a compila si valida modificarile fara a lasa procesul agatat:
```powershell
# 1. Oprire instante NexWin existente
Stop-Process -Name "NexWin" -Force -ErrorAction SilentlyContinue

# 2. Compilare in modul Release
dotnet build .\native\NexWin.Native.csproj -c Release

# 3. Captura de ecran automata pentru verificare vizuala (QA)
$p = Start-Process -FilePath ".\native\bin\Release\net10.0-windows\NexWin.exe" -ArgumentList "--screenshot-page", "Hardware", "$PWD\qa-hardware-v14.png" -Wait -PassThru
```
