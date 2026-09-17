// ============================================================================================
// S-T-E-A-L-T-H v8.0 — SANDBOX-SAFE TEST RUNNER
// Unlike the v7 live-fire suite, these tests do NOT damage the running system:
//   - registry tests run inside a self-created HKCU\Software\STEALTH_TEST sandbox key
//   - file tests run inside %TEMP%\STEALTH_TEST sandbox folder
//   - every action lambda is exercised in DRY-RUN mode (preview, no mutation)
//   - backup/quarantine/restore is verified end-to-end on sandbox files only
// ============================================================================================

using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace STEALTH
{
    public class TestSuite
    {
        private static int passed = 0;
        private static int failed = 0;
        private static List<string> failDetails = new List<string>();

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
                failDetails.Add(testName + " :: " + ex.Message);
            }
        }

        private static void Expect(OpResult r, string what)
        {
            if (r == null) throw new Exception(what + " returned null OpResult");
            if (!r.Ok) throw new Exception(what + " failed: " + r.Detail);
        }

        [STAThread]
        public static void Main()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" S-T-E-A-L-T-H v8.0 - 46-VECTOR SANDBOX-SAFE TEST RUNNER");
            Console.WriteLine(" (no system damage: dry-run + self-cleaning sandbox keys/files)");
            Console.WriteLine("===================================================================");
            Console.ResetColor();

            string sandboxKey = "HKCU\\Software\\STEALTH_TEST";
            string sandboxDir = Path.Combine(Path.GetTempPath(), "STEALTH_TEST");
            MainWindow window = null;
            int totalActions = 0;

            Kernel.Log = delegate(string m) { };

            // Test 1: window + XAML tree
            AssertTest("1. Window Instantiation & XAML Tree Resolution", delegate()
            {
                if (Application.Current == null) new Application(); // production path creates app before window
                window = new MainWindow();
                if (window == null) throw new Exception("MainWindow is null");
            });

            // Test 2: registry structure integrity
            AssertTest("2. Action Registry: 46 Categories, Unique Nums, All Actions Runnable", delegate()
            {
                List<Category> cats = ActionRegistry.Build();
                if (cats.Count != 46) throw new Exception("expected 46 categories, got " + cats.Count);
                HashSet<string> seenNums = new HashSet<string>();
                foreach (Category c in cats)
                {
                    if (!seenNums.Add(c.Num)) throw new Exception("duplicate category num " + c.Num);
                    if (c.Actions.Count == 0) throw new Exception("category " + c.Num + " has no actions");
                    foreach (StealthAction a in c.Actions)
                    {
                        totalActions++;
                        if (a.Run == null) throw new Exception("action " + c.Num + "/" + a.Num + " has null Run");
                        if (string.IsNullOrEmpty(a.Info)) throw new Exception("action " + c.Num + "/" + a.Num + " missing Info");
                    }
                }
                Console.Write("(46 cats, " + totalActions + " actions) ");
            });

            // Test 3: every action executes safely in DRY-RUN
            AssertTest("3. FULL DRY-RUN SWEEP: every action previewed without mutation", delegate()
            {
                Kernel.DryRun = true;
                Kernel.BackupOn = true;
                Kernel.QuarantineOn = true;
                int ran = 0, bad = 0;
                List<Category> cats = ActionRegistry.Build();
                foreach (Category c in cats)
                {
                    foreach (StealthAction a in c.Actions)
                    {
                        try
                        {
                            OpResult r = a.Run(true);
                            if (r == null) bad++;
                            else ran++;
                        }
                        catch (Exception ex)
                        {
                            bad++;
                            Console.Write("\n   [DRY-CRASH] " + c.Num + "/" + a.Num + ": " + ex.Message);
                        }
                    }
                }
                Kernel.DryRun = false;
                if (bad > 0) throw new Exception(bad + " actions crashed in dry-run");
                if (ran < totalActions) throw new Exception("ran " + ran + " < total " + totalActions);
                Console.Write("(" + ran + " actions previewed) ");
            });

            // Test 4: Kernel registry ops in sandbox key
            AssertTest("4. Kernel Registry Engine: set/get/delete-tree in sandbox key", delegate()
            {
                Expect(Kernel.RegSet(sandboxKey, "TestValue", 42, RegistryValueKind.DWord), "RegSet");
                object v = Kernel.RegGet(sandboxKey, "TestValue");
                if (v == null || v.ToString() != "42") throw new Exception("value roundtrip failed: " + v);
                Expect(Kernel.RegSet(sandboxKey + "\\Sub", "Nested", "yes", RegistryValueKind.String), "RegSet nested");
                if (!Kernel.RegExists(sandboxKey + "\\Sub")) throw new Exception("nested key missing");
                Expect(Kernel.RegDeleteTree(sandboxKey), "RegDeleteTree");
                if (Kernel.RegExists(sandboxKey)) throw new Exception("sandbox key still exists after delete");
            });

            // Test 5: BackupManager registry export + restore roundtrip
            AssertTest("5. BackupManager: reg export -> mutate -> restore roundtrip", delegate()
            {
                Kernel.RegSet(sandboxKey, "BeforeBackup", "original", RegistryValueKind.String);
                BackupManager bm = new BackupManager();
                bm.StartSession();
                if (bm.SessionDir == null) throw new Exception("session dir could not be created (ProgramData write denied?)");
                Kernel.Backup = bm;
                // this mutation triggers automatic backup
                Expect(Kernel.RegSet(sandboxKey, "BeforeBackup", "changed", RegistryValueKind.String), "RegSet after backup");
                object v = Kernel.RegGet(sandboxKey, "BeforeBackup");
                if (v.ToString() != "changed") throw new Exception("mutation did not apply");
                Kernel.Backup = null;
                BackupManager.Restore(bm.SessionDir, delegate(string m) { });
                v = Kernel.RegGet(sandboxKey, "BeforeBackup");
                if (v == null || v.ToString() != "original") throw new Exception("restore did not revert value (got " + v + ")");
                Kernel.RegDeleteTree(sandboxKey);
            });

            // Test 6: file sandbox + quarantine roundtrip
            AssertTest("6. File Engine: delete-with-quarantine -> restore", delegate()
            {
                Directory.CreateDirectory(sandboxDir);
                string f = Path.Combine(sandboxDir, "quar_me.txt");
                File.WriteAllText(f, "precious data");
                BackupManager bm = new BackupManager();
                bm.StartSession();
                if (bm.SessionDir == null) throw new Exception("no session dir");
                Kernel.Backup = bm;
                OpResult r = Kernel.FileDelete(f);
                if (!r.Ok) throw new Exception("FileDelete failed: " + r.Detail);
                if (File.Exists(f)) throw new Exception("file still present after delete");
                Kernel.Backup = null;
                bm.FinishSession(); // write manifest so Restore can map quarantined files back
                BackupManager.Restore(bm.SessionDir, delegate(string m) { });
                if (!File.Exists(f)) throw new Exception("file not restored from quarantine");
                if (File.ReadAllText(f) != "precious data") throw new Exception("restored content mismatch");
                File.Delete(f);
            });

            // Test 7: shredder destroys file content beyond recovery
            AssertTest("7. Shredder: 3-pass overwrite + delete", delegate()
            {
                Directory.CreateDirectory(sandboxDir);
                string f = Path.Combine(sandboxDir, "shred_me.bin");
                byte[] data = new byte[8192];
                new Random().NextBytes(data);
                File.WriteAllBytes(f, data);
                OpResult r = Kernel.ShredFile(f);
                if (!r.Ok) throw new Exception("shred failed: " + r.Detail);
                if (File.Exists(f)) throw new Exception("shredded file still exists");
            });

            // Test 8: DirWipe keeps folder, empties content
            AssertTest("8. DirWipe: content cleared, folder preserved", delegate()
            {
                Directory.CreateDirectory(Path.Combine(sandboxDir, "wipe_me"));
                File.WriteAllText(Path.Combine(sandboxDir, "wipe_me", "a.txt"), "x");
                File.WriteAllText(Path.Combine(sandboxDir, "wipe_me", "b.txt"), "y");
                OpResult r = Kernel.DirWipe(Path.Combine(sandboxDir, "wipe_me"));
                if (!r.Ok) throw new Exception("DirWipe failed: " + r.Detail);
                if (Directory.GetFiles(Path.Combine(sandboxDir, "wipe_me")).Length != 0) throw new Exception("folder not empty");
                if (!Directory.Exists(Path.Combine(sandboxDir, "wipe_me"))) throw new Exception("folder itself was removed (should be preserved)");
            });

            // Test 9: ProcRunner honest capture
            AssertTest("9. ProcRunner: exit code + stdout + stderr captured", delegate()
            {
                ProcResult r = ProcRunner.Run("cmd.exe", "/c echo STEALTH_HELLO", 10000);
                if (r.Code != 0) throw new Exception("exit code " + r.Code);
                if (!r.Out.Contains("STEALTH_HELLO")) throw new Exception("stdout missing: '" + r.Out + "'");
            });

            // Test 10: HostsList read-only integrity (no hosts modification in tests)
            AssertTest("10. HostsList: state detection + domain list integrity", delegate()
            {
                bool applied = HostsList.IsApplied(); // read-only
                if (HostsList.Domains.Length < 30) throw new Exception("domain list suspiciously small");
                if (!File.Exists(HostsList.HostsPath())) throw new Exception("hosts path unresolvable");
                Console.Write("(applied=" + applied + ") ");
            });

            // Test 11: Auditor generates real HTML report
            AssertTest("11. Auditor: HTML privacy report generation", delegate()
            {
                string outPath = Path.Combine(sandboxDir, "audit.html");
                string res = Auditor.Generate(outPath);
                if (res.StartsWith("ERROR")) throw new Exception(res);
                string html = File.ReadAllText(outPath);
                if (!html.Contains("Privacy & Forensic Audit Report")) throw new Exception("report content missing title");
                if (!html.Contains("Tracking Services")) throw new Exception("report missing services section");
                if (!html.Contains("Forensic Artifacts")) throw new Exception("report missing artifacts section");
            });

            // Test 12: Scheduler state check (read-only)
            AssertTest("12. Scheduler: task existence query", delegate()
            {
                bool exists = Scheduler.Exists();
                Console.Write("(taskExists=" + exists + ") ");
            });

            // Test 13: GUI structural checks (controls + search index)
            AssertTest("13. GUI Controls: master + toolbar + all 46 category cards wired", delegate()
            {
                var root = (UIElement)window.Content;
                string[] required = new string[] {
                    "TitleBar","BtnClose","BtnMin","BtnMax","BtnMasterRun","BtnAudit","BtnRestore","BtnSchedule","BtnShred","BtnPcName",
                    "CmbProfile","BtnDry","BtnBackup","BtnQuarantine","BtnTheme","TxtSearch",
                    "TxtHost","TxtNet","TxtTelem","TxtRam","TxtStats","TxtLogs","Scroller","PrgBar",
                    "InfoModal","TxtModalTitle","TxtModalDesc","TxtModalPaths","BtnCloseModal","BtnModalGotIt",
                    "PcNameModal","TxtNewPcName","BtnApplyPcName","BtnRandomPcName","BtnClosePcModal","BtnApplyVirtualIdentity",
                    "RestoreModal","LstSessions","BtnDoRestore","BtnRefreshSessions","BtnOpenSessions","BtnCloseRestore",
                    "ScheduleModal","TxtSchedTime","CmbSchedProfile","BtnSchedOn","BtnSchedOff","BtnCloseSched",
                    "ShredModal","TxtShredPath","BtnDoShredFile","BtnDoShredDir","BtnCloseShred"
                };
                foreach (string name in required)
                {
                    if (LogicalTreeHelper.FindLogicalNode(root, name) == null)
                        throw new Exception("required control missing: " + name);
                }
                int cards = 0;
                foreach (var fe in FindAll(root, delegate(string n) { return n != null && n.StartsWith("Card_"); })) cards++;
                if (cards != 46) throw new Exception("expected 46 cards, found " + cards);
            });

            // Test 14: dry-run guards actually block mutations
            AssertTest("14. Dry-Run Guard: mutations blocked when Kernel.DryRun=true", delegate()
            {
                // defensive: clear any leftover sandbox key from earlier runs
                bool wasDry = Kernel.DryRun;
                Kernel.DryRun = false;
                Kernel.RegDeleteTree(sandboxKey);
                bool pre = Kernel.RegExists(sandboxKey);
                Kernel.DryRun = true;
                Kernel.RegSet(sandboxKey, "ShouldNotExist", 1, RegistryValueKind.DWord);
                bool post = Kernel.RegExists(sandboxKey);
                if (post) throw new Exception("dry-run did NOT block registry write! (preExisted=" + pre + ")");
                Directory.CreateDirectory(sandboxDir);
                string f = Path.Combine(sandboxDir, "dry_block.txt");
                File.WriteAllText(f, "keep");
                Kernel.FileDelete(f);
                if (!File.Exists(f)) throw new Exception("dry-run did NOT block file delete!");
                Kernel.DryRun = false;
                File.Delete(f);
                if (!wasDry) Kernel.DryRun = false;
            });

            // Test 15: v8 headless CLI smoke test (dry sweep via child process)
            AssertTest("15. CLI Headless Mode: /sweep /dry exits cleanly", delegate()
            {
                string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "S-T-E-A-L-T-H.exe");
                if (!File.Exists(exe)) throw new Exception("main exe not found next to test exe");
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = "/sweep /profile:Safe /dry";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(120000)) throw new Exception("CLI sweep timed out");
                    if (p.ExitCode != 0) throw new Exception("CLI exit code " + p.ExitCode);
                    if (!outp.Contains("SWEEP COMPLETE")) throw new Exception("CLI did not print completion summary");
                    if (!outp.Contains("DRY")) throw new Exception("CLI did not run in dry mode");
                }
            });

            // Test 16: theme manager applies + persists Dark/Light/System
            AssertTest("16. ThemeManager: Dark/Light/System apply + persistence", delegate()
            {
                ThemeManager.Load();
                string original = ThemeManager.Mode;
                ThemeManager.SetMode("Light");
                System.Windows.Media.SolidColorBrush wl = Application.Current.Resources["WinBg"] as System.Windows.Media.SolidColorBrush;
                if (wl == null || wl.Color.ToString() != "#FFEFF4FB") throw new Exception("light WinBg wrong: " + (wl == null ? "null" : wl.Color.ToString()));
                ThemeManager.SetMode("Dark");
                System.Windows.Media.SolidColorBrush wd = Application.Current.Resources["WinBg"] as System.Windows.Media.SolidColorBrush;
                if (wd == null || wd.Color.ToString() != "#FF040711") throw new Exception("dark WinBg wrong: " + (wd == null ? "null" : wd.Color.ToString()));
                ThemeManager.SetMode("System");
                string resolved = ThemeManager.Resolved;
                if (resolved != "Dark" && resolved != "Light") throw new Exception("system resolve invalid: " + resolved);
                ThemeManager.SetMode(original); // restore user's saved mode
                if (ThemeManager.Mode != original) throw new Exception("mode persistence broken");
            });

            // Test 17: rotating identity formats — complete set, unique, non-empty
            AssertTest("17. IdentFormats: rotating identity notations (host/user/domain/IP styles)", delegate()
            {
                List<string> fmts = IdentFormats.GetAll();
                if (fmts.Count < 10) throw new Exception("expected >=10 unique formats, got " + fmts.Count);
                foreach (string f in fmts) if (string.IsNullOrWhiteSpace(f)) throw new Exception("empty format present");
                HashSet<string> uniq = new HashSet<string>(fmts);
                if (uniq.Count != fmts.Count) throw new Exception("duplicate formats present");
                if (!fmts[0].Contains(Environment.MachineName)) throw new Exception("format 0 missing machine name");
                string a1 = IdentFormats.Next();
                string a2 = IdentFormats.Next();
                if (a1 == a2) throw new Exception("rotation not advancing");
                Console.Write("(" + fmts.Count + " formats) ");
            });

            // cleanup
            try
            {
                Kernel.DryRun = false;
                Kernel.Backup = null;
                Kernel.RegDeleteTree(sandboxKey);
                if (Directory.Exists(sandboxDir)) Directory.Delete(sandboxDir, true);
            }
            catch { }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            if (failed == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ALL " + passed + "/" + passed + " SANDBOX-SAFE TESTS PASSED — 46-VECTOR ENGINE VERIFIED");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" TEST RUN COMPLETED WITH FAILURES: " + passed + " passed, " + failed + " failed.");
                foreach (string d in failDetails) Console.WriteLine("   - " + d);
            }
            Console.ResetColor();
            Console.WriteLine("===================================================================");
        }

        private static IEnumerable<FrameworkElement> FindAll(System.Windows.DependencyObject parent, Func<string, bool> namePredicate)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                var fe = child as FrameworkElement;
                if (fe != null && namePredicate(fe.Name)) yield return fe;
                var dep = child as System.Windows.DependencyObject;
                if (dep != null)
                {
                    foreach (var inner in FindAll(dep, namePredicate)) yield return inner;
                }
            }
        }
    }
}
