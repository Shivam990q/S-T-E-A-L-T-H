# 🛡️ S-T-E-A-L-T-H (v8.0 ABSOLUTE)

> **Absolute Forensic Annihilation, Windows Deep Privacy & Identity Suite**
> Native C# WPF GUI · **46 Vectors · 161 Granular Actions** · Dry-Run Preview · Automatic Backup & Restore · File Quarantine · 3-Pass Shredder · Read-Only Privacy Audit · Task Scheduler · Headless CLI

---

## 🌟 What's new in v8.0

v7.0 had 34 vectors whose "[OK]" messages were printed even when operations silently failed. v8.0 is a ground-up re-engineering:

| Area | v7.0 | v8.0 |
| :--- | :--- | :--- |
| Execution honesty | `SilentlyContinue` everywhere, always "[OK]" | Every op returns OK / FAIL / SKIP with a real reason; live counters in HUD |
| Safety net | none (destructive & irreversible) | Automatic `reg export` backup before every key mutation; files moved to **quarantine** (restorable) instead of deleted; one-click **Restore Session** |
| Preview | none | **Dry-Run mode** — every planned operation is listed, nothing is touched |
| ShellBags | only dialog MRUs (BagMRU untouched!) | Real `BagMRU` + `Bags` keys + dialog MRUs |
| Event logs | 5 channels | Named channels + **Defender Operational** + **sweep of ALL channels** |
| Cloud clipboard | current clipboard only | `AllowClipboardHistory` + `AllowCrossDeviceClipboard` policies |
| Recall (Win11 24H2) | legacy keys only | **`DisableAIDataAnalysis`** (authoritative 24H2 policy) + legacy + snapshot purge |
| SRUM | SRUDB.dat only (ESE logs survived) | Full ESE set: `.dat` + `.log` + `.chk` |
| Defender | direct folder pokes (Tamper blocks) | **`Remove-MpThreat`** API first, then folders; honest failure reporting |
| Browsers | `Default` profile only | **All profiles** (`Default`, `Profile 1..N`, Guest) |
| Crash dumps | minidumps/WER | + `MEMORY.DMP` + `DedicatedDumpFile` + optional `CrashDumpEnabled=0` |
| IDE resets | ID rotation only | + **telemetry forced off** in `settings.json` (backup taken first) |
| New vectors | — | 12 new categories (33–44) below |

---

## 📊 Quick Specifications

| Specification | Details |
| :--- | :--- |
| Version | **v8.0 ABSOLUTE** |
| Vectors | **46 categories** (0, 00, 1–32 original + 33–44 new) |
| Granular actions | **161** micro-action buttons |
| Interface | WPF GUI: **Dark / Light / System themes**, live HUD, result counters, search filter, 5 modals, DPI-aware fit-to-screen |
| Engine | Central Kernel: registry / files / services / processes with Dry-Run + backup hooks |
| Backup store | `C:\ProgramData\STEALTH\backups\session-YYYYMMDD-HHMMSS\` |
| Profiles | 🟢 Safe · 🟡 Balanced · 🔴 Paranoid (destructive) |
| CLI | `/sweep /profile:X /dry` · `/audit /out:...` · `/restore /dir:...` |
| Supported OS | Windows 10 & 11 (x64), elevation required for most vectors |
| Build | .NET Framework 4.x `csc.exe` (C# 5) — see `build.ps1` |

---

## 🗂️ The 46-Vector Matrix

### 👑 Identity & Spoofing (Gold)
- **0 — Machine GUID / SQM / Registered Owner / PC Rename** (per-action + random stealth names)
- **00 — Cursor / VS Code `storage.json` ID reset** (`devDeviceId`, `macMachineId`, `machineId`, `sqmId`) + **telemetry forced off** in `settings.json`

### 🛡️ Telemetry & Cloud (1, 2, 11, 26–28, 36, 39, 44)
DiagTrack/CEIP silencer · Timeline DB · Delivery-Opt + SmartScreen · OneDrive logs · **Recall 24H2 (`DisableAIDataAnalysis`) + Copilot + snapshot purge** · Scheduled-task killswitch · **Privacy toggle pack** (widgets via `Dsh` policy, sponsored content, online speech, feedback, Find-My-Device, background apps, Start recommendations, tailored experiences, Steps Recorder) · **Vendor telemetry** (VS CEIP, Office OSM, .NET CLI opt-out, NVIDIA service, npm/pip logs) · **Legacy WER/CEIP consent locks**

### 🧹 Forensic Artifacts (3–10, 12–25, 30, 32, 34, 35)
Terminal/RunMRU/Windows-Terminal history · **REAL ShellBags** · JumpLists · thumbnails/D3D/**cloud clipboard policies** · crash dumps + MEMORY.DMP · Cryptnet SSL cache · **event logs incl. Defender + ALL-channel sweep** · network stealth (DNS/ARP/LLMNR/WPAD/IPv6 privacy) · browser caches (all profiles) · Prefetch/SysMain · AmCache · ShimCache · UserAssist · BAM/DAM · **SRUM full ESE set** · TypedPaths/URLs/MUICache · RecentDocs/LNK · Notifications DB · RDP artifacts · WiFi profiles (destructive) · **Defender via API** · Cortana/Search · icon+font cache · sensor consent reset · **USN journal + TrayNotify** · **NetworkList / USBSTOR / MountPoints2 / MountedDevices / Bluetooth history**

### 🗄️ Storage, Secure Deletion & System (29, 33, 38, 40)
Pagefile/hibernation/LastAccess hardening · **VSS shadows + System Restore + RegBack + Windows.old** (destructive) · **3-pass shredder + free-space wipe (`cipher /w`) + TEMP sweeps + `$Recycle.Bin` shred** · **CBS/WU-cache/Panther/WDI/spooler/StickyNotes/GameDVR/speech-trained-data/targeted-content/SystemPowerReports sweeps**

### 🌐 Network Blocking & Browsers (37, 41, 42, 43)
**Telemetry hosts block (33 domains, marked & removable) + auto-DoH** · **browser history/cookies erase (all profiles, destructive)** · **taskbar/shell surface reduction** (search highlights, chat/copilot buttons, Meet Now — with UCPD caveats documented) · **developer shell traces** (`.bash_history`, WSL histories, REPL files)

### ⚙️ Engineering (31, 32 included above; plus suite-level)
PowerShell logging killswitch (with security-tradeoff note) · sensor permissions · plus suite-level features: dry-run, backup/restore, quarantine, shredder, audit report, scheduler, search filter, honest counters, CLI mode.

---

## ⚡ Using the GUI

1. Run `S-T-E-A-L-T-H.exe` **as Administrator** (non-elevated runs warn and report failures honestly).
2. Pick a **profile**:
   - 🟢 **Safe** — only safe actions
   - 🟡 **Balanced** — everything non-destructive (default)
   - 🔴 **Paranoid** — includes destructive actions (WiFi wipe, browser history, VSS deletion, free-space wipe)
3. Optional: toggle **DRY-RUN** to preview every planned operation with zero changes.
4. Hit **⚡ INITIATE 46-VECTOR PROTOCOL** or run individual micro-actions / category "Run All" (category masters skip destructive actions).
5. Every line in the stream is a real result: `[OK]`, `[FAIL]` (with reason), or `[SKIP]`. Counters live in the HUD.

**Toolbar tools:**
- **📊 PRIVACY AUDIT** — read-only HTML report to your Desktop (services, policies, artifacts present, hosts/DoH state, SSD detection) — changes nothing.
- **↩ RESTORE SESSION** — pick any backup session; re-imports every exported `.reg` and moves quarantined files back.
- **⏰ SCHEDULER** — registers a daily headless sweep (Task Scheduler, highest privileges) for the chosen profile.
- **🔥 SHRED PATH…** — 3-pass overwrite (random / 0xFF / 0x00) + truncate + delete for any file/folder you paste.
- **👑 PC NAME SPOOF** — custom/random host rename + RegisteredOwner/Org rewrite.

**Search box** filters the 46 cards live by number, tag, title or action name.

**Themes (◐ THEME button):** cycles **Dark → Light → System**. System follows your Windows personalization live (switching Windows theme re-skins the app instantly). Choice persists to `%LOCALAPPDATA%\STEALTH	heme.txt` and is restored on next launch. Every surface — cards, HUD, modals, ComboBox dropdowns, log stream — is themed; the app is also DPI-aware and always opens fully on-screen (fit-to-work-area on scaled displays).

**Rotating MACHINE IDENT:** the HUD's identity tile cycles automatically through every common identity notation every few seconds — `HOST (user)` (Windows), `user@HOST` (SSH/Linux), `HOST\user` (whoami/logon), `DOMAIN\user`, `user@host` lowercase (bash), `user@IPv4` (network), `user@HOST:~$` (terminal prompt), `HOST/user`, `user • HOST`, `USER@HOST`, `host.local (user)` (mDNS), `[user] HOST` (syslog), `HOST:user`, `user@domain` (UPN-ish) — deduplicated per machine (workgroup machines collapse domain-identical forms).

## 💻 Headless CLI

```powershell
# preview (no changes, works even where shells mangle leading-slash args):
.\S-T-E-A-L-T-H.exe /sweep /profile:Safe /dry

# real Balanced sweep with backup + quarantine:
.\S-T-E-A-L-T-H.exe /sweep /profile:Balanced

# everything including destructive (Paranoid):
.\S-T-E-A-L-T-H.exe /sweep /profile:Paranoid

# read-only audit report:
.\S-T-E-A-L-T-H.exe /audit /out:"C:\temp\report.html"

# restore a backup session:
.\S-T-E-A-L-T-H.exe /restore /dir:"C:\ProgramData\STEALTH\backups\session-20260917-030933"
```

## 🛠️ Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1          # build both exes
powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests # build + run test suite
```

Manual (single line per compile):
```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -nologo -target:winexe `
  "-out:S-T-E-A-L-T-H.exe" `
  "-r:$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll" `
  "-r:$env:WINDIR\Microsoft.NET\assembly\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll" `
  "-r:$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" `
  "-r:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" `
  "-r:System.dll" "-r:System.Core.dll" S-T-E-A-L-T-H.cs
```

## 🧪 Testing (sandbox-safe)

`S-T-E-A-L-T-H_test.exe` runs **15 tests** and — unlike v7's live-fire suite — does **not** damage the system:

1. Window + XAML tree resolution · 2. registry integrity (46 cats / unique nums / all actions carry Info) · 3. **full 161-action dry-run sweep** · 4. registry engine roundtrip in `HKCU\Software\STEALTH_TEST` · 5. **reg backup → mutate → restore** roundtrip · 6. file quarantine → restore · 7. shredder destroys content · 8. DirWipe preserves folder · 9. honest stdout/exit-code capture · 10. hosts state detection (read-only) · 11. real HTML audit generation · 12. scheduler query (read-only) · 13. all 46 cards + toolbar + modals wired · 14. **dry-run guard actually blocks writes** · 15. headless CLI dry sweep child-process smoke test.

---

## ⚠️ Known Constraints (by design, documented honestly)

- **Elevation**: non-admin runs make protected operations FAIL loudly instead of pretending.
- **Event 1102**: clearing the Security log always writes a "log cleared" event — unavoidable, documented.
- **Tamper Protection**: Defender history folders may refuse deletes even elevated — the tool reports it and uses the `Remove-MpThreat` API where possible.
- **UCPD**: Windows 11's User Choice Protection Driver resets some per-user `Taskbar*` values; the `Dsh` policy (Cat 36) is the authoritative widget kill.
- **SSD shredding**: wear-leveling makes overwrite-based wiping best-effort on SSDs; the auditor reports your disk media type.
- **Reboot-gated actions** are tagged in each card's SPECS modal (ShimCache, pagefile, PC rename, MountedDevices…).
- **Deliberately excluded**: EDR/AV evasion, timestomping, log forgery, HWID/driver-level spoofing. This is a privacy tool for machines you own — not a malware toolkit.

## 🔒 Security & Privacy Notice
For authorized privacy enhancement, digital hygiene and defensive auditing on systems you own. All registry mutations are export-backed; all file removals are quarantined and restorable via the built-in Restore Session tool.
