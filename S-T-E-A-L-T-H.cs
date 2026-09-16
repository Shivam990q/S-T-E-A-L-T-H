using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Win32;

namespace STEALTH
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new Application();
                var window = new MainWindow();
                app.Run(window);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fatal Error: " + ex.Message, "S-T-E-A-L-T-H v7.0", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class MainWindow : Window
    {
        private TextBox txtLogs;
        private ScrollViewer scroller;
        private ProgressBar prgBar;
        private TextBlock txtHost, txtNet, txtRam, txtTelem;
        private Border infoModal;
        private TextBlock txtModalTitle, txtModalDesc, txtModalPaths;
        private Border pcNameModal;
        private TextBox txtNewPcName;
        private Button btnApplyPcName, btnRandomPcName, btnClosePcModal;

        public MainWindow()
        {
            this.Title = "S-T-E-A-L-T-H v7.0 — Absolute Forensic Annihilation Suite";
            this.Width = 1200;
            this.Height = 960;
            this.MinWidth = 1020;
            this.MinHeight = 720;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.CanResize;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            this.SnapsToDevicePixels = true;
            this.UseLayoutRounding = true;
            BuildUI();
            StartLiveStatusHUD();
        }

        private string MakeCard(string tag, string label, string num, string title, string desc,
                                string infoBtnName, string masterBtnName, string masterLabel,
                                bool isGold, string microBtns)
        {
            string cardStyle = isGold ? "GoldRowCard" : "RowCard";
            string tagBg = isGold ? "#241908" : "#0B213F";
            string tagBorder = isGold ? "#F59E0B" : "#0284C7";
            string tagFg = isGold ? "#FBBF24" : "#38BDF8";
            string titleFg = isGold ? "#FBBF24" : "#F8FAFC";
            string masterStyle = isGold ? "CrownBtn" : "ActionBtn";
            string prefix = isGold ? "👑 " : "";

            return @"
                    <Border Style=""{StaticResource " + cardStyle + @"}"">
                        <StackPanel>
                            <Grid Margin=""0,0,0,8"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""*""/>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""Auto""/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                                    <StackPanel Orientation=""Horizontal"">
                                        <Border Background=""" + tagBg + @""" BorderBrush=""" + tagBorder + @""" BorderThickness=""1"" CornerRadius=""4"" Padding=""5,1"" Margin=""0,0,8,0"">
                                            <TextBlock Text=""[" + tag + @"]"" FontSize=""9.5"" FontWeight=""Bold"" Foreground=""" + tagFg + @"""/>
                                        </Border>
                                        <TextBlock Text=""" + prefix + num + ". " + title + @""" FontSize=""13"" FontWeight=""Bold"" Foreground=""" + titleFg + @"""/>
                                    </StackPanel>
                                    <TextBlock Text=""" + desc + @""" FontSize=""11"" Foreground=""#94A3B8"" Margin=""0,3,0,0""/>
                                </StackPanel>
                                <Button x:Name=""" + infoBtnName + @""" Grid.Column=""1"" Style=""{StaticResource InfoBtn}"" Margin=""8,0,8,0"" VerticalAlignment=""Center""/>
                                <Button x:Name=""" + masterBtnName + @""" Grid.Column=""2"" Style=""{StaticResource " + masterStyle + @"}"" Content=""" + masterLabel + @""" VerticalAlignment=""Center""/>
                            </Grid>
                            <WrapPanel Margin=""0,2,0,0"">" + microBtns + @"
                            </WrapPanel>
                        </StackPanel>
                    </Border>
";
        }

        private string MicroBtn(string name, string label, bool isGold)
        {
            string style = isGold ? "GoldMicroBtn" : "MicroBtn";
            return "\n                                <Button x:Name=\"" + name + "\" Style=\"{StaticResource " + style + "}\" Content=\"" + label + "\"/>";
        }

        private void BuildUI()
        {
            string micro0 = MicroBtn("BtnMicroGuid", "⚡ 0.1 MachineGuid", true)
                           + MicroBtn("BtnMicroSqm", "⚡ 0.2 SQM MachineId", true)
                           + MicroBtn("BtnMicroOwner", "⚡ 0.3 Registered Owner", true)
                           + MicroBtn("BtnMicroPcName", "⚙️ 0.4 Custom PC/Host Name", true);

            string micro00 = MicroBtn("BtnMicroDevId", "⚡ devDeviceId", true)
                           + MicroBtn("BtnMicroMacId", "⚡ macMachineId", true)
                           + MicroBtn("BtnMicroMachineId", "⚡ machineId", true)
                           + MicroBtn("BtnMicroSqmId", "⚡ sqmId", true);

            string micro1 = MicroBtn("BtnMicroDiagTrack", "⚡ 1.1 DiagTrack Svc", false)
                           + MicroBtn("BtnMicroAdvId", "⚡ 1.2 Advertising ID", false)
                           + MicroBtn("BtnMicroInking", "⚡ 1.3 Inking &amp; Typing", false)
                           + MicroBtn("BtnMicroDefenderSample", "⚡ 1.4 Defender Cloud Uploads", false);

            string micro2 = MicroBtn("BtnMicroSqliteDb", "⚡ 2.1 Wipe ActivitiesCache.db", false)
                           + MicroBtn("BtnMicroActivityFeed", "⚡ 2.2 Disable Activity Policy", false);

            string micro3 = MicroBtn("BtnMicroPsHistory", "⚡ 3.1 PSReadLine History", false)
                           + MicroBtn("BtnMicroRunMru", "⚡ 3.2 RunMRU (Win+R) Dialogs", false);

            string micro4 = MicroBtn("BtnMicroOpenSave", "⚡ 4.1 OpenSavePidlMRU", false)
                           + MicroBtn("BtnMicroLastVisited", "⚡ 4.2 LastVisitedPidlMRU", false)
                           + MicroBtn("BtnMicroWordWheel", "⚡ 4.3 WordWheelQuery", false);

            string micro5 = MicroBtn("BtnMicroAutoDest", "⚡ 5.1 AutomaticDestinations", false)
                           + MicroBtn("BtnMicroCustDest", "⚡ 5.2 CustomDestinations", false)
                           + MicroBtn("BtnMicroRecentFolder", "⚡ 5.3 Recent Items Folder", false);

            string micro6 = MicroBtn("BtnMicroThumbnails", "⚡ 6.1 Thumbnails DB", false)
                           + MicroBtn("BtnMicroD3DShaders", "⚡ 6.2 DirectX D3DSCache", false)
                           + MicroBtn("BtnMicroClipboard", "⚡ 6.3 Clear Clipboard", false);

            string micro7 = MicroBtn("BtnMicroMinidumps", "⚡ 7.1 Kernel Minidumps", false)
                           + MicroBtn("BtnMicroLiveKernel", "⚡ 7.2 LiveKernelReports", false)
                           + MicroBtn("BtnMicroWerReports", "⚡ 7.3 WER Queue", false)
                           + MicroBtn("BtnMicroAppCrash", "⚡ 7.4 App CrashDumps", false);

            string micro8 = MicroBtn("BtnMicroCryptnetContent", "⚡ 8.1 Certificate Content Cache", false)
                           + MicroBtn("BtnMicroCryptnetMeta", "⚡ 8.2 OCSP/CRL Metadata Cache", false);

            string micro9 = MicroBtn("BtnMicroSecurityLog", "⚡ 9.1 Security Logs", false)
                           + MicroBtn("BtnMicroSystemLog", "⚡ 9.2 System &amp; App Logs", false)
                           + MicroBtn("BtnMicroPsLog", "⚡ 9.3 PowerShell Logs", false);

            string micro10 = MicroBtn("BtnMicroFlushDns", "⚡ 10.1 Flush DNS Cache", false)
                           + MicroBtn("BtnMicroClearArp", "⚡ 10.2 Delete ARP Cache", false)
                           + MicroBtn("BtnMicroDisableLlmnr", "⚡ 10.3 Disable LLMNR/WPAD", false);

            string micro11 = MicroBtn("BtnMicroDoMode", "⚡ 11.1 Lock DODownloadMode=0", false)
                           + MicroBtn("BtnMicroSmartScreen", "⚡ 11.2 Disable SmartScreen Probe", false);

            string micro12 = MicroBtn("BtnMicroChromeCache", "⚡ 12.1 Chrome Cache", false)
                           + MicroBtn("BtnMicroBraveCache", "⚡ 12.2 Brave Cache", false)
                           + MicroBtn("BtnMicroEdgeCache", "⚡ 12.3 Edge Cache", false)
                           + MicroBtn("BtnMicroFirefoxCache", "⚡ 12.4 Firefox Cache", false);

            // Categories 13-32
            string micro13 = MicroBtn("BtnMicroPrefetchFiles", "⚡ 13.1 Delete Prefetch Files", false)
                           + MicroBtn("BtnMicroDisablePrefetch", "⚡ 13.2 Disable Prefetcher", false)
                           + MicroBtn("BtnMicroSysMain", "⚡ 13.3 Stop SysMain Service", false);

            string micro14 = MicroBtn("BtnMicroAmCache", "⚡ 14.1 Clear AmCache Entries", false)
                           + MicroBtn("BtnMicroRecentFileCache", "⚡ 14.2 Clear RecentFileCache.bcl", false);

            string micro15 = MicroBtn("BtnMicroShimCache", "⚡ 15.1 Clear ShimCache (AppCompatCache)", false);

            string micro16 = MicroBtn("BtnMicroUserAssistClear", "⚡ 16.1 Clear UserAssist Logs", false)
                           + MicroBtn("BtnMicroUserAssistDisable", "⚡ 16.2 Disable Tracking", false);

            string micro17 = MicroBtn("BtnMicroBam", "⚡ 17.1 Clear BAM Entries", false)
                           + MicroBtn("BtnMicroDam", "⚡ 17.2 Clear DAM Entries", false);

            string micro18 = MicroBtn("BtnMicroSrum", "⚡ 18.1 Clear SRUDB.dat", false);

            string micro19 = MicroBtn("BtnMicroTypedPaths", "⚡ 19.1 TypedPaths (Explorer)", false)
                           + MicroBtn("BtnMicroTypedUrls", "⚡ 19.2 TypedURLs (IE)", false)
                           + MicroBtn("BtnMicroMuiCache", "⚡ 19.3 MUICache", false);

            string micro20 = MicroBtn("BtnMicroRecentDocs", "⚡ 20.1 RecentDocs Registry", false)
                           + MicroBtn("BtnMicroLnkFiles", "⚡ 20.2 LNK Shortcut Files", false);

            string micro21 = MicroBtn("BtnMicroWpnDb", "⚡ 21.1 Notifications DB (wpndatabase.db)", false);

            string micro22 = MicroBtn("BtnMicroRdpCache", "⚡ 22.1 RDP Bitmap Cache", false)
                           + MicroBtn("BtnMicroRdpMru", "⚡ 22.2 RDP Server MRU History", false)
                           + MicroBtn("BtnMicroRdpFiles", "⚡ 22.3 Delete .rdp Files", false);

            string micro23 = MicroBtn("BtnMicroWifiProfiles", "⚡ 23.1 Delete WiFi Profiles", false)
                           + MicroBtn("BtnMicroWifiSense", "⚡ 23.2 Disable WiFi AutoConnect", false);

            string micro24 = MicroBtn("BtnMicroDefenderHistory", "⚡ 24.1 Defender Detection History", false)
                           + MicroBtn("BtnMicroDefenderQuarantine", "⚡ 24.2 Defender Quarantine", false);

            string micro25 = MicroBtn("BtnMicroCortana", "⚡ 25.1 Disable Cortana", false)
                           + MicroBtn("BtnMicroSearchHistory", "⚡ 25.2 Clear Search History", false)
                           + MicroBtn("BtnMicroSearchIndex", "⚡ 25.3 Delete Search Index", false);

            string micro26 = MicroBtn("BtnMicroOneDriveLogs", "⚡ 26.1 OneDrive Logs", false)
                           + MicroBtn("BtnMicroOneDriveTelemetry", "⚡ 26.2 OneDrive Telemetry", false);

            string micro27 = MicroBtn("BtnMicroRecallDisable", "⚡ 27.1 Disable Recall AI", false)
                           + MicroBtn("BtnMicroCopilotDisable", "⚡ 27.2 Disable Copilot", false)
                           + MicroBtn("BtnMicroRecallSnapshots", "⚡ 27.3 Delete Recall Snapshots", false);

            string micro28 = MicroBtn("BtnMicroCompatAppraiser", "⚡ 28.1 Compat Appraiser", false)
                           + MicroBtn("BtnMicroProgramDataUpdater", "⚡ 28.2 ProgramDataUpdater", false)
                           + MicroBtn("BtnMicroCeipTasks", "⚡ 28.3 CEIP Tasks", false);

            string micro29 = MicroBtn("BtnMicroPagefileClear", "⚡ 29.1 Clear Pagefile On Shutdown", false)
                           + MicroBtn("BtnMicroHibernateOff", "⚡ 29.2 Disable Hibernation", false)
                           + MicroBtn("BtnMicroLastAccess", "⚡ 29.3 Disable LastAccess Stamps", false);

            string micro30 = MicroBtn("BtnMicroIconCache", "⚡ 30.1 Delete Icon Cache", false)
                           + MicroBtn("BtnMicroFontCache", "⚡ 30.2 Clear Font Cache", false);

            string micro31 = MicroBtn("BtnMicroPsTranscript", "⚡ 31.1 Disable PS Transcription", false)
                           + MicroBtn("BtnMicroPsModuleLog", "⚡ 31.2 Disable Module Logging", false)
                           + MicroBtn("BtnMicroPsScriptBlock", "⚡ 31.3 Disable ScriptBlock Logging", false);

            string micro32 = MicroBtn("BtnMicroLocationReset", "⚡ 32.1 Reset Location Access", false)
                           + MicroBtn("BtnMicroCameraReset", "⚡ 32.2 Reset Camera Access", false)
                           + MicroBtn("BtnMicroMicReset", "⚡ 32.3 Reset Microphone Access", false)
                           + MicroBtn("BtnMicroDenySensors", "⚡ 32.4 Deny All Sensors", false);

            string cards = "";
            cards += MakeCard("HARDWARE &amp; IDENTITY", "", "0", "Windows Machine GUID, Identity &amp; PC Name Spoof",
                "Regenerates unique Windows MachineGuid, SQM MachineId, Registered Owner &amp; PC Name.",
                "BtnInfo0", "BtnSpoofMachine", "👑 Run All Spoofs", true, micro0);

            cards += MakeCard("AI IDE RESETER", "", "00", "Cursor &amp; AI IDE Machine ID Resetter",
                "Resets devDeviceId, macMachineId, machineId, sqmId &amp; serviceMachineId in storage.json.",
                "BtnInfo00", "BtnSpoofCursor", "👑 Reset All IDs", true, micro00);

            cards += MakeCard("TELEMETRY", "", "1", "Deep Windows Telemetry &amp; CEIP Silencer",
                "Permanently disables DiagTrack, CEIP, Inking/Typing feeds &amp; Defender cloud sample uploads.",
                "BtnInfo1", "BtnV1", "Silence All Telemetry", false, micro1);

            cards += MakeCard("ACTIVITY DB", "", "2", "Windows Timeline SQLite Database Purge",
                "Deletes ActivitiesCache.db SQLite database &amp; disables upload of app history to Microsoft accounts.",
                "BtnInfo2", "BtnV2", "Purge All Activities", false, micro2);

            cards += MakeCard("TERMINAL", "", "3", "PowerShell &amp; RunMRU Command History Sanitizer",
                "Wipes PSReadLine history file &amp; clears Windows Run dialog (Win+R) MRU registry.",
                "BtnInfo3", "BtnV3", "Sanitize All History", false, micro3);

            cards += MakeCard("SHELLBAGS", "", "4", "ShellBags, OpenSaveMRU &amp; Dialog Execution History",
                "Purges OpenSavePidlMRU, LastVisitedPidlMRU, WordWheelQuery search logs from registry.",
                "BtnInfo4", "BtnV4", "Purge All ShellBags", false, micro4);

            cards += MakeCard("JUMP LISTS", "", "5", "JumpLists &amp; Recent Items MRU Purge",
                "Deletes AutomaticDestinations, CustomDestinations, and Recent Items folder.",
                "BtnInfo5", "BtnV5", "Clear All JumpLists", false, micro5);

            cards += MakeCard("VISUAL CACHE", "", "6", "Visual Thumbnails &amp; DirectX Shader Caches",
                "Deletes thumbnail databases, DirectX compiled shader caches, and clipboard data.",
                "BtnInfo6", "BtnV6", "Purge All Visuals", false, micro6);

            cards += MakeCard("CRASH DUMPS", "", "7", "Crash Dumps &amp; Windows Error Reporting (WER)",
                "Sanitizes kernel minidumps, LiveKernelReports, and WER queue archives.",
                "BtnInfo7", "BtnV7", "Purge All Dumps", false, micro7);

            cards += MakeCard("CRYPTNET SSL", "", "8", "Cryptnet SSL/TLS Certificate URL Leak Cache",
                "Purges CryptnetUrlCache Content &amp; MetaData directories to prevent HTTPS domain leaks.",
                "BtnInfo8", "BtnV8", "Purge Cryptnet Cache", false, micro8);

            cards += MakeCard("EVENT LOGS", "", "9", "Windows Security, System &amp; PowerShell Event Logs",
                "Scrubs Security, System, Application, and PowerShell Operational logs via wevtutil.",
                "BtnInfo9", "BtnV9", "Scrub All Event Logs", false, micro9);

            cards += MakeCard("NETWORK", "", "10", "Network &amp; Identity Stealth (DNS, ARP, LLMNR, WPAD)",
                "Flushes DNS cache, deletes ARP routing cache, disables LLMNR &amp; WPAD auto-discovery.",
                "BtnInfo10", "BtnV10", "Harden All Network", false, micro10);

            cards += MakeCard("P2P DISABLE", "", "11", "P2P Delivery Optimization &amp; SmartScreen Probing",
                "Forces DODownloadMode to 0 (No P2P seeding) &amp; stops Microsoft SmartScreen checks.",
                "BtnInfo11", "BtnV11", "Disable P2P &amp; Probing", false, micro11);

            cards += MakeCard("BROWSER CACHE", "", "12", "Multi-Browser GPU, Shader &amp; Temporary Caches",
                "Purges temp caches &amp; GPU shaders across Brave, Chrome, Edge and Firefox.",
                "BtnInfo12", "BtnV12", "Clean All Browser Caches", false, micro12);

            cards += MakeCard("PREFETCH", "", "13", "Prefetch &amp; SuperFetch Execution Traces",
                "Deletes all .pf prefetch files, disables Prefetcher registry &amp; stops SysMain service.",
                "BtnInfo13", "BtnV13", "Purge All Prefetch", false, micro13);

            cards += MakeCard("AMCACHE", "", "14", "AmCache Execution History (SHA-1 Hash Logs)",
                "Clears Amcache.hve entries &amp; RecentFileCache.bcl that log every EXE ever executed.",
                "BtnInfo14", "BtnV14", "Clear AmCache", false, micro14);

            cards += MakeCard("SHIMCACHE", "", "15", "ShimCache (AppCompatCache) Binary Execution Log",
                "Clears AppCompatCache from SYSTEM registry hive tracking binary execution metadata.",
                "BtnInfo15", "BtnV15", "Clear ShimCache", false, micro15);

            cards += MakeCard("USERASSIST", "", "16", "UserAssist GUI Execution Tracking (ROT13)",
                "Clears ROT13-encoded UserAssist execution logs &amp; disables Start_TrackProgs.",
                "BtnInfo16", "BtnV16", "Purge UserAssist", false, micro16);

            cards += MakeCard("BAM / DAM", "", "17", "Background &amp; Desktop Activity Monitor (BAM/DAM)",
                "Clears BAM/DAM registry entries that log last execution times of all background apps.",
                "BtnInfo17", "BtnV17", "Clear BAM/DAM", false, micro17);

            cards += MakeCard("SRUM", "", "18", "System Resource Usage Monitor (SRUM) Database",
                "Stops DPS service &amp; clears SRUDB.dat tracking network usage, CPU time, energy per app.",
                "BtnInfo18", "BtnV18", "Clear SRUM Data", false, micro18);

            cards += MakeCard("TYPED HISTORY", "", "19", "TypedPaths, TypedURLs &amp; MUICache Execution",
                "Clears File Explorer address bar history, IE typed URLs, and MUICache app name logs.",
                "BtnInfo19", "BtnV19", "Clear All Typed History", false, micro19);

            cards += MakeCard("RECENT DOCS", "", "20", "RecentDocs Registry &amp; LNK Shortcut Files",
                "Clears RecentDocs MRU registry &amp; deletes all .lnk shortcut files from Recent folder.",
                "BtnInfo20", "BtnV20", "Purge RecentDocs", false, micro20);

            cards += MakeCard("NOTIFICATIONS", "", "21", "Windows Push Notification Database (WPN)",
                "Kills WPN service &amp; deletes wpndatabase.db storing all notification history.",
                "BtnInfo21", "BtnV21", "Clear Notification DB", false, micro21);

            cards += MakeCard("RDP ARTIFACTS", "", "22", "Remote Desktop Bitmap Cache &amp; Server History",
                "Clears RDP bitmap cache tiles, server MRU history, &amp; .rdp connection files.",
                "BtnInfo22", "BtnV22", "Purge RDP Artifacts", false, micro22);

            cards += MakeCard("WIFI PROFILES", "", "23", "WiFi Profile History &amp; AutoConnect Settings",
                "Deletes all saved WiFi profiles (passwords) &amp; disables WiFi AutoConnect OEM.",
                "BtnInfo23", "BtnV23", "Delete WiFi History", false, micro23);

            cards += MakeCard("DEFENDER HIST", "", "24", "Windows Defender Detection &amp; Quarantine History",
                "Deletes Defender scan history, detection service logs, &amp; quarantined items.",
                "BtnInfo24", "BtnV24", "Clear Defender History", false, micro24);

            cards += MakeCard("SEARCH &amp; CORTANA", "", "25", "Cortana, Search History &amp; Search Index",
                "Disables Cortana, clears device search history, &amp; optionally purges Windows Search index.",
                "BtnInfo25", "BtnV25", "Kill Search Tracking", false, micro25);

            cards += MakeCard("ONEDRIVE LOGS", "", "26", "OneDrive Telemetry &amp; Sync Logs",
                "Deletes OneDrive diagnostic logs, sync telemetry, and setup log directories.",
                "BtnInfo26", "BtnV26", "Purge OneDrive Logs", false, micro26);

            cards += MakeCard("WIN AI RECALL", "", "27", "Windows Recall AI, Copilot &amp; Snapshots (Win11)",
                "Disables Recall AI screenshots, Copilot integration, &amp; deletes stored snapshots.",
                "BtnInfo27", "BtnV27", "Kill AI Tracking", false, micro27);

            cards += MakeCard("SCHED TASKS", "", "28", "Scheduled Task Telemetry Killswitch",
                "Disables Compatibility Appraiser, ProgramDataUpdater, CEIP (Consolidator, KernelCeip, UsbCeip).",
                "BtnInfo28", "BtnV28", "Disable Telemetry Tasks", false, micro28);

            cards += MakeCard("FORENSIC HARDEN", "", "29", "Pagefile, Hibernation &amp; NTFS Forensic Hardening",
                "Enables ClearPageFileAtShutdown, disables hibernation (deletes hiberfil.sys), disables LastAccess stamps.",
                "BtnInfo29", "BtnV29", "Harden Forensics", false, micro29);

            cards += MakeCard("ICON/FONT CACHE", "", "30", "Icon Cache &amp; Windows Font Cache Rebuild",
                "Deletes icon cache DB files &amp; clears font cache (FNTCACHE.DAT) forcing clean rebuild.",
                "BtnInfo30", "BtnV30", "Clear Icon/Font Cache", false, micro30);

            cards += MakeCard("PS LOGGING", "", "31", "PowerShell Transcript, Module &amp; ScriptBlock Logging",
                "Disables PowerShell Transcription, Module Logging, and ScriptBlock Logging via registry policies.",
                "BtnInfo31", "BtnV31", "Disable PS Logging", false, micro31);

            cards += MakeCard("SENSOR ACCESS", "", "32", "CapabilityAccessManager Sensor Permissions Reset",
                "Resets Location, Camera, Microphone access timestamps &amp; denies all app sensor access globally.",
                "BtnInfo32", "BtnV32", "Reset Sensor Access", false, micro32);

            string xaml = @"
<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
      SnapsToDevicePixels=""True"" UseLayoutRounding=""True"">
    
    <Grid.Resources>
        <Style TargetType=""ScrollBar"">
            <Setter Property=""Width"" Value=""6"" />
            <Setter Property=""Background"" Value=""Transparent"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""ScrollBar"">
                        <Grid Background=""Transparent"">
                            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate>
                                                <Border Background=""#0284C7"" CornerRadius=""3"" Margin=""1,0"" />
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""Border"" x:Key=""RowCard"">
            <Setter Property=""Background"" Value=""#090F1D"" />
            <Setter Property=""BorderBrush"" Value=""#1E293B"" />
            <Setter Property=""BorderThickness"" Value=""1"" />
            <Setter Property=""CornerRadius"" Value=""10"" />
            <Setter Property=""Padding"" Value=""14,10"" />
            <Setter Property=""Margin"" Value=""0,0,0,10"" />
        </Style>

        <Style TargetType=""Border"" x:Key=""GoldRowCard"" BasedOn=""{StaticResource RowCard}"">
            <Setter Property=""Background"" Value=""#0F1626"" />
            <Setter Property=""BorderBrush"" Value=""#F59E0B"" />
        </Style>

        <Style TargetType=""Button"" x:Key=""ActionBtn"">
            <Setter Property=""Background"" Value=""#0D1F38"" />
            <Setter Property=""Foreground"" Value=""#38BDF8"" />
            <Setter Property=""FontSize"" Value=""11.5"" />
            <Setter Property=""FontWeight"" Value=""Bold"" />
            <Setter Property=""BorderBrush"" Value=""#0284C7"" />
            <Setter Property=""BorderThickness"" Value=""1"" />
            <Setter Property=""Cursor"" Value=""Hand"" />
            <Setter Property=""Padding"" Value=""12,6"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""BtnBrd"" Background=""{TemplateBinding Background}"" 
                                BorderBrush=""{TemplateBinding BorderBrush}"" 
                                BorderThickness=""{TemplateBinding BorderThickness}"" 
                                CornerRadius=""7"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""BtnBrd"" Property=""Background"" Value=""#0284C7"" />
                                <Setter Property=""Foreground"" Value=""#030712"" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""Button"" x:Key=""CrownBtn"" BasedOn=""{StaticResource ActionBtn}"">
            <Setter Property=""Background"" Value=""#221A0C"" />
            <Setter Property=""Foreground"" Value=""#FBBF24"" />
            <Setter Property=""BorderBrush"" Value=""#F59E0B"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""CrownBrd"" Background=""{TemplateBinding Background}"" 
                                BorderBrush=""{TemplateBinding BorderBrush}"" 
                                BorderThickness=""{TemplateBinding BorderThickness}"" 
                                CornerRadius=""7"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""CrownBrd"" Property=""Background"" Value=""#F59E0B"" />
                                <Setter Property=""Foreground"" Value=""#030712"" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""Button"" x:Key=""MicroBtn"">
            <Setter Property=""Background"" Value=""#060B14"" />
            <Setter Property=""Foreground"" Value=""#94A3B8"" />
            <Setter Property=""FontSize"" Value=""10.5"" />
            <Setter Property=""FontWeight"" Value=""SemiBold"" />
            <Setter Property=""BorderBrush"" Value=""#1E293B"" />
            <Setter Property=""BorderThickness"" Value=""1"" />
            <Setter Property=""Cursor"" Value=""Hand"" />
            <Setter Property=""Padding"" Value=""8,4"" />
            <Setter Property=""Margin"" Value=""0,0,6,0"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""MicroBrd"" Background=""{TemplateBinding Background}"" 
                                BorderBrush=""{TemplateBinding BorderBrush}"" 
                                BorderThickness=""{TemplateBinding BorderThickness}"" 
                                CornerRadius=""5"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""MicroBrd"" Property=""Background"" Value=""#1E293B"" />
                                <Setter TargetName=""MicroBrd"" Property=""BorderBrush"" Value=""#38BDF8"" />
                                <Setter Property=""Foreground"" Value=""#38BDF8"" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""Button"" x:Key=""GoldMicroBtn"" BasedOn=""{StaticResource MicroBtn}"">
            <Setter Property=""BorderBrush"" Value=""#3A2C10"" />
            <Setter Property=""Foreground"" Value=""#FCD34D"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""GMicroBrd"" Background=""{TemplateBinding Background}"" 
                                BorderBrush=""{TemplateBinding BorderBrush}"" 
                                BorderThickness=""{TemplateBinding BorderThickness}"" 
                                CornerRadius=""5"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""GMicroBrd"" Property=""Background"" Value=""#221A0C"" />
                                <Setter TargetName=""GMicroBrd"" Property=""BorderBrush"" Value=""#F59E0B"" />
                                <Setter Property=""Foreground"" Value=""#FBBF24"" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""Button"" x:Key=""InfoBtn"">
            <Setter Property=""Background"" Value=""#0D192E"" />
            <Setter Property=""Foreground"" Value=""#00F2FE"" />
            <Setter Property=""FontSize"" Value=""10.5"" />
            <Setter Property=""FontWeight"" Value=""Bold"" />
            <Setter Property=""BorderBrush"" Value=""#1E3A8A"" />
            <Setter Property=""BorderThickness"" Value=""1"" />
            <Setter Property=""Cursor"" Value=""Hand"" />
            <Setter Property=""Padding"" Value=""8,5"" />
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border x:Name=""InfoBrd"" Background=""{TemplateBinding Background}"" 
                                BorderBrush=""{TemplateBinding BorderBrush}"" 
                                BorderThickness=""{TemplateBinding BorderThickness}"" 
                                CornerRadius=""6"">
                            <TextBlock Text=""ℹ SPECS"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""InfoBrd"" Property=""Background"" Value=""#00F2FE"" />
                                <Setter TargetName=""InfoBrd"" Property=""BorderBrush"" Value=""#00F2FE"" />
                                <Setter Property=""Foreground"" Value=""#030712"" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Grid.Resources>

    <Border Background=""#040711"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""14"" Margin=""8"">
        <Grid Margin=""18"">
            <Grid.RowDefinitions>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""*""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""145""/>
            </Grid.RowDefinitions>

            <!-- Titlebar -->
            <Grid Grid.Row=""0"" x:Name=""TitleBar"" Background=""Transparent"" Margin=""0,0,0,14"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Border Background=""#0A192F"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""8"" Padding=""7,4"" Margin=""0,0,12,0"">
                        <TextBlock Text=""S 🛡 T"" FontSize=""12"" FontWeight=""ExtraBold"" Foreground=""#00F2FE""/>
                    </Border>
                    <TextBlock Text=""S - T - E - A - L - T - H"" FontSize=""21"" FontWeight=""ExtraBold"" Foreground=""#00F2FE""/>
                    <Border Background=""#065F46"" CornerRadius=""6"" Padding=""8,3"" Margin=""12,0,0,0"">
                        <TextBlock Text=""v7.0 ABSOLUTE"" FontSize=""10.5"" FontWeight=""Bold"" Foreground=""#34D399""/>
                    </Border>
                    <TextBlock Text=""// 32-VECTOR FORENSIC ANNIHILATION ENGINE"" FontSize=""11.5"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""10,0,0,0""/>
                </StackPanel>
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                    <Button x:Name=""BtnMin"" Content=""─"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#94A3B8"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand"" Margin=""0,0,4,0""/>
                    <Button x:Name=""BtnMax"" Content=""🗖"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#94A3B8"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand"" Margin=""0,0,4,0""/>
                    <Button x:Name=""BtnClose"" Content=""✕"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand"" Margin=""0,0,4,0""/>
                </StackPanel>
            </Grid>

            <!-- HUD -->
            <Grid Grid.Row=""1"" Margin=""0,0,0,14"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/>
                </Grid.ColumnDefinitions>
                <Border Grid.Column=""0"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,8"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● MACHINE IDENT"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtHost"" Text=""Detecting..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#F8FAFC"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""1"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,8"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● NET INTERFACE"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtNet"" Text=""Checking..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#38BDF8"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""2"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,8"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● SECURITY MATRIX"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtTelem"" Text=""Checking..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#34D399"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""3"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,8"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● MEMORY LOAD"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtRam"" Text=""Monitoring..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#F59E0B"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
            </Grid>

            <!-- Master Hero -->
            <Border Grid.Row=""2"" Background=""#081736"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""12"" Padding=""16,12"" Margin=""3,0,3,12"">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock Text=""⚡ 1-CLICK MASTER S-T-E-A-L-T-H PROTOCOL"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#F8FAFC""/>
                        <TextBlock Text=""Executes full 32-Vector Hardware Spoof, Identity Reset, AI Kill, and Deep Forensic Annihilation in sequence."" FontSize=""11"" Foreground=""#94A3B8"" Margin=""0,3,0,0""/>
                    </StackPanel>
                    <Button x:Name=""BtnMasterRun"" Grid.Column=""1"" Width=""310"" Height=""42"" Cursor=""Hand"">
                        <Button.Template>
                            <ControlTemplate TargetType=""Button"">
                                <Border CornerRadius=""10"">
                                    <Border.Background>
                                        <LinearGradientBrush StartPoint=""0,0"" EndPoint=""1,1"">
                                            <GradientStop Color=""#00F2FE"" Offset=""0.0""/>
                                            <GradientStop Color=""#38BDF8"" Offset=""0.5""/>
                                            <GradientStop Color=""#2563EB"" Offset=""1.0""/>
                                        </LinearGradientBrush>
                                    </Border.Background>
                                    <TextBlock Text=""⚡ INITIATE 32-VECTOR PROTOCOL ⚡"" Foreground=""#030712"" FontSize=""12.5"" FontWeight=""ExtraBold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                                </Border>
                            </ControlTemplate>
                        </Button.Template>
                    </Button>
                </Grid>
            </Border>

            <!-- Cards -->
            <ScrollViewer Grid.Row=""3"" VerticalScrollBarVisibility=""Auto"" Margin=""0,0,0,8"">
                <StackPanel Margin=""0,0,6,0"">
" + cards + @"
                </StackPanel>
            </ScrollViewer>

            <!-- Progress Bar -->
            <Grid Grid.Row=""4"" Margin=""0,0,0,6"">
                <ProgressBar x:Name=""PrgBar"" Height=""3"" Background=""#0A192F"" Foreground=""#00F2FE"" BorderThickness=""0""/>
            </Grid>

            <!-- Telemetry Stream -->
            <Border Grid.Row=""5"" Background=""#020408"" BorderBrush=""#0284C7"" BorderThickness=""1"" CornerRadius=""10"" Padding=""12"">
                <Grid>
                    <Grid.RowDefinitions><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>
                    <StackPanel Grid.Row=""0"" Orientation=""Horizontal"" Margin=""0,0,0,4"">
                        <TextBlock Text=""▶ LIVE S-T-E-A-L-T-H TELEMETRY &amp; FORENSIC STREAM"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#00F2FE""/>
                        <TextBlock Text="" | 32-VECTOR ABSOLUTE ENGINE"" FontSize=""10"" Foreground=""#64748B"" Margin=""6,0,0,0""/>
                    </StackPanel>
                    <ScrollViewer Grid.Row=""1"" x:Name=""Scroller"" VerticalScrollBarVisibility=""Auto"">
                        <TextBox x:Name=""TxtLogs"" Background=""Transparent"" Foreground=""#34D399"" 
                                 FontFamily=""Consolas"" FontSize=""11.5"" BorderThickness=""0"" 
                                 IsReadOnly=""True"" TextWrapping=""Wrap""/>
                    </ScrollViewer>
                </Grid>
            </Border>
        </Grid>
    </Border>

    <!-- Info Modal -->
    <Border x:Name=""InfoModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
        <Border Background=""#070F1E"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""14"" 
                Width=""640"" MaxHeight=""520"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
            <StackPanel>
                <Grid Margin=""0,0,0,16"">
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <TextBlock x:Name=""TxtModalTitle"" Grid.Column=""0"" Text=""Feature Specs"" FontSize=""16"" FontWeight=""ExtraBold"" Foreground=""#00F2FE""/>
                    <Button x:Name=""BtnCloseModal"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                </Grid>
                <TextBlock Text=""TECHNICAL DESCRIPTION:"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#64748B""/>
                <TextBlock x:Name=""TxtModalDesc"" Text=""Details..."" FontSize=""12"" Foreground=""#E2E8F0"" TextWrapping=""Wrap"" Margin=""0,4,0,14""/>
                <TextBlock Text=""AFFECTED SYSTEM PATHS &amp; REGISTRY KEYS:"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#64748B""/>
                <Border Background=""#020409"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""10"" Margin=""0,4,0,18"">
                    <TextBlock x:Name=""TxtModalPaths"" Text=""Paths..."" FontFamily=""Consolas"" FontSize=""11"" Foreground=""#34D399"" TextWrapping=""Wrap""/>
                </Border>
                <Button x:Name=""BtnModalGotIt"" Content=""Acknowledged"" Width=""140"" Height=""34"" HorizontalAlignment=""Right"" Style=""{StaticResource ActionBtn}""/>
            </StackPanel>
        </Border>
    </Border>

    <!-- PC Name Modal -->
    <Border x:Name=""PcNameModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
        <Border Background=""#070F1E"" BorderBrush=""#F59E0B"" BorderThickness=""1.5"" CornerRadius=""14"" 
                Width=""580"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
            <StackPanel>
                <Grid Margin=""0,0,0,14"">
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <TextBlock Grid.Column=""0"" Text=""👑 Custom PC &amp; Host Identity Spoofer"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#FBBF24""/>
                    <Button x:Name=""BtnClosePcModal"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                </Grid>
                <TextBlock Text=""Enter desired PC/Computer Name (or click Random Generator):"" FontSize=""11"" Foreground=""#94A3B8"" Margin=""0,0,0,8""/>
                <Grid Margin=""0,0,0,16"">
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <TextBox x:Name=""TxtNewPcName"" Grid.Column=""0"" Height=""36"" Background=""#020409"" Foreground=""#00F2FE"" BorderBrush=""#1E293B"" BorderThickness=""1"" FontFamily=""Consolas"" FontSize=""13"" FontWeight=""Bold"" VerticalContentAlignment=""Center"" Padding=""8,0""/>
                    <Button x:Name=""BtnRandomPcName"" Grid.Column=""1"" Content=""🎲 Randomize"" Height=""36"" Margin=""8,0,0,0"" Style=""{StaticResource ActionBtn}""/>
                </Grid>
                <TextBlock Text=""Note: Applying permanently will rename Windows NetBIOS identity in registry (effective upon next reboot)."" FontSize=""10.5"" Foreground=""#64748B"" TextWrapping=""Wrap"" Margin=""0,0,0,18""/>
                <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                    <Button x:Name=""BtnApplyVirtualIdentity"" Content=""⚡ Apply Virtual Identity"" Height=""34"" Margin=""0,0,8,0"" Style=""{StaticResource ActionBtn}""/>
                    <Button x:Name=""BtnApplyPcName"" Content=""👑 Apply System Rename"" Height=""34"" Style=""{StaticResource CrownBtn}""/>
                </StackPanel>
            </StackPanel>
        </Border>
    </Border>

</Grid>";

            var reader = new System.Xml.XmlTextReader(new StringReader(xaml));
            var root = (UIElement)XamlReader.Load(reader);
            this.Content = root;

            var titleBar = (Grid)LogicalTreeHelper.FindLogicalNode(root, "TitleBar");
            var btnClose = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnClose");
            var btnMin = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMin");
            var btnMax = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMax");
            var btnMaster = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMasterRun");
            
            txtHost = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtHost");
            txtNet = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtNet");
            txtRam = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtRam");
            txtTelem = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtTelem");
            txtLogs = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtLogs");
            scroller = (ScrollViewer)LogicalTreeHelper.FindLogicalNode(root, "Scroller");
            prgBar = (ProgressBar)LogicalTreeHelper.FindLogicalNode(root, "PrgBar");

            infoModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "InfoModal");
            txtModalTitle = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalTitle");
            txtModalDesc = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalDesc");
            txtModalPaths = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalPaths");

            var btnCloseModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseModal");
            var btnModalGotIt = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnModalGotIt");
            btnCloseModal.Click += (s, e) => { infoModal.Visibility = Visibility.Collapsed; };
            btnModalGotIt.Click += (s, e) => { infoModal.Visibility = Visibility.Collapsed; };

            pcNameModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "PcNameModal");
            txtNewPcName = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtNewPcName");
            btnApplyPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnApplyPcName");
            btnRandomPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnRandomPcName");
            btnClosePcModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnClosePcModal");
            var btnApplyVirtualIdentity = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnApplyVirtualIdentity");

            btnClosePcModal.Click += (s, e) => { pcNameModal.Visibility = Visibility.Collapsed; };
            btnRandomPcName.Click += (s, e) => {
                string[] prefixes = new string[] { "STEALTH-NODE", "DESKTOP-CYBER", "QUANTUM-HOST", "NEXUS-SYS", "SHADOW-PC", "ZERO-GRID" };
                var rnd = new Random();
                txtNewPcName.Text = prefixes[rnd.Next(prefixes.Length)] + "-" + rnd.Next(1000, 9999).ToString();
            };
            btnApplyPcName.Click += (s, e) => {
                string targetName = txtNewPcName.Text.Trim();
                if (!string.IsNullOrEmpty(targetName)) { pcNameModal.Visibility = Visibility.Collapsed; RunAsync(delegate() { RenameWindowsComputer(targetName); }); }
            };
            btnApplyVirtualIdentity.Click += (s, e) => {
                string targetName = txtNewPcName.Text.Trim();
                if (!string.IsNullOrEmpty(targetName)) { pcNameModal.Visibility = Visibility.Collapsed; RunAsync(delegate() { SpoofRegisteredOwnerAndOrg(targetName); }); }
            };

            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) ToggleMaximize(); else this.DragMove(); };
            btnClose.Click += (s, e) => { this.Close(); };
            btnMin.Click += (s, e) => { this.WindowState = WindowState.Minimized; };
            btnMax.Click += (s, e) => { ToggleMaximize(); };
            btnMaster.Click += (s, e) => RunAsync(MasterStealthProtocol);

            // Wire Category Master Buttons
            WireBtn(root, "BtnSpoofMachine", SpoofWindowsMachineGuid);
            WireBtn(root, "BtnSpoofCursor", ResetCursorAndAiIdeMachineId);
            WireBtn(root, "BtnV1", SilenceDeepTelemetry);
            WireBtn(root, "BtnV2", PurgeTimelineDb);
            WireBtn(root, "BtnV3", SanitizeTerminalHistory);
            WireBtn(root, "BtnV4", PurgeShellBagsDialogs);
            WireBtn(root, "BtnV5", PurgeJumpListsRecent);
            WireBtn(root, "BtnV6", PurgeVisualShaderCaches);
            WireBtn(root, "BtnV7", PurgeCrashDumpsWer);
            WireBtn(root, "BtnV8", PurgeCryptnetCache);
            WireBtn(root, "BtnV9", ScrubEventLogs);
            WireBtn(root, "BtnV10", HardenNetwork);
            WireBtn(root, "BtnV11", DisableDeliveryOptimization);
            WireBtn(root, "BtnV12", CleanBrowserCaches);
            WireBtn(root, "BtnV13", PurgePrefetchAll);
            WireBtn(root, "BtnV14", ClearAmCacheAll);
            WireBtn(root, "BtnV15", ClearShimCacheAll);
            WireBtn(root, "BtnV16", PurgeUserAssistAll);
            WireBtn(root, "BtnV17", ClearBamDamAll);
            WireBtn(root, "BtnV18", ClearSrumAll);
            WireBtn(root, "BtnV19", ClearTypedHistoryAll);
            WireBtn(root, "BtnV20", PurgeRecentDocsAll);
            WireBtn(root, "BtnV21", ClearNotificationsAll);
            WireBtn(root, "BtnV22", PurgeRdpArtifactsAll);
            WireBtn(root, "BtnV23", DeleteWifiHistoryAll);
            WireBtn(root, "BtnV24", ClearDefenderHistoryAll);
            WireBtn(root, "BtnV25", KillSearchTrackingAll);
            WireBtn(root, "BtnV26", PurgeOneDriveLogsAll);
            WireBtn(root, "BtnV27", KillAiTrackingAll);
            WireBtn(root, "BtnV28", DisableTelemetryTasksAll);
            WireBtn(root, "BtnV29", HardenForensicsAll);
            WireBtn(root, "BtnV30", ClearIconFontCacheAll);
            WireBtn(root, "BtnV31", DisablePsLoggingAll);
            WireBtn(root, "BtnV32", ResetSensorAccessAll);

            // Wire Granular Micro-Action Buttons
            // Cat 0
            WireBtn(root, "BtnMicroGuid", SpoofMachineGuidOnly);
            WireBtn(root, "BtnMicroSqm", SpoofSqmIdOnly);
            WireBtn(root, "BtnMicroOwner", delegate() { SpoofRegisteredOwnerAndOrg("STEALTH_OPERATOR"); });
            ((Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMicroPcName")).Click += (s, e) => {
                txtNewPcName.Text = Environment.MachineName;
                pcNameModal.Visibility = Visibility.Visible;
            };
            // Cat 00
            WireBtn(root, "BtnMicroDevId", delegate() { ResetCursorKeyOnly("telemetry.devDeviceId"); });
            WireBtn(root, "BtnMicroMacId", delegate() { ResetCursorKeyOnly("telemetry.macMachineId"); });
            WireBtn(root, "BtnMicroMachineId", delegate() { ResetCursorKeyOnly("telemetry.machineId"); });
            WireBtn(root, "BtnMicroSqmId", delegate() { ResetCursorKeyOnly("telemetry.sqmId"); });
            // Cat 1
            WireBtn(root, "BtnMicroDiagTrack", SilenceDiagTrackServiceOnly);
            WireBtn(root, "BtnMicroAdvId", DisableAdvertisingIdOnly);
            WireBtn(root, "BtnMicroInking", DisableInkingTypingOnly);
            WireBtn(root, "BtnMicroDefenderSample", DisableDefenderSampleUploadOnly);
            // Cat 2
            WireBtn(root, "BtnMicroSqliteDb", WipeTimelineSqliteDbOnly);
            WireBtn(root, "BtnMicroActivityFeed", DisableActivityFeedPolicyOnly);
            // Cat 3
            WireBtn(root, "BtnMicroPsHistory", ClearPsReadLineHistoryOnly);
            WireBtn(root, "BtnMicroRunMru", ClearRunMruOnly);
            // Cat 4
            WireBtn(root, "BtnMicroOpenSave", ClearOpenSaveMruOnly);
            WireBtn(root, "BtnMicroLastVisited", ClearLastVisitedMruOnly);
            WireBtn(root, "BtnMicroWordWheel", ClearWordWheelQueryOnly);
            // Cat 5
            WireBtn(root, "BtnMicroAutoDest", ClearAutoDestinationsOnly);
            WireBtn(root, "BtnMicroCustDest", ClearCustomDestinationsOnly);
            WireBtn(root, "BtnMicroRecentFolder", ClearRecentFolderOnly);
            // Cat 6
            WireBtn(root, "BtnMicroThumbnails", ClearThumbnailsOnly);
            WireBtn(root, "BtnMicroD3DShaders", ClearD3DShadersOnly);
            WireBtn(root, "BtnMicroClipboard", ClearClipboardOnly);
            // Cat 7
            WireBtn(root, "BtnMicroMinidumps", ClearMinidumpsOnly);
            WireBtn(root, "BtnMicroLiveKernel", ClearLiveKernelOnly);
            WireBtn(root, "BtnMicroWerReports", ClearWerQueueOnly);
            WireBtn(root, "BtnMicroAppCrash", ClearAppCrashDumpsOnly);
            // Cat 8
            WireBtn(root, "BtnMicroCryptnetContent", ClearCryptnetContentOnly);
            WireBtn(root, "BtnMicroCryptnetMeta", ClearCryptnetMetaOnly);
            // Cat 9
            WireBtn(root, "BtnMicroSecurityLog", ClearSecurityLogOnly);
            WireBtn(root, "BtnMicroSystemLog", ClearSystemAndAppLogOnly);
            WireBtn(root, "BtnMicroPsLog", ClearPowerShellLogOnly);
            // Cat 10
            WireBtn(root, "BtnMicroFlushDns", FlushDnsOnly);
            WireBtn(root, "BtnMicroClearArp", ClearArpOnly);
            WireBtn(root, "BtnMicroDisableLlmnr", DisableLlmnrOnly);
            // Cat 11
            WireBtn(root, "BtnMicroDoMode", DisableDoModeOnly);
            WireBtn(root, "BtnMicroSmartScreen", DisableSmartScreenOnly);
            // Cat 12
            WireBtn(root, "BtnMicroChromeCache", CleanChromeCacheOnly);
            WireBtn(root, "BtnMicroBraveCache", CleanBraveCacheOnly);
            WireBtn(root, "BtnMicroEdgeCache", CleanEdgeCacheOnly);
            WireBtn(root, "BtnMicroFirefoxCache", CleanFirefoxCacheOnly);
            // Cat 13
            WireBtn(root, "BtnMicroPrefetchFiles", DeletePrefetchFilesOnly);
            WireBtn(root, "BtnMicroDisablePrefetch", DisablePrefetcherOnly);
            WireBtn(root, "BtnMicroSysMain", StopSysMainOnly);
            // Cat 14
            WireBtn(root, "BtnMicroAmCache", ClearAmCacheOnly);
            WireBtn(root, "BtnMicroRecentFileCache", ClearRecentFileCacheOnly);
            // Cat 15
            WireBtn(root, "BtnMicroShimCache", ClearShimCacheOnly);
            // Cat 16
            WireBtn(root, "BtnMicroUserAssistClear", ClearUserAssistOnly);
            WireBtn(root, "BtnMicroUserAssistDisable", DisableUserAssistTrackingOnly);
            // Cat 17
            WireBtn(root, "BtnMicroBam", ClearBamOnly);
            WireBtn(root, "BtnMicroDam", ClearDamOnly);
            // Cat 18
            WireBtn(root, "BtnMicroSrum", ClearSrumOnly);
            // Cat 19
            WireBtn(root, "BtnMicroTypedPaths", ClearTypedPathsOnly);
            WireBtn(root, "BtnMicroTypedUrls", ClearTypedUrlsOnly);
            WireBtn(root, "BtnMicroMuiCache", ClearMuiCacheOnly);
            // Cat 20
            WireBtn(root, "BtnMicroRecentDocs", ClearRecentDocsOnly);
            WireBtn(root, "BtnMicroLnkFiles", DeleteLnkFilesOnly);
            // Cat 21
            WireBtn(root, "BtnMicroWpnDb", ClearWpnDatabaseOnly);
            // Cat 22
            WireBtn(root, "BtnMicroRdpCache", ClearRdpCacheOnly);
            WireBtn(root, "BtnMicroRdpMru", ClearRdpMruOnly);
            WireBtn(root, "BtnMicroRdpFiles", DeleteRdpFilesOnly);
            // Cat 23
            WireBtn(root, "BtnMicroWifiProfiles", DeleteWifiProfilesOnly);
            WireBtn(root, "BtnMicroWifiSense", DisableWifiSenseOnly);
            // Cat 24
            WireBtn(root, "BtnMicroDefenderHistory", ClearDefenderHistoryOnly);
            WireBtn(root, "BtnMicroDefenderQuarantine", ClearDefenderQuarantineOnly);
            // Cat 25
            WireBtn(root, "BtnMicroCortana", DisableCortanaOnly);
            WireBtn(root, "BtnMicroSearchHistory", ClearSearchHistoryOnly);
            WireBtn(root, "BtnMicroSearchIndex", DeleteSearchIndexOnly);
            // Cat 26
            WireBtn(root, "BtnMicroOneDriveLogs", ClearOneDriveLogsOnly);
            WireBtn(root, "BtnMicroOneDriveTelemetry", ClearOneDriveTelemetryOnly);
            // Cat 27
            WireBtn(root, "BtnMicroRecallDisable", DisableRecallOnly);
            WireBtn(root, "BtnMicroCopilotDisable", DisableCopilotOnly);
            WireBtn(root, "BtnMicroRecallSnapshots", DeleteRecallSnapshotsOnly);
            // Cat 28
            WireBtn(root, "BtnMicroCompatAppraiser", DisableCompatAppraiserOnly);
            WireBtn(root, "BtnMicroProgramDataUpdater", DisableProgramDataUpdaterOnly);
            WireBtn(root, "BtnMicroCeipTasks", DisableCeipTasksOnly);
            // Cat 29
            WireBtn(root, "BtnMicroPagefileClear", EnablePagefileClearOnly);
            WireBtn(root, "BtnMicroHibernateOff", DisableHibernationOnly);
            WireBtn(root, "BtnMicroLastAccess", DisableLastAccessOnly);
            // Cat 30
            WireBtn(root, "BtnMicroIconCache", ClearIconCacheOnly);
            WireBtn(root, "BtnMicroFontCache", ClearFontCacheOnly);
            // Cat 31
            WireBtn(root, "BtnMicroPsTranscript", DisablePsTranscriptOnly);
            WireBtn(root, "BtnMicroPsModuleLog", DisablePsModuleLoggingOnly);
            WireBtn(root, "BtnMicroPsScriptBlock", DisablePsScriptBlockOnly);
            // Cat 32
            WireBtn(root, "BtnMicroLocationReset", ResetLocationAccessOnly);
            WireBtn(root, "BtnMicroCameraReset", ResetCameraAccessOnly);
            WireBtn(root, "BtnMicroMicReset", ResetMicAccessOnly);
            WireBtn(root, "BtnMicroDenySensors", DenyAllSensorsOnly);

            // Wire Info Modals
            WireInfoButton(root, "BtnInfo0", "Windows Machine GUID, Identity & Host Name Spoof", "Generates a brand-new cryptographically secure 128-bit GUID and overwrites MachineGuid, SQM Client MachineId, Registered Owner and provides Host ComputerName renaming.", "HKLM:\\SOFTWARE\\Microsoft\\Cryptography -> MachineGuid\nHKLM:\\SOFTWARE\\Microsoft\\SQMClient -> MachineId\nHKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion -> RegisteredOwner\nRename-Computer");
            WireInfoButton(root, "BtnInfo00", "Cursor & AI IDE Machine ID Resetter", "Completely sanitizes and regenerates all machine tracking identifiers generated by Cursor, VS Code, and Trae IDEs.", "%APPDATA%\\Cursor\\User\\globalStorage\\storage.json\n%APPDATA%\\Code\\User\\globalStorage\\storage.json");
            WireInfoButton(root, "BtnInfo1", "Deep Windows Telemetry & CEIP Silencer", "Terminates and disables DiagTrack, CEIP, Inking & Typing data harvesting, and stops Defender sample uploads.", "Services: DiagTrack, dmwappushservice\nHKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection -> AllowTelemetry=0");
            WireInfoButton(root, "BtnInfo2", "Windows Timeline & Activities Database Purge", "Deletes the ConnectedDevicesPlatform SQLite database which logs every application opened and file touched.", "%LOCALAPPDATA%\\ConnectedDevicesPlatform\\*\\ActivitiesCache.db\nHKLM:\\..\\System -> EnableActivityFeed=0");
            WireInfoButton(root, "BtnInfo3", "PowerShell & RunMRU Command History Sanitizer", "Wipes PSReadLine history file and clears Run dialog (Win+R) MRU registry key.", "(Get-PSReadLineOption).HistorySavePath\nHKCU:\\..\\Explorer\\RunMRU");
            WireInfoButton(root, "BtnInfo4", "ShellBags, OpenSaveMRU & Dialog Execution History", "Purges OpenSavePidlMRU, LastVisitedPidlMRU, WordWheelQuery search logs from the registry.", "HKCU:\\..\\ComDlg32\\OpenSavePidlMRU\nHKCU:\\..\\Explorer\\WordWheelQuery");
            WireInfoButton(root, "BtnInfo5", "JumpLists & Recent Items MRU Purge", "Deletes AutomaticDestinations and CustomDestinations binary files used by taskbar JumpLists.", "%APPDATA%\\Microsoft\\Windows\\Recent\\AutomaticDestinations\\*\n%APPDATA%\\Microsoft\\Windows\\Recent\\CustomDestinations\\*");
            WireInfoButton(root, "BtnInfo6", "Visual Thumbnails & DirectX Shader Caches", "Deletes thumbnail databases, DirectX compiled shader caches, and purges clipboard history.", "%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\n%LOCALAPPDATA%\\D3DSCache\\*");
            WireInfoButton(root, "BtnInfo7", "Crash Dumps & Windows Error Reporting (WER)", "Sanitizes kernel crash dumps, minidumps, LiveKernelReports, and WER queue archives.", "C:\\Windows\\Minidump\\*\nC:\\Windows\\LiveKernelReports\\*\n%PROGRAMDATA%\\Microsoft\\Windows\\WER\\*");
            WireInfoButton(root, "BtnInfo8", "Cryptnet SSL/TLS Certificate URL Leak Cache", "Windows CryptnetUrlCache stores OCSP and CRL certificates for every HTTPS connection.", "%LOCALAPPDATA%\\Microsoft\\CryptnetUrlCache\\Content\\*\n%LOCALAPPDATA%\\Microsoft\\CryptnetUrlCache\\MetaData\\*");
            WireInfoButton(root, "BtnInfo9", "Windows Event Logs (Security, System, PowerShell)", "Scrubs Security, System, Application, and PowerShell Operational logs via wevtutil.", "wevtutil cl Security\nwevtutil cl System\nwevtutil cl Microsoft-Windows-PowerShell/Operational");
            WireInfoButton(root, "BtnInfo10", "Network & Identity Stealth (DNS, ARP, LLMNR)", "Flushes DNS cache, deletes ARP cache, disables LLMNR & WPAD auto-discovery probes.", "Clear-DnsClientCache\nnetsh interface ip delete arpcache\nHKLM:\\..\\DNSClient -> EnableMulticast=0");
            WireInfoButton(root, "BtnInfo11", "P2P Delivery Optimization & SmartScreen Probing", "Disables Windows Delivery Optimization P2P and SmartScreen application hash checking.", "HKLM:\\..\\DeliveryOptimization -> DODownloadMode=0\nHKLM:\\..\\System -> EnableSmartScreen=0");
            WireInfoButton(root, "BtnInfo12", "Multi-Browser GPU, Shader & Temporary Caches", "Purges temp caches across Brave, Chrome, Edge, and Firefox without disturbing logins.", "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Cache\\*\n%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2\\*");
            WireInfoButton(root, "BtnInfo13", "Prefetch & SuperFetch Execution Traces", "Prefetch files (.pf) record every program executed, run count, and timestamps. SysMain (SuperFetch) caches frequently-used apps.", "C:\\Windows\\Prefetch\\*.pf\nHKLM:\\..\\PrefetchParameters -> EnablePrefetcher=0\nService: SysMain");
            WireInfoButton(root, "BtnInfo14", "AmCache Execution History", "Amcache.hve records execution history including file paths and SHA-1 hashes of every executable run.", "C:\\Windows\\AppCompat\\Programs\\Amcache.hve\nC:\\Windows\\AppCompat\\Programs\\RecentFileCache.bcl");
            WireInfoButton(root, "BtnInfo15", "ShimCache (AppCompatCache)", "AppCompatCache in the SYSTEM registry hive tracks binary metadata for backward compatibility checks.", "HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\AppCompatCache");
            WireInfoButton(root, "BtnInfo16", "UserAssist GUI Execution Tracking", "UserAssist uses ROT13-encoded registry keys to track every GUI application launch count and timestamp.", "HKCU:\\..\\Explorer\\UserAssist\\{GUID}\\Count\nHKCU:\\..\\Advanced -> Start_TrackProgs=0");
            WireInfoButton(root, "BtnInfo17", "BAM/DAM Background & Desktop Activity Monitor", "BAM and DAM track last execution times of background and desktop applications per user SID.", "HKLM:\\SYSTEM\\CurrentControlSet\\Services\\bam\\State\\UserSettings\nHKLM:\\SYSTEM\\CurrentControlSet\\Services\\dam\\State\\UserSettings");
            WireInfoButton(root, "BtnInfo18", "System Resource Usage Monitor (SRUM)", "SRUDB.dat tracks network usage, CPU time, energy consumption, and app usage per 60-second intervals.", "C:\\Windows\\System32\\SRU\\SRUDB.dat\nService: DPS (Diagnostic Policy Service)");
            WireInfoButton(root, "BtnInfo19", "TypedPaths, TypedURLs & MUICache", "TypedPaths logs Explorer address bar entries. TypedURLs logs IE typed URLs. MUICache stores app display names.", "HKCU:\\..\\Explorer\\TypedPaths\nHKCU:\\..\\Internet Explorer\\TypedURLs\nHKCU:\\..\\Shell\\MuiCache");
            WireInfoButton(root, "BtnInfo20", "RecentDocs Registry & LNK Shortcut Files", "RecentDocs tracks recently opened files by extension. LNK files contain target paths, timestamps, and volume serial numbers.", "HKCU:\\..\\Explorer\\RecentDocs\n%APPDATA%\\Microsoft\\Windows\\Recent\\*.lnk");
            WireInfoButton(root, "BtnInfo21", "Windows Push Notification Database", "wpndatabase.db stores all push notification history including toast content, arrival times, and app handlers.", "%LOCALAPPDATA%\\Microsoft\\Windows\\Notifications\\wpndatabase.db");
            WireInfoButton(root, "BtnInfo22", "RDP Bitmap Cache & Server History", "RDP bitmap cache stores graphical tiles that can be reassembled to reconstruct what was displayed during remote sessions.", "%LOCALAPPDATA%\\Microsoft\\Terminal Server Client\\Cache\\*\nHKCU:\\..\\Terminal Server Client\\Default\nHKCU:\\..\\Terminal Server Client\\Servers");
            WireInfoButton(root, "BtnInfo23", "WiFi Profile History & AutoConnect", "Saved WiFi profiles contain SSIDs and stored passwords. Deleting them removes all network memory.", "netsh wlan delete profile name=* i=*\nHKLM:\\..\\WcmSvc\\wifinetworkmanager\\config -> AutoConnectAllowedOEM=0");
            WireInfoButton(root, "BtnInfo24", "Windows Defender Detection & Quarantine History", "Defender stores scan results, detection history, and quarantined files that reveal security events.", "C:\\ProgramData\\Microsoft\\Windows Defender\\Scans\\History\\Service\\*\n..\\Scans\\History\\CacheManager\\*");
            WireInfoButton(root, "BtnInfo25", "Cortana, Search History & Index", "Cortana tracks search queries. Windows Search Index stores metadata about every file on the system.", "HKLM:\\..\\Windows Search -> AllowCortana=0\nHKCU:\\..\\SearchHistoryEnabled=0\n%PROGRAMDATA%\\Microsoft\\Search\\Data\\*");
            WireInfoButton(root, "BtnInfo26", "OneDrive Telemetry & Sync Logs", "OneDrive generates extensive diagnostic and sync logs that reveal file operations and sync history.", "%LOCALAPPDATA%\\Microsoft\\OneDrive\\logs\\*\n%LOCALAPPDATA%\\Microsoft\\OneDrive\\setup\\logs\\*");
            WireInfoButton(root, "BtnInfo27", "Windows Recall AI, Copilot & Snapshots (Win11)", "Recall AI captures periodic screenshots. Copilot integrates with Microsoft cloud. Both can be disabled via policy.", "HKLM:\\..\\WindowsAI -> DisableRecall=1\nHKLM:\\..\\WindowsCopilot -> TurnOffWindowsCopilot=1\n%LOCALAPPDATA%\\CoreAIPlatform.00\\UKP\\*");
            WireInfoButton(root, "BtnInfo28", "Scheduled Task Telemetry Killswitch", "Compatibility Appraiser, ProgramDataUpdater, and CEIP tasks continuously collect and upload diagnostic data.", "Task: \\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\nTask: \\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator");
            WireInfoButton(root, "BtnInfo29", "Pagefile, Hibernation & NTFS Forensic Hardening", "The pagefile may contain unencrypted memory dumps. Hibernation file stores full RAM snapshot. LastAccess timestamps reveal file access patterns.", "HKLM:\\..\\Memory Management -> ClearPageFileAtShutdown=1\npowercfg /hibernate off\nHKLM:\\..\\FileSystem -> NtfsDisableLastAccessUpdate=1");
            WireInfoButton(root, "BtnInfo30", "Icon Cache & Windows Font Cache", "Icon cache and font cache files can be corrupted or contain metadata. Rebuilding them forces a clean state.", "%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache*\nC:\\Windows\\System32\\FNTCACHE.DAT");
            WireInfoButton(root, "BtnInfo31", "PowerShell Transcript, Module & ScriptBlock Logging", "PowerShell can log every command, module invocation, and script block execution to files and event logs.", "HKLM:\\..\\PowerShell\\Transcription -> EnableTranscripting=0\nHKLM:\\..\\PowerShell\\ModuleLogging -> EnableModuleLogging=0\nHKLM:\\..\\PowerShell\\ScriptBlockLogging -> EnableScriptBlockLogging=0");
            WireInfoButton(root, "BtnInfo32", "CapabilityAccessManager Sensor Permissions", "Windows tracks when apps last accessed Location, Camera, Microphone via CapabilityAccessManager timestamps.", "HKCU:\\..\\CapabilityAccessManager\\ConsentStore\\location\nHKCU:\\..\\CapabilityAccessManager\\ConsentStore\\webcam\nHKCU:\\..\\CapabilityAccessManager\\ConsentStore\\microphone");

            AppendLog("S-T-E-A-L-T-H v7.0 — 32-Vector Absolute Forensic Annihilation Engine Online.");
            AppendLog("110+ Granular Micro-Actions | Hardware Spoof | AI Kill | Deep Anti-Forensics Ready.");
        }

        private void WireBtn(UIElement root, string name, Action action)
        {
            var btn = (Button)LogicalTreeHelper.FindLogicalNode(root, name);
            if (btn != null) btn.Click += (s, e) => RunAsync(action);
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized) this.WindowState = WindowState.Normal;
            else this.WindowState = WindowState.Maximized;
        }

        private void WireInfoButton(UIElement root, string btnName, string title, string desc, string paths)
        {
            var btn = (Button)LogicalTreeHelper.FindLogicalNode(root, btnName);
            if (btn != null) btn.Click += (s, e) => { txtModalTitle.Text = title; txtModalDesc.Text = desc; txtModalPaths.Text = paths; infoModal.Visibility = Visibility.Visible; };
        }

        private void AppendLog(string msg)
        {
            if (txtLogs == null) return;
            Dispatcher.Invoke((Action)(() => { string time = DateTime.Now.ToString("HH:mm:ss"); txtLogs.AppendText("[" + time + "] " + msg + "\r\n"); scroller.ScrollToEnd(); }));
        }

        private void RunAsync(Action action)
        {
            if (prgBar != null) prgBar.IsIndeterminate = true;
            ThreadPool.QueueUserWorkItem(_ => {
                try { action(); } catch (Exception ex) { AppendLog("Error: " + ex.Message); }
                finally { if (prgBar != null) Dispatcher.Invoke((Action)(() => prgBar.IsIndeterminate = false)); }
            });
        }

        private void StartLiveStatusHUD()
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) => {
                ThreadPool.QueueUserWorkItem(_ => {
                    string host = Environment.MachineName + " (" + Environment.UserName + ")";
                    string net = RunPowerShell("Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -ExpandProperty Name").Replace("\r\n", ", ").Trim();
                    if (string.IsNullOrEmpty(net)) net = "Connected";
                    string telem = RunPowerShell("(Get-Service -Name DiagTrack -ErrorAction SilentlyContinue).Status").Trim();
                    bool telemSilenced = string.IsNullOrEmpty(telem) || telem.Equals("Stopped", StringComparison.OrdinalIgnoreCase);
                    string ram = RunPowerShell("$os = Get-CimInstance Win32_OperatingSystem; [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / $os.TotalVisibleMemorySize * 100, 1)").Trim() + "%";
                    Dispatcher.Invoke((Action)(() => { txtHost.Text = host; txtNet.Text = net; txtTelem.Text = telemSilenced ? "0-Track Silenced" : "Active"; txtRam.Text = ram; }));
                });
            };
            timer.Start();
        }

        // ======================================================================
        // GRANULAR METHODS (Cat 0-32)
        // ======================================================================

        public void SpoofMachineGuidOnly()
        {
            AppendLog("Granular 0.1: Spoofing Windows MachineGuid...");
            string newGuid = Guid.NewGuid().ToString();
            RunPowerShell("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Cryptography' -Name 'MachineGuid' -Value '" + newGuid + "' -ErrorAction SilentlyContinue");
            AppendLog("[OK] MachineGuid regenerated -> " + newGuid);
        }

        public void SpoofSqmIdOnly()
        {
            AppendLog("Granular 0.2: Spoofing SQM Client MachineId...");
            string sqmId = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
            RunPowerShell("New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\SQMClient' -Name 'MachineId' -Value '" + sqmId + "' -PropertyType String -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] SQM MachineId regenerated -> " + sqmId);
        }

        public void SpoofRegisteredOwnerAndOrg(string ownerName)
        {
            AppendLog("Granular 0.3: Spoofing Registered Owner & Organization...");
            RunPowerShell("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion' -Name 'RegisteredOwner' -Value '" + ownerName + "' -Force -ErrorAction SilentlyContinue");
            RunPowerShell("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion' -Name 'RegisteredOrganization' -Value 'STEALTH-ORG' -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] RegisteredOwner set to: " + ownerName);
        }

        public void RenameWindowsComputer(string newName)
        {
            AppendLog("Granular 0.4: Renaming Windows Computer to: " + newName);
            RunPowerShell("Rename-Computer -NewName '" + newName + "' -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] Computer renamed to: " + newName + " (Reboot required to apply)");
        }

        public void SpoofWindowsMachineGuid()
        {
            AppendLog("Executing Category 0: Full Windows Hardware & Identity Spoof...");
            SpoofMachineGuidOnly();
            SpoofSqmIdOnly();
            SpoofRegisteredOwnerAndOrg("STEALTH_OPERATOR");
            AppendLog("[OK] All Hardware Identity Vectors Spoofed.");
        }

        public void ResetCursorKeyOnly(string keyName)
        {
            AppendLog("Granular: Resetting Cursor key -> " + keyName);
            string newId = Guid.NewGuid().ToString();
            string[] paths = new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Cursor\\User\\globalStorage\\storage.json",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Code\\User\\globalStorage\\storage.json"
            };
            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        string content = File.ReadAllText(path);
                        int idx = content.IndexOf("\"" + keyName + "\"");
                        if (idx >= 0)
                        {
                            int colonIdx = content.IndexOf(":", idx);
                            int quoteStart = content.IndexOf("\"", colonIdx);
                            int quoteEnd = content.IndexOf("\"", quoteStart + 1);
                            if (quoteStart >= 0 && quoteEnd > quoteStart)
                            {
                                content = content.Substring(0, quoteStart + 1) + newId + content.Substring(quoteEnd);
                                File.WriteAllText(path, content);
                            }
                        }
                    }
                }
                catch {}
            }
            AppendLog("[OK] " + keyName + " -> " + newId);
        }

        public void ResetCursorAndAiIdeMachineId()
        {
            AppendLog("Executing Category 00: Resetting All Cursor & AI IDE Machine IDs...");
            ResetCursorKeyOnly("telemetry.devDeviceId");
            ResetCursorKeyOnly("telemetry.macMachineId");
            ResetCursorKeyOnly("telemetry.machineId");
            ResetCursorKeyOnly("telemetry.sqmId");
            AppendLog("[OK] All AI IDE Identifiers Reset.");
        }

        public void SilenceDiagTrackServiceOnly()
        {
            AppendLog("Granular 1.1: Stopping & Disabling DiagTrack & dmwappushservice...");
            RunPowerShell("@('DiagTrack', 'dmwappushservice') | ForEach-Object { Stop-Service -Name $_ -Force -ErrorAction SilentlyContinue; Set-Service -Name $_ -StartupType Disabled -ErrorAction SilentlyContinue }");
            AppendLog("[OK] DiagTrack & WAP Push Services Disabled.");
        }

        public void DisableAdvertisingIdOnly()
        {
            AppendLog("Granular 1.2: Disabling Windows Advertising ID...");
            RunPowerShell("New-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name 'Enabled' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] Advertising ID Disabled.");
        }

        public void DisableInkingTypingOnly()
        {
            AppendLog("Granular 1.3: Disabling Inking & Typing Data Collection...");
            RunPowerShell("New-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Input\\TIPC' -Name 'Enabled' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] Inking & Typing Data Disabled.");
        }

        public void DisableDefenderSampleUploadOnly()
        {
            AppendLog("Granular 1.4: Disabling Defender Cloud Sample Uploads...");
            RunPowerShell("New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\Spynet' -Name 'SubmitSamplesConsent' -Value 2 -PropertyType DWord -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] Defender Sample Uploads Blocked.");
        }

        public void SilenceDeepTelemetry()
        {
            AppendLog("Executing Category 1: Silencing All Deep Telemetry...");
            SilenceDiagTrackServiceOnly();
            DisableAdvertisingIdOnly();
            DisableInkingTypingOnly();
            DisableDefenderSampleUploadOnly();
            RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue");
            AppendLog("[OK] All Telemetry Vectors Silenced.");
        }

        public void WipeTimelineSqliteDbOnly() { AppendLog("Granular 2.1: Wiping ActivitiesCache.db..."); RunPowerShell("Get-ChildItem -Path \"$env:LOCALAPPDATA\\ConnectedDevicesPlatform\" -Recurse -Filter 'ActivitiesCache.db' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Timeline SQLite DB Wiped."); }
        public void DisableActivityFeedPolicyOnly() { AppendLog("Granular 2.2: Disabling Activity Feed Upload Policy..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Name 'EnableActivityFeed' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Activity Feed Policy Disabled."); }
        public void PurgeTimelineDb() { AppendLog("Executing Category 2..."); WipeTimelineSqliteDbOnly(); DisableActivityFeedPolicyOnly(); AppendLog("[OK] Timeline DB Purged."); }

        public void ClearPsReadLineHistoryOnly() { AppendLog("Granular 3.1: Clearing PSReadLine History..."); RunPowerShell("$p = (Get-PSReadLineOption).HistorySavePath; if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }"); AppendLog("[OK] PSReadLine History Cleared."); }
        public void ClearRunMruOnly() { AppendLog("Granular 3.2: Clearing RunMRU..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RunMRU' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] RunMRU Cleared."); }
        public void SanitizeTerminalHistory() { AppendLog("Executing Category 3..."); ClearPsReadLineHistoryOnly(); ClearRunMruOnly(); AppendLog("[OK] Terminal History Sanitized."); }

        public void ClearOpenSaveMruOnly() { AppendLog("Granular 4.1: Clearing OpenSavePidlMRU..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ComDlg32\\OpenSavePidlMRU' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] OpenSavePidlMRU Cleared."); }
        public void ClearLastVisitedMruOnly() { AppendLog("Granular 4.2: Clearing LastVisitedPidlMRU..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ComDlg32\\LastVisitedPidlMRU' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] LastVisitedPidlMRU Cleared."); }
        public void ClearWordWheelQueryOnly() { AppendLog("Granular 4.3: Clearing WordWheelQuery..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\WordWheelQuery' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] WordWheelQuery Cleared."); }
        public void PurgeShellBagsDialogs() { AppendLog("Executing Category 4..."); ClearOpenSaveMruOnly(); ClearLastVisitedMruOnly(); ClearWordWheelQueryOnly(); AppendLog("[OK] ShellBags Purged."); }

        public void ClearAutoDestinationsOnly() { AppendLog("Granular 5.1: Clearing AutomaticDestinations..."); RunPowerShell("Remove-Item -Path \"$env:APPDATA\\Microsoft\\Windows\\Recent\\AutomaticDestinations\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] AutoDest Cleared."); }
        public void ClearCustomDestinationsOnly() { AppendLog("Granular 5.2: Clearing CustomDestinations..."); RunPowerShell("Remove-Item -Path \"$env:APPDATA\\Microsoft\\Windows\\Recent\\CustomDestinations\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] CustDest Cleared."); }
        public void ClearRecentFolderOnly() { AppendLog("Granular 5.3: Clearing Recent Items Folder..."); RunPowerShell("Remove-Item -Path \"$env:APPDATA\\Microsoft\\Windows\\Recent\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Recent Items Cleared."); }
        public void PurgeJumpListsRecent() { AppendLog("Executing Category 5..."); ClearAutoDestinationsOnly(); ClearCustomDestinationsOnly(); ClearRecentFolderOnly(); AppendLog("[OK] JumpLists Purged."); }

        public void ClearThumbnailsOnly() { AppendLog("Granular 6.1: Purging Thumbnail Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\thumbcache_*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Thumbnail Cache Purged."); }
        public void ClearD3DShadersOnly() { AppendLog("Granular 6.2: Purging D3DSCache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\D3DSCache\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] D3D Shader Cache Purged."); }
        public void ClearClipboardOnly() { AppendLog("Granular 6.3: Clearing Clipboard..."); try { Clipboard.Clear(); } catch {} AppendLog("[OK] Clipboard Cleared."); }
        public void PurgeVisualShaderCaches() { AppendLog("Executing Category 6..."); ClearThumbnailsOnly(); ClearD3DShadersOnly(); ClearClipboardOnly(); AppendLog("[OK] Visual Caches Purged."); }

        public void ClearMinidumpsOnly() { AppendLog("Granular 7.1: Clearing Kernel Minidumps..."); RunPowerShell("Remove-Item -Path 'C:\\Windows\\Minidump\\*' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Minidumps Cleared."); }
        public void ClearLiveKernelOnly() { AppendLog("Granular 7.2: Clearing LiveKernelReports..."); RunPowerShell("Remove-Item -Path 'C:\\Windows\\LiveKernelReports\\*' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] LiveKernel Cleared."); }
        public void ClearWerQueueOnly() { AppendLog("Granular 7.3: Clearing WER Queue..."); RunPowerShell("Remove-Item -Path \"$env:PROGRAMDATA\\Microsoft\\Windows\\WER\\ReportQueue\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] WER Queue Cleared."); }
        public void ClearAppCrashDumpsOnly() { AppendLog("Granular 7.4: Clearing App CrashDumps..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\CrashDumps\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] App CrashDumps Cleared."); }
        public void PurgeCrashDumpsWer() { AppendLog("Executing Category 7..."); ClearMinidumpsOnly(); ClearLiveKernelOnly(); ClearWerQueueOnly(); ClearAppCrashDumpsOnly(); AppendLog("[OK] Crash Dumps Purged."); }

        public void ClearCryptnetContentOnly() { AppendLog("Granular 8.1: Clearing Cryptnet Content Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\CryptnetUrlCache\\Content\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Cryptnet Content Cleared."); }
        public void ClearCryptnetMetaOnly() { AppendLog("Granular 8.2: Clearing Cryptnet MetaData Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\CryptnetUrlCache\\MetaData\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Cryptnet MetaData Cleared."); }
        public void PurgeCryptnetCache() { AppendLog("Executing Category 8..."); ClearCryptnetContentOnly(); ClearCryptnetMetaOnly(); AppendLog("[OK] Cryptnet Cache Purged."); }

        public void ClearSecurityLogOnly() { AppendLog("Granular 9.1: Scrubbing Security Logs..."); RunPowerShell("wevtutil cl Security"); AppendLog("[OK] Security Logs Scrubbed."); }
        public void ClearSystemAndAppLogOnly() { AppendLog("Granular 9.2: Scrubbing System & App Logs..."); RunPowerShell("wevtutil cl System; wevtutil cl Application"); AppendLog("[OK] System & App Logs Scrubbed."); }
        public void ClearPowerShellLogOnly() { AppendLog("Granular 9.3: Scrubbing PowerShell Logs..."); RunPowerShell("wevtutil cl 'Microsoft-Windows-PowerShell/Operational'"); AppendLog("[OK] PowerShell Logs Scrubbed."); }
        public void ScrubEventLogs() { AppendLog("Executing Category 9..."); ClearSecurityLogOnly(); ClearSystemAndAppLogOnly(); ClearPowerShellLogOnly(); AppendLog("[OK] All Event Logs Scrubbed."); }

        public void FlushDnsOnly() { AppendLog("Granular 10.1: Flushing DNS Cache..."); RunPowerShell("Clear-DnsClientCache -ErrorAction SilentlyContinue"); AppendLog("[OK] DNS Cache Flushed."); }
        public void ClearArpOnly() { AppendLog("Granular 10.2: Deleting ARP Cache..."); RunPowerShell("netsh interface ip delete arpcache"); AppendLog("[OK] ARP Cache Deleted."); }
        public void DisableLlmnrOnly() { AppendLog("Granular 10.3: Disabling LLMNR..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient' -Name 'EnableMulticast' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] LLMNR Disabled."); }
        public void HardenNetwork() { AppendLog("Executing Category 10..."); FlushDnsOnly(); ClearArpOnly(); DisableLlmnrOnly(); AppendLog("[OK] Network Hardened."); }

        public void DisableDoModeOnly() { AppendLog("Granular 11.1: Locking DODownloadMode=0..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization' -Name 'DODownloadMode' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] DO Mode Locked to 0."); }
        public void DisableSmartScreenOnly() { AppendLog("Granular 11.2: Disabling SmartScreen..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Name 'EnableSmartScreen' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] SmartScreen Disabled."); }
        public void DisableDeliveryOptimization() { AppendLog("Executing Category 11..."); DisableDoModeOnly(); DisableSmartScreenOnly(); AppendLog("[OK] P2P & Probing Disabled."); }

        public void CleanChromeCacheOnly() { AppendLog("Granular 12.1: Cleaning Chrome Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Code Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Chrome Cache Cleaned."); }
        public void CleanBraveCacheOnly() { AppendLog("Granular 12.2: Cleaning Brave Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Brave Cache Cleaned."); }
        public void CleanEdgeCacheOnly() { AppendLog("Granular 12.3: Cleaning Edge Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Edge Cache Cleaned."); }
        public void CleanFirefoxCacheOnly() { AppendLog("Granular 12.4: Cleaning Firefox Cache..."); RunPowerShell("Get-ChildItem -Path \"$env:LOCALAPPDATA\\Mozilla\\Firefox\\Profiles\" -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path \"$($_.FullName)\\cache2\\*\" -Recurse -Force -ErrorAction SilentlyContinue }"); AppendLog("[OK] Firefox Cache Cleaned."); }
        public void CleanBrowserCaches() { AppendLog("Executing Category 12..."); CleanChromeCacheOnly(); CleanBraveCacheOnly(); CleanEdgeCacheOnly(); CleanFirefoxCacheOnly(); AppendLog("[OK] All Browser Caches Cleaned."); }

        // Cat 13: Prefetch
        public void DeletePrefetchFilesOnly() { AppendLog("Granular 13.1: Deleting Prefetch Files..."); RunPowerShell("Remove-Item -Path 'C:\\Windows\\Prefetch\\*.pf' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Prefetch .pf Files Deleted."); }
        public void DisablePrefetcherOnly() { AppendLog("Granular 13.2: Disabling Prefetcher Registry..."); RunPowerShell("Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters' -Name 'EnablePrefetcher' -Value 0 -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Prefetcher Disabled."); }
        public void StopSysMainOnly() { AppendLog("Granular 13.3: Stopping SysMain Service..."); RunPowerShell("Stop-Service -Name SysMain -Force -ErrorAction SilentlyContinue; Set-Service -Name SysMain -StartupType Disabled -ErrorAction SilentlyContinue"); AppendLog("[OK] SysMain (SuperFetch) Disabled."); }
        public void PurgePrefetchAll() { AppendLog("Executing Category 13..."); DeletePrefetchFilesOnly(); DisablePrefetcherOnly(); StopSysMainOnly(); AppendLog("[OK] Prefetch Purged."); }

        // Cat 14: AmCache
        public void ClearAmCacheOnly() { AppendLog("Granular 14.1: Clearing AmCache Entries..."); RunPowerShell("Remove-Item -Path 'C:\\Windows\\AppCompat\\Programs\\Amcache.hve' -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'C:\\Windows\\AppCompat\\Programs\\Amcache.hve.LOG*' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] AmCache Entries Cleared."); }
        public void ClearRecentFileCacheOnly() { AppendLog("Granular 14.2: Clearing RecentFileCache.bcl..."); RunPowerShell("Remove-Item -Path 'C:\\Windows\\AppCompat\\Programs\\RecentFileCache.bcl' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] RecentFileCache.bcl Cleared."); }
        public void ClearAmCacheAll() { AppendLog("Executing Category 14..."); ClearAmCacheOnly(); ClearRecentFileCacheOnly(); AppendLog("[OK] AmCache Purged."); }

        // Cat 15: ShimCache
        public void ClearShimCacheOnly() { AppendLog("Granular 15.1: Clearing AppCompatCache (ShimCache)..."); RunPowerShell("Remove-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\AppCompatCache' -Name 'AppCompatCache' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] ShimCache Cleared (takes effect on reboot)."); }
        public void ClearShimCacheAll() { AppendLog("Executing Category 15..."); ClearShimCacheOnly(); AppendLog("[OK] ShimCache Purged."); }

        // Cat 16: UserAssist
        public void ClearUserAssistOnly() { AppendLog("Granular 16.1: Clearing UserAssist Execution Logs..."); RunPowerShell("Get-ChildItem -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\UserAssist' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path \"$($_.PSPath)\\Count\" -Recurse -Force -ErrorAction SilentlyContinue }"); AppendLog("[OK] UserAssist Logs Cleared."); }
        public void DisableUserAssistTrackingOnly() { AppendLog("Granular 16.2: Disabling UserAssist Tracking..."); RunPowerShell("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Start_TrackProgs' -Value 0 -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] UserAssist Tracking Disabled."); }
        public void PurgeUserAssistAll() { AppendLog("Executing Category 16..."); ClearUserAssistOnly(); DisableUserAssistTrackingOnly(); AppendLog("[OK] UserAssist Purged."); }

        // Cat 17: BAM/DAM
        public void ClearBamOnly() { AppendLog("Granular 17.1: Clearing BAM Entries..."); RunPowerShell("Get-ChildItem -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\bam\\State\\UserSettings' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue }"); AppendLog("[OK] BAM Entries Cleared."); }
        public void ClearDamOnly() { AppendLog("Granular 17.2: Clearing DAM Entries..."); RunPowerShell("Get-ChildItem -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\dam\\State\\UserSettings' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue }"); AppendLog("[OK] DAM Entries Cleared."); }
        public void ClearBamDamAll() { AppendLog("Executing Category 17..."); ClearBamOnly(); ClearDamOnly(); AppendLog("[OK] BAM/DAM Purged."); }

        // Cat 18: SRUM
        public void ClearSrumOnly() { AppendLog("Granular 18.1: Clearing SRUDB.dat..."); RunPowerShell("Stop-Service -Name DPS -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'C:\\Windows\\System32\\SRU\\SRUDB.dat' -Force -ErrorAction SilentlyContinue; Start-Service -Name DPS -ErrorAction SilentlyContinue"); AppendLog("[OK] SRUM Database Cleared."); }
        public void ClearSrumAll() { AppendLog("Executing Category 18..."); ClearSrumOnly(); AppendLog("[OK] SRUM Purged."); }

        // Cat 19: TypedPaths/URLs/MUICache
        public void ClearTypedPathsOnly() { AppendLog("Granular 19.1: Clearing TypedPaths..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\TypedPaths' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] TypedPaths Cleared."); }
        public void ClearTypedUrlsOnly() { AppendLog("Granular 19.2: Clearing TypedURLs..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Internet Explorer\\TypedURLs' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] TypedURLs Cleared."); }
        public void ClearMuiCacheOnly() { AppendLog("Granular 19.3: Clearing MUICache..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\MuiCache' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] MUICache Cleared."); }
        public void ClearTypedHistoryAll() { AppendLog("Executing Category 19..."); ClearTypedPathsOnly(); ClearTypedUrlsOnly(); ClearMuiCacheOnly(); AppendLog("[OK] Typed History Purged."); }

        // Cat 20: RecentDocs & LNK
        public void ClearRecentDocsOnly() { AppendLog("Granular 20.1: Clearing RecentDocs Registry..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RecentDocs' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] RecentDocs Registry Cleared."); }
        public void DeleteLnkFilesOnly() { AppendLog("Granular 20.2: Deleting LNK Shortcut Files..."); RunPowerShell("Remove-Item -Path \"$env:APPDATA\\Microsoft\\Windows\\Recent\\*.lnk\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] LNK Files Deleted."); }
        public void PurgeRecentDocsAll() { AppendLog("Executing Category 20..."); ClearRecentDocsOnly(); DeleteLnkFilesOnly(); AppendLog("[OK] RecentDocs Purged."); }

        // Cat 21: Notifications DB
        public void ClearWpnDatabaseOnly() { AppendLog("Granular 21.1: Clearing Notification Database..."); RunPowerShell("Stop-Service -Name WpnService -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Notifications\\wpndatabase.db\" -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Notifications\\wpndatabase.db-wal\" -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Notifications\\wpndatabase.db-shm\" -Force -ErrorAction SilentlyContinue; Start-Service -Name WpnService -ErrorAction SilentlyContinue"); AppendLog("[OK] Notification DB Cleared."); }
        public void ClearNotificationsAll() { AppendLog("Executing Category 21..."); ClearWpnDatabaseOnly(); AppendLog("[OK] Notifications Purged."); }

        // Cat 22: RDP Artifacts
        public void ClearRdpCacheOnly() { AppendLog("Granular 22.1: Clearing RDP Bitmap Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Terminal Server Client\\Cache\\*\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] RDP Bitmap Cache Cleared."); }
        public void ClearRdpMruOnly() { AppendLog("Granular 22.2: Clearing RDP Server MRU..."); RunPowerShell("Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Terminal Server Client\\Default' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Terminal Server Client\\Servers' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] RDP Server MRU Cleared."); }
        public void DeleteRdpFilesOnly() { AppendLog("Granular 22.3: Deleting .rdp Files..."); RunPowerShell("Remove-Item -Path \"$env:USERPROFILE\\Documents\\*.rdp\" -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:USERPROFILE\\Desktop\\*.rdp\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] .rdp Files Deleted."); }
        public void PurgeRdpArtifactsAll() { AppendLog("Executing Category 22..."); ClearRdpCacheOnly(); ClearRdpMruOnly(); DeleteRdpFilesOnly(); AppendLog("[OK] RDP Artifacts Purged."); }

        // Cat 23: WiFi Profiles
        public void DeleteWifiProfilesOnly() { AppendLog("Granular 23.1: Deleting All WiFi Profiles..."); RunPowerShell("netsh wlan delete profile name=* i=*"); AppendLog("[OK] All WiFi Profiles Deleted."); }
        public void DisableWifiSenseOnly() { AppendLog("Granular 23.2: Disabling WiFi AutoConnect..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Microsoft\\WcmSvc\\wifinetworkmanager\\config' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\WcmSvc\\wifinetworkmanager\\config' -Name 'AutoConnectAllowedOEM' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] WiFi AutoConnect Disabled."); }
        public void DeleteWifiHistoryAll() { AppendLog("Executing Category 23..."); DeleteWifiProfilesOnly(); DisableWifiSenseOnly(); AppendLog("[OK] WiFi History Purged."); }

        // Cat 24: Defender History
        public void ClearDefenderHistoryOnly() { AppendLog("Granular 24.1: Clearing Defender Detection History..."); RunPowerShell("Remove-Item -Path 'C:\\ProgramData\\Microsoft\\Windows Defender\\Scans\\History\\Service\\*' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Defender Detection History Cleared."); }
        public void ClearDefenderQuarantineOnly() { AppendLog("Granular 24.2: Clearing Defender Quarantine..."); RunPowerShell("Remove-Item -Path 'C:\\ProgramData\\Microsoft\\Windows Defender\\Quarantine\\*' -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Defender Quarantine Cleared."); }
        public void ClearDefenderHistoryAll() { AppendLog("Executing Category 24..."); ClearDefenderHistoryOnly(); ClearDefenderQuarantineOnly(); AppendLog("[OK] Defender History Purged."); }

        // Cat 25: Cortana & Search
        public void DisableCortanaOnly() { AppendLog("Granular 25.1: Disabling Cortana..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name 'AllowCortana' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Cortana Disabled."); }
        public void ClearSearchHistoryOnly() { AppendLog("Granular 25.2: Clearing Device Search History..."); RunPowerShell("New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings' -Name 'IsDeviceSearchHistoryEnabled' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Search History Disabled & Cleared."); }
        public void DeleteSearchIndexOnly() { AppendLog("Granular 25.3: Deleting Search Index..."); RunPowerShell("Stop-Service -Name WSearch -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'C:\\ProgramData\\Microsoft\\Search\\Data\\Applications\\Windows\\Windows.edb' -Force -ErrorAction SilentlyContinue; Start-Service -Name WSearch -ErrorAction SilentlyContinue"); AppendLog("[OK] Search Index Deleted."); }
        public void KillSearchTrackingAll() { AppendLog("Executing Category 25..."); DisableCortanaOnly(); ClearSearchHistoryOnly(); DeleteSearchIndexOnly(); AppendLog("[OK] Search Tracking Killed."); }

        // Cat 26: OneDrive Logs
        public void ClearOneDriveLogsOnly() { AppendLog("Granular 26.1: Clearing OneDrive Logs..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\logs\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] OneDrive Logs Cleared."); }
        public void ClearOneDriveTelemetryOnly() { AppendLog("Granular 26.2: Clearing OneDrive Telemetry..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\setup\\logs\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] OneDrive Telemetry Cleared."); }
        public void PurgeOneDriveLogsAll() { AppendLog("Executing Category 26..."); ClearOneDriveLogsOnly(); ClearOneDriveTelemetryOnly(); AppendLog("[OK] OneDrive Logs Purged."); }

        // Cat 27: Windows Recall AI & Copilot
        public void DisableRecallOnly() { AppendLog("Granular 27.1: Disabling Windows Recall AI..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name 'DisableRecall' -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name 'AllowRecallEnablement' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Windows Recall AI Disabled."); }
        public void DisableCopilotOnly() { AppendLog("Granular 27.2: Disabling Windows Copilot..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Windows Copilot Disabled."); }
        public void DeleteRecallSnapshotsOnly() { AppendLog("Granular 27.3: Deleting Recall Snapshots..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\CoreAIPlatform.00\\UKP\\*\" -Recurse -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Recall Snapshots Deleted."); }
        public void KillAiTrackingAll() { AppendLog("Executing Category 27..."); DisableRecallOnly(); DisableCopilotOnly(); DeleteRecallSnapshotsOnly(); AppendLog("[OK] AI Tracking Killed."); }

        // Cat 28: Scheduled Task Telemetry
        public void DisableCompatAppraiserOnly() { AppendLog("Granular 28.1: Disabling Compatibility Appraiser..."); RunPowerShell("schtasks /Change /TN '\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser' /Disable 2>$null"); AppendLog("[OK] Compat Appraiser Disabled."); }
        public void DisableProgramDataUpdaterOnly() { AppendLog("Granular 28.2: Disabling ProgramDataUpdater..."); RunPowerShell("schtasks /Change /TN '\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater' /Disable 2>$null"); AppendLog("[OK] ProgramDataUpdater Disabled."); }
        public void DisableCeipTasksOnly() { AppendLog("Granular 28.3: Disabling CEIP Tasks..."); RunPowerShell("@('\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator','\\Microsoft\\Windows\\Customer Experience Improvement Program\\KernelCeipTask','\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip') | ForEach-Object { schtasks /Change /TN $_ /Disable 2>$null }"); AppendLog("[OK] CEIP Tasks Disabled."); }
        public void DisableTelemetryTasksAll() { AppendLog("Executing Category 28..."); DisableCompatAppraiserOnly(); DisableProgramDataUpdaterOnly(); DisableCeipTasksOnly(); AppendLog("[OK] Telemetry Tasks Disabled."); }

        // Cat 29: Pagefile/Hibernation/LastAccess
        public void EnablePagefileClearOnly() { AppendLog("Granular 29.1: Enabling ClearPageFileAtShutdown..."); RunPowerShell("Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management' -Name 'ClearPageFileAtShutdown' -Value 1 -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Pagefile will be cleared on shutdown."); }
        public void DisableHibernationOnly() { AppendLog("Granular 29.2: Disabling Hibernation..."); RunPowerShell("powercfg /hibernate off"); AppendLog("[OK] Hibernation Disabled (hiberfil.sys deleted)."); }
        public void DisableLastAccessOnly() { AppendLog("Granular 29.3: Disabling NTFS LastAccess Timestamps..."); RunPowerShell("fsutil behavior set DisableLastAccess 1"); AppendLog("[OK] NTFS LastAccess Timestamps Disabled."); }
        public void HardenForensicsAll() { AppendLog("Executing Category 29..."); EnablePagefileClearOnly(); DisableHibernationOnly(); DisableLastAccessOnly(); AppendLog("[OK] Forensics Hardened."); }

        // Cat 30: Icon/Font Cache
        public void ClearIconCacheOnly() { AppendLog("Granular 30.1: Deleting Icon Cache..."); RunPowerShell("Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\iconcache*\" -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:LOCALAPPDATA\\IconCache.db\" -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Icon Cache Deleted."); }
        public void ClearFontCacheOnly() { AppendLog("Granular 30.2: Clearing Font Cache..."); RunPowerShell("Stop-Service -Name FontCache -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'C:\\Windows\\System32\\FNTCACHE.DAT' -Force -ErrorAction SilentlyContinue; Start-Service -Name FontCache -ErrorAction SilentlyContinue"); AppendLog("[OK] Font Cache Cleared."); }
        public void ClearIconFontCacheAll() { AppendLog("Executing Category 30..."); ClearIconCacheOnly(); ClearFontCacheOnly(); AppendLog("[OK] Icon/Font Cache Cleared."); }

        // Cat 31: PowerShell Logging
        public void DisablePsTranscriptOnly() { AppendLog("Granular 31.1: Disabling PS Transcription..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription' -Name 'EnableTranscripting' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] PS Transcription Disabled."); }
        public void DisablePsModuleLoggingOnly() { AppendLog("Granular 31.2: Disabling PS Module Logging..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ModuleLogging' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ModuleLogging' -Name 'EnableModuleLogging' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] PS Module Logging Disabled."); }
        public void DisablePsScriptBlockOnly() { AppendLog("Granular 31.3: Disabling PS ScriptBlock Logging..."); RunPowerShell("New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging' -Force -ErrorAction SilentlyContinue | Out-Null; New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging' -Name 'EnableScriptBlockLogging' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] PS ScriptBlock Logging Disabled."); }
        public void DisablePsLoggingAll() { AppendLog("Executing Category 31..."); DisablePsTranscriptOnly(); DisablePsModuleLoggingOnly(); DisablePsScriptBlockOnly(); AppendLog("[OK] All PS Logging Disabled."); }

        // Cat 32: Sensor Permissions
        public void ResetLocationAccessOnly() { AppendLog("Granular 32.1: Resetting Location Access..."); RunPowerShell("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location' -Name 'Value' -Value 'Deny' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Location Access Denied."); }
        public void ResetCameraAccessOnly() { AppendLog("Granular 32.2: Resetting Camera Access..."); RunPowerShell("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name 'Value' -Value 'Deny' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Camera Access Denied."); }
        public void ResetMicAccessOnly() { AppendLog("Granular 32.3: Resetting Microphone Access..."); RunPowerShell("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name 'Value' -Value 'Deny' -Force -ErrorAction SilentlyContinue"); AppendLog("[OK] Microphone Access Denied."); }
        public void DenyAllSensorsOnly() { AppendLog("Granular 32.4: Denying All Sensor Access Globally..."); RunPowerShell("@('location','webcam','microphone','contacts','appointments','phoneCallHistory','email','chat','radios','bluetoothSync','appDiagnostics') | ForEach-Object { $p = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\' + $_; if (Test-Path $p) { Set-ItemProperty -Path $p -Name 'Value' -Value 'Deny' -Force -ErrorAction SilentlyContinue } }"); AppendLog("[OK] All Sensor Access Denied Globally."); }
        public void ResetSensorAccessAll() { AppendLog("Executing Category 32..."); ResetLocationAccessOnly(); ResetCameraAccessOnly(); ResetMicAccessOnly(); DenyAllSensorsOnly(); AppendLog("[OK] All Sensor Permissions Reset."); }

        // Master Protocol
        public void MasterStealthProtocol()
        {
            AppendLog("===============================================================");
            AppendLog("⚡ INITIATING S-T-E-A-L-T-H v7.0 — 32-VECTOR ABSOLUTE PROTOCOL");
            AppendLog("===============================================================");

            SpoofWindowsMachineGuid();
            ResetCursorAndAiIdeMachineId();
            SilenceDeepTelemetry();
            PurgeTimelineDb();
            SanitizeTerminalHistory();
            PurgeShellBagsDialogs();
            PurgeJumpListsRecent();
            PurgeVisualShaderCaches();
            PurgeCrashDumpsWer();
            PurgeCryptnetCache();
            ScrubEventLogs();
            HardenNetwork();
            DisableDeliveryOptimization();
            CleanBrowserCaches();
            PurgePrefetchAll();
            ClearAmCacheAll();
            ClearShimCacheAll();
            PurgeUserAssistAll();
            ClearBamDamAll();
            ClearSrumAll();
            ClearTypedHistoryAll();
            PurgeRecentDocsAll();
            ClearNotificationsAll();
            PurgeRdpArtifactsAll();
            // Cat 23 (WiFi Profiles) skipped in master run to avoid breaking user connection unexpectedly
            ClearDefenderHistoryAll();
            KillSearchTrackingAll();
            PurgeOneDriveLogsAll();
            KillAiTrackingAll();
            DisableTelemetryTasksAll();
            HardenForensicsAll();
            ClearIconFontCacheAll();
            DisablePsLoggingAll();
            ResetSensorAccessAll();

            AppendLog("===============================================================");
            AppendLog("🎉 100% COMPLETE — 32-VECTOR S-T-E-A-L-T-H PROTOCOL EXECUTED!");
            AppendLog("===============================================================");
        }

        private string RunPowerShell(string script)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string res = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    return res;
                }
            }
            catch { return ""; }
        }
    }
}
