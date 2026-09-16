# 🛡️ S-T-E-A-L-T-H (v7.0 Absolute)

> **Absolute Forensic Annihilation, Windows Deep Privacy & Hardware/ID Spoofing Suite**  
> Native C# WPF Desktop GUI with Real-Time System HUD, 34 Defense Vectors, 90+ Micro-Action Triggers, and Standalone PowerShell Automation Core.

---

## 🌟 Overview

**S-T-E-A-L-T-H v7.0** is an enterprise-grade Windows privacy, anti-forensics, and identity camouflage suite. It is engineered to perform surgical and deep sanitization across modern Windows systems (Windows 10 & 11), neutralizing telemetry trackers, forensic execution artifacts, hardware identifiers, and OS surveillance mechanisms.

---

## 📊 Quick Specifications

| Specification | Details |
| :--- | :--- |
| **Current Version** | **v7.0 ABSOLUTE** |
| **Forensic Vectors** | **34 Comprehensive Vectors** (0, 00, 1 through 32) |
| **Granular Controls** | **90+ Individual Micro-Execution Buttons** |
| **Primary Interface** | Native C# WPF Dark GUI with Live System HUD & Thread-Safe Async Logger |
| **Secondary Engine** | Standalone 12-Vector PowerShell Core Engine (`S-T-E-A-L-T-H.ps1`) |
| **Test Framework** | Diagnostic Validation Test Suite (`S-T-E-A-L-T-H_test.exe`) |
| **Supported OS** | Windows 10 & Windows 11 (x64) |
| **Required Privileges** | Administrator / Elevated UAC |

---

## 🚀 Complete 34-Vector Architectural Matrix

### 👑 Identity, Hardware & AI Spoofing Vectors
* **Vector 0: Windows Machine GUID, Identity & PC Name Spoof**
  * `0.1 MachineGuid`: Generates randomized cryptographically secure GUID in `HKLM:\SOFTWARE\Microsoft\Cryptography`.
  * `0.2 SQM MachineId`: Spoofs Software Quality Metrics client GUID.
  * `0.3 Registered Owner`: Replaces registered user and organization metadata strings.
  * `0.4 Custom PC/Host Name`: Custom computer name and hostname rotation dialog.
* **Vector 00: Cursor & AI IDE Machine ID Resetter**
  * `devDeviceId`: Randomizes developer device hardware tokens.
  * `macMachineId`: Replaces physical MAC-derived telemetry hashes.
  * `machineId`: Generates fresh SHA-256 machine identifier.
  * `sqmId`: Rotates SQM tracking telemetry ID.
  * `serviceMachineId`: Regenerates service-layer tokens in IDE `storage.json`.

### 🛡️ Windows Telemetry & Cloud Snooping
* **Vector 1: Deep Windows Telemetry & CEIP Silencer**
  * Disables `DiagTrack` (Connected User Experiences and Telemetry), `dmwappushservice`, and `WerSvc`.
  * Neutralizes Advertising ID, Inking/Typing telemetry dictionaries, and Defender cloud sample uploads.
* **Vector 2: Windows Timeline SQLite Database Purge**
  * Obliterates `ActivitiesCache.db` SQLite database in `ConnectedDevicesPlatform`.
  * Disables Activity Feed publishing and Microsoft Account activity synchronization.
* **Vector 11: P2P Delivery Optimization & SmartScreen Probing**
  * Locks `DODownloadMode` to `0` (disables P2P bandwidth harvesting).
  * Suppresses Microsoft SmartScreen outbound file reputation probes.
* **Vector 26: OneDrive Telemetry & Sync Logs**
  * Purges OneDrive diagnostic logs, telemetry event caches, and update records.
* **Vector 27: Windows Recall AI, Copilot & Snapshots (Win11)**
  * Permanently disables Windows Recall AI screenshot tracking and indexing.
  * Terminates Copilot shell integration and wipes stored screen snapshots.
* **Vector 28: Scheduled Task Telemetry Killswitch**
  * Disables Microsoft Compatibility Appraiser, ProgramDataUpdater, and Customer Experience Improvement tasks (Consolidator, KernelCeip, UsbCeip).

### 🧹 Execution Traces & Anti-Forensic Annihilation
* **Vector 3: Terminal, PowerShell & RunMRU Command History Sanitizer**
  * Wipes `ConsoleHost_history.txt` (PSReadLine history).
  * Purges Windows Run dialog (`Win+R`) `RunMRU` registry history.
* **Vector 4: ShellBags, OpenSaveMRU & Dialog Execution History**
  * Sanitizes `OpenSavePidlMRU` and `LastVisitedPidlMRU` registry keys.
  * Erases File Explorer search queries (`WordWheelQuery`).
* **Vector 5: JumpLists & Recent Items MRU Purge**
  * Removes `AutomaticDestinations` and `CustomDestinations` JumpList caches.
  * Empties user Recent Items shortcuts.
* **Vector 6: Visual Thumbnails & DirectX Shader Caches**
  * Deletes Windows thumbnail cache databases (`thumbcache_*.db`).
  * Cleans compiled DirectX shader caches (`D3DSCache`) and wipes the system clipboard.
* **Vector 7: Crash Dumps & Windows Error Reporting (WER)**
  * Deletes kernel minidumps, `LiveKernelReports`, WER report queues, and AppCrash dumps.
* **Vector 8: Cryptnet SSL/TLS Certificate URL Leak Cache**
  * Purges `CryptnetUrlCache\Content` and `CryptnetUrlCache\MetaData` to prevent certificate domain leaks.
* **Vector 9: Windows Security, System & PowerShell Event Logs**
  * Clears Security, System, Application, and PowerShell Operational event logs via `wevtutil`.
* **Vector 13: Prefetch & SuperFetch Execution Traces**
  * Deletes all `.pf` prefetch files, sets Prefetcher policy to `0`, and stops `SysMain`.
* **Vector 14: AmCache Execution History (SHA-1 Hash Logs)**
  * Clears `Amcache.hve` execution records and `RecentFileCache.bcl`.
* **Vector 15: ShimCache (AppCompatCache) Binary Execution Log**
  * Wipes `AppCompatCache` from SYSTEM hive tracking binary executions and paths.
* **Vector 16: UserAssist GUI Execution Tracking (ROT13)**
  * Cleans ROT13-encoded GUI execution history and disables `Start_TrackProgs`.
* **Vector 17: Background & Desktop Activity Monitor (BAM / DAM)**
  * Removes BAM/DAM registry timestamps logging execution times of background processes.
* **Vector 18: System Resource Usage Monitor (SRUM) Database**
  * Stops Data Protection Service (`DPS`) and wipes `SRUDB.dat` tracking app resource/network consumption.
* **Vector 19: TypedPaths, TypedURLs & MUICache Execution**
  * Clears Explorer typed folder paths, legacy browser typed URLs, and `MUICache` binary names.
* **Vector 20: RecentDocs Registry & LNK Shortcut Files**
  * Purges `RecentDocs` MRU lists and erases `.lnk` shortcut files.
* **Vector 21: Windows Push Notification Database (WPN)**
  * Halts WPN services and removes `wpndatabase.db`.
* **Vector 22: Remote Desktop Bitmap Cache & Server History**
  * Clears RDP bitmap cache tiles (`bcache*.bmc`), server MRUs, and connection `.rdp` files.
* **Vector 24: Windows Defender Detection & Quarantine History**
  * Clears local Defender scan history, detection caches, and quarantined items.
* **Vector 25: Cortana, Search History & Search Index**
  * Disables Cortana, wipes device search history, and rebuilds Windows Search index.

### 🌐 Network, System & Hardware Hardening
* **Vector 10: Network & Identity Stealth (DNS, ARP, LLMNR, WPAD)**
  * Flushes DNS cache, clears ARP table, and disables LLMNR and WPAD multicast lookups.
* **Vector 12: Multi-Browser GPU, Shader & Temporary Caches**
  * Cleans temp cache and shader cache across Chrome, Brave, Edge, and Firefox.
* **Vector 23: WiFi Profile History & AutoConnect Settings**
  * Removes saved WiFi profiles and disables automatic open-network connections.
* **Vector 29: Forensic Hardening (Pagefile, Hibernation & NTFS)**
  * Enables `ClearPageFileAtShutdown`, disables hibernation (deletes `hiberfil.sys`), and turns off NTFS `LastAccess` timestamps.
* **Vector 30: Icon Cache & Windows Font Cache Rebuild**
  * Rebuilds Explorer icon cache and purges `FNTCACHE.DAT`.
* **Vector 31: PowerShell Transcript, Module & ScriptBlock Logging**
  * Enforces registry policies to disable PS ScriptBlock logging, Module logging, and Transcription.
* **Vector 32: CapabilityAccessManager Sensor Permissions Reset**
  * Resets Location, Camera, and Microphone access histories and applies global sensor restrictions.

---

## ⚡ 1-Click Master Protocol

Clicking **"⚡ INITIATE 32-VECTOR PROTOCOL ⚡"** in the GUI triggers a coordinated, non-blocking asynchronous pipeline that iterates through every vector in sequence, streaming status messages live into the integrated terminal HUD with millisecond precision.

---

## 📂 Repository Contents

```
S-T-E-A-L-T-H/
├── S-T-E-A-L-T-H.exe        # Pre-compiled Standalone WPF Desktop GUI (v7.0 Absolute)
├── S-T-E-A-L-T-H.cs         # Full C# WPF Application source code (1,387 lines)
├── S-T-E-A-L-T-H.ps1        # Standalone 12-Vector PowerShell Core Engine (360 lines)
├── S-T-E-A-L-T-H_test.exe   # Diagnostic Validation Test Executable
├── S-T-E-A-L-T-H_test.cs    # Test Suite source code
├── README.md                # Comprehensive Architecture & Vector Documentation
└── .gitignore               # System & compilation filter configuration
```

---

## 🛠️ Build & Compilation Instructions

To build `S-T-E-A-L-T-H.exe` from source using the built-in Microsoft .NET Framework C# compiler (`csc.exe`):

```powershell
# Navigate to directory
cd "C:\Users\Rose\.gemini\antigravity-ide\S-T-E-A-L-T-H"

# Compile with native WPF presentation assemblies
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /target:winexe `
    /out:S-T-E-A-L-T-H.exe `
    /reference:PresentationFramework.dll `
    /reference:PresentationCore.dll `
    /reference:WindowsBase.dll `
    /reference:System.Xaml.dll `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Windows.Forms.dll `
    /reference:Microsoft.CSharp.dll `
    S-T-E-A-L-T-H.cs
```

---

## 💻 How to Run

### 1. Launching GUI (Recommended):
Double-click `S-T-E-A-L-T-H.exe` or execute from PowerShell with elevated privileges:
```powershell
Start-Process -FilePath ".\S-T-E-A-L-T-H.exe" -Verb RunAs
```

### 2. Launching Standalone PowerShell Core:
```powershell
powershell -ExecutionPolicy Bypass -File .\S-T-E-A-L-T-H.ps1
```

### 3. Running Diagnostic Tests:
```powershell
.\S-T-E-A-L-T-H_test.exe
```

---

## 🔒 Security & Privacy Notice
* Developed strictly for authorized privacy enhancement, digital hygiene, and defensive security auditing.
* Running with Administrator privileges ensures deep registry and protected folder access.
