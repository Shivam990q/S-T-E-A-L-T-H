import io, os

# ================= test file updates =================
p = "S-T-E-A-L-T-H_test.cs"
t = io.open(p, encoding="utf-8").read()

def rept(old, new):
    global t
    assert t.count(old) == 1, "count=%d :: %s" % (t.count(old), old[:80])
    t = t.replace(old, new)

rept('                if (cats.Count != 46) throw new Exception("expected 46 categories, got " + cats.Count);',
     '                if (cats.Count != 53) throw new Exception("expected 53 categories, got " + cats.Count);')
rept('                if (cards != 46) throw new Exception("expected 46 cards, found " + cards);',
     '                if (cards != 53) throw new Exception("expected 53 cards, found " + cards);')
rept('"BtnMenuAbout","TxtSearch",',
     '"BtnMenuAbout","BtnCancel","TxtSearch",')

test19 = '''            // Test 19: settings INI persists dry-run/backup/quarantine toggles
            AssertTest("19. AppSettings: INI persistence roundtrip for toggles", delegate()
            {
                bool od = Kernel.DryRun, ob = Kernel.BackupOn, oq = Kernel.QuarantineOn;
                Kernel.DryRun = true; Kernel.BackupOn = false; Kernel.QuarantineOn = true;
                AppSettings.Save();
                Kernel.DryRun = false; Kernel.BackupOn = true; Kernel.QuarantineOn = false;
                AppSettings.Load();
                if (!Kernel.DryRun || Kernel.BackupOn || !Kernel.QuarantineOn) throw new Exception("roundtrip mismatch: dry=" + Kernel.DryRun + " backup=" + Kernel.BackupOn + " quar=" + Kernel.QuarantineOn);
                Kernel.DryRun = od; Kernel.BackupOn = ob; Kernel.QuarantineOn = oq;
                AppSettings.Save(); // restore user's saved posture
            });

            // Test 20: CLI /help and /list via child process
            AssertTest("20. CLI: /help and /list produce full action catalog", delegate()
            {
                string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "S-T-E-A-L-T-H.exe");
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe; psi.Arguments = "/help"; psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true;
                using (Process p1 = Process.Start(psi))
                {
                    string o1 = p1.StandardOutput.ReadToEnd();
                    if (!p1.WaitForExit(30000)) throw new Exception("/help timed out");
                    if (!o1.Contains("/sweep") || !o1.Contains("/run")) throw new Exception("/help output incomplete");
                }
                psi.Arguments = "/list";
                using (Process p2 = Process.Start(psi))
                {
                    string o2 = p2.StandardOutput.ReadToEnd();
                    if (!p2.WaitForExit(60000)) throw new Exception("/list timed out");
                    if (!o2.Contains("TOTAL:") || !o2.Contains("53 vectors")) throw new Exception("/list catalog wrong: " + (o2.Length < 80 ? o2 : o2.Substring(o2.Length - 80)));
                    if (!o2.Contains("45.") || !o2.Contains("51.")) throw new Exception("/list missing new vectors");
                }
            });

            // cleanup
            try
            {'''
rept('''            // cleanup
            try
            {''', test19)

tmp = p + ".tmp"; io.open(tmp, "w", encoding="utf-8", newline="").write(t); os.replace(tmp, p)
print("test file updated")

# ================= Auditor v2: privacy score + recommendations =================
p2 = "S-T-E-A-L-T-H.cs"
s = io.open(p2, encoding="utf-8").read()

def rep1(old, new):
    global s
    assert s.count(old) == 1, "count=%d :: %s" % (s.count(old), old[:90])
    s = s.replace(old, new)

rep1('''            h.Append("<h1>S-T-E-A-L-T-H v8.1 ADVANCED \\u2014 Privacy & Forensic Audit Report</h1>");''',
'''            h.Append("<h1>S-T-E-A-L-T-H v8.1 ADVANCED \\u2014 Privacy & Forensic Audit Report</h1>");

            // ---- privacy score + recommendations (computed BEFORE the detailed sections) ----
            int score = 100;
            List<string> recs = new List<string>();
            string[] telSvcs = new string[] { "DiagTrack", "dmwappushservice", "WerSvc", "SysMain", "DPS", "WpnService", "DoSvc", "PcaSvc" };
            foreach (string sv in telSvcs)
            {
                string st = SvcState(sv);
                if (st == "Running") { score -= 6; recs.Add("Telemetry service '" + sv + "' is RUNNING \\u2014 stop+disable via Cat 1 / 50 sweepers."); }
            }
            CheckPolicy(ref score, recs, "HKLM\\\\SOFTWARE\\\\Policies\\\\Microsoft\\\\Windows\\\\DataCollection", "AllowTelemetry", 0, 6, "Telemetry not locked to Security-only \\u2014 run 1.5.");
            CheckPolicy(ref score, recs, "HKLM\\\\SOFTWARE\\\\Policies\\\\Microsoft\\\\Windows\\\\WindowsAI", "DisableAIDataAnalysis", 1, 5, "Recall/24H2 AI analysis not disabled \\u2014 run 27.1.");
            CheckPolicy(ref score, recs, "HKLM\\\\SOFTWARE\\\\Policies\\\\Microsoft\\\\Windows\\\\System", "AllowClipboardHistory", 0, 3, "Cloud clipboard history on \\u2014 run 6.4/6.5.");
            CheckPolicy(ref score, recs, "HKLM\\\\SOFTWARE\\\\Policies\\\\Microsoft\\\\Windows\\\\DeliveryOptimization", "DODownloadMode", 0, 3, "P2P Delivery Optimization on \\u2014 run 11.1.");
            CheckPolicy(ref score, recs, "HKLM\\\\SOFTWARE\\\\Policies\\\\Microsoft\\\\Windows\\\\PowerShell\\\\ScriptBlockLogging", "EnableScriptBlockLogging", 0, 3, "PS script-block logging on \\u2014 run 31.3.");
            if (!HostsList.IsApplied()) { score -= 5; recs.Add("Telemetry hosts-block not applied \\u2014 run 37.1."); }
            string bagState = Kernel.RegExists("HKCU\\\\Software\\\\Classes\\\\Local Settings\\\\Software\\\\Microsoft\\\\Windows\\\\Shell\\\\BagMRU") ? "present" : "clean";
            if (bagState == "present") { score -= 4; recs.Add("Real ShellBags present \\u2014 run 4.1/4.2."); }
            if (Directory.Exists("C:\\\\Windows\\\\Prefetch") && Directory.GetFiles("C:\\\\Windows\\\\Prefetch", "*.pf").Length > 40) { score -= 3; recs.Add("Prefetch traces heavy \\u2014 run 13.1."); }
            if (Directory.Exists(LA() + "\\\\Microsoft\\\\CryptnetUrlCache") && Kernel.DirSize(LA() + "\\\\Microsoft\\\\CryptnetUrlCache") > 512 * 1024) { score -= 3; recs.Add("Cryptnet SSL leak cache large \\u2014 run 8.1/8.2."); }
            if (score < 0) score = 0;
            string grade = score >= 90 ? "EXCELLENT" : (score >= 70 ? "GOOD" : (score >= 45 ? "FAIR" : "POOR"));
            string gradeColor = score >= 90 ? "#34D399" : (score >= 70 ? "#FBBF24" : "#F87171");
            h.Append("<h2>0. Privacy Score</h2>");
            h.Append("<p style='font-size:26px;font-weight:bold;color:" + gradeColor + "'>SCORE: " + score + "/100 \\u2014 " + grade + "</p>");
            if (recs.Count == 0)
            {
                h.Append("<p class='good'>No high-impact privacy gaps detected by the automated checks.</p>");
            }
            else
            {
                h.Append("<table><tr><th>Recommendation (highest impact first)</th></tr>");
                foreach (string r in recs) h.Append("<tr><td>" + Esc(r) + "</td></tr>");
                h.Append("</table>");
            }''')

# helpers: SvcState + CheckPolicy inside Auditor
rep1('''        private static void AppendPolicy(StringBuilder h, string name, string key, string valueName, string desired)''',
'''        private static string SvcState(string name)
        {
            ProcResult r = ProcRunner.Run("powershell.exe", "-NoProfile -NonInteractive -Command \\"(Get-Service -Name '" + name + "' -ErrorAction SilentlyContinue).Status\\"", 12000);
            return (r.Out ?? "").Trim();
        }

        private static void CheckPolicy(ref int score, List<string> recs, string key, string valueName, int desired, int penalty, string advice)
        {
            object v = Kernel.RegGet(key, valueName);
            if (v == null || Convert.ToInt32(v) != desired)
            {
                score -= penalty;
                recs.Add(advice);
            }
        }

        private static void AppendPolicy(StringBuilder h, string name, string key, string valueName, string desired)''')

tmp2 = p2 + ".tmp"; io.open(tmp2, "w", encoding="utf-8", newline="").write(s); os.replace(tmp2, p2)
print("auditor v2 OK")
