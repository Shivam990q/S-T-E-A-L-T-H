# 🛡️ S-T-E-A-L-T-H (StealthShield Titanium v3.0)

> **Advanced Windows Privacy, Anti-Forensic Purge & Hardware/ID Spoofing Suite**  
> Native C# WPF Desktop GUI with Real-Time HUD and 12-Vector PowerShell Core Engine.

---

## 🌟 Overview

**S-T-E-A-L-T-H** is an advanced privacy, anti-forensics, and system camouflage utility engineered for Windows. It provides comprehensive neutralization of Windows telemetry, cloud tracking, forensic trace artifacts, and machine hardware identifiers.

---

## 🚀 Key Features & Architectural Vectors

### 1. 🎭 Identity & Hardware Spoofing
* **MachineGuid Randomization**: Spoofs `HKLM:\SOFTWARE\Microsoft\Cryptography\MachineGuid` on demand.
* **SQM ID & Telemetry GUID Cloaking**: Generates randomized telemetry IDs for SQM client and diagnostics.
* **Registered Owner & Organization**: Re-assigns Windows identity strings to eliminate personal metadata.
* **Windows Computer Rename**: Automated local and network hostname rotation.
* **IDE & Tool Token Reset**: Resets hardware and telemetry keys for modern AI IDEs (e.g., Cursor key reset).

### 2. 🧹 Deep Anti-Forensic Cleaning
* **Activity History & Timeline**: Complete purge of Windows Timeline database and user engagement activities.
* **ShellBags & Explorer Artifacts**: Eliminates Explorer ShellBags, folder view state caches, and MRU lists.
* **Prefetch, Shimcache & Amcache**: Thorough cleanup of application execution caches and OS compatibility shims.
* **Cryptnet SSL/TLS Caches**: Purges SSL revocation caches, thumbprint records, and certificate verification traces.
* **Thumbcache & Iconcache**: Deletes visual folder thumbnails and explorer caches.

### 3. 🔇 Telemetry & Diagnostic Neutralization
* Disables `DiagTrack` (Connected User Experiences and Telemetry), `WerSvc` (Windows Error Reporting), and cloud reporting.
* Blocks telemetry data collection policies in registry (`AllowTelemetry = 0`).
* Suppresses Windows feedback notifications, CEIP, tailored experiences, and Advertising ID.

### 4. 🖥️ Native WPF Dark UI + Live Status HUD
* Sleek dark-mode aesthetic with custom cards, micro-buttons, and execution logs.
* Real-time HUD displaying system identity, active protections, and privilege states.
* Thread-safe background execution (`RunAsync`) with continuous live console streaming.

---

## 📂 Repository Contents

| File | Type | Description |
| :--- | :--- | :--- |
| **`S-T-E-A-L-T-H.exe`** | Binary | Pre-compiled Standalone WPF Desktop Application (165 KB) |
| **`S-T-E-A-L-T-H.cs`** | Source | Full C# WPF Application source code (1,380+ lines) |
| **`S-T-E-A-L-T-H.ps1`** | Script | Standalone 12-Vector PowerShell Automation Engine (20 KB) |
| **`S-T-E-A-L-T-H_test.exe`** | Binary | Diagnostic & Validation Test Suite Executable (173 KB) |
| **`S-T-E-A-L-T-H_test.cs`** | Source | Test suite source code for integrity and API verification |

---

## 🛠️ Build & Compilation Instructions

To recompile `S-T-E-A-L-T-H.cs` from source using the native Windows .NET Framework C# compiler (`csc.exe`):

```powershell
# Navigate to project directory
cd "C:\Users\Rose\.gemini\antigravity-ide\S-T-E-A-L-T-H"

# Compile with WPF references (PresentationFramework, PresentationCore, WindowsBase)
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
    /win32manifest:app.manifest `
    S-T-E-A-L-T-H.cs
```

---

## ⚡ Usage

### Running the Desktop GUI:
* Double-click `S-T-E-A-L-T-H.exe` (run as Administrator for full forensic and registry access).
* Or execute via PowerShell:
  ```powershell
  Start-Process -FilePath ".\S-T-E-A-L-T-H.exe" -Verb RunAs
  ```

### Running the PowerShell Engine:
```powershell
# Run with Administrator privileges
powershell -ExecutionPolicy Bypass -File .\S-T-E-A-L-T-H.ps1
```

---

## 🔒 Security & Privacy
Designed exclusively for authorized privacy protection, security auditing, and local machine hygiene.
