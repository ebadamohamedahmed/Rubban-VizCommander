using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Rubban.Core;

namespace Rubban.Launcher
{
    public sealed class SplashScreenForm : Form
    {
        private readonly ProgressBar _bar = new ProgressBar();
        private readonly Label _lbl = new Label();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private int _val = 0;

        public SplashScreenForm(string baseDir)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(760, 428);
            BackColor = Color.White;
            TopMost = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            string splashPath = Path.Combine(baseDir, "Assets", "splash_bg.png");
            if (File.Exists(splashPath))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(splashPath)))
                    BackgroundImage = Image.FromStream(ms);
                BackgroundImageLayout = ImageLayout.Stretch;
            }

            _bar.Bounds = new Rectangle(60, 382, 640, 8);
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.ForeColor = Color.FromArgb(220, 110, 20);

            _lbl.Bounds = new Rectangle(60, 395, 640, 22);
            _lbl.BackColor = Color.Transparent;
            _lbl.ForeColor = Color.FromArgb(64, 64, 64);
            _lbl.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lbl.Text = "RUBBAN — Probing workstation & Vizrt ports...";

            Controls.AddRange(new Control[] { _bar, _lbl });

            _timer.Interval = 25;
            _timer.Tick += (s, e) =>
            {
                _val += 4;
                if (_val <= 100) _bar.Value = _val;
                else { _timer.Stop(); Close(); }
            };
            _timer.Start();
        }
    }

    public sealed class ProfileOverrideForm : Form
    {
        private readonly ComboBox _cmb = new ComboBox();
        private readonly Label _lbl = new Label();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private int _sec;
        public WorkstationProfile Chosen { get; private set; }
        public bool Aborted { get; private set; }

        public ProfileOverrideForm(List<WorkstationProfile> profiles, WorkstationProfile detected, int timeoutSec)
        {
            Chosen = detected; _sec = Math.Max(1, timeoutSec);
            Text = "RUBBAN (رُبّان) — Workstation Selector";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(410, 155);
            MaximizeBox = false; MinimizeBox = false; TopMost = true;
            Font = new Font("Segoe UI", 9.5F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _lbl.Bounds = new Rectangle(15, 12, 380, 38);
            _lbl.Text = $"Detected: {detected.Description}\nLaunching in {_sec}s (select to override):";

            _cmb.Bounds = new Rectangle(15, 56, 380, 28);
            _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmb.DisplayMember = "Description";
            foreach (var p in profiles) _cmb.Items.Add(p);
            _cmb.SelectedItem = detected;
            _cmb.DropDown += (s, e) => { _timer.Stop(); _lbl.Text = "Auto-launch paused. Select profile:"; };
            _cmb.SelectedIndexChanged += (s, e) => { if (_cmb.SelectedItem is WorkstationProfile wp) Chosen = wp; };

            var btnGo = new Button { Text = "Launch Now", Bounds = new Rectangle(215, 102, 90, 32) };
            btnGo.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            var btnStop = new Button { Text = "Cancel", Bounds = new Rectangle(312, 102, 83, 32) };
            btnStop.Click += (s, e) => { Aborted = true; DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { _lbl, _cmb, btnGo, btnStop });
            AcceptButton = btnGo;

            _timer.Interval = 1000;
            _timer.Tick += (s, e) =>
            {
                if (--_sec <= 0) { _timer.Stop(); DialogResult = DialogResult.OK; Close(); }
                else _lbl.Text = $"Detected: {Chosen.Description}\nLaunching in {_sec}s (select to override):";
            };
            _timer.Start();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var mutex = new Mutex(true, "Global\\RubbanVizCommander_SingleInstance", out bool created))
            {
                if (!created)
                {
                    MessageBox.Show("RUBBAN is already running on this workstation.", "RUBBAN");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                using (var splash = new SplashScreenForm(baseDir))
                    splash.ShowDialog();

                var cfg = ProfilesConfig.LoadOrCreate(baseDir);
                var detected = Detect(cfg);

                using (var dlg = new ProfileOverrideForm(cfg.Profiles, detected, cfg.PromptTimeoutSeconds))
                {
                    dlg.ShowDialog();
                    if (dlg.Aborted) return;
                    detected = dlg.Chosen;
                }

                string appExe = Path.Combine(baseDir, "Rubban.App.exe");
                if (File.Exists(appExe))
                    Process.Start(new ProcessStartInfo { FileName = appExe, Arguments = $"--profile \"{detected.Name}\"", WorkingDirectory = baseDir, UseShellExecute = false });
            }
        }

        static WorkstationProfile Detect(ProfilesConfig cfg)
        {
            string hostName = Environment.MachineName;
            var byMachine = cfg.Profiles.FirstOrDefault(p => p.MatchMachineNames != null && p.MatchMachineNames.Any(m => m.Equals(hostName, StringComparison.OrdinalIgnoreCase)));
            if (byMachine != null) return byMachine;
            foreach (var p in cfg.Profiles) if (IsOpen(p.Host, p.Port, 250)) return p;
            return cfg.Profiles.FirstOrDefault(p => p.Name == cfg.DefaultProfile) ?? cfg.Profiles.First();
        }

        static bool IsOpen(string host, int port, int ms)
        {
            try { using (var c = new TcpClient()) { var t = c.ConnectAsync(host == "localhost" ? "127.0.0.1" : host, port); return t.Wait(ms) && c.Connected; } }
            catch { return false; }
        }
    }
}
