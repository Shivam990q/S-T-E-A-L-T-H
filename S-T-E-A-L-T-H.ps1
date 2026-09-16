# ==========================================================================================
# StealthShield TITANIUM (v3.0) - The Definitive Windows Privacy and Deep Forensic Suite
# Complete 12-Vector Architecture: Telemetry, Memory, ShellBags, Cryptnet, Network and System
# ==========================================================================================

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

function Show-Header {
    Clear-Host
    Write-Host "==========================================================================================" -ForegroundColor Cyan
    Write-Host "       STEALTHSHIELD TITANIUM (v3.0) - COMPLETE DEEP PRIVACY AND ANTI-FORENSIC SUITE       " -ForegroundColor Green
    Write-Host "==========================================================================================" -ForegroundColor Cyan
    Write-Host " [Host: $env:COMPUTERNAME] | [User: $env:USERNAME] | [Admin Privileges: $isAdmin]" -ForegroundColor Gray
    Write-Host ""
}

# 1. Deep Telemetry, Diagnostics and Cloud Submissions
function Invoke-DeepTelemetry {
    Write-Host "[*] [1/12] Silencing Windows Telemetry, DiagTrack, CEIP and Error Submissions..." -ForegroundColor Yellow
    
    $services = @('DiagTrack', 'dmwappushservice', 'WerSvc', 'DoSvc', 'diagnosticshub.standardcollector.service', 'PcaSvc', 'lfsvc', 'MapsBroker')
    foreach ($svc in $services) {
        if (Get-Service -Name $svc -ErrorAction SilentlyContinue) {
            Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
            Set-Service -Name $svc -StartupType Disabled -ErrorAction SilentlyContinue
            Write-Host "  [+] Service Disabled: $svc" -ForegroundColor Green
        }
    }

    $regTweaks = @(
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"; Name = "AllowTelemetry"; Value = 0; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"; Name = "DoNotShowFeedbackNotifications"; Value = 1; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\SQMClient\Windows"; Name = "CEIPEnable"; Value = 0; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"; Name = "Enabled"; Value = 0; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy"; Name = "TailoredExperiencesWithDiagnosticDataEnabled"; Value = 0; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"; Name = "AllowCortana"; Value = 0; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"; Name = "DisableWebSearch"; Value = 1; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"; Name = "ConnectedSearchUseWeb"; Value = 0; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search"; Name = "BingSearchEnabled"; Value = 0; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search"; Name = "CortanaConsent"; Value = 0; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\InputPersonalization"; Name = "RestrictImplicitInkCollection"; Value = 1; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\InputPersonalization"; Name = "RestrictImplicitTextCollection"; Value = 1; Type = "DWord" },
        @{ Path = "HKCU:\Software\Microsoft\Personalization\Settings"; Name = "AcceptedPrivacyPolicy"; Value = 0; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors"; Name = "DisableLocation"; Value = 1; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors"; Name = "DisableLocationScripting"; Value = 1; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting"; Name = "Disabled"; Value = 1; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting"; Name = "DoReport"; Value = 0; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet"; Name = "SubmitSamplesConsent"; Value = 2; Type = "DWord" },
        @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet"; Name = "SpynetReporting"; Value = 0; Type = "DWord" }
    )

    foreach ($tweak in $regTweaks) {
        if (!(Test-Path $tweak.Path)) { New-Item -Path $tweak.Path -Force | Out-Null }
        Set-ItemProperty -Path $tweak.Path -Name $tweak.Name -Value $tweak.Value -Type $tweak.Type -Force | Out-Null
    }
    Write-Host "[OK] Deep Telemetry and Cloud Snooping Silenced.`n" -ForegroundColor Green
}

# 2. Activity History and Windows Timeline Database
function Invoke-TimelinePurge {
    Write-Host "[*] [2/12] Purging Windows Timeline and Activities Database..." -ForegroundColor Yellow
    
    $activityPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System"
    if (!(Test-Path $activityPath)) { New-Item -Path $activityPath -Force | Out-Null }
    Set-ItemProperty -Path $activityPath -Name "EnableActivityFeed" -Value 0 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $activityPath -Name "PublishUserActivities" -Value 0 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $activityPath -Name "UploadUserActivities" -Value 0 -Type DWord -Force | Out-Null

    $cdpPath = "$env:LOCALAPPDATA\ConnectedDevicesPlatform"
    if (Test-Path $cdpPath) {
        Get-ChildItem -Path $cdpPath -Filter "ActivitiesCache.db*" -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        Write-Host "  [+] ActivitiesCache.db SQLite Database Wiped" -ForegroundColor Green
    }
    Write-Host "[OK] Timeline and User Activities Sanitized.`n" -ForegroundColor Green
}

# 3. Terminal, Console and PowerShell History
function Invoke-TerminalHistorySanitizer {
    Write-Host "[*] [3/12] Sanitizing Terminal, PowerShell and Console History..." -ForegroundColor Yellow
    
    try {
        $psHistory = (Get-PSReadLineOption).HistorySavePath
        if (Test-Path $psHistory) {
            Clear-Content -Path $psHistory -Force -ErrorAction SilentlyContinue
            Write-Host "  [+] PowerShell PSReadLine History Wiped ($psHistory)" -ForegroundColor Green
        }
    } catch {}

    $runMru = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU"
    if (Test-Path $runMru) {
        Remove-Item -Path $runMru -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -Path $runMru -Force | Out-Null
        Write-Host "  [+] Run Dialog MRU History Cleared" -ForegroundColor Green
    }
    Write-Host "[OK] Terminal and Command History Sanitized.`n" -ForegroundColor Green
}

# 4. Explorer ShellBags, Dialog History and MUICache
function Invoke-ShellBagsDialogPurge {
    Write-Host "[*] [4/12] Sanitizing Explorer ShellBags, OpenSavePidlMRU and MUICache..." -ForegroundColor Yellow
    
    $dialogPaths = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRULegacy",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery"
    )
    foreach ($dp in $dialogPaths) {
        if (Test-Path $dp) {
            Remove-Item -Path $dp -Recurse -Force -ErrorAction SilentlyContinue
            New-Item -Path $dp -Force | Out-Null
            Write-Host "  [+] Cleared: $dp" -ForegroundColor Green
        }
    }

    $muiCache = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"
    if (Test-Path $muiCache) {
        Get-Item -Path $muiCache | ForEach-Object {
            $_.GetValueNames() | ForEach-Object { Remove-ItemProperty -Path $muiCache -Name $_ -Force -ErrorAction SilentlyContinue }
        }
        Write-Host "  [+] MUICache App Execution History Sanitized" -ForegroundColor Green
    }
    Write-Host "[OK] ShellBags and Dialog Traces Sanitized.`n" -ForegroundColor Green
}

# 5. JumpLists, Recent Items and Quick Access MRU
function Invoke-JumpListsRecentPurge {
    Write-Host "[*] [5/12] Purging JumpLists, Recent Items and QuickAccess Cache..." -ForegroundColor Yellow
    
    $recentDirs = @(
        "$env:APPDATA\Microsoft\Windows\Recent",
        "$env:APPDATA\Microsoft\Windows\Recent\AutomaticDestinations",
        "$env:APPDATA\Microsoft\Windows\Recent\CustomDestinations"
    )
    foreach ($rd in $recentDirs) {
        if (Test-Path $rd) {
            Get-ChildItem -Path $rd -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "  [+] JumpLists and Recent Items Completely Wiped" -ForegroundColor Green
    Write-Host "[OK] QuickAccess and Recent Documents Cleared.`n" -ForegroundColor Green
}

# 6. Visual Artifacts, Thumbcache, DirectX Shader and Font Cache
function Invoke-VisualShaderCachesPurge {
    Write-Host "[*] [6/12] Purging Visual Thumbnails, Font Cache and DirectX Shader Caches..." -ForegroundColor Yellow
    
    $explorerCaches = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
    if (Test-Path $explorerCaches) {
        Get-ChildItem -Path $explorerCaches -Filter "thumbcache_*.db" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $explorerCaches -Filter "iconcache_*.db" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
    
    $d3dCache = "$env:LOCALAPPDATA\D3DSCache"
    if (Test-Path $d3dCache) { Remove-Item -Path $d3dCache -Recurse -Force -ErrorAction SilentlyContinue }

    $fontCache = "$env:LOCALAPPDATA\FontCache"
    if (Test-Path $fontCache) { Remove-Item -Path $fontCache -Recurse -Force -ErrorAction SilentlyContinue }

    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.Clipboard]::Clear()
        Write-Host "  [+] Clipboard History Emptied" -ForegroundColor Green
    } catch {}

    Write-Host "[OK] Visual and Shader Caches Sanitized.`n" -ForegroundColor Green
}

# 7. Forensic Crash Dumps, LiveKernelReports and WER Queues
function Invoke-CrashDumpsWerPurge {
    Write-Host "[*] [7/12] Purging Forensic Crash Dumps, LiveKernelReports and WER Queues..." -ForegroundColor Yellow
    
    $dumpDirs = @(
        "$env:LOCALAPPDATA\CrashDumps",
        "C:\Windows\Minidump",
        "C:\Windows\LiveKernelReports",
        "$env:PROGRAMDATA\Microsoft\Windows\WER\ReportQueue",
        "$env:PROGRAMDATA\Microsoft\Windows\WER\ReportArchive",
        "$env:PROGRAMDATA\Microsoft\Windows\WER\Temp",
        "$env:LOCALAPPDATA\Microsoft\Windows\WER\ReportQueue",
        "$env:LOCALAPPDATA\Microsoft\Windows\WER\ReportArchive"
    )

    foreach ($dd in $dumpDirs) {
        if (Test-Path $dd) {
            Get-ChildItem -Path $dd -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [+] Cleared: $dd" -ForegroundColor Green
        }
    }
    Write-Host "[OK] Forensic Dumps and WER Queues Sanitized.`n" -ForegroundColor Green
}

# 8. CryptnetUrlCache (SSL/TLS Certificate Query Leak Cache)
function Invoke-CryptnetCachePurge {
    Write-Host "[*] [8/12] Purging CryptnetUrlCache (SSL Certificate Resolution Cache)..." -ForegroundColor Yellow
    
    $cryptnetDirs = @(
        "$env:LOCALAPPDATA\Microsoft\CryptnetUrlCache\Content",
        "$env:LOCALAPPDATA\Microsoft\CryptnetUrlCache\MetaData",
        "$env:APPDATA\Microsoft\CryptnetUrlCache\Content",
        "$env:APPDATA\Microsoft\CryptnetUrlCache\MetaData"
    )

    foreach ($cd in $cryptnetDirs) {
        if (Test-Path $cd) {
            Get-ChildItem -Path $cd -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [+] Wiped SSL URL Cache: $cd" -ForegroundColor Green
        }
    }
    Write-Host "[OK] Cryptnet Certificate Caches Sanitized.`n" -ForegroundColor Green
}

# 9. Windows Event Logs Scrubber
function Invoke-EventLogsPurge {
    Write-Host "[*] [9/12] Scrubbing Windows Event Logs..." -ForegroundColor Yellow
    $logs = @('Application', 'Security', 'System', 'Setup', 'Microsoft-Windows-PowerShell/Operational', 'Windows PowerShell', 'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational')
    foreach ($log in $logs) {
        try {
            wevtutil cl $log 2>$null
            Write-Host "  [+] Event Log Cleared: $log" -ForegroundColor Green
        } catch {}
    }
    Write-Host "[OK] Windows Event Logs Scrubbed.`n" -ForegroundColor Green
}

# 10. Network Privacy: DNS, ARP, LLMNR, WPAD and Random MAC
function Invoke-DeepNetworkHardening {
    Write-Host "[*] [10/12] Hardening Network, Flushing ARP, DNS and Disabling WPAD/LLMNR..." -ForegroundColor Yellow
    
    Clear-DnsClientCache
    Write-Host "  [+] DNS Resolver Cache Flushed" -ForegroundColor Green

    try {
        netsh interface ip delete arpcache | Out-Null
        Write-Host "  [+] ARP Cache Flushed" -ForegroundColor Green
    } catch {}

    $wifiAdapters = Get-NetAdapter | Where-Object { $_.PhysicalMediaType -eq 'Native 802.11' }
    foreach ($adapter in $wifiAdapters) {
        Set-NetAdapterAdvancedProperty -Name $adapter.Name -DisplayName "Random MAC Address" -DisplayValue "Enabled" -ErrorAction SilentlyContinue
        Write-Host "  [+] Random MAC Enforced for: $($adapter.Name)" -ForegroundColor Green
    }

    $llmnrPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient"
    if (!(Test-Path $llmnrPath)) { New-Item -Path $llmnrPath -Force | Out-Null }
    Set-ItemProperty -Path $llmnrPath -Name "EnableMulticast" -Value 0 -Type DWord -Force | Out-Null
    Write-Host "  [+] LLMNR Broadcasts Disabled" -ForegroundColor Green

    $wpadPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Wpad"
    if (!(Test-Path $wpadPath)) { New-Item -Path $wpadPath -Force | Out-Null }
    Set-ItemProperty -Path $wpadPath -Name "WpadOverride" -Value 1 -Type DWord -Force | Out-Null
    Write-Host "  [+] WPAD Proxy Probing Disabled" -ForegroundColor Green

    Write-Host "[OK] Network Stealth Hardening Completed.`n" -ForegroundColor Green
}

# 11. Delivery Optimization and SmartScreen Telemetry
function Invoke-DeliveryOptPrivacy {
    Write-Host "[*] [11/12] Disabling P2P Delivery Optimization and Cloud SmartScreen Probing..." -ForegroundColor Yellow
    
    $doPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization"
    if (!(Test-Path $doPath)) { New-Item -Path $doPath -Force | Out-Null }
    Set-ItemProperty -Path $doPath -Name "DODownloadMode" -Value 0 -Type DWord -Force | Out-Null
    Write-Host "  [+] Delivery Optimization P2P Sharing Disabled" -ForegroundColor Green

    $smartPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System"
    if (!(Test-Path $smartPath)) { New-Item -Path $smartPath -Force | Out-Null }
    Set-ItemProperty -Path $smartPath -Name "EnableSmartScreen" -Value 0 -Type DWord -Force | Out-Null
    Write-Host "  [+] SmartScreen Application Telemetry Disabled" -ForegroundColor Green

    Write-Host "[OK] Delivery Optimization and Cloud Probing Disabled.`n" -ForegroundColor Green
}

# 12. Multi-Browser Shader, GPU and Temp Caches Sanitization
function Invoke-MultiBrowserSanitizer {
    Write-Host "[*] [12/12] Sanitizing Multi-Browser Temp and Shader Caches (Brave, Chrome, Edge, Firefox)..." -ForegroundColor Yellow
    
    $browserPaths = @(
        "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data\Default\Cache",
        "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data\Default\GPUCache",
        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache",
        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\GPUCache",
        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache",
        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\GPUCache"
    )

    foreach ($bp in $browserPaths) {
        if (Test-Path $bp) {
            Get-ChildItem -Path $bp -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [+] Purged: $bp" -ForegroundColor Green
        }
    }
    Write-Host "[OK] Multi-Browser Temporary Caches Sanitized.`n" -ForegroundColor Green
}

# Master 1-Click Execution
function Run-MasterTitaniumStealth {
    Show-Header
    Write-Host "EXECUTING MASTER TITANIUM STEALTH PROTOCOL (ALL 12 VECTORS)..." -ForegroundColor Cyan
    Write-Host "------------------------------------------------------------------------------------------" -ForegroundColor Gray
    Invoke-DeepTelemetry
    Invoke-TimelinePurge
    Invoke-TerminalHistorySanitizer
    Invoke-ShellBagsDialogPurge
    Invoke-JumpListsRecentPurge
    Invoke-VisualShaderCachesPurge
    Invoke-CrashDumpsWerPurge
    Invoke-CryptnetCachePurge
    Invoke-EventLogsPurge
    Invoke-DeepNetworkHardening
    Invoke-DeliveryOptPrivacy
    Invoke-MultiBrowserSanitizer
    Write-Host "==========================================================================================" -ForegroundColor Cyan
    Write-Host "100% COMPLETE TITANIUM STEALTH PROTOCOL EXECUTED ACROSS ALL 12 VECTORS!" -ForegroundColor Green
    Write-Host "==========================================================================================" -ForegroundColor Cyan
    Write-Host "Press any key to return to menu..." -ForegroundColor Gray
    [Console]::ReadKey() | Out-Null
}

function Show-Menu {
    while ($true) {
        Show-Header
        Write-Host "Select a Titanium Module:" -ForegroundColor White
        Write-Host " [1] 1-Click Master Titanium Stealth (ALL 12 VECTORS IN ONE SHOT)" -ForegroundColor Cyan
        Write-Host " [2] Deep Telemetry, CEIP, Location and Defender Cloud Samples Off" -ForegroundColor Yellow
        Write-Host " [3] Wipe Windows Timeline and Activities Database" -ForegroundColor Yellow
        Write-Host " [4] Sanitize Terminal and RunMRU Command History" -ForegroundColor Yellow
        Write-Host " [5] ShellBags, OpenSaveDialogs and MUICache Execution History" -ForegroundColor Yellow
        Write-Host " [6] Purge JumpLists, Recent Items and QuickAccess MRU" -ForegroundColor Yellow
        Write-Host " [7] Visual Thumbnails, FontCache and DirectX Shader Caches" -ForegroundColor Yellow
        Write-Host " [8] Crash Dumps, LiveKernelReports and WER Queues Purge" -ForegroundColor Yellow
        Write-Host " [9] CryptnetUrlCache (SSL/TLS Certificate Query Leak Cache)" -ForegroundColor Yellow
        Write-Host " [10] Scrub Windows Event Logs (System, Security, PowerShell)" -ForegroundColor Yellow
        Write-Host " [11] Network Stealth (DNS, ARP, LLMNR, WPAD and Random MAC)" -ForegroundColor Yellow
        Write-Host " [12] Multi-Browser GPU, Shader and Temporary Caches" -ForegroundColor Yellow
        Write-Host " [0] Exit" -ForegroundColor Red
        Write-Host ""
        $choice = Read-Host "Enter Choice (0-12)"
        
        switch ($choice) {
            '1' { Run-MasterTitaniumStealth }
            '2' { Invoke-DeepTelemetry; Pause }
            '3' { Invoke-TimelinePurge; Pause }
            '4' { Invoke-TerminalHistorySanitizer; Pause }
            '5' { Invoke-ShellBagsDialogPurge; Pause }
            '6' { Invoke-JumpListsRecentPurge; Pause }
            '7' { Invoke-VisualShaderCachesPurge; Pause }
            '8' { Invoke-CrashDumpsWerPurge; Pause }
            '9' { Invoke-CryptnetCachePurge; Pause }
            '10' { Invoke-EventLogsPurge; Pause }
            '11' { Invoke-DeepNetworkHardening; Pause }
            '12' { Invoke-MultiBrowserSanitizer; Pause }
            '0' { return }
            default { Write-Host "Invalid Selection!" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

Show-Menu
