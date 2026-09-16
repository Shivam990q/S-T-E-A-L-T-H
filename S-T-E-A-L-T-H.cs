// ============================================================================================
// S-T-E-A-L-T-H v8.0 — ABSOLUTE FORENSIC ANNIHILATION SUITE
// 46-Vector Engine | 160+ Micro-Actions | Dry-Run | Backup/Restore | Quarantine | Audit
// ============================================================================================
// Architecture:
//   Kernel        - centralized execution engine (registry / files / services / processes)
//                   with Dry-Run preview, automatic backup and quarantine support
//   BackupManager - timestamped session folders: reg exports + deleted/quarantined manifests
//   Auditor       - read-only HTML privacy report generator
//   Scheduler     - Task Scheduler registration for automated sweeps
//   ActionRegistry- all 46 categories / 160+ granular actions
//   MainWindow    - WPF GUI built on a runtime XAML shell + programmatically generated cards
// Build (C# 5, .NET Framework 4.x):
//   csc /target:winexe /out:S-T-E-A-L-T-H.exe /r:PresentationFramework.dll
//       /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll
//       /r:System.dll /r:System.Core.dll S-T-E-A-L-T-H.cs
// ============================================================================================

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
using System.Security.Principal;
using Microsoft.Win32;

namespace STEALTH
{
    // ======================================================================================
    // MODELS
    // ======================================================================================

    public enum RiskLevel { Safe = 0, Balanced = 1, Paranoid = 2 }

    public class OpResult
    {
        public bool Ok;
        public bool Skipped;
        public string Detail;
        public static OpResult OK(string d) { OpResult r = new OpResult(); r.Ok = true; r.Detail = d; return r; }
        public static OpResult FAIL(string d) { OpResult r = new OpResult(); r.Ok = false; r.Detail = d; return r; }
        public static OpResult SKIP(string d) { OpResult r = new OpResult(); r.Ok = true; r.Skipped = true; r.Detail = d; return r; }
    }

    public class ProcResult
    {
        public int Code = -1;
        public string Out = "";
        public string Err = "";
        public bool TimedOut;
    }

    /// <summary>A single granular micro-action. Run(dryRun) must never mutate the system
    /// when dryRun is true (Kernel ops enforce this themselves).</summary>
    public class StealthAction
    {
        public string Num;              // "13.1"
        public string Title;
        public string Desc;
        public RiskLevel Risk = RiskLevel.Safe;
        public bool Destructive;        // only included in Paranoid master profile
        public bool RebootNeeded;
        public string Info;             // affected paths (info modal)
        public Func<bool, OpResult> Run;
    }

    /// <summary>A category card = one vector with master button + micro-action buttons.</summary>
    public class Category
    {
        public string Num;              // "13"
        public string Tag;              // "PREFETCH"
        public string Title;
        public string Desc;
        public bool Gold;
        public string Info;             // modal description
        public string InfoPaths;
        public List<StealthAction> Actions = new List<StealthAction>();

        public RiskLevel Risk()
        {
            RiskLevel max = RiskLevel.Safe;
            foreach (StealthAction a in Actions) if (a.Risk > max) max = a.Risk;
            return max;
        }
        public bool Destructive()
        {
            foreach (StealthAction a in Actions) if (a.Destructive) return true;
            return false;
        }
    }

    // ======================================================================================
    // BACKUP MANAGER — every mutation is reversible (registry) / quarantinable (files)
    // ======================================================================================

    public class BackupManager
    {
        public static string BaseDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "STEALTH", "backups");
        }

        public string SessionDir;
        public List<string> Manifest = new List<string>();
        public bool Enabled = true;
        public bool Quarantine = true;
        private int _seq;

        public void StartSession()
        {
            try
            {
                SessionDir = Path.Combine(BaseDir(), "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(SessionDir);
                Directory.CreateDirectory(Path.Combine(SessionDir, "reg"));
                Directory.CreateDirectory(Path.Combine(SessionDir, "quarantine"));
                Manifest.Add("STEALTH v8.0 BACKUP SESSION " + DateTime.Now.ToString());
            }
            catch (Exception ex)
            {
                SessionDir = null;
                Kernel.Log("[BACKUP] session start failed: " + ex.Message);
            }
        }

        public void FinishSession()
        {
            if (SessionDir == null) return;
            try
            {
                Manifest.Add("END " + DateTime.Now.ToString());
                File.WriteAllLines(Path.Combine(SessionDir, "manifest.txt"), Manifest.ToArray());
                Kernel.Log("[BACKUP] session saved -> " + SessionDir);
            }
            catch (Exception ex) { Kernel.Log("[BACKUP] manifest write failed: " + ex.Message); }
        }

        private string SafeName(string key)
        {
            StringBuilder sb = new StringBuilder();
            string dirty = key.Replace('\\', '_').Replace(':', '_').Replace('/', '_').Replace('*', '_');
            foreach (char c in dirty)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            string s = sb.ToString();
            if (s.Length > 70) s = s.Substring(0, 70);
            _seq++;
            return "reg" + _seq.ToString("000") + "_" + s + ".reg";
        }

        /// <summary>Export a registry key into the session before mutating it.</summary>
        public string RegExport(string keyPath)
        {
            if (!Enabled || SessionDir == null) return null;
            if (!Kernel.RegExists(keyPath)) return null;
            try
            {
                string file = Path.Combine(SessionDir, "reg", SafeName(keyPath));
                ProcResult r = ProcRunner.Run("reg.exe", "export \"" + keyPath + "\" \"" + file + "\" /y", 15000);
                if (r.Code == 0 && File.Exists(file))
                {
                    Manifest.Add("REG\t" + keyPath + "\t" + file);
                    return file;
                }
            }
            catch { }
            return null;
        }

        public void NoteFileDeleted(string path, long size)
        {
            Manifest.Add("DEL\t" + path + "\t" + size.ToString());
        }

        /// <summary>Move a file into the quarantine area instead of deleting it.</summary>
        public string QuarantineFile(string origPath)
        {
            if (!Quarantine || SessionDir == null) return null;
            try
            {
                string dest = Path.Combine(SessionDir, "quarantine", Guid.NewGuid().ToString("N") + Path.GetExtension(origPath));
                File.Move(origPath, dest);
                Manifest.Add("QUAR\t" + origPath + "\t" + dest);
                return dest;
            }
            catch (Exception) { return null; }
        }

        public static List<string> ListSessions()
        {
            List<string> result = new List<string>();
            try
            {
                if (Directory.Exists(BaseDir()))
                {
                    string[] dirs = Directory.GetDirectories(BaseDir(), "session-*");
                    Array.Sort(dirs);
                    Array.Reverse(dirs);
                    result.AddRange(dirs);
                }
            }
            catch { }
            return result;
        }

        /// <summary>Restore a session: re-import all .reg exports and move quarantined files back.</summary>
        public static void Restore(string sessionDir, Action<string> log)
        {
            if (!Directory.Exists(sessionDir)) { log("[RESTORE] session folder not found: " + sessionDir); return; }
            string manifest = Path.Combine(sessionDir, "manifest.txt");
            int regCount = 0, quarCount = 0, delCount = 0;
            // 1. registry imports
            try
            {
                string[] regs = Directory.GetFiles(Path.Combine(sessionDir, "reg"), "*.reg");
                foreach (string f in regs)
                {
                    ProcResult r = ProcRunner.Run("reg.exe", "import \"" + f + "\"", 20000);
                    if (r.Code == 0) { regCount++; log("[RESTORE] imported " + Path.GetFileName(f)); }
                    else log("[RESTORE] FAILED import " + Path.GetFileName(f) + " : " + r.Err.Trim());
                }
            }
            catch (Exception ex) { log("[RESTORE] reg import error: " + ex.Message); }
            // 2. quarantined files back
            try
            {
                if (File.Exists(manifest))
                {
                    string[] lines = File.ReadAllLines(manifest);
                    foreach (string line in lines)
                    {
                        string[] p = line.Split('\t');
                        if (p.Length == 3 && p[0] == "QUAR")
                        {
                            try
                            {
                                if (File.Exists(p[2]))
                                {
                                    if (!File.Exists(p[1]))
                                    {
                                        string dir = Path.GetDirectoryName(p[1]);
                                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                        File.Move(p[2], p[1]);
                                        quarCount++;
                                        log("[RESTORE] restored file " + p[1]);
                                    }
                                    else log("[RESTORE] skipped (target exists) " + p[1]);
                                }
                            }
                            catch (Exception ex) { log("[RESTORE] file restore failed " + p[1] + " : " + ex.Message); }
                        }
                        else if (p.Length == 3 && p[0] == "DEL") delCount++;
                    }
                }
            }
            catch (Exception ex) { log("[RESTORE] manifest read error: " + ex.Message); }
            log("[RESTORE] DONE. Registry keys restored: " + regCount + ", files restored: " + quarCount + ", deleted files (unrecoverable, listed in manifest): " + delCount);
        }
    }

    // ======================================================================================
    // PROCESS RUNNER — captures exit code + stdout + stderr (honest reporting)
    // ======================================================================================

    public static class ProcRunner
    {
        public static ProcResult Run(string exe, string args, int timeoutMs)
        {
            ProcResult res = new ProcResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = args;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    StringBuilder o = new StringBuilder();
                    StringBuilder e = new StringBuilder();
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs d) { if (d.Data != null) o.AppendLine(d.Data); };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs d) { if (d.Data != null) e.AppendLine(d.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (p.WaitForExit(timeoutMs)) { res.Code = p.ExitCode; }
                    else { try { p.Kill(); } catch { } res.TimedOut = true; }
                    res.Out = o.ToString();
                    res.Err = e.ToString();
                }
            }
            catch (Exception ex)
            {
                res.Err = ex.Message;
            }
            return res;
        }
    }

    // ======================================================================================
    // KERNEL — every mutation goes through here: DryRun preview + backup + honest result
    // ======================================================================================

    public static class Kernel
    {
        public static Action<string> Log = delegate(string s) { };
        public static BackupManager Backup;             // active session (null = no backups)
        public static bool DryRun;
        public static bool QuarantineOn = true;         // move files to quarantine instead of delete
        public static bool BackupOn = true;
        public static bool? _adminCache;

        public static bool IsAdmin()
        {
            if (_adminCache != null) return _adminCache.Value;
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal pr = new WindowsPrincipal(id);
                _adminCache = pr.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { _adminCache = false; }
            return _adminCache.Value;
        }

        private static bool Guard(string what)
        {
            if (DryRun) { Log("[DRY] " + what); return false; }
            return true;
        }

        // ---------------- Registry ----------------

        private static RegistryKey HiveOf(string path, out string sub)
        {
            string p = path.Replace('/', '\\');
            if (p.StartsWith("HKLM:", StringComparison.OrdinalIgnoreCase) || p.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) { sub = TrimHive(p, "HKLM"); return Registry.LocalMachine; }
            if (p.StartsWith("HKCU:", StringComparison.OrdinalIgnoreCase) || p.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) { sub = TrimHive(p, "HKCU"); return Registry.CurrentUser; }
            if (p.StartsWith("HKU:", StringComparison.OrdinalIgnoreCase) || p.StartsWith("HKU\\", StringComparison.OrdinalIgnoreCase)) { sub = TrimHive(p, "HKU"); return Registry.Users; }
            if (p.StartsWith("HKCR:", StringComparison.OrdinalIgnoreCase) || p.StartsWith("HKCR\\", StringComparison.OrdinalIgnoreCase)) { sub = TrimHive(p, "HKCR"); return Registry.ClassesRoot; }
            sub = null; return null;
        }

        private static string TrimHive(string p, string hive)
        {
            string t = p.Substring(hive.Length);
            if (t.StartsWith(":")) t = t.Substring(1);
            if (t.StartsWith("\\")) t = t.Substring(1);
            return t;
        }

        public static bool RegExists(string path)
        {
            string sub;
            RegistryKey hive = HiveOf(path, out sub);
            if (hive == null) return false;
            try
            {
                using (RegistryKey k = hive.OpenSubKey(sub, false)) { return k != null; }
            }
            catch { return false; }
        }

        public static object RegGet(string path, string name)
        {
            string sub;
            RegistryKey hive = HiveOf(path, out sub);
            if (hive == null) return null;
            try
            {
                using (RegistryKey k = hive.OpenSubKey(sub, false))
                {
                    if (k == null) return null;
                    return k.GetValue(name);
                }
            }
            catch { return null; }
        }

        public static OpResult RegDeleteTree(string path)
        {
            if (!RegExists(path)) return OpResult.SKIP("key absent: " + path);
            if (!Guard("REG-DELETE-TREE " + path)) return OpResult.OK("preview");
            DoBackupReg(path);
            string sub;
            RegistryKey hive = HiveOf(path, out sub);
            try
            {
                string parent = Path.GetDirectoryName(sub.Replace('\\', Path.DirectorySeparatorChar));
                parent = sub.Substring(0, sub.LastIndexOf('\\'));
                string leaf = sub.Substring(sub.LastIndexOf('\\') + 1);
                using (RegistryKey pk = hive.OpenSubKey(parent, true))
                {
                    if (pk == null) return OpResult.FAIL("parent not accessible: " + path);
                    pk.DeleteSubKeyTree(leaf);
                }
                return OpResult.OK("deleted " + path);
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        public static OpResult RegSet(string path, string name, object value, RegistryValueKind kind)
        {
            if (!Guard("REG-SET " + path + " :: " + name + "=" + value)) return OpResult.OK("preview");
            DoBackupReg(path);
            string sub;
            RegistryKey hive = HiveOf(path, out sub);
            try
            {
                using (RegistryKey k = hive.CreateSubKey(sub))
                {
                    if (k == null) return OpResult.FAIL("cannot create " + path);
                    k.SetValue(name, value, kind);
                }
                return OpResult.OK("set " + path + " :: " + name + "=" + value);
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        public static OpResult RegDelValue(string path, string name)
        {
            if (!RegExists(path)) return OpResult.SKIP("key absent " + path);
            if (!Guard("REG-DELETE-VALUE " + path + " :: " + name)) return OpResult.OK("preview");
            DoBackupReg(path);
            string sub;
            RegistryKey hive = HiveOf(path, out sub);
            try
            {
                using (RegistryKey k = hive.OpenSubKey(sub, true))
                {
                    if (k == null) return OpResult.SKIP("key absent " + path);
                    k.DeleteValue(name, false);
                }
                return OpResult.OK("deleted value " + name);
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        private static void DoBackupReg(string path)
        {
            if (BackupOn && Backup != null && Backup.Enabled) Backup.RegExport(path);
        }

        // ---------------- Files ----------------

        public static OpResult FileDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return OpResult.SKIP("absent " + path);
                if (!Guard("FILE-DELETE " + path)) return OpResult.OK("preview");
                long size = 0;
                try { FileInfo fi = new FileInfo(path); size = fi.Length; } catch { }
                if (QuarantineOn && Backup != null && Backup.Quarantine)
                {
                    string q = Backup.QuarantineFile(path);
                    if (q != null) { Log("[QUAR] " + path + " -> quarantine"); return OpResult.OK("quarantined " + path); }
                }
                File.Delete(path);
                if (Backup != null) Backup.NoteFileDeleted(path, size);
                return OpResult.OK("deleted " + path);
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        public static OpResult FileDeleteGlob(string dir, string pattern)
        {
            return DirWipeInternal(dir, pattern, false, false);
        }

        /// <summary>Delete everything inside a directory (optionally matching a glob), keep the directory itself.</summary>
        public static OpResult DirWipe(string path) { return DirWipeInternal(path, "*", true, false); }

        private static OpResult DirWipeInternal(string dir, string pattern, bool recursiveAll, bool shred)
        {
            try
            {
                if (!Directory.Exists(dir)) return OpResult.SKIP("absent " + dir);
                string[] entries = Directory.GetFileSystemEntries(dir, pattern);
                if (entries.Length == 0) return OpResult.SKIP("empty " + dir);
                if (!Guard((shred ? "SHRED-DIR " : "DIR-WIPE ") + dir + (pattern != "*" ? " (" + pattern + ")" : ""))) return OpResult.OK("preview");
                int ok = 0, fail = 0;
                foreach (string e in entries)
                {
                    OpResult r = shred ? ShredInternal(e) : DeleteEntryDeep(e);
                    if (r.Ok && !r.Skipped) ok++; else if (!r.Ok) fail++;
                }
                if (fail == 0) return OpResult.OK(dir + " (" + ok + " entries)");
                return OpResult.FAIL(dir + " (" + ok + " ok, " + fail + " FAILED — file in use or protected?)");
            }
            catch (Exception ex) { return OpResult.FAIL(dir + " : " + ex.Message); }
        }

        private static OpResult DeleteEntryDeep(string entry)
        {
            try
            {
                if (File.Exists(entry)) return FileDelete(entry);
                if (Directory.Exists(entry))
                {
                    string[] children = Directory.GetFileSystemEntries(entry);
                    foreach (string c in children) DeleteEntryDeep(c);
                    try { Directory.Delete(entry, false); } catch { }
                    return OpResult.OK("removed dir " + entry);
                }
                return OpResult.SKIP("absent " + entry);
            }
            catch (Exception ex) { return OpResult.FAIL(entry + " : " + ex.Message); }
        }

        public static OpResult DirDelete(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return OpResult.SKIP("absent " + path);
                if (!Guard("DIR-DELETE " + path)) return OpResult.OK("preview");
                OpResult inner = DirWipeInternal(path, "*", true, false);
                Directory.Delete(path, true);
                return OpResult.OK("removed " + path + " (" + inner.Detail + ")");
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        public static long DirSize(string path)
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(path)) return 0;
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (string f in files) { try { FileInfo fi = new FileInfo(f); total += fi.Length; } catch { } }
            }
            catch { }
            return total;
        }

        // ---------------- Secure deletion (DoD 5220.22-M style 3-pass) ----------------

        public static OpResult ShredInternal(string path)
        {
            try
            {
                if (!File.Exists(path)) return OpResult.SKIP("absent " + path);
                File.SetAttributes(path, FileAttributes.Normal);
                long len = new FileInfo(path).Length;
                int passes = 3;
                Random rnd = new Random();
                for (int pass = 0; pass < passes; pass++)
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[4096];
                        long written = 0;
                        while (written < len)
                        {
                            int chunk = (int)Math.Min(buf.Length, len - written);
                            if (pass == 0) rnd.NextBytes(buf);
                            else if (pass == 1) for (int i = 0; i < chunk; i++) buf[i] = 0xFF;
                            else for (int i = 0; i < chunk; i++) buf[i] = 0x00;
                            fs.Write(buf, 0, chunk);
                            written += chunk;
                        }
                        fs.Flush();
                        fs.SetLength(0); // truncate so file length metadata also resets
                    }
                }
                File.Delete(path);
                if (Backup != null) Backup.NoteFileDeleted(path, len);
                return OpResult.OK("shredded (3-pass) " + path);
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        public static OpResult ShredFile(string path)
        {
            if (!File.Exists(path)) return OpResult.SKIP("absent " + path);
            if (!Guard("SHRED " + path)) return OpResult.OK("preview");
            return ShredInternal(path);
        }

        public static OpResult ShredDir(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return OpResult.SKIP("absent " + path);
                if (!Guard("SHRED-DIR " + path)) return OpResult.OK("preview");
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                int ok = 0, fail = 0;
                foreach (string f in files)
                {
                    OpResult r = ShredFile(f);
                    if (r.Ok && !r.Skipped) ok++; else if (!r.Ok) fail++;
                }
                try { Directory.Delete(path, true); } catch { }
                return OpResult.OK("shredded " + ok + " files" + (fail > 0 ? " (" + fail + " FAILED)" : ""));
            }
            catch (Exception ex) { return OpResult.FAIL(path + " : " + ex.Message); }
        }

        // ---------------- Services ----------------

        public static OpResult SvcStopDisable(string name)
        {
            if (!Guard("SVC-STOP+DISABLE " + name)) return OpResult.OK("preview");
            ProcResult s = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name '" + name + "' -Force -ErrorAction SilentlyContinue\"", 20000);
            ProcResult d = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Set-Service -Name '" + name + "' -StartupType Disabled -ErrorAction SilentlyContinue\"", 20000);
            return OpResult.OK("stop+disable '" + name + "' (exit " + s.Code + "/" + d.Code + ")");
        }

        public static OpResult SvcRestart(string name)
        {
            if (!Guard("SVC-START " + name)) return OpResult.OK("preview");
            ProcResult r = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name '" + name + "' -ErrorAction SilentlyContinue\"", 30000);
            return OpResult.OK("started '" + name + "' (exit " + r.Code + ")");
        }

        public static string SvcStatus(string name)
        {
            ProcResult r = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"(Get-Service -Name '" + name + "' -ErrorAction SilentlyContinue).Status\"", 15000);
            return (r.Out ?? "").Trim();
        }

        // ---------------- PowerShell / external tools ----------------

        public static ProcResult PS(string script, int timeoutMs)
        {
            if (DryRun) { Log("[DRY] PS> " + script); ProcResult pv = new ProcResult(); pv.Code = 0; pv.Out = "(preview)"; return pv; }
            return ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"", timeoutMs);
        }

        public static OpResult PSCheck(string script, string failHint, int timeoutMs)
        {
            ProcResult r = PS(script, timeoutMs);
            if (r.Code == 0) return OpResult.OK(((r.Out ?? "").Trim().Length > 0 ? (r.Out.Trim().Split('\n')[0].Trim()) : "done"));
            string err = (r.Err ?? "").Trim();
            if (err.Length == 0) err = (r.Out ?? "").Trim();
            if (err.Length > 160) err = err.Substring(0, 160);
            return OpResult.FAIL(failHint + " -> " + err);
        }

        public static OpResult Tool(string exe, string args, string what, int timeoutMs)
        {
            if (DryRun) { Log("[DRY] " + exe + " " + args); return OpResult.OK("preview"); }
            ProcResult r = ProcRunner.Run(exe, args, timeoutMs);
            if (r.Code == 0) return OpResult.OK(what + " ok");
            string err = (r.Err ?? "").Trim(); if (err.Length == 0) err = (r.Out ?? "").Trim();
            if (err.Length > 160) err = err.Substring(0, 160);
            return OpResult.FAIL(what + " exit=" + r.Code + " " + err);
        }

        public static string Expand(string s)
        {
            return Environment.ExpandEnvironmentVariables(s);
        }
    }

    // ======================================================================================
    // HOSTS BLOCKLIST — conservative telemetry domains (Windows Update endpoints NOT blocked)
    // ======================================================================================

    public static class HostsList
    {
        public static readonly string[] Domains = new string[] {
            "vortex.data.microsoft.com", "vortex-win.data.microsoft.com",
            "telecommand.telemetry.microsoft.com", "telecommand.telemetry.microsoft.com.nsatc.net",
            "oca.telemetry.microsoft.com", "sqm.telemetry.microsoft.com",
            "watson.telemetry.microsoft.com", "watson.telemetry.microsoft.com.nsatc.net",
            "redir.metaservices.microsoft.com", "choice.microsoft.com",
            "choice.microsoft.com.nsatc.net", "wes.df.telemetry.microsoft.com",
            "services.wes.df.telemetry.microsoft.com", "sqm.df.telemetry.microsoft.com",
            "telemetry.microsoft.com", "telemetry.appex.bing.net",
            "telemetry.urs.microsoft.com", "settings-sandbox.data.microsoft.com",
            "watson.ppe.telemetry.microsoft.com", "vortex-sandbox.data.microsoft.com",
            "survey.watson.microsoft.com", "watson.live.com",
            "statsfe2.ws.microsoft.com", "statsfe1.ws.microsoft.com",
            "compatexchange.cloudapp.net", "a-0001.a-msedge.net",
            "corpext.msitadfs.glbdns2.microsoft.com", "corp.sts.microsoft.com",
            "statsfe2.update.microsoft.com.akadns.net",
            "diagnostics.support.microsoft.com", "feedback.windows.com",
            "feedback.microsoft-hohm.com", "feedback.search.microsoft.com"
        };

        public static string HostsPath() { return Environment.SystemDirectory + "\\drivers\\etc\\hosts"; }
        public const string BeginMark = "# BEGIN S-T-E-A-L-T-H TELEMETRY BLOCK";
        public const string EndMark = "# END S-T-E-A-L-T-H TELEMETRY BLOCK";

        public static bool IsApplied()
        {
            try { return File.ReadAllText(HostsPath()).Contains(BeginMark); } catch { return false; }
        }

        public static OpResult Apply(Action<string> log)
        {
            string hosts = HostsPath();
            if (!File.Exists(hosts)) return OpResult.FAIL("hosts file not found: " + hosts);
            if (IsApplied()) return OpResult.SKIP("already applied");
            if (Kernel.DryRun) { Kernel.Log("[DRY] HOSTS-BLOCK apply (" + Domains.Length + " domains)"); return OpResult.OK("preview"); }
            try
            {
                if (Kernel.Backup != null && Kernel.BackupOn)
                {
                    string q = Kernel.Backup.QuarantineFile(hosts);
                    if (q == null) Kernel.Backup.NoteFileDeleted(hosts, new FileInfo(hosts).Length);
                }
                StringBuilder sb = new StringBuilder();
                sb.Append(File.ReadAllText(hosts));
                sb.Append("\r\n").Append(BeginMark).Append("\r\n");
                foreach (string d in Domains) sb.Append("0.0.0.0 ").Append(d).Append("\r\n");
                sb.Append(EndMark).Append("\r\n");
                File.WriteAllText(hosts, sb.ToString());
                // DNS flush so it takes effect immediately
                ProcRunner.Run("ipconfig.exe", "/flushdns", 15000);
                return OpResult.OK(Domains.Length + " telemetry domains blocked (DNS flushed)");
            }
            catch (Exception ex) { return OpResult.FAIL(ex.Message); }
        }

        public static OpResult Remove(Action<string> log)
        {
            string hosts = HostsPath();
            if (!File.Exists(hosts)) return OpResult.FAIL("hosts file not found");
            if (!IsApplied()) return OpResult.SKIP("not applied");
            if (Kernel.DryRun) { Kernel.Log("[DRY] HOSTS-BLOCK remove"); return OpResult.OK("preview"); }
            try
            {
                string[] lines = File.ReadAllLines(hosts);
                StringBuilder sb = new StringBuilder();
                bool inBlock = false;
                foreach (string line in lines)
                {
                    if (line.Trim() == BeginMark) { inBlock = true; continue; }
                    if (line.Trim() == EndMark) { inBlock = false; continue; }
                    if (!inBlock) sb.Append(line).Append("\r\n");
                }
                File.WriteAllText(hosts, sb.ToString());
                ProcRunner.Run("ipconfig.exe", "/flushdns", 15000);
                return OpResult.OK("telemetry block removed (DNS flushed)");
            }
            catch (Exception ex) { return OpResult.FAIL(ex.Message); }
        }
    }

    // ======================================================================================
    // SCHEDULER — register/unregister a daily automated privacy sweep via Task Scheduler
    // ======================================================================================

    public static class Scheduler
    {
        public const string TaskName = "STEALTHPrivacySweep";

        public static bool Exists()
        {
            ProcResult r = ProcRunner.Run("schtasks.exe", "/Query /TN " + TaskName, 10000);
            return r.Code == 0;
        }

        public static OpResult Register(string time, string profile)
        {
            if (Kernel.DryRun) { Kernel.Log("[DRY] SCHEDULE register daily '" + time + "' profile=" + profile); return OpResult.OK("preview"); }
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string tr = "\"" + exe + "\" /sweep /profile:" + profile;
            ProcResult r = ProcRunner.Run("schtasks.exe", "/Create /F /SC DAILY /ST " + time + " /TN " + TaskName + " /RL HIGHEST /TR \"" + tr + "\"", 20000);
            if (r.Code == 0) return OpResult.OK("daily sweep '" + TaskName + "' @ " + time + " (" + profile + ")");
            return OpResult.FAIL("schtasks exit=" + r.Code + " " + (r.Err ?? "").Trim());
        }

        public static OpResult Unregister()
        {
            if (Kernel.DryRun) { Kernel.Log("[DRY] SCHEDULE remove"); return OpResult.OK("preview"); }
            ProcResult r = ProcRunner.Run("schtasks.exe", "/Delete /F /TN " + TaskName, 15000);
            if (r.Code == 0) return OpResult.OK("sweep task removed");
            return OpResult.FAIL("schtasks exit=" + r.Code + " " + (r.Err ?? "").Trim());
        }
    }

    // ======================================================================================
    // AUDITOR — read-only HTML privacy report (analysis without modification)
    // ======================================================================================

    public static class Auditor
    {
        private static void Section(StringBuilder h, string title)
        {
            h.Append("<h2>").Append(title).Append("</h2>");
        }

        private static string Esc(string s) { return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }

        public static string Generate(string outPath)
        {
            StringBuilder h = new StringBuilder();
            h.Append("<html><head><title>S-T-E-A-L-T-H Privacy Audit</title><style>");
            h.Append("body{font-family:Consolas,monospace;background:#040711;color:#34D399;padding:24px;}");
            h.Append("h1{color:#00F2FE}h2{color:#FBBF24;border-bottom:1px solid #1E293B;padding-bottom:4px;}");
            h.Append("table{border-collapse:collapse;width:100%;margin-bottom:18px;}");
            h.Append("td,th{border:1px solid #1E293B;padding:5px 9px;font-size:12px;text-align:left;}");
            h.Append("th{background:#08101E;color:#38BDF8;}.bad{color:#F87171}.good{color:#34D399}.warn{color:#FBBF24}");
            h.Append("</style></head><body>");
            h.Append("<h1>S-T-E-A-L-T-H v8.0 — Privacy & Forensic Audit Report</h1>");
            h.Append("<p>Host: ").Append(Esc(Environment.MachineName)).Append(" | User: ").Append(Esc(Environment.UserName));
            h.Append(" | Generated: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            h.Append(" | Elevated: ").Append(Kernel.IsAdmin() ? "<span class='good'>YES</span>" : "<span class='warn'>NO (results may be incomplete)</span>").Append("</p>");

            // ---- services ----
            Section(h, "1. Tracking Services");
            h.Append("<table><tr><th>Service</th><th>Status</th><th>Startup</th></tr>");
            string[] svcs = new string[] { "DiagTrack", "dmwappushservice", "WerSvc", "SysMain", "DPS", "WSearch", "WpnService", "DoSvc", "PcaSvc", "lfsvc", "MapsBroker", "NvTelemetry", "diagnosticshub.standardcollector.service" };
            foreach (string s in svcs)
            {
                ProcResult r = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"$s=Get-Service -Name '" + s + "' -ErrorAction SilentlyContinue; if($s){\\\"$($s.Status)|$($s.StartType)\\\"}else{'NotFound'}\"", 15000);
                string v = (r.Out ?? "").Trim();
                if (v.Length == 0) v = "NotFound";
                string cls = v.StartsWith("Stopped") ? "good" : (v == "NotFound" ? "good" : "bad");
                h.Append("<tr><td>").Append(s).Append("</td><td class='").Append(cls).Append("'>").Append(Esc(v)).Append("</td></tr>");
            }
            h.Append("</table>");

            // ---- policy values ----
            Section(h, "2. Privacy Policy State");
            h.Append("<table><tr><th>Policy</th><th>Location</th><th>Value</th><th>Desired</th></tr>");
            AppendPolicy(h, "AllowTelemetry", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry", "0 (Security)");
            AppendPolicy(h, "Recall disabled (24H2)", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "DisableAIDataAnalysis", "1");
            AppendPolicy(h, "Copilot off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot", "TurnOffWindowsCopilot", "1");
            AppendPolicy(h, "Clipboard history off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowClipboardHistory", "0");
            AppendPolicy(h, "Cloud clipboard off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowCrossDeviceClipboard", "0");
            AppendPolicy(h, "Activity feed off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "EnableActivityFeed", "0");
            AppendPolicy(h, "Delivery Optimization locked", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization", "DODownloadMode", "0");
            AppendPolicy(h, "Widgets off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Dsh", "AllowNewsAndInterests", "0");
            AppendPolicy(h, "PS transcription off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription", "EnableTranscripting", "0");
            AppendPolicy(h, "PS scriptblock log off", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging", "EnableScriptBlockLogging", "0");
            AppendPolicy(h, "Clear pagefile at shutdown", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management", "ClearPageFileAtShutdown", "1");
            AppendPolicy(h, "LastAccess stamps off", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters", "(NtfsDisableLastAccess via fsutil)", "1");
            h.Append("</table>");

            // ---- artifacts ----
            Section(h, "3. Forensic Artifacts Present");
            h.Append("<table><tr><th>Artifact</th><th>Path</th><th>State</th></tr>");
            string la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string ap = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            AppendArtifact(h, "Timeline DB", la + "\\ConnectedDevicesPlatform");
            AppendArtifact(h, "PSReadLine history", ap + "\\Microsoft\\Windows\\PowerShell\\PSReadLine");
            AppendArtifact(h, "JumpLists", ap + "\\Microsoft\\Windows\\Recent");
            AppendArtifact(h, "Thumbcache", la + "\\Microsoft\\Windows\\Explorer");
            AppendArtifact(h, "D3D shader cache", la + "\\D3DSCache");
            AppendArtifact(h, "Crash dumps", la + "\\CrashDumps");
            AppendArtifact(h, "Cryptnet URL cache", la + "\\Microsoft\\CryptnetUrlCache");
            AppendArtifact(h, "Notifications DB", la + "\\Microsoft\\Windows\\Notifications");
            AppendArtifact(h, "RDP cache", la + "\\Microsoft\\Terminal Server Client");
            AppendArtifact(h, "Recall snapshots", la + "\\CoreAIPlatform.00");
            AppendArtifact(h, "Real ShellBags", "HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\BagMRU");
            AppendArtifact(h, "UserAssist", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\UserAssist");
            AppendArtifact(h, "AmCache", "C:\\Windows\\AppCompat\\Programs");
            AppendArtifact(h, "Prefetch", "C:\\Windows\\Prefetch");
            AppendArtifact(h, "SRUM DB", "C:\\Windows\\System32\\SRU");
            AppendArtifact(h, "Registry backups", "C:\\Windows\\System32\\config\\RegBack");
            AppendArtifact(h, "CBS logs", "C:\\Windows\\Logs\\CBS");
            AppendArtifact(h, "Windows Update cache", "C:\\Windows\\SoftwareDistribution\\Download");
            AppendArtifact(h, "Minidumps", "C:\\Windows\\Minidump");
            AppendArtifact(h, "WER archive", pd + "\\Microsoft\\Windows\\WER");
            h.Append("</table>");

            // ---- network ----
            Section(h, "4. Network & Blocking");
            h.Append("<table><tr><th>Check</th><th>State</th></tr>");
            h.Append("<tr><td>Hosts telemetry block</td><td class='").Append(HostsList.IsApplied() ? "good" : "warn").Append("'>").Append(HostsList.IsApplied() ? "APPLIED" : "not applied").Append("</td></tr>");
            ProcResult dns = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"(Get-DnsClientDohServerAddress -ErrorAction SilentlyContinue | Measure-Object).Count\"", 15000);
            string doh = (dns.Out ?? "").Trim();
            h.Append("<tr><td>DoH-configured servers</td><td>").Append(doh.Length > 0 ? Esc(doh) : "0").Append("</td></tr>");
            h.Append("</table>");

            Section(h, "5. Disk Media Type (SSD detection for shredding decisions)");
            h.Append("<table><tr><th>Disk</th><th>Media</th></tr>");
            ProcResult disks = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Get-PhysicalDisk | Select-Object FriendlyName,MediaType | ConvertTo-Csv -NoTypeInformation\"", 20000);
            foreach (string line in (disks.Out ?? "").Split(new char[] { '\n' }))
            {
                string t = line.Trim();
                if (t.Length > 0) h.Append("<tr><td colspan='2'>").Append(Esc(t)).Append("</td></tr>");
            }
            h.Append("</table>");

            h.Append("<hr/><p style='color:#64748B'>Generated by S-T-E-A-L-T-H v8.0 — read-only audit, nothing was modified.</p>");
            h.Append("</body></html>");

            try
            {
                File.WriteAllText(outPath, h.ToString());
                return outPath;
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        private static void AppendPolicy(StringBuilder h, string name, string key, string valueName, string desired)
        {
            object v = Kernel.RegGet(key, valueName);
            string shown = (v == null) ? "(not set)" : v.ToString();
            h.Append("<tr><td>").Append(Esc(name)).Append("</td><td>").Append(Esc(key)).Append("</td><td>")
              .Append(Esc(shown)).Append("</td><td>").Append(Esc(desired)).Append("</td></tr>");
        }

        private static void AppendArtifact(StringBuilder h, string name, string path)
        {
            bool isReg = path.StartsWith("HK");
            bool exists = isReg ? Kernel.RegExists(path) : Directory.Exists(path) || File.Exists(path);
            string size = "";
            if (!isReg && exists && Directory.Exists(path))
            {
                long b = Kernel.DirSize(path);
                if (b > 1024 * 1024) size = " (" + (b / 1024 / 1024) + " MB)"; else if (b > 1024) size = " (" + (b / 1024) + " KB)";
            }
            h.Append("<tr><td>").Append(Esc(name)).Append("</td><td>").Append(Esc(path)).Append("</td><td class='")
              .Append(exists ? "warn" : "good").Append("'>").Append(exists ? "PRESENT" + size : "clean").Append("</td></tr>");
        }
    }

    // ======================================================================================
    // ACTION REGISTRY — every vector & micro-action in the suite
    // ======================================================================================

    public static class ActionRegistry
    {
        private static StealthAction A(string num, string title, RiskLevel risk, Func<bool, OpResult> run, string info)
        {
            StealthAction a = new StealthAction();
            a.Num = num; a.Title = title; a.Risk = risk; a.Run = run; a.Info = info;
            return a;
        }

        private static StealthAction A(string num, string title, RiskLevel risk, bool destructive, bool reboot, Func<bool, OpResult> run, string info)
        {
            StealthAction a = A(num, title, risk, run, info);
            a.Destructive = destructive; a.RebootNeeded = reboot;
            return a;
        }

        private static string LA() { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        private static string AP() { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
        private static string PD() { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); }

        public static List<Category> Build()
        {
            List<Category> cats = new List<Category>();
            Category c;

            // ==================================================================================
            // CAT 0 (GOLD) — Hardware & Identity Spoof
            // ==================================================================================
            c = new Category();
            c.Num = "0"; c.Tag = "HARDWARE & IDENTITY"; c.Gold = true;
            c.Title = "Windows Machine GUID, Identity & PC Name Spoof";
            c.Desc = "Regenerates MachineGuid, SQM MachineId, Registered Owner & PC Name.";
            c.Info = "Generates new cryptographically secure GUIDs and overwrites Windows machine identity values. NOTE: MachineGuid changes may affect Windows Update history, MS licensing and app activations.";
            c.InfoPaths = "HKLM\\SOFTWARE\\Microsoft\\Cryptography -> MachineGuid\nHKLM\\SOFTWARE\\Microsoft\\SQMClient -> MachineId\nHKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion -> RegisteredOwner/Organization\nRename-Computer";
            c.Actions.Add(A("0.1", "Spoof MachineGuid", RiskLevel.Balanced, delegate(bool dry)
            {
                string g = Guid.NewGuid().ToString();
                Kernel.Log("[PLAN] new MachineGuid = " + g);
                return Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Cryptography", "MachineGuid", g, RegistryValueKind.String);
            }, "HKLM\\SOFTWARE\\Microsoft\\Cryptography -> MachineGuid (may affect licensing/activation)"));
            c.Actions.Add(A("0.2", "Spoof SQM MachineId", RiskLevel.Safe, delegate(bool dry)
            {
                string g = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                return Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\SQMClient", "MachineId", g, RegistryValueKind.String);
            }, "HKLM\\SOFTWARE\\Microsoft\\SQMClient -> MachineId"));
            c.Actions.Add(A("0.3", "Spoof Registered Owner/Org", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOwner", "STEALTH_OPERATOR", RegistryValueKind.String);
                OpResult r2 = Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOrganization", "STEALTH-ORG", RegistryValueKind.String);
                return r1.Ok && r2.Ok ? OpResult.OK("owner+org spoofed") : OpResult.FAIL(r1.Detail + " ; " + r2.Detail);
            }, "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion -> RegisteredOwner / RegisteredOrganization"));
            // 0.4 handled by GUI modal (PcNameModal) — placeholder action for master runs
            c.Actions.Add(A("0.4", "Rename PC (random stealth name)", RiskLevel.Balanced, true, true, delegate(bool dry)
            {
                string[] prefixes = new string[] { "STEALTH-NODE", "DESKTOP-CYBER", "QUANTUM-HOST", "NEXUS-SYS", "SHADOW-PC", "ZERO-GRID" };
                Random rnd = new Random();
                string name = prefixes[rnd.Next(prefixes.Length)] + "-" + rnd.Next(1000, 9999);
                return Kernel.PSCheck("Rename-Computer -NewName '" + name + "' -Force -WarningAction SilentlyContinue | Out-Null; 'renamed " + name + "'", "Rename-Computer", 30000);
            }, "Rename-Computer (takes effect after reboot)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 00 (GOLD) — AI IDE Machine ID Resetter + telemetry settings off
            // ==================================================================================
            c = new Category();
            c.Num = "00"; c.Tag = "AI IDE RESETER"; c.Gold = true;
            c.Title = "Cursor & AI IDE Machine ID Resetter";
            c.Desc = "Resets devDeviceId, macMachineId, machineId, sqmId in storage.json + disables IDE telemetry.";
            c.Info = "Sanitizes and regenerates machine tracking identifiers used by Cursor / VS Code, and forces telemetry settings off in settings.json.";
            c.InfoPaths = "%APPDATA%\\Cursor\\User\\globalStorage\\storage.json\n%APPDATA%\\Code\\User\\globalStorage\\storage.json\n%APPDATA%\\Cursor\\User\\settings.json\n%APPDATA%\\Code\\User\\settings.json";
            c.Actions.Add(A("00.1", "Reset telemetry.devDeviceId", RiskLevel.Balanced, delegate(bool dry) { return ResetIdeKey("telemetry.devDeviceId"); }, "storage.json -> telemetry.devDeviceId"));
            c.Actions.Add(A("00.2", "Reset telemetry.macMachineId", RiskLevel.Balanced, delegate(bool dry) { return ResetIdeKey("telemetry.macMachineId"); }, "storage.json -> telemetry.macMachineId"));
            c.Actions.Add(A("00.3", "Reset telemetry.machineId", RiskLevel.Balanced, delegate(bool dry) { return ResetIdeKey("telemetry.machineId"); }, "storage.json -> telemetry.machineId"));
            c.Actions.Add(A("00.4", "Reset telemetry.sqmId", RiskLevel.Balanced, delegate(bool dry) { return ResetIdeKey("telemetry.sqmId"); }, "storage.json -> telemetry.sqmId"));
            c.Actions.Add(A("00.5", "Force IDE telemetry OFF (settings.json)", RiskLevel.Safe, delegate(bool dry) { return IdeTelemetryOff(); },
                "settings.json -> telemetry.telemetryLevel='off', enableCrashReporter=false, redhat.telemetry.enabled=false, workbench.enableExperiments=false"));
            cats.Add(c);

            // ==================================================================================
            // CAT 1 — Telemetry silencer
            // ==================================================================================
            c = new Category();
            c.Num = "1"; c.Tag = "TELEMETRY";
            c.Title = "Deep Windows Telemetry & CEIP Silencer";
            c.Desc = "Disables DiagTrack, CEIP, Advertising ID, Inking/Typing & Defender sample uploads.";
            c.Info = "Stops and disables telemetry services and locks diagnostic data to the minimum via policy.";
            c.InfoPaths = "Services: DiagTrack, dmwappushservice\nHKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection -> AllowTelemetry=0\nHKCU\\...\\AdvertisingInfo -> Enabled=0\nHKCU\\...\\Input\\TIPC -> Enabled=0\nHKLM\\...\\Windows Defender\\Spynet -> SubmitSamplesConsent=2";
            c.Actions.Add(A("1.1", "Stop+disable DiagTrack & dmwappushservice", RiskLevel.Safe, delegate(bool dry) { return Kernel.SvcStopDisable("DiagTrack"); }, "Services DiagTrack + dmwappushservice"));
            c.Actions.Add(A("1.2", "Disable Advertising ID", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord);
            }, "HKCU\\...\\AdvertisingInfo -> Enabled=0"));
            c.Actions.Add(A("1.3", "Disable Inking & Typing collection", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKCU\\Software\\Microsoft\\Input\\TIPC", "Enabled", 0, RegistryValueKind.DWord);
                OpResult r2 = Kernel.RegSet("HKCU\\Software\\Microsoft\\Personalization\\Settings", "AcceptedPrivacyPolicy", 0, RegistryValueKind.DWord);
                return r1.Ok ? r1 : r2;
            }, "HKCU\\...\\Input\\TIPC + Personalization\\Settings"));
            c.Actions.Add(A("1.4", "Block Defender sample uploads", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\Spynet", "SubmitSamplesConsent", 2, RegistryValueKind.DWord);
            }, "SubmitSamplesConsent=2 (never send)"));
            c.Actions.Add(A("1.5", "Lock AllowTelemetry=0 (Security only)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord);
            }, "AllowTelemetry=0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 2 — Timeline / Activity DB
            // ==================================================================================
            c = new Category();
            c.Num = "2"; c.Tag = "ACTIVITY DB";
            c.Title = "Windows Timeline & Activities Database Purge";
            c.Desc = "Deletes ActivitiesCache.db and disables activity feed upload policies.";
            c.Info = "The ConnectedDevicesPlatform SQLite database records every app opened and file touched. Deleting it plus disabling the feed policies stops local and cloud activity history.";
            c.InfoPaths = "%LOCALAPPDATA%\\ConnectedDevicesPlatform\\*\\ActivitiesCache.db\nHKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System -> EnableActivityFeed=0, PublishUserActivities=0, UploadUserActivities=0";
            c.Actions.Add(A("2.1", "Wipe ActivitiesCache.db (all profiles)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem -Path \"$env:LOCALAPPDATA\\ConnectedDevicesPlatform\" -Recurse -Filter 'ActivitiesCache.db*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue; 'done'", "ActivitiesCache.db wipe", 30000);
            }, "ActivitiesCache.db + -wal/-shm"));
            c.Actions.Add(A("2.2", "Disable Activity Feed policies", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "EnableActivityFeed", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "PublishUserActivities", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "UploadUserActivities", 0, RegistryValueKind.DWord);
                return r1;
            }, "EnableActivityFeed / PublishUserActivities / UploadUserActivities = 0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 3 — Terminal history
            // ==================================================================================
            c = new Category();
            c.Num = "3"; c.Tag = "TERMINAL";
            c.Title = "PowerShell, Terminal & RunMRU History Sanitizer";
            c.Desc = "Wipes PSReadLine history, Windows Terminal saved commands & Run dialog MRU.";
            c.Info = "Command history lives in three places: PSReadLine console history file, Windows Terminal state.json, and the Win+R RunMRU registry key.";
            c.InfoPaths = "(Get-PSReadLineOption).HistorySavePath (ConsoleHost_history.txt)\n%LOCALAPPDATA%\\Packages\\Microsoft.WindowsTerminal*\\LocalState\\state.json\nHKCU\\...\\Explorer\\RunMRU";
            c.Actions.Add(A("3.1", "Clear PSReadLine history", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("$p=(Get-PSReadLineOption).HistorySavePath; if(Test-Path $p){ Remove-Item $p -Force }; 'cleared'", "PSReadLine history", 20000);
            }, "ConsoleHost_history.txt"));
            c.Actions.Add(A("3.2", "Clear RunMRU (Win+R)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RunMRU");
            }, "HKCU\\...\\Explorer\\RunMRU"));
            c.Actions.Add(A("3.3", "Clear Windows Terminal saved commands", RiskLevel.Safe, delegate(bool dry)
            {
                string pat = LA() + "\\Packages\\Microsoft.WindowsTerminal_8wekyb3d8bbwe\\LocalState\\state.json";
                OpResult r1 = Kernel.FileDelete(pat);
                string pat2 = LA() + "\\Packages\\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\\LocalState\\state.json";
                OpResult r2 = Kernel.FileDelete(pat2);
                return OpResult.OK(r1.Detail + " ; " + r2.Detail);
            }, "Windows Terminal state.json command history"));
            cats.Add(c);

            // ==================================================================================
            // CAT 4 — REAL ShellBags (v8 FIX: actual BagMRU/Bags + dialog MRUs)
            // ==================================================================================
            c = new Category();
            c.Num = "4"; c.Tag = "SHELLBAGS";
            c.Title = "REAL ShellBags (BagMRU/Bags) + Dialog History";
            c.Desc = "v8 FIX: clears the actual ShellBags keys + OpenSave/LastVisited MRU + WordWheelQuery.";
            c.Info = "v7 cleared dialog MRUs only. v8 also destroys the REAL ShellBags: HKCU BagMRU/Bags record every folder opened, its path, view settings and timestamps. Requires Explorer restart.";
            c.InfoPaths = "HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\BagMRU\nHKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\Bags\nHKCU\\...\\ComDlg32\\OpenSavePidlMRU\nHKCU\\...\\ComDlg32\\LastVisitedPidlMRU\nHKCU\\...\\Explorer\\WordWheelQuery";
            c.Actions.Add(A("4.1", "Delete REAL ShellBags BagMRU", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\BagMRU");
            }, "Shell\\BagMRU (folder open history)"));
            c.Actions.Add(A("4.2", "Delete ShellBags Bags", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\Bags");
            }, "Shell\\Bags (folder view memory)"));
            c.Actions.Add(A("4.3", "Clear OpenSavePidlMRU", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ComDlg32\\OpenSavePidlMRU");
            }, "ComDlg32\\OpenSavePidlMRU"));
            c.Actions.Add(A("4.4", "Clear LastVisitedPidlMRU", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ComDlg32\\LastVisitedPidlMRU");
            }, "ComDlg32\\LastVisitedPidlMRU"));
            c.Actions.Add(A("4.5", "Clear WordWheelQuery searches", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\WordWheelQuery");
            }, "Explorer search history"));
            c.Actions.Add(A("4.6", "Restart Explorer (apply shellbags)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; 'explorer restarted'", "explorer restart", 20000);
            }, "Restart Explorer to flush shell in-memory bags"));
            cats.Add(c);

            // ==================================================================================
            // CAT 5 — JumpLists
            // ==================================================================================
            c = new Category();
            c.Num = "5"; c.Tag = "JUMP LISTS";
            c.Title = "JumpLists & Recent Items MRU Purge";
            c.Desc = "Deletes AutomaticDestinations, CustomDestinations and Recent Items.";
            c.Info = "JumpLists are binary OLE files that store recently opened documents per application.";
            c.InfoPaths = "%APPDATA%\\Microsoft\\Windows\\Recent\\AutomaticDestinations\\*\n%APPDATA%\\Microsoft\\Windows\\Recent\\CustomDestinations\\*\n%APPDATA%\\Microsoft\\Windows\\Recent\\*";
            c.Actions.Add(A("5.1", "Clear AutomaticDestinations", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDeleteGlob(AP() + "\\Microsoft\\Windows\\Recent\\AutomaticDestinations", "*");
            }, "AutomaticDestinations jumplists"));
            c.Actions.Add(A("5.2", "Clear CustomDestinations", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDeleteGlob(AP() + "\\Microsoft\\Windows\\Recent\\CustomDestinations", "*");
            }, "CustomDestinations jumplists"));
            c.Actions.Add(A("5.3", "Clear Recent Items folder", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDeleteGlob(AP() + "\\Microsoft\\Windows\\Recent", "*.lnk");
            }, "Recent folder .lnk files"));
            cats.Add(c);

            // ==================================================================================
            // CAT 6 — Visual caches + CLOUD CLIPBOARD (v8)
            // ==================================================================================
            c = new Category();
            c.Num = "6"; c.Tag = "VISUAL & CLIPBOARD";
            c.Title = "Visual Caches, Clipboard & Cloud Clipboard Control";
            c.Desc = "Thumbnails, D3D shaders, clipboard + v8: Win+V history policy + cross-device sync off.";
            c.Info = "v7 only cleared the current clipboard. v8 adds the AllowClipboardHistory / AllowCrossDeviceClipboard policies so Win+V history and cloud sync are actually disabled.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\n%LOCALAPPDATA%\\D3DSCache\\*\nHKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System -> AllowClipboardHistory=0, AllowCrossDeviceClipboard=0";
            c.Actions.Add(A("6.1", "Purge thumbnail cache", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDeleteGlob(LA() + "\\Microsoft\\Windows\\Explorer", "thumbcache_*");
            }, "thumbcache_*.db"));
            c.Actions.Add(A("6.2", "Purge D3D shader cache", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.DirWipe(LA() + "\\D3DSCache");
            }, "D3DSCache"));
            c.Actions.Add(A("6.3", "Clear current clipboard", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] Clipboard.Clear()"); return OpResult.OK("preview"); }
                try { System.Windows.Clipboard.Clear(); return OpResult.OK("clipboard cleared"); }
                catch (Exception ex) { return OpResult.FAIL(ex.Message); }
            }, "Current clipboard content"));
            c.Actions.Add(A("6.4", "Disable Win+V clipboard HISTORY", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowClipboardHistory", 0, RegistryValueKind.DWord);
            }, "AllowClipboardHistory=0 (OS Policies)"));
            c.Actions.Add(A("6.5", "Disable cross-device cloud clipboard", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowCrossDeviceClipboard", 0, RegistryValueKind.DWord);
            }, "AllowCrossDeviceClipboard=0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 7 — Crash dumps
            // ==================================================================================
            c = new Category();
            c.Num = "7"; c.Tag = "CRASH DUMPS";
            c.Title = "Crash Dumps, MEMORY.DMP & WER";
            c.Desc = "Minidumps, LiveKernelReports, WER queues, app crash dumps + MEMORY.DMP.";
            c.Info = "Kernel and application crash dumps contain full memory snapshots — passwords, keys, file paths. v8 also deletes C:\\Windows\\MEMORY.DMP and DedicatedDumpFile.";
            c.InfoPaths = "C:\\Windows\\Minidump\\*\nC:\\Windows\\LiveKernelReports\\*\nC:\\Windows\\MEMORY.DMP\n%PROGRAMDATA%\\Microsoft\\Windows\\WER\\*\n%LOCALAPPDATA%\\CrashDumps\\*";
            c.Actions.Add(A("7.1", "Clear kernel minidumps", RiskLevel.Safe, delegate(bool dry) { return Kernel.FileDeleteGlob("C:\\Windows\\Minidump", "*"); }, "Minidump\\*"));
            c.Actions.Add(A("7.2", "Clear LiveKernelReports", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe("C:\\Windows\\LiveKernelReports"); }, "LiveKernelReports"));
            c.Actions.Add(A("7.3", "Clear WER queue+archive", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.DirWipe(PD() + "\\Microsoft\\Windows\\WER\\ReportQueue");
                Kernel.DirWipe(PD() + "\\Microsoft\\Windows\\WER\\ReportArchive");
                Kernel.DirWipe(PD() + "\\Microsoft\\Windows\\WER\\Temp");
                return r1;
            }, "WER ReportQueue/Archive/Temp"));
            c.Actions.Add(A("7.4", "Clear app crash dumps", RiskLevel.Safe, delegate(bool dry) { return Kernel.FileDeleteGlob(LA() + "\\CrashDumps", "*"); }, "%LOCALAPPDATA%\\CrashDumps"));
            c.Actions.Add(A("7.5", "Delete MEMORY.DMP + dedicated dumps", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.FileDelete("C:\\Windows\\MEMORY.DMP");
                string dedicated = (string)Kernel.RegGet("HKLM\\SYSTEM\\CurrentControlSet\\Control\\CrashControl", "DedicatedDumpFile");
                OpResult r2 = OpResult.SKIP("no dedicated dump configured");
                if (dedicated != null && dedicated.Length > 0) r2 = Kernel.FileDelete(dedicated);
                return OpResult.OK(r1.Detail + " ; " + r2.Detail);
            }, "MEMORY.DMP + DedicatedDumpFile"));
            c.Actions.Add(A("7.6", "Disable new crash dumps (CrashDumpEnabled=0)", RiskLevel.Paranoid, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SYSTEM\\CurrentControlSet\\Control\\CrashControl", "CrashDumpEnabled", 0, RegistryValueKind.DWord);
            }, "CrashControl -> CrashDumpEnabled=0 (BSOD analysis lost)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 8 — Cryptnet
            // ==================================================================================
            c = new Category();
            c.Num = "8"; c.Tag = "CRYPTNET SSL";
            c.Title = "Cryptnet SSL/TLS Certificate URL Leak Cache";
            c.Desc = "Purges CryptnetUrlCache Content & MetaData (HTTPS domain leak cache).";
            c.Info = "CryptnetUrlCache stores OCSP/CRL certificates for every HTTPS site visited — a browsing-history leak invisible to normal cache cleaners.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\CryptnetUrlCache\\Content\\*\n%LOCALAPPDATA%\\Microsoft\\CryptnetUrlCache\\MetaData\\*";
            c.Actions.Add(A("8.1", "Clear certificate content cache", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe(LA() + "\\Microsoft\\CryptnetUrlCache\\Content"); }, "CryptnetUrlCache\\Content"));
            c.Actions.Add(A("8.2", "Clear OCSP/CRL metadata cache", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe(LA() + "\\Microsoft\\CryptnetUrlCache\\MetaData"); }, "CryptnetUrlCache\\MetaData"));
            cats.Add(c);

            // ==================================================================================
            // CAT 9 — Event logs (v8: Defender ops log + full sweep)
            // ==================================================================================
            c = new Category();
            c.Num = "9"; c.Tag = "EVENT LOGS";
            c.Title = "Event Log Scrubber (incl. Defender + ALL channels)";
            c.Desc = "Security/System/App/PS logs + v8: Defender Operational log + full wevtutil sweep.";
            c.Info = "v7 missed the Windows Defender Operational log and dozens of forensic channels (TaskScheduler, WMI-Activity, WinRM, Sysmon...). The full sweep clears every channel. NOTE: clearing the Security log itself writes a 'log cleared' event 1102 — unavoidable.";
            c.InfoPaths = "wevtutil cl Security / System / Application\nwevtutil cl Microsoft-Windows-Windows Defender/Operational\nwevtutil el | wevtutil cl (all channels)";
            c.Actions.Add(A("9.1", "Scrub Security log", RiskLevel.Balanced, delegate(bool dry) { return Kernel.Tool("wevtutil.exe", "cl Security", "Security log", 30000); }, "wevtutil cl Security (writes event 1102)"));
            c.Actions.Add(A("9.2", "Scrub System+Application logs", RiskLevel.Balanced, delegate(bool dry)
            {
                OpResult r1 = Kernel.Tool("wevtutil.exe", "cl System", "System log", 30000);
                Kernel.Tool("wevtutil.exe", "cl Application", "Application log", 30000);
                return r1;
            }, "System + Application"));
            c.Actions.Add(A("9.3", "Scrub PowerShell logs", RiskLevel.Balanced, delegate(bool dry)
            {
                OpResult r1 = Kernel.Tool("wevtutil.exe", "cl Microsoft-Windows-PowerShell/Operational", "PS operational", 30000);
                Kernel.Tool("wevtutil.exe", "cl \"Windows PowerShell\"", "PS classic", 30000);
                return r1;
            }, "PowerShell operational + classic"));
            c.Actions.Add(A("9.4", "Scrub Defender Operational log", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.Tool("wevtutil.exe", "cl Microsoft-Windows-Windows Defender/Operational", "Defender operational log", 30000);
            }, "Microsoft-Windows-Windows Defender/Operational (v7 gap)"));
            c.Actions.Add(A("9.5", "SWEEP ALL event channels", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] wevtutil sweep of ALL channels"); return OpResult.OK("preview"); }
                ProcResult el = ProcRunner.Run("wevtutil.exe", "el", 30000);
                string[] logs = (el.Out ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int ok = 0, fail = 0;
                foreach (string lg in logs)
                {
                    string name = lg.Trim();
                    if (name.Length == 0) continue;
                    ProcResult r = ProcRunner.Run("wevtutil.exe", "cl \"" + name + "\"", 15000);
                    if (r.Code == 0) ok++; else fail++;
                    if ((ok + fail) % 50 == 0) Kernel.Log("[SWEEP] " + (ok + fail) + "/" + logs.Length + " channels...");
                }
                return OpResult.OK("swept " + ok + " channels" + (fail > 0 ? " (" + fail + " access denied/in-use)" : ""));
            }, "ALL event channels (irreversible; some protected channels will refuse)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 10 — Network stealth
            // ==================================================================================
            c = new Category();
            c.Num = "10"; c.Tag = "NETWORK";
            c.Title = "Network Identity Stealth (DNS, ARP, LLMNR, WPAD, IPv6)";
            c.Desc = "Flushes DNS/ARP, disables LLMNR+WPAD, ensures IPv6 temporary addresses.";
            c.Info = "Transient caches plus persistent spoofing-resistant settings. IPv6 temporary addresses rotate your IPv6 identity.";
            c.InfoPaths = "Clear-DnsClientCache\nnetsh interface ip delete arpcache\nHKLM\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient -> EnableMulticast=0\nnetsh interface ipv6 set privacy state=enabled";
            c.Actions.Add(A("10.1", "Flush DNS cache", RiskLevel.Safe, delegate(bool dry) { return Kernel.Tool("ipconfig.exe", "/flushdns", "DNS flush", 15000); }, "ipconfig /flushdns"));
            c.Actions.Add(A("10.2", "Delete ARP cache", RiskLevel.Safe, delegate(bool dry) { return Kernel.Tool("netsh.exe", "interface ip delete arpcache", "ARP delete", 15000); }, "netsh delete arpcache"));
            c.Actions.Add(A("10.3", "Disable LLMNR", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient", "EnableMulticast", 0, RegistryValueKind.DWord);
            }, "EnableMulticast=0 (LLMNR off)"));
            c.Actions.Add(A("10.4", "Disable WPAD probing", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\Wpad", "WpadOverride", 1, RegistryValueKind.DWord);
            }, "WpadOverride=1"));
            c.Actions.Add(A("10.5", "Enable IPv6 temporary addresses", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.Tool("netsh.exe", "interface ipv6 set privacy state=enabled", "IPv6 privacy", 15000);
            }, "netsh ipv6 set privacy state=enabled"));
            cats.Add(c);

            // ==================================================================================
            // CAT 11 — DO + SmartScreen
            // ==================================================================================
            c = new Category();
            c.Num = "11"; c.Tag = "P2P & SMARTSCREEN";
            c.Title = "Delivery Optimization & SmartScreen";
            c.Desc = "Locks DODownloadMode=0 (no P2P) & optionally stops SmartScreen probing.";
            c.Info = "SmartScreen off REDUCES security (unknown binaries no longer reputation-checked). Tagged Balanced for that reason.";
            c.InfoPaths = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization -> DODownloadMode=0\nHKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System -> EnableSmartScreen=0 (security tradeoff!)";
            c.Actions.Add(A("11.1", "Lock DODownloadMode=0", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization", "DODownloadMode", 0, RegistryValueKind.DWord);
            }, "DODownloadMode=0"));
            c.Actions.Add(A("11.2", "Disable SmartScreen probing", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "EnableSmartScreen", 0, RegistryValueKind.DWord);
            }, "EnableSmartScreen=0 (SECURITY TRADEOFF)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 12 — Browser caches (v8: multi-profile)
            // ==================================================================================
            c = new Category();
            c.Num = "12"; c.Tag = "BROWSER CACHE";
            c.Title = "Multi-Browser Caches (all profiles)";
            c.Desc = "Chrome/Brave/Edge/Firefox temp+GPU+code caches across ALL profiles (Profile 1..N).";
            c.Info = "v7 cleaned only the Default profile. v8 enumerates every 'User Data\\*' profile folder. Logins/cookies untouched.";
            c.InfoPaths = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\{Cache,Code Cache,GPUCache}\n%LOCALAPPDATA%\\BraveSoftware\\...\n%LOCALAPPDATA%\\Microsoft\\Edge\\...\n%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2";
            c.Actions.Add(A("12.1", "Clean Chrome (all profiles)", RiskLevel.Safe, delegate(bool dry) { return BrowserClean("Google\\Chrome"); }, "Chrome User Data\\* profiles"));
            c.Actions.Add(A("12.2", "Clean Brave (all profiles)", RiskLevel.Safe, delegate(bool dry) { return BrowserClean("BraveSoftware\\Brave-Browser"); }, "Brave profiles"));
            c.Actions.Add(A("12.3", "Clean Edge (all profiles)", RiskLevel.Safe, delegate(bool dry) { return BrowserClean("Microsoft\\Edge"); }, "Edge profiles"));
            c.Actions.Add(A("12.4", "Clean Firefox (all profiles)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem \"$env:LOCALAPPDATA\\Mozilla\\Firefox\\Profiles\" -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item \"$($_.FullName)\\cache2\\*\" -Recurse -Force -ErrorAction SilentlyContinue }; 'done'", "Firefox cache2", 60000);
            }, "Firefox cache2"));
            cats.Add(c);

            // ==================================================================================
            // CAT 13 — Prefetch
            // ==================================================================================
            c = new Category();
            c.Num = "13"; c.Tag = "PREFETCH";
            c.Title = "Prefetch & SuperFetch Execution Traces";
            c.Desc = "Deletes .pf files, disables Prefetcher & stops SysMain.";
            c.Info = "Prefetch files record every program executed with run counts and timestamps.";
            c.InfoPaths = "C:\\Windows\\Prefetch\\*.pf\nHKLM\\..\\PrefetchParameters -> EnablePrefetcher=0\nService: SysMain";
            c.Actions.Add(A("13.1", "Delete prefetch files", RiskLevel.Safe, delegate(bool dry) { return Kernel.FileDeleteGlob("C:\\Windows\\Prefetch", "*.pf"); }, "Prefetch\\*.pf"));
            c.Actions.Add(A("13.2", "Disable prefetcher", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters", "EnablePrefetcher", 0, RegistryValueKind.DWord);
            }, "EnablePrefetcher=0"));
            c.Actions.Add(A("13.3", "Stop+disable SysMain", RiskLevel.Balanced, delegate(bool dry) { return Kernel.SvcStopDisable("SysMain"); }, "SysMain (SuperFetch) off"));
            cats.Add(c);

            // ==================================================================================
            // CAT 14 — AmCache
            // ==================================================================================
            c = new Category();
            c.Num = "14"; c.Tag = "AMCACHE";
            c.Title = "AmCache Execution History (SHA-1 logs)";
            c.Desc = "Deletes Amcache.hve (+LOG files) & RecentFileCache.bcl.";
            c.Info = "Amcache.hve is a registry hive logging every executable run with paths and SHA-1 hashes. If locked by the OS it may need a reboot-then-rerun.";
            c.InfoPaths = "C:\\Windows\\AppCompat\\Programs\\Amcache.hve{,.LOG1,.LOG2}\nC:\\Windows\\AppCompat\\Programs\\RecentFileCache.bcl";
            c.Actions.Add(A("14.1", "Delete Amcache.hve + logs", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.FileDelete("C:\\Windows\\AppCompat\\Programs\\Amcache.hve");
                Kernel.FileDelete("C:\\Windows\\AppCompat\\Programs\\Amcache.hve.LOG1");
                Kernel.FileDelete("C:\\Windows\\AppCompat\\Programs\\Amcache.hve.LOG2");
                return r1;
            }, "Amcache.hve + .LOG*"));
            c.Actions.Add(A("14.2", "Delete RecentFileCache.bcl", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDelete("C:\\Windows\\AppCompat\\Programs\\RecentFileCache.bcl");
            }, "RecentFileCache.bcl"));
            cats.Add(c);

            // ==================================================================================
            // CAT 15 — ShimCache
            // ==================================================================================
            c = new Category();
            c.Num = "15"; c.Tag = "SHIMCACHE";
            c.Title = "ShimCache (AppCompatCache) Purge";
            c.Desc = "Clears AppCompatCache binary execution metadata from the SYSTEM hive.";
            c.Info = "ShimCache tracks executable file paths and sizes for compatibility — standard forensic evidence of execution. Takes effect on reboot.";
            c.InfoPaths = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\AppCompatCache -> AppCompatCache";
            c.Actions.Add(A("15.1", "Clear AppCompatCache value", RiskLevel.Safe, false, true, delegate(bool dry)
            {
                return Kernel.RegDelValue("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\AppCompatCache", "AppCompatCache");
            }, "AppCompatCache value (effective after reboot)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 16 — UserAssist
            // ==================================================================================
            c = new Category();
            c.Num = "16"; c.Tag = "USERASSIST";
            c.Title = "UserAssist GUI Execution Tracking (ROT13)";
            c.Desc = "Clears ROT13 execution logs & disables Start_TrackProgs.";
            c.Info = "UserAssist counts every GUI application launch per-user with ROT13-obfuscated keys.";
            c.InfoPaths = "HKCU\\...\\Explorer\\UserAssist\\{GUID}\\Count\nHKCU\\...\\Explorer\\Advanced -> Start_TrackProgs=0";
            c.Actions.Add(A("16.1", "Clear UserAssist logs", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\UserAssist' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item \"$($_.PSPath)\\Count\" -Recurse -Force -ErrorAction SilentlyContinue }; 'cleared'", "UserAssist", 30000);
            }, "UserAssist Count keys"));
            c.Actions.Add(A("16.2", "Disable program tracking", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_TrackProgs", 0, RegistryValueKind.DWord);
            }, "Start_TrackProgs=0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 17 — BAM/DAM
            // ==================================================================================
            c = new Category();
            c.Num = "17"; c.Tag = "BAM / DAM";
            c.Title = "Background & Desktop Activity Monitor";
            c.Desc = "Clears BAM/DAM per-user last-execution-time registry entries.";
            c.Info = "BAM/DAM record the last execution time of every background/desktop program per user SID.";
            c.InfoPaths = "HKLM\\SYSTEM\\CurrentControlSet\\Services\\bam\\State\\UserSettings\nHKLM\\SYSTEM\\CurrentControlSet\\Services\\dam\\State\\UserSettings";
            c.Actions.Add(A("17.1", "Clear BAM entries", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\bam\\State\\UserSettings' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue }; 'cleared'", "BAM", 30000);
            }, "bam\\State\\UserSettings"));
            c.Actions.Add(A("17.2", "Clear DAM entries", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\dam\\State\\UserSettings' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue }; 'cleared'", "DAM", 30000);
            }, "dam\\State\\UserSettings"));
            cats.Add(c);

            // ==================================================================================
            // CAT 18 — SRUM (v8: full ESE database + transaction logs)
            // ==================================================================================
            c = new Category();
            c.Num = "18"; c.Tag = "SRUM";
            c.Title = "SRUM Database + ESE Transaction Logs";
            c.Desc = "v8 FIX: wipes SRUDB.dat AND .log/.chk files (v7 left ESE logs behind).";
            c.Info = "SRUM tracks network usage, CPU time and energy per app in 60-min buckets. The ESE database has transaction/checkpoint logs that survive a bare SRUDB.dat delete — v8 wipes them all after stopping DPS.";
            c.InfoPaths = "C:\\Windows\\System32\\SRU\\SRUDB.dat{,.log,.chk}\nService: DPS (stopped during wipe)";
            c.Actions.Add(A("18.1", "Stop DPS & wipe entire SRU DB set", RiskLevel.Balanced, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop DPS, delete SRU\\SRUDB.dat + logs, start DPS"); return OpResult.OK("preview"); }
                Kernel.SvcStopDisable("DPS");
                OpResult r1 = Kernel.FileDeleteGlob("C:\\Windows\\System32\\SRU", "SRUDB.dat*");
                Kernel.FileDeleteGlob("C:\\Windows\\System32\\SRU", "*.log");
                Kernel.FileDeleteGlob("C:\\Windows\\System32\\SRU", "*.chk");
                // re-enable DPS startup (we only wanted it stopped during the wipe)
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Set-Service -Name DPS -StartupType Automatic -ErrorAction SilentlyContinue; Start-Service -Name DPS -ErrorAction SilentlyContinue\"", 30000);
                return r1;
            }, "SRU folder ESE database set"));
            cats.Add(c);

            // ==================================================================================
            // CAT 19 — TypedPaths/URLs/MUICache
            // ==================================================================================
            c = new Category();
            c.Num = "19"; c.Tag = "TYPED HISTORY";
            c.Title = "TypedPaths, TypedURLs & MUICache";
            c.Desc = "Explorer address bar, IE typed URLs & MUICache app execution names.";
            c.Info = "TypedPaths = folders typed in Explorer. TypedURLs = legacy IE. MUICache logs display names of executed applications.";
            c.InfoPaths = "HKCU\\...\\Explorer\\TypedPaths\nHKCU\\...\\Internet Explorer\\TypedURLs\nHKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\MuiCache";
            c.Actions.Add(A("19.1", "Clear TypedPaths", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\TypedPaths");
            }, "Explorer address bar history"));
            c.Actions.Add(A("19.2", "Clear TypedURLs", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Internet Explorer\\TypedURLs");
            }, "IE typed URLs"));
            c.Actions.Add(A("19.3", "Clear MUICache", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\MuiCache");
            }, "MUICache executed-app names"));
            cats.Add(c);

            // ==================================================================================
            // CAT 20 — RecentDocs
            // ==================================================================================
            c = new Category();
            c.Num = "20"; c.Tag = "RECENT DOCS";
            c.Title = "RecentDocs Registry & LNK Files";
            c.Desc = "Clears RecentDocs MRU registry and .lnk shortcut evidence.";
            c.Info = "LNK files embed target paths, MAC timestamps and volume serial numbers.";
            c.InfoPaths = "HKCU\\...\\Explorer\\RecentDocs\n%APPDATA%\\Microsoft\\Windows\\Recent\\*.lnk";
            c.Actions.Add(A("20.1", "Clear RecentDocs registry", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RecentDocs");
            }, "RecentDocs"));
            c.Actions.Add(A("20.2", "Delete .lnk files", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.FileDeleteGlob(AP() + "\\Microsoft\\Windows\\Recent", "*.lnk");
            }, "Recent\\*.lnk"));
            cats.Add(c);

            // ==================================================================================
            // CAT 21 — Notifications DB
            // ==================================================================================
            c = new Category();
            c.Num = "21"; c.Tag = "NOTIFICATIONS";
            c.Title = "Windows Push Notification Database";
            c.Desc = "Stops WPN service & deletes wpndatabase.db (+WAL/SHM).";
            c.Info = "The notification SQLite database stores toast content, arrival times and app handlers.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\Windows\\Notifications\\wpndatabase.db{,-wal,-shm}\nService: WpnService";
            c.Actions.Add(A("21.1", "Wipe notification DB", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop WpnService, delete wpndatabase.db*, start"); return OpResult.OK("preview"); }
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name WpnService -Force -ErrorAction SilentlyContinue\"", 20000);
                OpResult r = Kernel.FileDeleteGlob(LA() + "\\Microsoft\\Windows\\Notifications", "wpndatabase.db*");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name WpnService -ErrorAction SilentlyContinue\"", 20000);
                return r;
            }, "wpndatabase.db + WAL/SHM"));
            cats.Add(c);

            // ==================================================================================
            // CAT 22 — RDP
            // ==================================================================================
            c = new Category();
            c.Num = "22"; c.Tag = "RDP ARTIFACTS";
            c.Title = "RDP Bitmap Cache & Server History";
            c.Desc = "Clears RDP bitmap tiles, server MRU and .rdp connection files.";
            c.Info = "RDP bitmap caches can be reassembled to reconstruct remote session screens.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\Terminal Server Client\\Cache\\*\nHKCU\\...\\Terminal Server Client\\{Default,Servers}\nDocuments\\*.rdp, Desktop\\*.rdp";
            c.Actions.Add(A("22.1", "Clear RDP bitmap cache", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe(LA() + "\\Microsoft\\Terminal Server Client\\Cache"); }, "RDP bitmap tiles"));
            c.Actions.Add(A("22.2", "Clear RDP server MRU", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Terminal Server Client\\Default");
                Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Terminal Server Client\\Servers");
                return r1;
            }, "Terminal Server Client MRU"));
            c.Actions.Add(A("22.3", "Delete .rdp files", RiskLevel.Safe, delegate(bool dry)
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                OpResult r1 = Kernel.FileDeleteGlob(docs, "*.rdp");
                Kernel.FileDeleteGlob(desk, "*.rdp");
                return r1;
            }, "*.rdp connection files"));
            cats.Add(c);

            // ==================================================================================
            // CAT 23 — WiFi (destructive: paranoid master only)
            // ==================================================================================
            c = new Category();
            c.Num = "23"; c.Tag = "WIFI PROFILES";
            c.Title = "WiFi Profile History (DESTRUCTIVE)";
            c.Desc = "Deletes ALL saved WiFi profiles+passwords & disables WiFi-Sense style autoconnect.";
            c.Info = "Deleting all profiles disconnects saved networks (you will re-enter WiFi passwords). Destructive: only runs in Paranoid master profile or when clicked manually.";
            c.InfoPaths = "netsh wlan delete profile name=* i=*\nHKLM\\...\\WcmSvc\\wifinetworkmanager\\config -> AutoConnectAllowedOEM=0";
            c.Actions.Add(A("23.1", "Delete ALL WiFi profiles", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.Tool("netsh.exe", "wlan delete profile name=* i=*", "wifi profiles", 30000);
            }, "all saved WiFi profiles (passwords lost)"));
            c.Actions.Add(A("23.2", "Disable WiFi autoconnect (OEM)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\WcmSvc\\wifinetworkmanager\\config", "AutoConnectAllowedOEM", 0, RegistryValueKind.DWord);
            }, "AutoConnectAllowedOEM=0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 24 — Defender history (v8: honest + ops log + Remove-MpThreat)
            // ==================================================================================
            c = new Category();
            c.Num = "24"; c.Tag = "DEFENDER HIST";
            c.Title = "Defender Detection & Quarantine History";
            c.Desc = "Removes detection history (Remove-MpThreat first), quarantine files, ops log.";
            c.Info = "v8 uses the SUPPORTED API (Remove-MpThreat) before touching folders. NOTE: with Tamper Protection ON, ProgramData Defender folders are protected even from admins — direct deletes may fail; the tool reports this honestly instead of silently claiming success.";
            c.InfoPaths = "Remove-MpThreat (API)\nC:\\ProgramData\\Microsoft\\Windows Defender\\Scans\\History\\Service\\*\nC:\\ProgramData\\Microsoft\\Windows Defender\\Quarantine\\*\nEvent log: Microsoft-Windows-Windows Defender/Operational";
            c.Actions.Add(A("24.1", "Remove threats via API", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Remove-MpThreat -ErrorAction SilentlyContinue; 'threat history removed'", "Remove-MpThreat", 60000);
            }, "Remove-MpThreat"));
            c.Actions.Add(A("24.2", "Clear detection history folder", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.DirWipe("C:\\ProgramData\\Microsoft\\Windows Defender\\Scans\\History\\Service");
            }, "Scans\\History\\Service (Tamper Protection may block)"));
            c.Actions.Add(A("24.3", "Clear quarantine folder", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.DirWipe("C:\\ProgramData\\Microsoft\\Windows Defender\\Quarantine");
            }, "Quarantine (Tamper Protection may block)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 25 — Search & Cortana
            // ==================================================================================
            c = new Category();
            c.Num = "25"; c.Tag = "SEARCH & CORTANA";
            c.Title = "Cortana, Search History & Index";
            c.Desc = "Disables Cortana, clears search history & optionally rebuilds the index.";
            c.Info = "Windows.edb indexes file names/content metadata system-wide.";
            c.InfoPaths = "HKLM\\...\\Windows Search -> AllowCortana=0\nHKCU\\...\\SearchSettings -> IsDeviceSearchHistoryEnabled=0\nC:\\ProgramData\\Microsoft\\Search\\Data\\Applications\\Windows\\Windows.edb";
            c.Actions.Add(A("25.1", "Disable Cortana", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "AllowCortana", 0, RegistryValueKind.DWord);
            }, "AllowCortana=0"));
            c.Actions.Add(A("25.2", "Disable device search history", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings", "IsDeviceSearchHistoryEnabled", 0, RegistryValueKind.DWord);
            }, "IsDeviceSearchHistoryEnabled=0"));
            c.Actions.Add(A("25.3", "Rebuild search index", RiskLevel.Balanced, false, true, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop WSearch, delete Windows.edb, start"); return OpResult.OK("preview"); }
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name WSearch -Force -ErrorAction SilentlyContinue\"", 30000);
                OpResult r = Kernel.FileDelete("C:\\ProgramData\\Microsoft\\Search\\Data\\Applications\\Windows\\Windows.edb");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name WSearch -ErrorAction SilentlyContinue\"", 30000);
                return r;
            }, "Windows.edb (index rebuilds slowly)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 26 — OneDrive
            // ==================================================================================
            c = new Category();
            c.Num = "26"; c.Tag = "ONEDRIVE LOGS";
            c.Title = "OneDrive Telemetry & Sync Logs";
            c.Desc = "Deletes OneDrive diagnostic, sync telemetry and setup logs.";
            c.Info = "OneDrive logs reveal file names and sync history.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\OneDrive\\logs\\*\n%LOCALAPPDATA%\\Microsoft\\OneDrive\\setup\\logs\\*";
            c.Actions.Add(A("26.1", "Clear OneDrive logs", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe(LA() + "\\Microsoft\\OneDrive\\logs"); }, "OneDrive\\logs"));
            c.Actions.Add(A("26.2", "Clear OneDrive setup logs", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe(LA() + "\\Microsoft\\OneDrive\\setup\\logs"); }, "OneDrive\\setup\\logs"));
            cats.Add(c);

            // ==================================================================================
            // CAT 27 — Recall/Copilot (v8: 24H2 DisableAIDataAnalysis)
            // ==================================================================================
            c = new Category();
            c.Num = "27"; c.Tag = "WIN AI RECALL";
            c.Title = "Recall AI, Copilot & Snapshots (24H2 policies)";
            c.Desc = "v8: DisableAIDataAnalysis (24H2 authoritative) + legacy keys + snapshot purge.";
            c.Info = "For Windows 11 24H2+ Microsoft's authoritative policy is DisableAIDataAnalysis=1 (disables Recall AND removes its bits). Legacy DisableRecall kept for 23H2. Snapshot store is purged separately.";
            c.InfoPaths = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI -> DisableAIDataAnalysis=1 (24H2+)\nHKLM\\...\\WindowsAI -> DisableRecall=1, AllowRecallEnablement=0 (23H2)\nHKLM\\...\\WindowsCopilot -> TurnOffWindowsCopilot=1\n%LOCALAPPDATA%\\CoreAIPlatform.00\\UKP\\*";
            c.Actions.Add(A("27.1", "DisableAIDataAnalysis=1 (24H2)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "DisableAIDataAnalysis", 1, RegistryValueKind.DWord);
            }, "WindowsAI -> DisableAIDataAnalysis=1"));
            c.Actions.Add(A("27.2", "Legacy DisableRecall keys (23H2)", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "DisableRecall", 1, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "AllowRecallEnablement", 0, RegistryValueKind.DWord);
                return r1;
            }, "DisableRecall=1 + AllowRecallEnablement=0"));
            c.Actions.Add(A("27.3", "Disable Windows Copilot", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot", "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
            }, "TurnOffWindowsCopilot=1"));
            c.Actions.Add(A("27.4", "Delete Recall snapshots", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.DirWipe(LA() + "\\CoreAIPlatform.00\\UKP");
            }, "CoreAIPlatform.00\\UKP"));
            cats.Add(c);

            // ==================================================================================
            // CAT 28 — Scheduled task telemetry
            // ==================================================================================
            c = new Category();
            c.Num = "28"; c.Tag = "SCHED TASKS";
            c.Title = "Scheduled Task Telemetry Killswitch";
            c.Desc = "Disables Compatibility Appraiser, ProgramDataUpdater, CEIP & RAC tasks.";
            c.Info = "These scheduled tasks continuously collect and upload diagnostic data.";
            c.InfoPaths = "\\Microsoft\\Windows\\Application Experience\\{Microsoft Compatibility Appraiser, ProgramDataUpdater}\n\\Microsoft\\Windows\\Customer Experience Improvement Program\\{Consolidator, KernelCeipTask, UsbCeipTask}\n\\Microsoft\\Windows\\Application Experience\\StartupAppTask";
            c.Actions.Add(A("28.1", "Disable Compatibility Appraiser", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] schtasks disable Compat Appraiser"); return OpResult.OK("preview"); }
                ProcResult q = ProcRunner.Run("schtasks.exe", "/Query /TN \"\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\"", 15000);
                if (q.Code != 0) return OpResult.SKIP("task not present on this system (removed/renamed by Windows build)");
                return Kernel.Tool("schtasks.exe", "/Change /TN \"\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser\" /Disable", "Compat Appraiser", 20000);
            }, "Application Experience tasks"));
            c.Actions.Add(A("28.2", "Disable ProgramDataUpdater", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] schtasks disable ProgramDataUpdater"); return OpResult.OK("preview"); }
                ProcResult q = ProcRunner.Run("schtasks.exe", "/Query /TN \"\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater\"", 15000);
                if (q.Code != 0) return OpResult.SKIP("task not present on this system (removed/renamed by Windows build)");
                return Kernel.Tool("schtasks.exe", "/Change /TN \"\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater\" /Disable", "ProgramDataUpdater", 20000);
            }, "ProgramDataUpdater"));
            c.Actions.Add(A("28.3", "Disable CEIP tasks", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] disable CEIP Consolidator/KernelCeip/UsbCeip"); return OpResult.OK("preview"); }
                string[] tasks = new string[] {
                    "\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator",
                    "\\Microsoft\\Windows\\Customer Experience Improvement Program\\KernelCeipTask",
                    "\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip" };
                int ok = 0, absent = 0;
                foreach (string t in tasks)
                {
                    ProcResult q = ProcRunner.Run("schtasks.exe", "/Query /TN \"" + t + "\"", 15000);
                    if (q.Code != 0) { absent++; continue; }
                    ProcResult r = ProcRunner.Run("schtasks.exe", "/Change /TN \"" + t + "\" /Disable", 15000);
                    if (r.Code == 0) ok++;
                }
                if (absent == tasks.Length) return OpResult.SKIP("CEIP tasks not present on this system (removed/renamed by Windows build)");
                return OpResult.OK(ok + "/" + (tasks.Length - absent) + " CEIP tasks disabled (" + absent + " absent)");
            }, "CEIP Consolidator/KernelCeip/UsbCeip"));
            cats.Add(c);

            // ==================================================================================
            // CAT 29 — Forensic hardening
            // ==================================================================================
            c = new Category();
            c.Num = "29"; c.Tag = "FORENSIC HARDEN";
            c.Title = "Pagefile, Hibernation & NTFS Hardening";
            c.Desc = "ClearPageFileAtShutdown, hibernation off (hiberfil.sys), LastAccess stamps off.";
            c.Info = "Pagefile/hibernation hold full RAM snapshots. LastAccess timestamps record every file touch.";
            c.InfoPaths = "HKLM\\..\\Memory Management -> ClearPageFileAtShutdown=1\npowercfg /hibernate off\nfsutil behavior set DisableLastAccess 1";
            c.Actions.Add(A("29.1", "Enable pagefile clear at shutdown", RiskLevel.Safe, false, true, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management", "ClearPageFileAtShutdown", 1, RegistryValueKind.DWord);
            }, "ClearPageFileAtShutdown=1 (shutdown slower)"));
            c.Actions.Add(A("29.2", "Disable hibernation", RiskLevel.Balanced, true, false, delegate(bool dry)
            {
                return Kernel.Tool("powercfg.exe", "/hibernate off", "hibernate off", 20000);
            }, "hibernate off (Fast Startup also disabled; hiberfil.sys deleted)"));
            c.Actions.Add(A("29.3", "Disable NTFS LastAccess", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.Tool("fsutil.exe", "behavior set DisableLastAccess 1", "DisableLastAccess", 15000);
            }, "fsutil DisableLastAccess=1"));
            cats.Add(c);

            // ==================================================================================
            // CAT 30 — Icon/Font cache
            // ==================================================================================
            c = new Category();
            c.Num = "30"; c.Tag = "ICON/FONT CACHE";
            c.Title = "Icon Cache & Font Cache Rebuild";
            c.Desc = "Deletes iconcache DBs & FNTCACHE.DAT forcing a clean rebuild.";
            c.Info = "Icon/font caches can retain stale traces and are rebuilt automatically.";
            c.InfoPaths = "%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\n%LOCALAPPDATA%\\IconCache.db\nC:\\Windows\\System32\\FNTCACHE.DAT";
            c.Actions.Add(A("30.1", "Delete icon cache", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] kill explorer, delete iconcache*, restart explorer"); return OpResult.OK("preview"); }
                ProcRunner.Run("taskkill.exe", "/F /IM explorer.exe", 15000);
                OpResult r = Kernel.FileDeleteGlob(LA() + "\\Microsoft\\Windows\\Explorer", "iconcache_*");
                Kernel.FileDelete(LA() + "\\IconCache.db");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Process explorer.exe\"", 15000);
                return r;
            }, "iconcache DBs (Explorer restarts)"));
            c.Actions.Add(A("30.2", "Clear font cache", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop FontCache, delete FNTCACHE.DAT, start"); return OpResult.OK("preview"); }
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name FontCache -Force -ErrorAction SilentlyContinue\"", 20000);
                OpResult r = Kernel.FileDelete("C:\\Windows\\System32\\FNTCACHE.DAT");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name FontCache -ErrorAction SilentlyContinue\"", 20000);
                return r;
            }, "FNTCACHE.DAT"));
            cats.Add(c);

            // ==================================================================================
            // CAT 31 — PowerShell logging off
            // ==================================================================================
            c = new Category();
            c.Num = "31"; c.Tag = "PS LOGGING";
            c.Title = "PowerShell Logging Killswitch";
            c.Desc = "Disables Transcription, Module Logging & ScriptBlock Logging policies.";
            c.Info = "PowerShell can record every command, module and script block to logs. NOTE: disabling reduces detection of malicious PS on your own machine — a security tradeoff.";
            c.InfoPaths = "HKLM\\...\\PowerShell\\Transcription -> EnableTranscripting=0\nHKLM\\...\\PowerShell\\ModuleLogging -> EnableModuleLogging=0\nHKLM\\...\\PowerShell\\ScriptBlockLogging -> EnableScriptBlockLogging=0";
            c.Actions.Add(A("31.1", "Disable transcription", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription", "EnableTranscripting", 0, RegistryValueKind.DWord);
            }, "EnableTranscripting=0"));
            c.Actions.Add(A("31.2", "Disable module logging", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ModuleLogging", "EnableModuleLogging", 0, RegistryValueKind.DWord);
            }, "EnableModuleLogging=0"));
            c.Actions.Add(A("31.3", "Disable scriptblock logging", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging", "EnableScriptBlockLogging", 0, RegistryValueKind.DWord);
            }, "EnableScriptBlockLogging=0"));
            cats.Add(c);

            // ==================================================================================
            // CAT 32 — Sensor permissions
            // ==================================================================================
            c = new Category();
            c.Num = "32"; c.Tag = "SENSOR ACCESS";
            c.Title = "CapabilityAccessManager Sensor Reset";
            c.Desc = "Denies Location/Camera/Mic + all sensors; clears per-app consent timestamps.";
            c.Info = "CapabilityAccessManager records which app used sensors and when. Deny-by-default plus consent-store cleanup removes that history.";
            c.InfoPaths = "HKCU\\...\\CapabilityAccessManager\\ConsentStore\\{location,webcam,microphone,userNotificationData...}";
            c.Actions.Add(A("32.1", "Deny Location globally", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location", "Value", "Deny", RegistryValueKind.String);
            }, "ConsentStore\\location -> Deny"));
            c.Actions.Add(A("32.2", "Deny Camera globally", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam", "Value", "Deny", RegistryValueKind.String);
            }, "ConsentStore\\webcam -> Deny"));
            c.Actions.Add(A("32.3", "Deny Microphone globally", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone", "Value", "Deny", RegistryValueKind.String);
            }, "ConsentStore\\microphone -> Deny"));
            c.Actions.Add(A("32.4", "Deny ALL sensor categories", RiskLevel.Balanced, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] Deny all 11 ConsentStore categories"); return OpResult.OK("preview"); }
                string[] catsToDeny = new string[] { "location", "webcam", "microphone", "contacts", "appointments", "phoneCallHistory", "email", "chat", "radios", "bluetoothSync", "appDiagnostics", "userAccountInformation", "documentsLibrary", "picturesLibrary", "videosLibrary" };
                int denied = 0;
                foreach (string s in catsToDeny)
                {
                    OpResult r = Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\" + s, "Value", "Deny", RegistryValueKind.String);
                    if (r.Ok && !r.Skipped) denied++;
                }
                return OpResult.OK(denied + " sensor categories denied");
            }, "All ConsentStore categories -> Deny"));
            c.Actions.Add(A("32.5", "Clear per-app consent history", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like 'NonPackaged' -or $_.Property -contains 'LastUsedTimeStop' } | Out-Null; Get-ChildItem 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore' -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.GetValueNames() | Where-Object { $_ -like 'LastUsedTime*' } | ForEach-Object { Remove-ItemProperty -Path $_.PSPath -Name $_ -Force -ErrorAction SilentlyContinue } }; 'cleared'", "consent history", 30000);
            }, "LastUsedTime* values (per-app sensor usage timestamps)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 33 (NEW v8) — Storage & Recovery artifacts
            // ==================================================================================
            c = new Category();
            c.Num = "33"; c.Tag = "STORAGE/RECOVERY";
            c.Title = "VSS Shadows, Restore Points, RegBack & Windows.old";
            c.Desc = "NEW: deletes Volume Shadow Copies, disables restore, wipes RegBack hives & Windows.old.";
            c.Info = "Shadow copies and RegBack hold recoverable copies of registry hives and old files. DESTRUCTIVE: past restore points are lost forever.";
            c.InfoPaths = "vssadmin delete shadows /all\nDisable-ComputerRestore\nC:\\Windows\\System32\\config\\RegBack\\*\nC:\\Windows.old";
            c.Actions.Add(A("33.1", "Delete ALL shadow copies", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.Tool("vssadmin.exe", "delete shadows /all /quiet", "VSS shadows", 120000);
            }, "vssadmin delete shadows /all (restore points lost)"));
            c.Actions.Add(A("33.2", "Disable System Restore", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.PSCheck("Disable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction SilentlyContinue; 'restore disabled'", "System Restore", 30000);
            }, "System Restore off (no future restore points)"));
            c.Actions.Add(A("33.3", "Shrink VSS storage to zero", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.Tool("vssadmin.exe", "resize shadowstorage /for=" + Environment.SystemDirectory.Substring(0, 2) + " /on=" + Environment.SystemDirectory.Substring(0, 2) + " /maxsize=0", "VSS storage", 60000);
            }, "shadowstorage maxsize=0"));
            c.Actions.Add(A("33.4", "Wipe registry backups (RegBack)", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.DirWipe("C:\\Windows\\System32\\config\\RegBack");
            }, "RegBack hive copies"));
            c.Actions.Add(A("33.5", "Delete Windows.old upgrade remnant", RiskLevel.Balanced, true, false, delegate(bool dry)
            {
                OpResult r = Kernel.DirDelete("C:\\Windows.old");
                Kernel.DirDelete("C:\\$Windows.~BT");
                Kernel.DirDelete("C:\\$Windows.~WS");
                return r;
            }, "Windows.old / $Windows.~BT / $Windows.~WS"));
            c.Actions.Add(A("33.6", "Empty Recycle Bin", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Clear-RecycleBin -Force -ErrorAction SilentlyContinue; 'bin emptied'", "Recycle Bin", 60000);
            }, "Clear-RecycleBin"));
            cats.Add(c);

            // ==================================================================================
            // CAT 34 (NEW v8) — Forensic databases
            // ==================================================================================
            c = new Category();
            c.Num = "34"; c.Tag = "FORENSIC DB";
            c.Title = "USN Journal & TrayNotify Artifacts";
            c.Desc = "NEW: deletes NTFS USN change journal ($UsnJrnl) & notification-area icon history.";
            c.Info = "The USN journal records EVERY file create/delete/rename on the volume — a top forensic source. Deleting it is safe (NTFS recreates on demand). TrayNotify records notification-area app history.";
            c.InfoPaths = "fsutil usn deletejournal /n /d C:\nHKCU\\...\\Explorer\\TrayNotify {IconStreams, PromotedIconCache}";
            c.Actions.Add(A("34.1", "Delete USN change journal (C:)", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.Tool("fsutil.exe", "usn deletejournal /n /d " + Environment.SystemDirectory.Substring(0, 2), "USN journal", 60000);
            }, "fsutil usn deletejournal (NTFS recreates automatically)"));
            c.Actions.Add(A("34.2", "Clear TrayNotify icon history", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegDelValue("HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\TrayNotify", "IconStreams");
                Kernel.RegDelValue("HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\TrayNotify", "PromotedIconCache");
                return r1;
            }, "TrayNotify IconStreams (Explorer restart needed)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 35 (NEW v8) — Device & network history
            // ==================================================================================
            c = new Category();
            c.Num = "35"; c.Tag = "DEVICE HISTORY";
            c.Title = "NetworkList, USB, Mount & Bluetooth History";
            c.Desc = "NEW: erases network profiles registry, USBSTOR enumeration, MountPoints2, BT cache.";
            c.Info = "Records of every network joined, every USB device ever plugged, drive-letter mounts and paired Bluetooth devices. MountedDevices/Bluetooth keys may require reboot/permissions — failures reported honestly.";
            c.InfoPaths = "HKLM\\...\\NetworkList\\{Profiles,Signatures}\nHKLM\\SYSTEM\\CurrentControlSet\\Enum\\USBSTOR\nHKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MountPoints2\nHKLM\\SYSTEM\\MountedDevices\nHKLM\\SYSTEM\\CurrentControlSet\\Services\\BTHPORT\\Parameters\\Devices";
            c.Actions.Add(A("35.1", "Delete NetworkList profiles+signatures", RiskLevel.Balanced, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegDeleteTree("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\NetworkList\\Profiles");
                Kernel.RegDeleteTree("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\NetworkList\\Signatures");
                return r1;
            }, "NetworkList (all networks ever joined)"));
            c.Actions.Add(A("35.2", "Delete USBSTOR enumeration", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKLM\\SYSTEM\\CurrentControlSet\\Enum\\USBSTOR");
            }, "USBSTOR (USB device serials)"));
            c.Actions.Add(A("35.3", "Delete MountPoints2 mount history", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MountPoints2");
            }, "MountPoints2"));
            c.Actions.Add(A("35.4", "Clear MountedDevices map", RiskLevel.Paranoid, true, true, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKLM\\SYSTEM\\MountedDevices");
            }, "MountedDevices (drive letters re-assigned at next boot)"));
            c.Actions.Add(A("35.5", "Clear Bluetooth pairing cache", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegDeleteTree("HKLM\\SYSTEM\\CurrentControlSet\\Services\\BTHPORT\\Parameters\\Devices");
            }, "BTHPORT\\Parameters\\Devices (pairings may need re-pairing)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 36 (NEW v8) — Privacy toggles
            // ==================================================================================
            c = new Category();
            c.Num = "36"; c.Tag = "PRIVACY TOGGLES";
            c.Title = "Windows 10/11 Privacy Toggle Pack";
            c.Desc = "NEW: widgets, suggested content, online speech, feedback, Find-My-Device, background apps.";
            c.Info = "The consumer-facing privacy toggles v7 never touched. Widgets use the Dsh policy (UCPD-safe); TaskbarDa direct edits are blocked by UCPD on Win11 — noted, not used.";
            c.InfoPaths = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Dsh -> AllowNewsAndInterests=0\nHKLM\\...\\Windows Feeds -> EnableFeeds=0\nHKCU\\...\\ContentDeliveryManager -> SubscribedContent-*Enabled=0\nHKCU\\...\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy -> HasAccepted=0\nHKCU\\Software\\Microsoft\\Siuf\\Rules -> NumberOfSIUFInPeriod=0\nHKLM\\...\\FindMyDevice -> AllowFindMyDevice=0";
            c.Actions.Add(A("36.1", "Disable Widgets/News&Interests", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Dsh", "AllowNewsAndInterests", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Feeds", "EnableFeeds", 0, RegistryValueKind.DWord);
                return r1;
            }, "Dsh AllowNewsAndInterests=0 (Win11) + Windows Feeds EnableFeeds=0 (Win10)"));
            c.Actions.Add(A("36.2", "Disable suggested/sponsored content", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] ContentDeliveryManager SubscribedContent-*Enabled=0 etc"); return OpResult.OK("preview"); }
                string baseKey = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager";
                string[] vals = new string[] { "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-338393Enabled", "SubscribedContent-353694Enabled", "SubscribedContent-353695Enabled", "SubscribedContent-353698Enabled", "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled", "RotatingLockScreenOverlayEnabled", "PreInstalledAppsEnabled", "OemPreInstalledAppsEnabled", "FeatureManagementEnabled" };
                int n = 0;
                foreach (string v in vals)
                {
                    OpResult r = Kernel.RegSet(baseKey, v, 0, RegistryValueKind.DWord);
                    if (r.Ok && !r.Skipped) n++;
                }
                return OpResult.OK(n + " content-delivery toggles off");
            }, "ContentDeliveryManager (Start suggestions, lock-screen ads, silent app installs)"));
            c.Actions.Add(A("36.3", "Disable online speech recognition", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy", "HasAccepted", 0, RegistryValueKind.DWord);
            }, "OnlineSpeechPrivacy HasAccepted=0"));
            c.Actions.Add(A("36.4", "Silence feedback requests", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKCU\\Software\\Microsoft\\Siuf\\Rules", "NumberOfSIUFInPeriod", 0, RegistryValueKind.DWord);
                Kernel.RegDelValue("HKCU\\Software\\Microsoft\\Siuf\\Rules", "PeriodInNanoSeconds");
                return r1;
            }, "Siuf Rules (feedback popups off)"));
            c.Actions.Add(A("36.5", "Disable Find My Device", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\FindMyDevice", "AllowFindMyDevice", 0, RegistryValueKind.DWord);
            }, "AllowFindMyDevice=0"));
            c.Actions.Add(A("36.6", "Disable background apps (Win10)", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications", "GlobalUserDisabled", 1, RegistryValueKind.DWord);
            }, "GlobalUserDisabled=1"));
            c.Actions.Add(A("36.7", "Disable Start recommendations", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_IrisRecommendations", 0, RegistryValueKind.DWord);
            }, "Start_IrisRecommendations=0 (Win11 Recommended section)"));
            c.Actions.Add(A("36.8", "Disable tailored experiences", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord);
            }, "TailoredExperiences=0"));
            c.Actions.Add(A("36.9", "Disable Steps Recorder & App Impact Telemetry", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "AitEnable", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "DisableUAR", 1, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "DisablePCA", 1, RegistryValueKind.DWord);
                return r1;
            }, "AppCompat AitEnable=0, DisableUAR=1 (Steps Recorder), DisablePCA=1"));
            cats.Add(c);

            // ==================================================================================
            // CAT 37 (NEW v8) — Network blocking
            // ==================================================================================
            c = new Category();
            c.Num = "37"; c.Tag = "NET BLOCKING";
            c.Title = "Telemetry Endpoint Blocking (hosts) & DoH";
            c.Desc = "NEW: hosts-file block of 33 telemetry domains + auto-DoH encryption policy.";
            c.Info = "Blocks telemetry endpoints at DNS level via a clearly-marked hosts block (removable). Encrypted-DNS policy makes Windows prefer DoH where templates exist. Windows Update endpoints deliberately NOT blocked.";
            c.InfoPaths = "C:\\Windows\\System32\\drivers\\etc\\hosts (S-T-E-A-L-T-H block)\nHKLM\\...\\DNSClient -> EnableAutoDoH=1";
            c.Actions.Add(A("37.1", "APPLY telemetry hosts block", RiskLevel.Balanced, delegate(bool dry) { return HostsList.Apply(Kernel.Log); }, HostsList.BeginMark + " (0.0.0.0 sinks)"));
            c.Actions.Add(A("37.2", "REMOVE telemetry hosts block", RiskLevel.Safe, delegate(bool dry) { return HostsList.Remove(Kernel.Log); }, "removes the marked block"));
            c.Actions.Add(A("37.3", "Enable automatic DNS-over-HTTPS", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient", "EnableAutoDoH", 1, RegistryValueKind.DWord);
            }, "EnableAutoDoH=1 (known DoH templates used automatically)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 38 (NEW v8) — Secure deletion engine
            // ==================================================================================
            c = new Category();
            c.Num = "38"; c.Tag = "SECURE DELETE";
            c.Title = "Secure Deletion Engine (shred / wipe / sweeps)";
            c.Desc = "NEW: 3-pass shredder, free-space wipe, TEMP sweeps, shred-any-path dialog.";
            c.Info = "Shredding overwrites files 3x (random/0xFF/0x00) before delete. SSD caveat: wear-leveling means overwriting is best-effort on SSDs — TRIM handles the rest eventually. Free-space wipe uses cipher /w (slow).";
            c.InfoPaths = "3-pass overwrite + truncate + delete\nWindows TEMP / %TEMP%\\* sweep\ncipher /w:C:\\ (free space)";
            c.Actions.Add(A("38.1", "Shred Windows TEMP folders", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.ShredDir(Environment.GetEnvironmentVariable("TEMP"));
                Kernel.ShredDir(Environment.SystemDirectory + "\\..\\Temp");
                return r1;
            }, "%TEMP% + C:\\Windows\\Temp (in-use files skipped)"));
            c.Actions.Add(A("38.2", "Wipe free disk space (cipher /w)", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.Tool("cipher.exe", "/w:" + Environment.SystemDirectory.Substring(0, 2) + "\\", "free space wipe", 600000);
            }, "cipher /w (VERY slow; overwrites free space 3x)"));
            c.Actions.Add(A("38.3", "Shred Recycle Bin contents", RiskLevel.Balanced, delegate(bool dry)
            {
                return Kernel.ShredDir("C:\\$Recycle.Bin");
            }, "$Recycle.Bin (deleted-but-recoverable files)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 39 (NEW v8) — App & vendor telemetry
            // ==================================================================================
            c = new Category();
            c.Num = "39"; c.Tag = "APP TELEMETRY";
            c.Title = "Vendor Telemetry (VS, Office, .NET, NVIDIA)";
            c.Desc = "NEW: Visual Studio CEIP, Office telemetry, .NET CLI opt-out, NVIDIA services.";
            c.Info = "Vendor-run telemetry on your machine beyond Windows itself.";
            c.InfoPaths = "HKCU\\...\\VSCommon\\{15.0,16.0,17.0}\\SQM (opt-in 0)\nHKCU\\Software\\Policies\\Microsoft\\office\\16.0\\osm -> EnableLogging=0\nDOTNET_CLI_TELEMETRY_OPTOUT=1 (machine)\nServices: NvTelemetryContainer";
            c.Actions.Add(A("39.1", "Disable Visual Studio CEIP/SQM", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] VS SQM opt-out 15.0/16.0/17.0"); return OpResult.OK("preview"); }
                string[] vers = new string[] { "15.0", "16.0", "17.0" };
                int n = 0;
                foreach (string v in vers)
                {
                    OpResult r = Kernel.RegSet("HKCU\\Software\\Microsoft\\VSCommon\\" + v + "\\SQM", "OptIn", 0, RegistryValueKind.DWord);
                    if (r.Ok && !r.Skipped) n++;
                }
                return OpResult.OK(n + " VS CEIP versions opted out");
            }, "VSCommon\\{15,16,17}\\SQM OptIn=0"));
            c.Actions.Add(A("39.2", "Disable Office telemetry (OSM)", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKCU\\Software\\Policies\\Microsoft\\office\\16.0\\osm", "EnableLogging", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKCU\\Software\\Policies\\Microsoft\\office\\16.0\\osm", "EnableUpload", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\office\\16.0\\osm", "EnableLogging", 0, RegistryValueKind.DWord);
                return r1;
            }, "Office 16.0 osm EnableLogging/EnableUpload=0"));
            c.Actions.Add(A("39.3", ".NET CLI telemetry opt-out (machine)", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] setx /M DOTNET_CLI_TELEMETRY_OPTOUT 1"); return OpResult.OK("preview"); }
                ProcResult r = ProcRunner.Run("setx.exe", "/M DOTNET_CLI_TELEMETRY_OPTOUT 1", 15000);
                return r.Code == 0 ? OpResult.OK("DOTNET_CLI_TELEMETRY_OPTOUT=1 (machine)") : OpResult.FAIL("setx exit " + r.Code);
            }, "Machine env DOTNET_CLI_TELEMETRY_OPTOUT=1"));
            c.Actions.Add(A("39.4", "Stop+disable NVIDIA telemetry service", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.SvcStopDisable("NvTelemetryContainer");
            }, "NvTelemetryContainer (absent on many systems — reported)"));
            c.Actions.Add(A("39.5", "Clear npm & pip logs", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.DirWipe(LA() + "\\npm-cache\\_logs");
                Kernel.DirWipe(LA() + "\\pip\\log");
                return r1;
            }, "npm-cache\\_logs + pip\\log"));
            cats.Add(c);

            // ==================================================================================
            // CAT 40 (NEW v8) — System logs & caches (maintenance sweeps)
            // ==================================================================================
            c = new Category();
            c.Num = "40"; c.Tag = "SYS LOGS/CACHES";
            c.Title = "System Log & Cache Sweeps (CBS, WU, Panther, WDI, Spooler)";
            c.Desc = "NEW: CBS/DISM logs, Windows Update cache, setup logs, WDI, print spooler, misc DBs.";
            c.Info = "Secondary log sources investigators also collect: CBS/DISM, setup/upgrade logs, WDI traces, print job artifacts, Sticky Notes DB, GameDVR captures, speech trained data, targeted-content ad cache.";
            c.InfoPaths = "C:\\Windows\\Logs\\CBS\nC:\\Windows\\SoftwareDistribution\\Download\nC:\\Windows\\Panther\nC:\\Windows\\System32\\WDI\\LogFiles\nC:\\Windows\\System32\\SPOOLER\\PRINTERS\nStickyNotes plum.sqlite, Videos\\Captures, InputPersonalization\\TrainedDataStore, ContentDeliveryManager\\TargetedContentCache";
            c.Actions.Add(A("40.1", "Clear CBS & DISM logs", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe("C:\\Windows\\Logs\\CBS"); }, "CBS logs"));
            c.Actions.Add(A("40.2", "Clear Windows Update download cache", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop wuauserv, clear SoftwareDistribution\\Download, start"); return OpResult.OK("preview"); }
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name wuauserv -Force -ErrorAction SilentlyContinue\"", 30000);
                OpResult r = Kernel.DirWipe("C:\\Windows\\SoftwareDistribution\\Download");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name wuauserv -ErrorAction SilentlyContinue\"", 30000);
                return r;
            }, "SoftwareDistribution\\Download"));
            c.Actions.Add(A("40.3", "Clear setup/Panther logs", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe("C:\\Windows\\Panther"); }, "Panther setup logs (contains install history)"));
            c.Actions.Add(A("40.4", "Clear WDI diagnostic traces", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe("C:\\Windows\\System32\\WDI\\LogFiles"); }, "WDI LogFiles (etl traces)"));
            c.Actions.Add(A("40.5", "Purge print spooler artifacts", RiskLevel.Safe, delegate(bool dry)
            {
                if (Kernel.DryRun) { Kernel.Log("[DRY] stop spooler, clear SPOOLER\\PRINTERS, start"); return OpResult.OK("preview"); }
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue\"", 30000);
                OpResult r = Kernel.DirWipe("C:\\Windows\\System32\\SPOOLER\\PRINTERS");
                ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Service -Name Spooler -ErrorAction SilentlyContinue\"", 30000);
                return r;
            }, "Print job SPL/SHD files"));
            c.Actions.Add(A("40.6", "Delete Sticky Notes database", RiskLevel.Balanced, true, false, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem \"$env:LOCALAPPDATA\\Packages\" -Filter 'Microsoft.MicrosoftStickyNotes*' -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item \"$($_.FullName)\\LocalState\\plum.sqlite*\" -Force -ErrorAction SilentlyContinue }; 'done'", "StickyNotes DB", 30000);
            }, "plum.sqlite (YOUR NOTES — backup first)"));
            c.Actions.Add(A("40.7", "Delete GameDVR captures", RiskLevel.Safe, delegate(bool dry)
            {
                string vids = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                return Kernel.DirWipe(vids + "\\Captures");
            }, "Videos\\Captures"));
            c.Actions.Add(A("40.8", "Clear speech/ink trained data", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.DirWipe(LA() + "\\Microsoft\\InputPersonalization\\TrainedDataStore");
                Kernel.DirWipe(LA() + "\\Microsoft\\InputPersonalization\\StoredInkPictures");
                return r1;
            }, "InputPersonalization TrainedDataStore + StoredInkPictures"));
            c.Actions.Add(A("40.9", "Clear targeted-content ad cache", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem \"$env:LOCALAPPDATA\\Packages\" -Filter 'Microsoft.Windows.ContentDeliveryManager*' -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item \"$($_.FullName)\\LocalState\\TargetedContentCache\\*\" -Recurse -Force -ErrorAction SilentlyContinue }; 'done'", "TargetedContentCache", 30000);
            }, "Spotlight/targeted ads cache"));
            c.Actions.Add(A("40.10", "Clear SystemPowerReports", RiskLevel.Safe, delegate(bool dry) { return Kernel.DirWipe("C:\\Windows\\System32\\SystemPowerReports"); }, "sleep/usage timeline ETLs"));
            cats.Add(c);

            // ==================================================================================
            // CAT 41 (NEW v8) — Browser privacy (DESTRUCTIVE)
            // ==================================================================================
            c = new Category();
            c.Num = "41"; c.Tag = "BROWSER PRIVACY";
            c.Title = "Browser History & Cookies (DESTRUCTIVE)";
            c.Desc = "NEW: Chromium history/cookies/sessions + Firefox places — all profiles. DESTRUCTIVE.";
            c.Info = "Deletes browsing history, cookies and session data across ALL profiles. You WILL be logged out everywhere. Only in Paranoid master or manual click. Close browsers first.";
            c.InfoPaths = "Chromium: User Data\\*\\{History,Cookies,Top Sites,Session*}\nFirefox: places.sqlite, cookies.sqlite, formhistory.sqlite";
            c.Actions.Add(A("41.1", "Erase Chromium history/cookies (all profiles)", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.PSCheck("$browsers=@('Google\\Chrome','BraveSoftware\\Brave-Browser','Microsoft\\Edge'); foreach($b in $browsers){ $root=\"$env:LOCALAPPDATA\\$b\\User Data\"; if(Test-Path $root){ Get-ChildItem $root -Directory | Where-Object { $_.Name -eq 'Default' -or $_.Name -like 'Profile *' } | ForEach-Object { Remove-Item \"$($_.FullName)\\History*\",\"$($_.FullName)\\Cookies*\",\"$($_.FullName)\\Top Sites*\" -Force -ErrorAction SilentlyContinue; Remove-Item \"$($_.FullName)\\Sessions\\*\" -Recurse -Force -ErrorAction SilentlyContinue } } }; 'done'", "chromium history", 60000);
            }, "History/Cookies/Sessions for Chrome+Brave+Edge (LOGOUTS happen)"));
            c.Actions.Add(A("41.2", "Erase Firefox history/cookies", RiskLevel.Paranoid, true, false, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem \"$env:LOCALAPPDATA\\Mozilla\\Firefox\\Profiles\" -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item \"$($_.FullName)\\places.sqlite*\",\"$($_.FullName)\\cookies.sqlite*\",\"$($_.FullName)\\formhistory.sqlite\" -Force -ErrorAction SilentlyContinue }; 'done'", "firefox places", 60000);
            }, "places/cookies/formhistory (LOGOUTS happen)"));
            cats.Add(c);

            // ==================================================================================
            // CAT 42 (NEW v8) — Taskbar & shell privacy
            // ==================================================================================
            c = new Category();
            c.Num = "42"; c.Tag = "TASKBAR/SHELL";
            c.Title = "Taskbar & Shell Surface Reduction";
            c.Desc = "NEW: search highlights, taskbar chat button, Meet Now, third-party browser probes.";
            c.Info = "UCPD note: per-user Taskbar* values may be reset by Windows on Win11 22H2+ (UCPD driver); the Dsh policy in Cat 36 is the authoritative widget kill. These complement it.";
            c.InfoPaths = "HKCU\\...\\SearchSettings -> IsDynamicSearchBoxEnabled=0\nHKCU\\...\\Explorer\\Advanced -> TaskbarMn=0 (chat), ShowCopilotButton=0\nHKCU\\...\\Explorer -> DisableSearchBoxSuggestions=1";
            c.Actions.Add(A("42.1", "Disable search highlights/bing", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings", "IsDynamicSearchBoxEnabled", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_SearchFiles", 0, RegistryValueKind.DWord);
                return r1;
            }, "IsDynamicSearchBoxEnabled=0"));
            c.Actions.Add(A("42.2", "Hide taskbar chat & copilot buttons", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarMn", 0, RegistryValueKind.DWord);
                Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowCopilotButton", 0, RegistryValueKind.DWord);
                return r1;
            }, "TaskbarMn=0, ShowCopilotButton=0 (UCPD may re-enable; policy preferred)"));
            c.Actions.Add(A("42.3", "Disable search-box web suggestions", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Policies\\Microsoft\\Windows\\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
            }, "DisableSearchBoxSuggestions=1 (policy — UCPD-safe)"));
            c.Actions.Add(A("42.4", "Kill Meet Now (Win10)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "HideSCAMeetNow", 1, RegistryValueKind.DWord);
            }, "HideSCAMeetNow=1"));
            cats.Add(c);

            // ==================================================================================
            // CAT 43 (NEW v8) — Developer traces
            // ==================================================================================
            c = new Category();
            c.Num = "43"; c.Tag = "DEV TRACES";
            c.Title = "Developer Shell Traces (bash/python/node/WSL)";
            c.Desc = "NEW: .bash_history, .python_history, .node_repl_history, WSL histories, npm logs.";
            c.Info = "Shell REPL histories in the user profile and inside WSL distros record every command typed.";
            c.InfoPaths = "%USERPROFILE%\\.{bash,python,node_repl,mysql}_history\n%LOCALAPPDATA%\\Packages\\*\\LocalState\\rootfs\\home\\*\\.*history (WSL1)\nnpm-cache\\_logs";
            c.Actions.Add(A("43.1", "Shred profile shell histories", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("$u=$env:USERPROFILE; @('.bash_history','.python_history','.node_repl_history','.mysql_history','.sqlite_history','.sh_history') | ForEach-Object { $p=Join-Path $u $_; if(Test-Path $p){ Clear-Content $p -Force -ErrorAction SilentlyContinue } }; 'cleared'", "shell histories", 30000);
            }, "profile .*history files (emptied)"));
            c.Actions.Add(A("43.2", "Clear WSL distro histories", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.PSCheck("Get-ChildItem \"$env:LOCALAPPDATA\\Packages\" -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path \"$($_.FullName)\\LocalState\\rootfs\\home\" } | ForEach-Object { Get-ChildItem \"$($_.FullName)\\LocalState\\rootfs\\home\" -Directory -ErrorAction SilentlyContinue | ForEach-Object { Get-ChildItem $_.FullName -Filter '.*history' -Force -ErrorAction SilentlyContinue | Clear-Content -Force -ErrorAction SilentlyContinue } }; 'done'", "wsl histories", 30000);
            }, "WSL1 home .*history files"));
            cats.Add(c);

            // ==================================================================================
            // CAT 44 (NEW v8) — Legacy telemetry consent
            // ==================================================================================
            c = new Category();
            c.Num = "44"; c.Tag = "LEGACY TELEMETRY";
            c.Title = "Legacy WER/CEIP Consent Registry Locks";
            c.Desc = "NEW: hard registry locks on WER reporting and CEIP consent (defense-in-depth).";
            c.Info = "Registry-level consent locks complementing service/task kills: Windows Error Reporting, CEIP/SQM, ErrorReporting policies.";
            c.InfoPaths = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting -> Disabled=1\nHKLM\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows -> CEIPEnable=0\nHKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection -> AllowTelemetry=0";
            c.Actions.Add(A("44.1", "Lock WER reporting off", RiskLevel.Safe, delegate(bool dry)
            {
                OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting", "Disabled", 1, RegistryValueKind.DWord);
                Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting", "DoReport", 0, RegistryValueKind.DWord);
                return r1;
            }, "WER Disabled=1 + DoReport=0"));
            c.Actions.Add(A("44.2", "Lock CEIP/SQM consent off", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows", "CEIPEnable", 0, RegistryValueKind.DWord);
            }, "CEIPEnable=0"));
            c.Actions.Add(A("44.3", "Lock DataCollection policy (legacy path)", RiskLevel.Safe, delegate(bool dry)
            {
                return Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord);
            }, "Legacy DataCollection AllowTelemetry=0"));
            cats.Add(c);

            return cats;
        }

        // ======================================================================================
        // IDE (Cursor/VS Code) storage.json reset + telemetry settings
        // ======================================================================================

        private static OpResult ResetIdeKey(string keyName)
        {
            if (Kernel.DryRun) { Kernel.Log("[DRY] IDE storage.json reset " + keyName); return OpResult.OK("preview"); }
            string[] paths = new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Cursor\\User\\globalStorage\\storage.json",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Code\\User\\globalStorage\\storage.json"
            };
            string newId = Guid.NewGuid().ToString();
            int touched = 0;
            foreach (string path in paths)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    string content = File.ReadAllText(path);
                    int idx = content.IndexOf("\"" + keyName + "\"");
                    if (idx >= 0)
                    {
                        int colonIdx = content.IndexOf(':', idx);
                        int quoteStart = content.IndexOf('"', colonIdx);
                        int quoteEnd = content.IndexOf('"', quoteStart + 1);
                        if (quoteStart >= 0 && quoteEnd > quoteStart)
                        {
                            // backup original
                            try
                            {
                                if (Kernel.Backup != null && Kernel.BackupOn)
                                {
                                    string bdir = Kernel.Backup.SessionDir;
                                    if (bdir != null)
                                    {
                                        string bfile = Path.Combine(bdir, "quarantine", "storage.json." + Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))) + "." + Path.GetFileName(path) + ".bak");
                                        Directory.CreateDirectory(Path.GetDirectoryName(bfile));
                                        if (!File.Exists(bfile)) File.Copy(path, bfile);
                                        Kernel.Backup.Manifest.Add("QUAR\t" + path + "\t" + bfile);
                                    }
                                }
                            }
                            catch { }
                            content = content.Substring(0, quoteStart + 1) + newId + content.Substring(quoteEnd);
                            File.WriteAllText(path, content);
                            touched++;
                        }
                    }
                }
                catch (Exception ex) { Kernel.Log("[IDE] " + path + " : " + ex.Message); }
            }
            if (touched == 0) return OpResult.SKIP(keyName + " not found in any IDE storage.json");
            return OpResult.OK(keyName + " reset in " + touched + " file(s) -> " + newId);
        }

        private static OpResult IdeTelemetryOff()
        {
            if (Kernel.DryRun) { Kernel.Log("[DRY] IDE settings.json telemetry off"); return OpResult.OK("preview"); }
            string[] paths = new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Cursor\\User\\settings.json",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Code\\User\\settings.json"
            };
            int touched = 0;
            foreach (string path in paths)
            {
                try
                {
                    if (!File.Exists(path)) { Directory.CreateDirectory(Path.GetDirectoryName(path)); }
                    string content = File.Exists(path) ? File.ReadAllText(path) : "{}";
                    string stripped = StripSetting(content, "telemetry.telemetryLevel");
                    stripped = StripSetting(stripped, "telemetry.enableTelemetry");
                    stripped = StripSetting(stripped, "telemetry.enableCrashReporter");
                    stripped = StripSetting(stripped, "redhat.telemetry.enabled");
                    string trimmed = stripped.TrimEnd();
                    if (trimmed.EndsWith("}"))
                    {
                        string body = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
                        string add = "\"telemetry.telemetryLevel\": \"off\", \"telemetry.enableTelemetry\": false, \"telemetry.enableCrashReporter\": false, \"redhat.telemetry.enabled\": false";
                        string merged = body.Length == 0 ? "{\n  " + add + "\n}" : body + ",\n  " + add + "\n}";
                        try
                        {
                            if (Kernel.Backup != null && Kernel.BackupOn && Kernel.Backup.SessionDir != null && File.Exists(path))
                            {
                                string bfile = Path.Combine(Kernel.Backup.SessionDir, "quarantine", "settings-" + Path.GetFileName(Path.GetDirectoryName(path)) + ".json.bak");
                                if (!File.Exists(bfile)) File.Copy(path, bfile);
                            }
                        }
                        catch { }
                        File.WriteAllText(path, merged);
                        touched++;
                    }
                }
                catch (Exception ex) { Kernel.Log("[IDE] " + path + " : " + ex.Message); }
            }
            return touched > 0 ? OpResult.OK("telemetry settings forced off in " + touched + " IDE settings.json") : OpResult.SKIP("no IDE settings found");
        }

        /// <summary>Remove a top-level "key": ... pair from a flat settings.json body (regex-free).</summary>
        private static string StripSetting(string json, string key)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx < 0) return json;
            int lineStart = json.LastIndexOf('\n', idx);
            if (lineStart < 0) lineStart = 0;
            int lineEnd = json.IndexOf('\n', idx + key.Length);
            if (lineEnd < 0) lineEnd = json.Length;
            return json.Substring(0, lineStart) + json.Substring(lineEnd);
        }

        // ======================================================================================
        // Browser cache cleaner (all profiles)
        // ======================================================================================

        private static OpResult BrowserClean(string relRoot)
        {
            if (Kernel.DryRun) { Kernel.Log("[DRY] browser cache clean " + relRoot); return OpResult.OK("preview"); }
            string root = LA() + "\\" + relRoot + "\\User Data";
            if (!Directory.Exists(root)) return OpResult.SKIP("not installed: " + relRoot);
            string[] cacheDirs = new string[] { "Cache", "Code Cache", "GPUCache", "ShaderCache", "DawnCache" };
            int cleaned = 0, failed = 0;
            try
            {
                string[] profiles = Directory.GetDirectories(root);
                foreach (string prof in profiles)
                {
                    string name = Path.GetFileName(prof);
                    bool isProfile = name == "Default" || name.StartsWith("Profile ") || name == "Guest Profile";
                    if (!isProfile) continue;
                    foreach (string cd in cacheDirs)
                    {
                        string target = Path.Combine(prof, cd);
                        if (!Directory.Exists(target)) continue;
                        OpResult r = Kernel.DirWipe(target);
                        if (r.Ok && !r.Skipped) cleaned++; else if (!r.Ok) failed++;
                    }
                }
            }
            catch (Exception ex) { return OpResult.FAIL(relRoot + " : " + ex.Message); }
            if (cleaned == 0 && failed == 0) return OpResult.SKIP("no caches found (" + relRoot + ")");
            return OpResult.OK(relRoot + ": " + cleaned + " cache dirs cleaned" + (failed > 0 ? ", " + failed + " FAILED" : ""));
        }
    }

    // ======================================================================================
    // APPLICATION ENTRY — GUI by default, headless CLI via switches:
    //   S-T-E-A-L-T-H.exe /sweep /profile:Balanced /dry   (Safe|Balanced|Paranoid)
    //   S-T-E-A-L-T-H.exe /audit /out:"C:\report.html"
    //   S-T-E-A-L-T-H.exe /restore /dir:"C:\ProgramData\STEALTH\backups\session-..."
    // Switches are matched with EndsWith so they work verbatim (cmd / Task Scheduler)
    // AND when POSIX shells (Git Bash) mangle leading-slash args into file paths.
    // ======================================================================================

    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool sweep = false, audit = false, restore = false, dry = false;
            string profile = "Balanced", outPath = null, restoreDir = null;
            if (args != null)
            {
                foreach (string raw in args)
                {
                    string s = raw.ToLowerInvariant();
                    if (s.EndsWith("sweep")) sweep = true;
                    else if (s.EndsWith("dry")) dry = true;
                    else if (s.EndsWith("audit")) audit = true;
                    else if (s.EndsWith("restore")) restore = true;
                    else if (s.StartsWith("/profile:")) profile = s.Substring(9);
                    else if (s.StartsWith("--profile:")) profile = s.Substring(10);
                    else if (s.StartsWith("/out:")) outPath = raw.Substring(5).Trim('"');
                    else if (s.StartsWith("--out:")) outPath = raw.Substring(6).Trim('"');
                    else if (s.StartsWith("/dir:")) restoreDir = raw.Substring(5).Trim('"');
                    else if (s.StartsWith("--dir:")) restoreDir = raw.Substring(6).Trim('"');
                }
            }
            if (sweep)
            {
                CliSweep(profile, dry);
                return;
            }
            if (audit)
            {
                string path = outPath;
                if (string.IsNullOrEmpty(path))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\STEALTH-Audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".html";
                Console.WriteLine("[AUDIT] generating " + path);
                string res = Auditor.Generate(path);
                Console.WriteLine("[AUDIT] " + res);
                return;
            }
            if (restore)
            {
                if (string.IsNullOrEmpty(restoreDir))
                {
                    Console.WriteLine("[RESTORE] usage: /restore /dir:\"<session folder>\"");
                    return;
                }
                BackupManager.Restore(restoreDir, delegate(string m) { Console.WriteLine(m); });
                return;
            }
            try
            {
                Application app = new Application();
                MainWindow window = new MainWindow();
                app.Run(window);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fatal Error: " + ex.Message, "S-T-E-A-L-T-H v8.0", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void CliSweep(string profile, bool dry)
        {
            Console.WriteLine("=== S-T-E-A-L-T-H v8.0 headless sweep (" + profile + (dry ? ", DRY-RUN" : "") + ") ===");
            if (!Kernel.IsAdmin()) Console.WriteLine("[WARN] not elevated — many operations will fail honestly below.");
            RiskLevel target = RiskLevel.Balanced;
            if (profile.Equals("Safe", StringComparison.OrdinalIgnoreCase)) target = RiskLevel.Safe;
            if (profile.Equals("Paranoid", StringComparison.OrdinalIgnoreCase)) target = RiskLevel.Paranoid;
            Kernel.DryRun = dry;
            Kernel.Log = delegate(string m) { Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + m); };
            List<Category> cats = ActionRegistry.Build();
            int ok = 0, fail = 0, skip = 0;
            if (!Kernel.DryRun)
            {
                BackupManager bm = new BackupManager();
                bm.Enabled = Kernel.BackupOn; bm.Quarantine = Kernel.QuarantineOn;
                bm.StartSession(); Kernel.Backup = bm;
                foreach (Category cat in cats)
                {
                    Console.WriteLine("--- [" + cat.Num + "] " + cat.Title);
                    foreach (StealthAction a in cat.Actions)
                    {
                        if (a.Risk > target) { skip++; continue; }
                        if (a.Destructive && target != RiskLevel.Paranoid) { skip++; continue; }
                        OpResult r = a.Run(Kernel.DryRun);
                        if (r.Ok && !r.Skipped) { ok++; Console.WriteLine("   [OK] " + a.Num + " " + r.Detail); }
                        else if (r.Skipped) { skip++; Console.WriteLine("   [SKIP] " + a.Num + " " + r.Detail); }
                        else { fail++; Console.WriteLine("   [FAIL] " + a.Num + " " + r.Detail); }
                    }
                }
                bm.FinishSession();
            }
            else
            {
                foreach (Category cat in cats)
                    foreach (StealthAction a in cat.Actions)
                        if (a.Risk <= target && (!a.Destructive || target == RiskLevel.Paranoid))
                        {
                            OpResult r = a.Run(true);
                            Console.WriteLine("[DRY] " + a.Num + " -> " + r.Detail);
                        }
            }
            Console.WriteLine("=== SWEEP COMPLETE: " + ok + " OK, " + fail + " FAILED, " + skip + " SKIPPED ===");
        }
    }

    // ======================================================================================
    // MAIN WINDOW — WPF shell
    // ======================================================================================

    public class MainWindow : Window
    {
        private TextBox txtLogs;
        private ScrollViewer scroller;
        private ProgressBar prgBar;
        private TextBlock txtHost, txtNet, txtTelem, txtRam;
        private TextBlock txtOk;
        private Border infoModal, pcNameModal, restoreModal, scheduleModal, shredModal;
        private TextBlock txtModalTitle, txtModalDesc, txtModalPaths;
        private TextBox txtNewPcName, txtShredPath, txtSchedTime;
        private TextBlock txtSchedStatus, txtRestoreInfo;
        private ListBox lstSessions;
        private ComboBox cmbProfile;
        private TextBox txtSearch;
        private Button btnDry, btnBackup, btnQuarantine, btnMaster;
        private List<KeyValuePair<string, FrameworkElement[]>> cardIndex = new List<KeyValuePair<string, FrameworkElement[]>>();
        private DispatcherTimer hudTimer;
        private List<Category> allCats;
        private bool busy;
        private int okCount, failCount, skipCount;

        public MainWindow()
        {
            this.Title = "S-T-E-A-L-T-H v8.0 — Absolute Forensic Annihilation Suite";
            this.Width = 1280; this.Height = 1000;
            this.MinWidth = 1040; this.MinHeight = 700;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.CanResize;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            this.SnapsToDevicePixels = true;
            this.UseLayoutRounding = true;
            allCats = ActionRegistry.Build();
            BuildUI();
            WireShell();
            StartHud();
            if (!Kernel.IsAdmin()) AppendLog("[WARN] NOT RUNNING AS ADMINISTRATOR — protected operations will FAIL and be reported as such.");
            else AppendLog("[OK] elevated — full access.");
            AppendLog("S-T-E-A-L-T-H v8.0 online. 46 vectors | " + CountActions() + " micro-actions | Dry-Run, Backup, Quarantine, Audit, Scheduler ready.");
            AppendLog("Pick a profile (Safe/Balanced/Paranoid) and hit the master button, or click any granular action.");
        }

        private int CountActions()
        {
            int n = 0;
            foreach (Category c in allCats) n += c.Actions.Count;
            return n;
        }

        private static string EscXml(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        // ------------------------------------------------------------------ XAML construction

        private string MicroName(Category c, StealthAction a)
        {
            return ("BtnMicro_" + c.Num + "_" + a.Num).Replace(".", "_");
        }

        private string MicroBtn(string name, string label, bool gold)
        {
            string style = gold ? "GoldMicroBtn" : "MicroBtn";
            return "<Button x:Name=\"" + name + "\" Style=\"{StaticResource " + style + "}\" Content=\"" + EscXml(label) + "\"/>";
        }

        private string MakeCard(Category c)
        {
            bool gold = c.Gold;
            string cardStyle = gold ? "GoldRowCard" : "RowCard";
            string tagBg = gold ? "#241908" : "#0B213F";
            string tagBorder = gold ? "#F59E0B" : "#0284C7";
            string tagFg = gold ? "#FBBF24" : "#38BDF8";
            string titleFg = gold ? "#FBBF24" : "#F8FAFC";
            string masterStyle = gold ? "CrownBtn" : "ActionBtn";
            string prefix = gold ? "\uD83D\uDC51 " : "";
            string risk = c.Risk().ToString();
            string des = c.Destructive() ? "  \u26A0 DESTRUCTIVE CAPABLE" : "";
            string micros = "";
            foreach (StealthAction a in c.Actions)
            {
                string icon = a.Destructive ? "\u2622" : "\u26A1";
                micros += MicroBtn(MicroName(c, a), icon + " " + a.Num + " " + a.Title, gold);
            }
            return "<Border x:Name=\"Card_" + c.Num + "\" Style=\"{StaticResource " + cardStyle + "}\">" +
                "<StackPanel>" +
                "<Grid Margin=\"0,0,0,8\">" +
                  "<Grid.ColumnDefinitions><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"Auto\"/><ColumnDefinition Width=\"Auto\"/></Grid.ColumnDefinitions>" +
                  "<StackPanel Grid.Column=\"0\" VerticalAlignment=\"Center\">" +
                    "<StackPanel Orientation=\"Horizontal\">" +
                      "<Border Background=\"" + tagBg + "\" BorderBrush=\"" + tagBorder + "\" BorderThickness=\"1\" CornerRadius=\"4\" Padding=\"5,1\" Margin=\"0,0,8,0\">" +
                        "<TextBlock Text=\"[" + EscXml(c.Tag) + "]\" FontSize=\"9.5\" FontWeight=\"Bold\" Foreground=\"" + tagFg + "\"/></Border>" +
                      "<TextBlock Text=\"" + prefix + EscXml(c.Num) + ". " + EscXml(c.Title) + "\" FontSize=\"13\" FontWeight=\"Bold\" Foreground=\"" + titleFg + "\"/>" +
                      "<TextBlock Text=\"  [" + risk + des + "]\" FontSize=\"9.5\" Foreground=\"#64748B\" VerticalAlignment=\"Center\"/>" +
                    "</StackPanel>" +
                    "<TextBlock Text=\"" + EscXml(c.Desc) + "\" FontSize=\"11\" Foreground=\"#94A3B8\" Margin=\"0,3,0,0\" TextWrapping=\"Wrap\"/>" +
                  "</StackPanel>" +
                  "<Button x:Name=\"BtnInfo_" + c.Num + "\" Grid.Column=\"1\" Style=\"{StaticResource InfoBtn}\" Margin=\"8,0,8,0\" VerticalAlignment=\"Center\"/>" +
                  "<Button x:Name=\"BtnCat_" + c.Num + "\" Grid.Column=\"2\" Style=\"{StaticResource " + masterStyle + "}\" Content=\"" + (gold ? "\uD83D\uDC51 Run All" : "Run All") + "\" VerticalAlignment=\"Center\"/>" +
                "</Grid>" +
                "<WrapPanel Margin=\"0,2,0,0\">" + micros + "</WrapPanel>" +
                "</StackPanel></Border>";
        }

        private void BuildUI()
        {
            List<Category> cats = allCats;
            StringBuilder cards = new StringBuilder();
            foreach (Category c in cats) cards.Append(MakeCard(c));

            string xaml = @"
<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
      SnapsToDevicePixels=""True"" UseLayoutRounding=""True"">
    <Grid.Resources>
        <Style TargetType=""ScrollBar"">
            <Setter Property=""Width"" Value=""6""/>
            <Setter Property=""Background"" Value=""Transparent""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""ScrollBar"">
                    <Grid Background=""Transparent"">
                        <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                            <Track.Thumb><Thumb><Thumb.Template><ControlTemplate>
                                <Border Background=""#0284C7"" CornerRadius=""3"" Margin=""1,0""/>
                            </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                        </Track>
                    </Grid>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Border"" x:Key=""RowCard"">
            <Setter Property=""Background"" Value=""#090F1D""/>
            <Setter Property=""BorderBrush"" Value=""#1E293B""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""CornerRadius"" Value=""10""/>
            <Setter Property=""Padding"" Value=""14,10""/>
            <Setter Property=""Margin"" Value=""0,0,0,10""/>
        </Style>
        <Style TargetType=""Border"" x:Key=""GoldRowCard"" BasedOn=""{StaticResource RowCard}"">
            <Setter Property=""Background"" Value=""#0F1626""/>
            <Setter Property=""BorderBrush"" Value=""#F59E0B""/>
        </Style>
        <Style TargetType=""Button"" x:Key=""ActionBtn"">
            <Setter Property=""Background"" Value=""#0D1F38""/>
            <Setter Property=""Foreground"" Value=""#38BDF8""/>
            <Setter Property=""FontSize"" Value=""11.5""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""BorderBrush"" Value=""#0284C7""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Padding"" Value=""12,6""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""7"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#0284C7""/>
                            <Setter Property=""Foreground"" Value=""#030712""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Button"" x:Key=""CrownBtn"" BasedOn=""{StaticResource ActionBtn}"">
            <Setter Property=""Background"" Value=""#221A0C""/>
            <Setter Property=""Foreground"" Value=""#FBBF24""/>
            <Setter Property=""BorderBrush"" Value=""#F59E0B""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""7"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#F59E0B""/>
                            <Setter Property=""Foreground"" Value=""#030712""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Button"" x:Key=""MicroBtn"">
            <Setter Property=""Background"" Value=""#060B14""/>
            <Setter Property=""Foreground"" Value=""#94A3B8""/>
            <Setter Property=""FontSize"" Value=""10.5""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""BorderBrush"" Value=""#1E293B""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Padding"" Value=""8,4""/>
            <Setter Property=""Margin"" Value=""0,0,6,4""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""5"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#1E293B""/>
                            <Setter TargetName=""Brd"" Property=""BorderBrush"" Value=""#38BDF8""/>
                            <Setter Property=""Foreground"" Value=""#38BDF8""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Button"" x:Key=""GoldMicroBtn"" BasedOn=""{StaticResource MicroBtn}"">
            <Setter Property=""BorderBrush"" Value=""#3A2C10""/>
            <Setter Property=""Foreground"" Value=""#FCD34D""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""5"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#221A0C""/>
                            <Setter TargetName=""Brd"" Property=""BorderBrush"" Value=""#F59E0B""/>
                            <Setter Property=""Foreground"" Value=""#FBBF24""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Button"" x:Key=""InfoBtn"">
            <Setter Property=""Background"" Value=""#0D192E""/>
            <Setter Property=""Foreground"" Value=""#00F2FE""/>
            <Setter Property=""FontSize"" Value=""10.5""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""BorderBrush"" Value=""#1E3A8A""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Padding"" Value=""8,5""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""6"">
                        <TextBlock Text=""ℹ SPECS"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#00F2FE""/>
                            <Setter Property=""Foreground"" Value=""#030712""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""Button"" x:Key=""ToolBtn"">
            <Setter Property=""Background"" Value=""#0A1424""/>
            <Setter Property=""Foreground"" Value=""#7DD3FC""/>
            <Setter Property=""FontSize"" Value=""10.5""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""BorderBrush"" Value=""#155E75""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Padding"" Value=""10,6""/>
            <Setter Property=""Margin"" Value=""0,0,6,0""/>
            <Setter Property=""Template"">
                <Setter.Value><ControlTemplate TargetType=""Button"">
                    <Border x:Name=""Brd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""6"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Brd"" Property=""Background"" Value=""#155E75""/>
                            <Setter Property=""Foreground"" Value=""#F8FAFC""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
            </Setter>
        </Style>
        <Style TargetType=""ComboBox"">
            <Setter Property=""Background"" Value=""#0A1424""/>
            <Setter Property=""Foreground"" Value=""#F8FAFC""/>
            <Setter Property=""FontSize"" Value=""11""/>
            <Setter Property=""Padding"" Value=""8,5""/>
        </Style>
        <Style TargetType=""TextBox"">
            <Setter Property=""Background"" Value=""#020409""/>
            <Setter Property=""Foreground"" Value=""#00F2FE""/>
            <Setter Property=""BorderBrush"" Value=""#1E293B""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""CaretBrush"" Value=""#00F2FE""/>
        </Style>
    </Grid.Resources>

    <Border Background=""#040711"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""14"" Margin=""8"">
        <Grid Margin=""16"">
            <Grid.RowDefinitions>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""*""/>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""150""/>
            </Grid.RowDefinitions>

            <!-- Titlebar -->
            <Grid Grid.Row=""0"" x:Name=""TitleBar"" Background=""Transparent"" Margin=""0,0,0,10"">
                <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <Border Background=""#0A192F"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""8"" Padding=""7,4"" Margin=""0,0,12,0"">
                        <TextBlock Text=""S 🛡 T"" FontSize=""12"" FontWeight=""ExtraBold"" Foreground=""#00F2FE""/>
                    </Border>
                    <TextBlock Text=""S - T - E - A - L - T - H"" FontSize=""21"" FontWeight=""ExtraBold"" Foreground=""#00F2FE""/>
                    <Border Background=""#065F46"" CornerRadius=""6"" Padding=""8,3"" Margin=""12,0,0,0"">
                        <TextBlock Text=""v8.0 ABSOLUTE"" FontSize=""10.5"" FontWeight=""Bold"" Foreground=""#34D399""/>
                    </Border>
                    <TextBlock Text=""// 46-VECTOR ENGINE · DRY-RUN · BACKUP · QUARANTINE · AUDIT · SCHEDULER"" FontSize=""10.5"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""10,0,0,0""/>
                </StackPanel>
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                    <Button x:Name=""BtnMin"" Content=""─"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#94A3B8"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand"" Margin=""0,0,4,0""/>
                    <Button x:Name=""BtnMax"" Content=""🗖"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#94A3B8"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand"" Margin=""0,0,4,0""/>
                    <Button x:Name=""BtnClose"" Content=""✕"" Width=""32"" Height=""32"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                </StackPanel>
            </Grid>

            <!-- HUD -->
            <Grid Grid.Row=""1"" Margin=""0,0,0,10"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <Border Grid.Column=""0"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,7"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● MACHINE IDENT"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtHost"" Text=""..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#F8FAFC"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""1"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,7"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● NET INTERFACE"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtNet"" Text=""..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#38BDF8"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""2"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,7"" Margin=""3"">
                    <StackPanel><TextBlock Text=""● TELEMETRY STATE"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/><TextBlock x:Name=""TxtTelem"" Text=""..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#34D399"" Margin=""0,2,0,0""/></StackPanel>
                </Border>
                <Border Grid.Column=""3"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""12,7"" Margin=""3"">
                    <StackPanel>
                        <TextBlock Text=""● RAM / RESULT COUNTERS"" FontSize=""9"" FontWeight=""Bold"" Foreground=""#64748B""/>
                        <TextBlock x:Name=""TxtRam"" Text=""..."" FontSize=""11.5"" FontWeight=""Bold"" Foreground=""#F59E0B"" Margin=""0,2,0,0""/>
                        <TextBlock x:Name=""TxtStats"" Text=""OK:0  FAIL:0  SKIP:0"" FontSize=""10.5"" FontWeight=""Bold"" Margin=""0,2,0,0""/>
                    </StackPanel>
                </Border>
            </Grid>

            <!-- Toolbar -->
            <Border Grid.Row=""2"" Background=""#08101E"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""10,8"" Margin=""0,0,0,10"">
                <StackPanel Orientation=""Horizontal"">
                    <TextBlock Text=""PROFILE:"" FontSize=""10.5"" FontWeight=""Bold"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""0,0,6,0""/>
                    <ComboBox x:Name=""CmbProfile"" Width=""130"" Height=""30"" SelectedIndex=""1"" Margin=""0,0,10,0"">
                        <ComboBoxItem Content=""🟢 Safe""/>
                        <ComboBoxItem Content=""🟡 Balanced""/>
                        <ComboBoxItem Content=""🔴 Paranoid (destructive)""/>
                    </ComboBox>
                    <Button x:Name=""BtnDry"" Style=""{StaticResource ToolBtn}"" Content=""◌ DRY-RUN: OFF"" Height=""30""/>
                    <Button x:Name=""BtnBackup"" Style=""{StaticResource ToolBtn}"" Content=""💾 BACKUP: ON"" Height=""30""/>
                    <Button x:Name=""BtnQuarantine"" Style=""{StaticResource ToolBtn}"" Content=""📦 QUARANTINE: ON"" Height=""30""/>
                    <TextBox x:Name=""TxtSearch"" Width=""210"" Height=""30"" VerticalContentAlignment=""Center"" Padding=""6,0"" Margin=""10,0,0,0"" FontSize=""11""/>
                    <TextBlock Text=""🔍 filter"" FontSize=""10"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""6,0,0,0""/>
                </StackPanel>
            </Border>

            <!-- Master hero -->
            <Border Grid.Row=""3"" Background=""#081736"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""12"" Padding=""14,10"" Margin=""0,0,0,10"">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                        <TextBlock Text=""⚡ 1-CLICK MASTER S-T-E-A-L-T-H PROTOCOL"" FontSize=""14.5"" FontWeight=""ExtraBold"" Foreground=""#F8FAFC""/>
                        <TextBlock Text=""Runs every vector filtered by profile. Safe=only-safe · Balanced=+medium (non-destructive) · Paranoid=everything incl. destructive. Backups+quarantine always recorded."" FontSize=""10.5"" Foreground=""#94A3B8"" TextWrapping=""Wrap""/>
                        <StackPanel Orientation=""Horizontal"" Margin=""0,6,0,0"">
                            <Button x:Name=""BtnAudit"" Style=""{StaticResource ToolBtn}"" Content=""📊 PRIVACY AUDIT (read-only)""/>
                            <Button x:Name=""BtnRestore"" Style=""{StaticResource ToolBtn}"" Content=""↩ RESTORE SESSION""/>
                            <Button x:Name=""BtnSchedule"" Style=""{StaticResource ToolBtn}"" Content=""⏰ SCHEDULER""/>
                            <Button x:Name=""BtnShred"" Style=""{StaticResource ToolBtn}"" Content=""🔥 SHRED PATH…""/>
                            <Button x:Name=""BtnPcName"" Style=""{StaticResource ToolBtn}"" Content=""👑 PC NAME SPOOF""/>
                        </StackPanel>
                    </StackPanel>
                    <Button x:Name=""BtnMasterRun"" Grid.Column=""1"" Width=""300"" Height=""46"" Cursor=""Hand"" Margin=""10,0,0,0"">
                        <Button.Template><ControlTemplate TargetType=""Button"">
                            <Border x:Name=""MBrd"" CornerRadius=""10"">
                                <Border.Background><LinearGradientBrush StartPoint=""0,0"" EndPoint=""1,1"">
                                    <GradientStop Color=""#00F2FE"" Offset=""0.0""/>
                                    <GradientStop Color=""#38BDF8"" Offset=""0.5""/>
                                    <GradientStop Color=""#2563EB"" Offset=""1.0""/>
                                </LinearGradientBrush></Border.Background>
                                <TextBlock x:Name=""MTxt"" Text=""⚡ INITIATE 46-VECTOR PROTOCOL ⚡"" Foreground=""#030712"" FontSize=""12"" FontWeight=""ExtraBold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" TextAlignment=""Center""/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property=""IsMouseOver"" Value=""True"">
                                    <Setter TargetName=""MBrd"" Property=""Opacity"" Value=""0.85""/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate></Button.Template>
                    </Button>
                </Grid>
            </Border>

            <!-- Cards -->
            <ScrollViewer Grid.Row=""4"" VerticalScrollBarVisibility=""Auto"" Margin=""0,0,4,8"">
                <StackPanel Margin=""0,0,6,0"">" + cards.ToString() + @"</StackPanel>
            </ScrollViewer>

            <!-- Progress -->
            <Grid Grid.Row=""5"" Margin=""0,0,0,6"">
                <ProgressBar x:Name=""PrgBar"" Height=""3"" Background=""#0A192F"" Foreground=""#00F2FE"" BorderThickness=""0""/>
            </Grid>

            <!-- Log -->
            <Border Grid.Row=""6"" Background=""#020408"" BorderBrush=""#0284C7"" BorderThickness=""1"" CornerRadius=""10"" Padding=""12"">
                <Grid>
                    <Grid.RowDefinitions><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>
                    <StackPanel Grid.Row=""0"" Orientation=""Horizontal"" Margin=""0,0,0,4"">
                        <TextBlock Text=""▶ LIVE FORENSIC STREAM — every line is a real result (OK/FAIL/SKIP), not a marketing claim"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#00F2FE""/>
                    </StackPanel>
                    <ScrollViewer Grid.Row=""1"" x:Name=""Scroller"" VerticalScrollBarVisibility=""Auto"">
                        <TextBox x:Name=""TxtLogs"" Background=""Transparent"" Foreground=""#34D399"" FontFamily=""Consolas"" FontSize=""11"" BorderThickness=""0"" IsReadOnly=""True"" TextWrapping=""Wrap""/>
                    </ScrollViewer>
                </Grid>
            </Border>

            <!-- INFO MODAL -->
            <Border x:Name=""InfoModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
                <Border Background=""#070F1E"" BorderBrush=""#00F2FE"" BorderThickness=""1.5"" CornerRadius=""14"" Width=""680"" MaxHeight=""560"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
                    <StackPanel>
                        <Grid Margin=""0,0,0,14"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBlock x:Name=""TxtModalTitle"" Grid.Column=""0"" Text=""Feature Specs"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#00F2FE"" TextWrapping=""Wrap""/>
                            <Button x:Name=""BtnCloseModal"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <ScrollViewer MaxHeight=""420"" VerticalScrollBarVisibility=""Auto"">
                            <StackPanel>
                                <TextBlock Text=""TECHNICAL DESCRIPTION:"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#64748B""/>
                                <TextBlock x:Name=""TxtModalDesc"" Text=""Details..."" FontSize=""12"" Foreground=""#E2E8F0"" TextWrapping=""Wrap"" Margin=""0,4,0,12""/>
                                <TextBlock Text=""AFFECTED PATHS / REGISTRY / RISK NOTES:"" FontSize=""10"" FontWeight=""Bold"" Foreground=""#64748B""/>
                                <Border Background=""#020409"" BorderBrush=""#1E293B"" BorderThickness=""1"" CornerRadius=""8"" Padding=""10"" Margin=""0,4,0,14"">
                                    <TextBlock x:Name=""TxtModalPaths"" Text=""Paths..."" FontFamily=""Consolas"" FontSize=""10.5"" Foreground=""#34D399"" TextWrapping=""Wrap""/>
                                </Border>
                            </StackPanel>
                        </ScrollViewer>
                        <Button x:Name=""BtnModalGotIt"" Content=""Acknowledged"" Width=""140"" Height=""34"" HorizontalAlignment=""Right"" Style=""{StaticResource ActionBtn}""/>
                    </StackPanel>
                </Border>
            </Border>

            <!-- PC NAME MODAL -->
            <Border x:Name=""PcNameModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
                <Border Background=""#070F1E"" BorderBrush=""#F59E0B"" BorderThickness=""1.5"" CornerRadius=""14"" Width=""580"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
                    <StackPanel>
                        <Grid Margin=""0,0,0,14"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBlock Grid.Column=""0"" Text=""👑 Custom PC &amp; Host Identity Spoofer"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#FBBF24""/>
                            <Button x:Name=""BtnClosePcModal"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <TextBlock Text=""Enter desired PC/Computer Name (or click Random Generator):"" FontSize=""11"" Foreground=""#94A3B8"" Margin=""0,0,0,8""/>
                        <Grid Margin=""0,0,0,14"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBox x:Name=""TxtNewPcName"" Grid.Column=""0"" Height=""34"" FontFamily=""Consolas"" FontSize=""13"" FontWeight=""Bold"" VerticalContentAlignment=""Center"" Padding=""8,0""/>
                            <Button x:Name=""BtnRandomPcName"" Grid.Column=""1"" Content=""🎲 Randomize"" Height=""34"" Margin=""8,0,0,0"" Style=""{StaticResource ActionBtn}""/>
                        </Grid>
                        <TextBlock Text=""System Rename changes the NetBIOS computer name (effective after reboot). Virtual Identity only rewrites RegisteredOwner/Organization metadata."" FontSize=""10.5"" Foreground=""#64748B"" TextWrapping=""Wrap"" Margin=""0,0,0,14""/>
                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""BtnApplyVirtualIdentity"" Content=""⚡ Apply Virtual Identity"" Height=""34"" Margin=""0,0,8,0"" Style=""{StaticResource ActionBtn}""/>
                            <Button x:Name=""BtnApplyPcName"" Content=""👑 Apply System Rename"" Height=""34"" Style=""{StaticResource CrownBtn}""/>
                        </StackPanel>
                    </StackPanel>
                </Border>
            </Border>

            <!-- RESTORE MODAL -->
            <Border x:Name=""RestoreModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
                <Border Background=""#070F1E"" BorderBrush=""#34D399"" BorderThickness=""1.5"" CornerRadius=""14"" Width=""640"" MaxHeight=""520"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
                    <StackPanel>
                        <Grid Margin=""0,0,0,12"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBlock Grid.Column=""0"" Text=""↩ Restore a Backup Session"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#34D399""/>
                            <Button x:Name=""BtnCloseRestore"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <TextBlock Text=""Sessions contain .reg exports + quarantined files. Restore re-imports registry keys and moves files back."" FontSize=""10.5"" Foreground=""#94A3B8"" TextWrapping=""Wrap"" Margin=""0,0,0,8""/>
                        <ListBox x:Name=""LstSessions"" Height=""220"" Background=""#020409"" Foreground=""#34D399"" BorderBrush=""#1E293B"" FontFamily=""Consolas"" FontSize=""11"" Margin=""0,0,0,8""/>
                        <TextBlock x:Name=""TxtRestoreInfo"" Text=""No session selected."" FontSize=""10"" Foreground=""#64748B"" Margin=""0,0,0,10""/>
                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""BtnRefreshSessions"" Content=""⟳ Refresh"" Height=""32"" Margin=""0,0,8,0"" Style=""{StaticResource ActionBtn}""/>
                            <Button x:Name=""BtnOpenSessions"" Content=""📂 Open Folder"" Height=""32"" Margin=""0,0,8,0"" Style=""{StaticResource ActionBtn}""/>
                            <Button x:Name=""BtnDoRestore"" Content=""↩ RESTORE SELECTED"" Height=""32"" Style=""{StaticResource CrownBtn}""/>
                        </StackPanel>
                    </StackPanel>
                </Border>
            </Border>

            <!-- SCHEDULE MODAL -->
            <Border x:Name=""ScheduleModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
                <Border Background=""#070F1E"" BorderBrush=""#38BDF8"" BorderThickness=""1.5"" CornerRadius=""14"" Width=""560"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
                    <StackPanel>
                        <Grid Margin=""0,0,0,12"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBlock Grid.Column=""0"" Text=""⏰ Automated Daily Sweep Scheduler"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#38BDF8""/>
                            <Button x:Name=""BtnCloseSched"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <TextBlock Text=""Registers a Windows Task Scheduler job (highest privileges) that runs the selected profile headless every day at HH:MM."" FontSize=""10.5"" Foreground=""#94A3B8"" TextWrapping=""Wrap"" Margin=""0,0,0,10""/>
                        <Grid Margin=""0,0,0,10"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""Auto""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions>
                            <TextBlock Grid.Column=""0"" Text=""Time:"" FontSize=""11"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""0,0,6,0""/>
                            <TextBox x:Name=""TxtSchedTime"" Grid.Column=""1"" Height=""32"" Text=""03:00"" FontFamily=""Consolas"" VerticalContentAlignment=""Center"" Padding=""8,0""/>
                            <TextBlock Grid.Column=""2"" Text=""Profile:"" FontSize=""11"" Foreground=""#64748B"" VerticalAlignment=""Center"" Margin=""10,0,6,0""/>
                            <ComboBox x:Name=""CmbSchedProfile"" Grid.Column=""3"" Height=""32"" SelectedIndex=""1"">
                                <ComboBoxItem Content=""Safe""/><ComboBoxItem Content=""Balanced""/><ComboBoxItem Content=""Paranoid""/>
                            </ComboBox>
                        </Grid>
                        <TextBlock x:Name=""TxtSchedStatus"" Text=""checking..."" FontSize=""10.5"" Foreground=""#34D399"" Margin=""0,0,0,12""/>
                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""BtnSchedOn"" Content=""⏰ Register Daily Sweep"" Height=""32"" Margin=""0,0,8,0"" Style=""{StaticResource CrownBtn}""/>
                            <Button x:Name=""BtnSchedOff"" Content=""✖ Remove Task"" Height=""32"" Style=""{StaticResource ActionBtn}""/>
                        </StackPanel>
                    </StackPanel>
                </Border>
            </Border>

            <!-- SHRED MODAL -->
            <Border x:Name=""ShredModal"" Background=""#E6000000"" Visibility=""Collapsed"" CornerRadius=""14"" Margin=""8"">
                <Border Background=""#070F1E"" BorderBrush=""#F87171"" BorderThickness=""1.5"" CornerRadius=""14"" Width=""600"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Padding=""24"">
                    <StackPanel>
                        <Grid Margin=""0,0,0,12"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
                            <TextBlock Grid.Column=""0"" Text=""🔥 Secure Shredder (3-pass overwrite)"" FontSize=""15"" FontWeight=""ExtraBold"" Foreground=""#F87171""/>
                            <Button x:Name=""BtnCloseShred"" Grid.Column=""1"" Content=""✕"" Width=""28"" Height=""28"" Background=""Transparent"" Foreground=""#F87171"" FontWeight=""Bold"" BorderThickness=""0"" Cursor=""Hand""/>
                        </Grid>
                        <TextBlock Text=""Paste a full file or folder path. Content is overwritten 3x (random / 0xFF / 0x00) then deleted. IRREVERSIBLE on HDD; best-effort on SSD (wear-leveling)."" FontSize=""10.5"" Foreground=""#94A3B8"" TextWrapping=""Wrap"" Margin=""0,0,0,10""/>
                        <TextBox x:Name=""TxtShredPath"" Height=""34"" FontFamily=""Consolas"" FontSize=""12"" VerticalContentAlignment=""Center"" Padding=""8,0"" Margin=""0,0,0,14""/>
                        <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                            <Button x:Name=""BtnDoShredFile"" Content=""🔥 Shred FILE"" Height=""32"" Margin=""0,0,8,0"" Style=""{StaticResource ActionBtn}""/>
                            <Button x:Name=""BtnDoShredDir"" Content=""🔥 Shred FOLDER"" Height=""32"" Style=""{StaticResource CrownBtn}""/>
                        </StackPanel>
                    </StackPanel>
                </Border>
            </Border>
        </Grid>
    </Border>
</Grid>";

            var reader = new System.Xml.XmlTextReader(new StringReader(xaml));
            UIElement root;
            try { root = (UIElement)XamlReader.Load(reader); }
            catch (Exception xex)
            {
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "stealth_xaml_fail.xml"), xaml); } catch { }
                throw new Exception("XAML parse failed: " + xex.Message, xex);
            }
            this.Content = root;

            // counters
            txtOk = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtStats");
            txtHost = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtHost");
            txtNet = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtNet");
            txtTelem = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtTelem");
            txtLogs = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtLogs");
            scroller = (ScrollViewer)LogicalTreeHelper.FindLogicalNode(root, "Scroller");
            prgBar = (ProgressBar)LogicalTreeHelper.FindLogicalNode(root, "PrgBar");
            txtRam = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtRam");

            txtSearch = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtSearch");
            cmbProfile = (ComboBox)LogicalTreeHelper.FindLogicalNode(root, "CmbProfile");
            btnDry = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnDry");
            btnBackup = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnBackup");
            btnQuarantine = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnQuarantine");
            btnMaster = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMasterRun");

            infoModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "InfoModal");
            txtModalTitle = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalTitle");
            txtModalDesc = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalDesc");
            txtModalPaths = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtModalPaths");
            pcNameModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "PcNameModal");
            txtNewPcName = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtNewPcName");
            restoreModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "RestoreModal");
            lstSessions = (ListBox)LogicalTreeHelper.FindLogicalNode(root, "LstSessions");
            txtRestoreInfo = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtRestoreInfo");
            scheduleModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "ScheduleModal");
            txtSchedTime = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtSchedTime");
            txtSchedStatus = (TextBlock)LogicalTreeHelper.FindLogicalNode(root, "TxtSchedStatus");
            shredModal = (Border)LogicalTreeHelper.FindLogicalNode(root, "ShredModal");
            txtShredPath = (TextBox)LogicalTreeHelper.FindLogicalNode(root, "TxtShredPath");

            // wire cards
            List<Category> catsW = allCats;
            foreach (Category c in catsW)
            {
                Border card = (Border)LogicalTreeHelper.FindLogicalNode(root, "Card_" + c.Num);
                Button info = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnInfo_" + c.Num);
                Button master = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCat_" + c.Num);
                List<string> searchable = new List<string>();
                searchable.Add(c.Num); searchable.Add(c.Tag); searchable.Add(c.Title); searchable.Add(c.Desc);
                foreach (StealthAction a in c.Actions)
                {
                    searchable.Add(a.Num); searchable.Add(a.Title);
                    Button mb = (Button)LogicalTreeHelper.FindLogicalNode(root, MicroName(c, a));
                    if (mb != null)
                    {
                        StealthAction captured = a;
                        mb.Click += delegate(object s, RoutedEventArgs e) { RunAction(captured); };
                    }
                }
                if (info != null)
                {
                    Category capC = c;
                    info.Click += delegate(object s, RoutedEventArgs e) { ShowInfo(capC); };
                }
                if (master != null)
                {
                    Category capM = c;
                    master.Click += delegate(object s, RoutedEventArgs e) { RunCategory(capM); };
                }
                cardIndex.Add(new KeyValuePair<string, FrameworkElement[]>(c.Num + " " + c.Tag + " " + c.Title + " " + c.Desc + " " + string.Join(" ", searchable.ToArray()), new FrameworkElement[] { card }));
            }
        }

        // ------------------------------------------------------------------ wiring & logic

        private void WireShell()
        {
            Kernel.Log = AppendLog;
            var root = this.Content as UIElement;

            var btnClose = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnClose");
            var btnMin = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMin");
            var btnMax = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnMax");
            var titleBar = (Grid)LogicalTreeHelper.FindLogicalNode(root, "TitleBar");
            var btnAudit = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnAudit");
            var btnRestore = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnRestore");
            var btnSchedule = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnSchedule");
            var btnShred = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnShred");
            var btnPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnPcName");

            btnClose.Click += delegate { this.Close(); };
            btnMin.Click += delegate { this.WindowState = WindowState.Minimized; };
            btnMax.Click += delegate { ToggleMaximize(); };
            titleBar.MouseLeftButtonDown += delegate(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) ToggleMaximize(); else try { this.DragMove(); } catch { }
            };

            btnDry.Click += delegate
            {
                Kernel.DryRun = !Kernel.DryRun;
                btnDry.Content = Kernel.DryRun ? "\u25CF DRY-RUN: ON" : "\u25CB DRY-RUN: OFF";
                btnDry.Foreground = new SolidColorBrush(Kernel.DryRun ? Color.FromRgb(0xFB, 0xBF, 0x24) : Color.FromRgb(0x7D, 0xD3, 0xFC));
                AppendLog(Kernel.DryRun ? "[MODE] DRY-RUN enabled — nothing will be modified, every planned op is previewed." : "[MODE] DRY-RUN disabled — live execution.");
            };
            btnBackup.Click += delegate
            {
                Kernel.BackupOn = !Kernel.BackupOn;
                btnBackup.Content = Kernel.BackupOn ? "\uD83D\uDCBE BACKUP: ON" : "\uD83D\uDCBE BACKUP: OFF";
                AppendLog("[MODE] registry backup " + (Kernel.BackupOn ? "ENABLED (reg export before every key mutation)" : "DISABLED (not recommended)"));
            };
            btnQuarantine.Click += delegate
            {
                Kernel.QuarantineOn = !Kernel.QuarantineOn;
                btnQuarantine.Content = Kernel.QuarantineOn ? "\uD83D\uDCE6 QUARANTINE: ON" : "\uD83D\uDCE6 QUARANTINE: OFF";
                AppendLog("[MODE] file quarantine " + (Kernel.QuarantineOn ? "ENABLED (files moved to quarantine, restorable)" : "DISABLED (files hard-deleted, sizes recorded in manifest)"));
            };
            btnMaster.Click += delegate
            {
                int idx = cmbProfile.SelectedIndex;
                string prof = idx == 0 ? "Safe" : (idx == 2 ? "Paranoid" : "Balanced");
                if (prof == "Paranoid" && !Kernel.DryRun)
                {
                    int desCount = 0;
                    StringBuilder desList = new StringBuilder();
                    foreach (Category c in allCats)
                        foreach (StealthAction a in c.Actions)
                            if (a.Destructive)
                            {
                                desCount++;
                                if (desList.Length < 700) desList.Append("  \u2622 ").Append(c.Num).Append(".").Append(a.Num).Append(" ").Append(a.Title).Append("\n");
                            }
                    MessageBoxResult mbr = MessageBox.Show(this,
                        "PARANOID profile will execute " + desCount + " DESTRUCTIVE actions:\n\n" + desList.ToString() +
                        "\nBackups/quarantine still apply where possible, but several of these are IRREVERSIBLE (VSS shadows, browser history, WiFi passwords).\n\nContinue?",
                        "S-T-E-A-L-T-H v8.0 — Paranoid Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (mbr != MessageBoxResult.Yes) { AppendLog("[CANCEL] Paranoid master protocol cancelled by user."); return; }
                }
                RunAsync(MasterProtocol);
            };
            btnAudit.Click += delegate { RunAsync(RunAudit); };
            btnPcName.Click += delegate
            {
                txtNewPcName.Text = Environment.MachineName;
                pcNameModal.Visibility = Visibility.Visible;
            };

            var btnRandomPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnRandomPcName");
            var btnApplyPcName = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnApplyPcName");
            var btnApplyVirtualIdentity = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnApplyVirtualIdentity");
            var btnClosePcModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnClosePcModal");
            btnClosePcModal.Click += delegate { pcNameModal.Visibility = Visibility.Collapsed; };
            btnRandomPcName.Click += delegate
            {
                string[] prefixes = new string[] { "STEALTH-NODE", "DESKTOP-CYBER", "QUANTUM-HOST", "NEXUS-SYS", "SHADOW-PC", "ZERO-GRID" };
                Random rnd = new Random();
                txtNewPcName.Text = prefixes[rnd.Next(prefixes.Length)] + "-" + rnd.Next(1000, 9999);
            };
            btnApplyPcName.Click += delegate
            {
                string name = txtNewPcName.Text.Trim();
                if (name.Length == 0) return;
                pcNameModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    Kernel.Log("[PLAN] Rename-Computer -> " + name);
                    OpResult r = Kernel.PSCheck("Rename-Computer -NewName '" + name + "' -Force -WarningAction SilentlyContinue | Out-Null; 'renamed'", "Rename-Computer", 30000);
                    LogResult("0.4 PC Rename", r);
                });
            };
            btnApplyVirtualIdentity.Click += delegate
            {
                string name = txtNewPcName.Text.Trim();
                if (name.Length == 0) return;
                pcNameModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    OpResult r1 = Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOwner", name, RegistryValueKind.String);
                    Kernel.RegSet("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "RegisteredOrganization", "STEALTH-ORG", RegistryValueKind.String);
                    LogResult("0.3 Virtual Identity", r1);
                });
            };

            // restore modal
            var btnCloseRestore = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseRestore");
            var btnDoRestore = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnDoRestore");
            var btnRefreshSessions = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnRefreshSessions");
            var btnOpenSessions = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnOpenSessions");
            btnCloseRestore.Click += delegate { restoreModal.Visibility = Visibility.Collapsed; };
            btnRestore.Click += delegate { RefreshSessions(); restoreModal.Visibility = Visibility.Visible; };
            btnRefreshSessions.Click += delegate { RefreshSessions(); };
            btnOpenSessions.Click += delegate
            {
                try { Process.Start("explorer.exe", BackupManager.BaseDir()); } catch (Exception ex) { AppendLog("[RESTORE] open folder failed: " + ex.Message); }
            };
            lstSessions.SelectionChanged += delegate
            {
                if (lstSessions.SelectedItem != null)
                    txtRestoreInfo.Text = "Selected: " + lstSessions.SelectedItem.ToString();
            };
            btnDoRestore.Click += delegate
            {
                if (lstSessions.SelectedItem == null) { AppendLog("[RESTORE] select a session first."); return; }
                string dir = lstSessions.SelectedItem.ToString();
                restoreModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate { BackupManager.Restore(dir, AppendLog); });
            };

            // schedule modal
            var btnCloseSched = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseSched");
            var btnSchedOn = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnSchedOn");
            var btnSchedOff = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnSchedOff");
            btnCloseSched.Click += delegate { scheduleModal.Visibility = Visibility.Collapsed; };
            btnSchedule.Click += delegate
            {
                txtSchedStatus.Text = Scheduler.Exists() ? "Status: task '" + Scheduler.TaskName + "' EXISTS" : "Status: not registered";
                scheduleModal.Visibility = Visibility.Visible;
            };
            btnSchedOn.Click += delegate
            {
                string time = txtSchedTime.Text.Trim();
                if (time.Length != 5 || time[2] != ':') { AppendLog("[SCHED] time must be HH:MM"); return; }
                string profile = "Balanced";
                var cmbSched = (ComboBox)LogicalTreeHelper.FindLogicalNode((DependencyObject)this.Content, "CmbSchedProfile");
                if (cmbSched.SelectedItem is ComboBoxItem) profile = ((ComboBoxItem)cmbSched.SelectedItem).Content.ToString();
                scheduleModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    OpResult r = Scheduler.Register(time, profile);
                    LogResult("Scheduler", r);
                    txtSchedStatus.Text = Scheduler.Exists() ? "Status: registered" : "Status: not registered";
                });
            };
            btnSchedOff.Click += delegate
            {
                scheduleModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    OpResult r = Scheduler.Unregister();
                    LogResult("Scheduler", r);
                    txtSchedStatus.Text = Scheduler.Exists() ? "Status: registered" : "Status: not registered";
                });
            };

            // shred modal
            var btnCloseShred = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseShred");
            var btnDoShredFile = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnDoShredFile");
            var btnDoShredDir = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnDoShredDir");
            btnCloseShred.Click += delegate { shredModal.Visibility = Visibility.Collapsed; };
            btnShred.Click += delegate { shredModal.Visibility = Visibility.Visible; };
            btnDoShredFile.Click += delegate
            {
                string p = txtShredPath.Text.Trim().Trim('"');
                shredModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    if (!File.Exists(p)) { LogResult("Shred", OpResult.FAIL("file not found: " + p)); return; }
                    LogResult("Shred File", Kernel.ShredFile(p));
                });
            };
            btnDoShredDir.Click += delegate
            {
                string p = txtShredPath.Text.Trim().Trim('"');
                shredModal.Visibility = Visibility.Collapsed;
                RunAsync(delegate
                {
                    if (!Directory.Exists(p)) { LogResult("Shred", OpResult.FAIL("folder not found: " + p)); return; }
                    AppendLog("[SHRED] shredding folder tree: " + p);
                    LogResult("Shred Folder", Kernel.ShredDir(p));
                });
            };

            // search filter
            txtSearch.TextChanged += delegate { ApplySearchFilter(); };

            // info modal buttons
            var btnCloseModal = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnCloseModal");
            var btnModalGotIt = (Button)LogicalTreeHelper.FindLogicalNode(root, "BtnModalGotIt");
            btnCloseModal.Click += delegate { infoModal.Visibility = Visibility.Collapsed; };
            btnModalGotIt.Click += delegate { infoModal.Visibility = Visibility.Collapsed; };

            ResetCounters();
        }

        private void ToggleMaximize()
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void ApplySearchFilter()
        {
            string q = txtSearch.Text.Trim().ToLowerInvariant();
            foreach (KeyValuePair<string, FrameworkElement[]> entry in cardIndex)
            {
                bool show = q.Length == 0 || entry.Key.ToLowerInvariant().Contains(q);
                foreach (FrameworkElement fe in entry.Value) fe.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ShowInfo(Category c)
        {
            StringBuilder paths = new StringBuilder();
            paths.Append(c.InfoPaths);
            paths.Append("\n\n---- GRANULAR ACTIONS ----");
            foreach (StealthAction a in c.Actions)
            {
                paths.Append("\n[").Append(a.Num).Append("] ").Append(a.Title);
                if (a.Destructive) paths.Append("  \u26A0 DESTRUCTIVE");
                if (a.RebootNeeded) paths.Append("  (reboot to take effect)");
                paths.Append("\n    ").Append(a.Info);
            }
            txtModalTitle.Text = "[" + c.Num + "] " + c.Title;
            txtModalDesc.Text = c.Info;
            txtModalPaths.Text = paths.ToString();
            infoModal.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------ execution engine

        private void LogResult(string label, OpResult r)
        {
            if (r == null) { AppendLog("[????] " + label); return; }
            if (r.Ok && !r.Skipped)
            {
                okCount++; AppendLog("[OK]   " + label + " — " + r.Detail);
            }
            else if (r.Skipped)
            {
                skipCount++; AppendLog("[SKIP] " + label + " — " + r.Detail);
            }
            else
            {
                failCount++; AppendLog("[FAIL] " + label + " — " + r.Detail);
            }
            UpdateCounters();
        }

        private void ResetCounters() { okCount = 0; failCount = 0; skipCount = 0; UpdateCounters(); }

        private void UpdateCounters()
        {
            if (txtOk == null) return;
            Dispatcher.Invoke((Action)(delegate
            {
                txtOk.Text = "OK:" + okCount + "  FAIL:" + failCount + "  SKIP:" + skipCount;
                txtOk.Foreground = new SolidColorBrush(
                    failCount > 0 ? Color.FromRgb(0xF8, 0x71, 0x71) :
                    okCount > 0 ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x64, 0x74, 0x8B));
            }));
        }

        private void RunAction(StealthAction a)
        {
            if (a.Destructive && !Kernel.DryRun)
            {
                MessageBoxResult mbr = MessageBox.Show(this,
                    "DESTRUCTIVE ACTION [" + a.Num + "]: " + a.Title + "\n\n" + a.Info + "\n\nContinue?",
                    "S-T-E-A-L-T-H v8.0 — Destructive Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (mbr != MessageBoxResult.Yes) { AppendLog("[CANCEL] " + a.Num + " cancelled by user."); return; }
            }
            RunAsync(delegate
            {
                OpResult r = a.Run(Kernel.DryRun);
                LogResult(a.Num + " " + a.Title, r);
            });
        }

        private void RunCategory(Category c)
        {
            RunAsync(delegate
            {
                AppendLog("────── [" + c.Num + "] " + c.Title + " — running all non-destructive actions ──────");
                if (!Kernel.DryRun)
                {
                    if (Kernel.Backup == null || Kernel.Backup.SessionDir == null)
                    {
                        BackupManager bm = new BackupManager();
                        bm.Enabled = Kernel.BackupOn; bm.Quarantine = Kernel.QuarantineOn;
                        bm.StartSession(); Kernel.Backup = bm;
                    }
                }
                int localOk = 0, localFail = 0, localSkip = 0;
                foreach (StealthAction a in c.Actions)
                {
                    if (a.Destructive)
                    {
                        AppendLog("[SKIP] " + a.Num + " destructive — click it directly or use Paranoid master profile.");
                        skipCount++; localSkip++;
                        continue;
                    }
                    OpResult r = a.Run(Kernel.DryRun);
                    string before = Describe(r);
                    if (r.Ok && !r.Skipped) { okCount++; localOk++; }
                    else if (r.Skipped) { skipCount++; localSkip++; }
                    else { failCount++; localFail++; }
                    AppendLog((r.Ok && !r.Skipped ? "[OK]   " : (r.Skipped ? "[SKIP] " : "[FAIL] ")) + a.Num + " " + a.Title + " — " + r.Detail);
                }
                UpdateCounters();
                AppendLog("────── [" + c.Num + "] done: " + localOk + " ok, " + localFail + " FAILED, " + localSkip + " skipped ──────");
                if (Kernel.Backup != null && Kernel.Backup.SessionDir != null && localOk + localFail > 0) Kernel.Backup.FinishSession();
            });
        }

        private string Describe(OpResult r) { return r == null ? "null" : r.Detail; }

        private void MasterProtocol()
        {
            string profile = "Balanced";
            Dispatcher.Invoke((Action)(delegate
            {
                int idx = cmbProfile.SelectedIndex;
                profile = idx == 0 ? "Safe" : (idx == 2 ? "Paranoid" : "Balanced");
            }));
            RiskLevel target = profile == "Safe" ? RiskLevel.Safe : (profile == "Paranoid" ? RiskLevel.Paranoid : RiskLevel.Balanced);

            AppendLog("===============================================================");
            AppendLog("\u26A1 MASTER S-T-E-A-L-T-H v8.0 PROTOCOL — profile: " + profile.ToUpper() + (Kernel.DryRun ? "  [DRY-RUN PREVIEW]" : ""));
            AppendLog("===============================================================");

            ResetCounters();
            if (!Kernel.DryRun)
            {
                BackupManager bm = new BackupManager();
                bm.Enabled = Kernel.BackupOn; bm.Quarantine = Kernel.QuarantineOn;
                bm.StartSession(); Kernel.Backup = bm;
                if (!Kernel.BackupOn) AppendLog("[BACKUP] disabled by user — registry changes NOT exported (not recommended).");
            }
            else AppendLog("[DRY] preview mode — no changes will be made.");

            List<Category> cats = allCats;
            int total = 0;
            foreach (Category cat in cats)
            {
                int run = 0;
                foreach (StealthAction a in cat.Actions) if (a.Risk <= target && (!a.Destructive || target == RiskLevel.Paranoid)) run++;
                total += run;
            }
            AppendLog("[PLAN] " + total + " actions queued across " + cats.Count + " vectors.");

            int done = 0;
            foreach (Category cat in cats)
            {
                AppendLog("────── [" + cat.Num + "] " + cat.Title + " ──────");
                foreach (StealthAction a in cat.Actions)
                {
                    if (a.Risk > target) continue;
                    if (a.Destructive && target != RiskLevel.Paranoid) continue;
                    OpResult r = a.Run(Kernel.DryRun);
                    LogResult(a.Num + " " + a.Title, r);
                    done++;
                    Dispatcher.Invoke((Action)(delegate { prgBar.Value = (double)done / Math.Max(1, total) * 100.0; }));
                }
            }
            if (!Kernel.DryRun && Kernel.Backup != null) Kernel.Backup.FinishSession();
            AppendLog("===============================================================");
            AppendLog("\uD83C\uDF89 PROTOCOL COMPLETE — " + profile.ToUpper() + ": OK " + okCount + " | FAILED " + failCount + " | SKIPPED " + skipCount);
            if (failCount > 0) AppendLog("[NOTE] FAILED items are real failures (permissions/in-use/protected) — re-run elevated or close apps and retry those specific actions.");
            AppendLog("[NOTE] Some actions need a REBOOT to fully apply (ShimCache, pagefile, PC rename, MountedDevices).");
            AppendLog("===============================================================");
            Dispatcher.Invoke((Action)(delegate { prgBar.Value = 0; }));
        }

        private void RunAudit()
        {
            AppendLog("[AUDIT] generating read-only privacy report (no changes will be made)...");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path = Path.Combine(desktop, "STEALTH-Audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".html");
            string res = Auditor.Generate(path);
            if (res.StartsWith("ERROR"))
            {
                AppendLog("[AUDIT] FAILED: " + res);
                failCount++; UpdateCounters();
                return;
            }
            AppendLog("[OK]   Audit report written: " + res);
            try { Process.Start(res); AppendLog("[OK]   Opening report in default browser."); }
            catch { AppendLog("[NOTE] Open the report manually: " + res); }
        }

        // ------------------------------------------------------------------ infra

        private void RunAsync(Action action)
        {
            if (busy)
            {
                AppendLog("[BUSY] another operation is running — wait for it to finish.");
                return;
            }
            busy = true;
            if (prgBar != null) Dispatcher.Invoke((Action)(delegate { prgBar.IsIndeterminate = true; }));
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { action(); }
                catch (Exception ex) { AppendLog("[CRASH] " + ex.GetType().Name + ": " + ex.Message); }
                finally
                {
                    busy = false;
                    if (prgBar != null) Dispatcher.Invoke((Action)(delegate { prgBar.IsIndeterminate = false; }));
                }
            });
        }

        private void AppendLog(string msg)
        {
            if (txtLogs == null) return;
            Dispatcher.Invoke((Action)(delegate
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                txtLogs.AppendText("[" + time + "] " + msg + "\r\n");
                scroller.ScrollToEnd();
            }));
        }

        private void RefreshSessions()
        {
            lstSessions.Items.Clear();
            List<string> sessions = BackupManager.ListSessions();
            foreach (string s in sessions) lstSessions.Items.Add(s);
            txtRestoreInfo.Text = sessions.Count + " backup session(s) found in " + BackupManager.BaseDir();
        }

        private void StartHud()
        {
            hudTimer = new DispatcherTimer();
            hudTimer.Interval = TimeSpan.FromSeconds(4);
            hudTimer.Tick += delegate
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string host = Environment.MachineName + " (" + Environment.UserName + ")";
                        ProcResult r = Kernel.PS("$n=(Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -First 2 -ExpandProperty Name) -join ', '; $t=(Get-Service -Name DiagTrack -ErrorAction SilentlyContinue).Status; $os=Get-CimInstance Win32_OperatingSystem; $m=[math]::Round(($os.TotalVisibleMemorySize-$os.FreePhysicalMemory)/$os.TotalVisibleMemorySize*100,1); Write-Output (\"{0}|{1}|{2}\" -f $n,$t,$m)", 12000);
                        string[] parts = (r.Out ?? "").Trim().Split('|');
                        string net = parts.Length > 0 ? parts[0].Trim() : "";
                        string telem = parts.Length > 1 ? parts[1].Trim() : "";
                        string ram = parts.Length > 2 ? parts[2].Trim() : "";
                        if (net.Length == 0) net = "Connected";
                        bool silenced = telem.Length == 0 || telem.Equals("Stopped", StringComparison.OrdinalIgnoreCase);
                        Dispatcher.Invoke((Action)(delegate
                        {
                            txtHost.Text = host;
                            txtNet.Text = net;
                            txtTelem.Text = silenced ? "0-Track Silenced" : "Active (" + telem + ")";
                            txtTelem.Foreground = new SolidColorBrush(silenced ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0xF8, 0x71, 0x71));
                            txtRam.Text = (ram.Length > 0 ? ram : "?") + "% RAM";
                        }));
                    }
                    catch { }
                });
            };
            hudTimer.Start();
        }
    }
}
