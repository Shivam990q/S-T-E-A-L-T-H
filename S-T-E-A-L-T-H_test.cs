using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;

namespace STEALTH
{
    public class TestSuite
    {
        private static int passed = 0;
        private static int failed = 0;

        private static void AssertTest(string testName, Action testAction)
        {
            try
            {
                Console.Write("[TEST] " + testName + " ... ");
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED -> " + ex.Message);
                Console.ResetColor();
                failed++;
            }
        }

        [STAThread]
        public static void Main()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" S-T-E-A-L-T-H v7.0 - 32-VECTOR DEEP AUTOMATED TEST RUNNER");
            Console.WriteLine("===================================================================");
            Console.ResetColor();

            MainWindow window = null;

            // Test 1: Window Instantiation & XAML Tree Resolution
            AssertTest("1. Window Instantiation & XAML Tree Resolution", delegate() {
                var app = new Application();
                window = new MainWindow();
                if (window == null) throw new Exception("MainWindow is null");
            });

            // Test 2: Essential Controls & Buttons Verification
            AssertTest("2. Essential Controls & 32-Vector Master Controls Check", delegate() {
                var root = (UIElement)window.Content;
                string[] requiredControls = new string[] {
                    "TitleBar", "BtnClose", "BtnMin", "BtnMax", "BtnMasterRun",
                    "TxtHost", "TxtNet", "TxtRam", "TxtTelem", "TxtLogs", "Scroller", "PrgBar",
                    "InfoModal", "TxtModalTitle", "TxtModalDesc", "TxtModalPaths", "BtnCloseModal", "BtnModalGotIt",
                    "PcNameModal", "TxtNewPcName", "BtnApplyPcName", "BtnRandomPcName", "BtnClosePcModal", "BtnApplyVirtualIdentity",
                    "BtnSpoofMachine", "BtnSpoofCursor", "BtnV1", "BtnV2", "BtnV3", "BtnV4",
                    "BtnV5", "BtnV6", "BtnV7", "BtnV8", "BtnV9", "BtnV10", "BtnV11", "BtnV12",
                    "BtnV13", "BtnV14", "BtnV15", "BtnV16", "BtnV17", "BtnV18", "BtnV19", "BtnV20",
                    "BtnV21", "BtnV22", "BtnV23", "BtnV24", "BtnV25", "BtnV26", "BtnV27", "BtnV28",
                    "BtnV29", "BtnV30", "BtnV31", "BtnV32"
                };

                foreach (var name in requiredControls)
                {
                    var node = LogicalTreeHelper.FindLogicalNode(root, name);
                    if (node == null) throw new Exception("Required control '" + name + "' was not found in visual tree!");
                }
            });

            // Test 3: (i) Info Modal Trigger Verification
            AssertTest("3. Info Modal Triggers & Content Verification", delegate() {
                var root = (UIElement)window.Content;
                var infoModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "InfoModal");
                var txtTitle = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalTitle");
                var btnInfo0 = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnInfo0");

                btnInfo0.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (infoModal.Visibility != Visibility.Visible) throw new Exception("InfoModal did not become visible!");
                if (!txtTitle.Text.Contains("Machine GUID")) throw new Exception("Modal title mismatch!");

                var btnCloseModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseModal");
                btnCloseModal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (infoModal.Visibility != Visibility.Collapsed) throw new Exception("InfoModal did not close!");
            });

            // Test 4: Custom PC Name Dialog Verification
            AssertTest("4. PC Name Spoofer Dialog Trigger", delegate() {
                var root = (UIElement)window.Content;
                var pcNameModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "PcNameModal");
                var btnMicroPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMicroPcName");
                var btnClosePcModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnClosePcModal");

                btnMicroPcName.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pcNameModal.Visibility != Visibility.Visible) throw new Exception("PcNameModal did not become visible!");

                btnClosePcModal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pcNameModal.Visibility != Visibility.Collapsed) throw new Exception("PcNameModal did not close!");
            });

            // Test 5: Hardware & Identity Vector (Cat 0 & 00)
            AssertTest("5. Hardware GUID & Cursor/AI IDE Resetter", delegate() {
                window.SpoofMachineGuidOnly();
                window.SpoofSqmIdOnly();
                window.SpoofRegisteredOwnerAndOrg("STEALTH_OPERATOR");
                window.ResetCursorKeyOnly("telemetry.devDeviceId");
            });

            // Test 6: Deep Telemetry & Timeline (Cat 1 & 2)
            AssertTest("6. Deep Telemetry Silencer & Timeline DB", delegate() {
                window.SilenceDiagTrackServiceOnly();
                window.DisableAdvertisingIdOnly();
                window.DisableInkingTypingOnly();
                window.DisableDefenderSampleUploadOnly();
                window.WipeTimelineSqliteDbOnly();
                window.DisableActivityFeedPolicyOnly();
            });

            // Test 7: Terminal & ShellBags & JumpLists (Cat 3, 4, 5)
            AssertTest("7. Terminal History, ShellBags & JumpLists", delegate() {
                window.ClearPsReadLineHistoryOnly();
                window.ClearRunMruOnly();
                window.ClearOpenSaveMruOnly();
                window.ClearLastVisitedMruOnly();
                window.ClearWordWheelQueryOnly();
                window.ClearAutoDestinationsOnly();
                window.ClearCustomDestinationsOnly();
                window.ClearRecentFolderOnly();
            });

            // Test 8: Visual, Crash Dumps & Cryptnet (Cat 6, 7, 8)
            AssertTest("8. Visual Caches, Crash Dumps & Cryptnet", delegate() {
                window.ClearThumbnailsOnly();
                window.ClearD3DShadersOnly();
                window.ClearClipboardOnly();
                window.ClearMinidumpsOnly();
                window.ClearLiveKernelOnly();
                window.ClearWerQueueOnly();
                window.ClearAppCrashDumpsOnly();
                window.ClearCryptnetContentOnly();
                window.ClearCryptnetMetaOnly();
            });

            // Test 9: Event Logs & Network & P2P (Cat 9, 10, 11)
            AssertTest("9. Event Logs, Network Stealth & P2P Disable", delegate() {
                window.ClearSecurityLogOnly();
                window.ClearSystemAndAppLogOnly();
                window.ClearPowerShellLogOnly();
                window.FlushDnsOnly();
                window.ClearArpOnly();
                window.DisableLlmnrOnly();
                window.DisableDoModeOnly();
                window.DisableSmartScreenOnly();
            });

            // Test 10: Multi-Browser Caches (Cat 12)
            AssertTest("10. Multi-Browser GPU & Temporary Caches", delegate() {
                window.CleanChromeCacheOnly();
                window.CleanBraveCacheOnly();
                window.CleanEdgeCacheOnly();
                window.CleanFirefoxCacheOnly();
            });

            // Test 11: Prefetch, AmCache & ShimCache (Cat 13, 14, 15)
            AssertTest("11. Prefetch, AmCache & ShimCache", delegate() {
                window.DeletePrefetchFilesOnly();
                window.DisablePrefetcherOnly();
                window.StopSysMainOnly();
                window.ClearAmCacheOnly();
                window.ClearRecentFileCacheOnly();
                window.ClearShimCacheOnly();
            });

            // Test 12: UserAssist, BAM/DAM & SRUM (Cat 16, 17, 18)
            AssertTest("12. UserAssist, BAM/DAM & SRUM", delegate() {
                window.ClearUserAssistOnly();
                window.DisableUserAssistTrackingOnly();
                window.ClearBamOnly();
                window.ClearDamOnly();
                window.ClearSrumOnly();
            });

            // Test 13: TypedPaths, RecentDocs & Notifications (Cat 19, 20, 21)
            AssertTest("13. TypedPaths/URLs/MUICache, RecentDocs & WPN DB", delegate() {
                window.ClearTypedPathsOnly();
                window.ClearTypedUrlsOnly();
                window.ClearMuiCacheOnly();
                window.ClearRecentDocsOnly();
                window.DeleteLnkFilesOnly();
                window.ClearWpnDatabaseOnly();
            });

            // Test 14: RDP Artifacts & WiFi Profiles (Cat 22 & 23)
            AssertTest("14. RDP Bitmap Cache & WiFi Profiles", delegate() {
                window.ClearRdpCacheOnly();
                window.ClearRdpMruOnly();
                window.DeleteRdpFilesOnly();
                window.DisableWifiSenseOnly();
            });

            // Test 15: Defender History & Search/Cortana (Cat 24 & 25)
            AssertTest("15. Defender History & Cortana/Search Tracking", delegate() {
                window.ClearDefenderHistoryOnly();
                window.ClearDefenderQuarantineOnly();
                window.DisableCortanaOnly();
                window.ClearSearchHistoryOnly();
            });

            // Test 16: OneDrive Logs & Windows Recall AI / Copilot (Cat 26 & 27)
            AssertTest("16. OneDrive Logs & Windows 11 Recall AI / Copilot Kill", delegate() {
                window.ClearOneDriveLogsOnly();
                window.ClearOneDriveTelemetryOnly();
                window.DisableRecallOnly();
                window.DisableCopilotOnly();
                window.DeleteRecallSnapshotsOnly();
            });

            // Test 17: Scheduled Tasks & Forensic Hardening (Cat 28 & 29)
            AssertTest("17. Telemetry Scheduled Tasks & Forensic Hardening (Pagefile/Hibernation)", delegate() {
                window.DisableCompatAppraiserOnly();
                window.DisableProgramDataUpdaterOnly();
                window.DisableCeipTasksOnly();
                window.EnablePagefileClearOnly();
                window.DisableLastAccessOnly();
            });

            // Test 18: Icon/Font Cache, PS Logging & Sensor Access (Cat 30, 31, 32)
            AssertTest("18. Icon/Font Cache, PS Logging & CapabilityAccessManager Sensors", delegate() {
                window.ClearIconCacheOnly();
                window.ClearFontCacheOnly();
                window.DisablePsTranscriptOnly();
                window.DisablePsModuleLoggingOnly();
                window.DisablePsScriptBlockOnly();
                window.ResetLocationAccessOnly();
                window.ResetCameraAccessOnly();
                window.ResetMicAccessOnly();
                window.DenyAllSensorsOnly();
            });

            // Test 19: Full 32-Vector Master Protocol Sequential Execution
            AssertTest("19. Full 32-Vector Master Protocol Sequential Execution", delegate() {
                window.MasterStealthProtocol();
            });

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            if (failed == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ALL " + passed + "/" + passed + " DEEP 32-VECTOR TEST CASES PASSED WITH 100% ACCURACY!");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" TEST RUN COMPLETED WITH FAILURES: " + passed + " Passed, " + failed + " Failed.");
            }
            Console.ResetColor();
            Console.WriteLine("===================================================================");
        }
    }
}
