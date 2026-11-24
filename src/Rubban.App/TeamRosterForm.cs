using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Rubban.Core;

namespace Rubban.App
{
    public class TeamRosterForm : Form
    {
        private readonly string _baseDir;
        private readonly bool _isRtl;
        private readonly ComboBox _cmbSaved = new ComboBox();
        private readonly TextBox _txtTeamName = new TextBox();
        private readonly TextBox _txtCoach = new TextBox();
        private readonly TextBox _txtLogo = new TextBox();
        private readonly TextBox _txtColor = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private List<TeamRoster> _savedTeams = new List<TeamRoster>();

        public TeamRoster SelectedForA { get; private set; }
        public TeamRoster SelectedForB { get; private set; }

        public TeamRosterForm(string baseDir, bool isRtl)
        {
            _baseDir = baseDir;
            _isRtl = isRtl;
            Text = isRtl ? "إدارة الفرق واللاعبين (فتح وتعديل الملفات بدون أوفيس)" : "Team & Roster Manager (Open & Edit Office-Free)";
            Size = new Size(740, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = isRtl;
            Font = new Font("Segoe UI", 9.5F);

            var lblSaved = new Label { Text = isRtl ? "الفرق المحفوظة:" : "Saved Teams:", Bounds = new Rectangle(15, 16, 110, 24) };
            _cmbSaved.Bounds = new Rectangle(130, 13, 220, 26);
            _cmbSaved.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSaved.SelectedIndexChanged += (s, e) => LoadSelectedTeam();

            var btnOpenFile = new Button { Text = isRtl ? "فتح ملف للتعديل" : "Open File...", BackColor = Color.LightYellow, Bounds = new Rectangle(358, 12, 115, 28) };
            btnOpenFile.Click += (s, e) => OpenExistingFile();

            var btnNew = new Button { Text = isRtl ? "فريق جديد" : "New", Bounds = new Rectangle(480, 12, 80, 28) };
            btnNew.Click += (s, e) => { _txtTeamName.Text = ""; _txtCoach.Text = ""; _txtLogo.Text = ""; _txtColor.Text = ""; _grid.Rows.Clear(); };

            var btnTpl = new Button { Text = isRtl ? "توليد قالب Excel" : "Blank .XLSX", Bounds = new Rectangle(566, 12, 135, 28) };
            btnTpl.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog { FileName = "Rubban_Team_Template.xlsx", Filter = "Excel (*.xlsx)|*.xlsx" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        ExcelRosterImporter.CreateBlankTemplate(sfd.FileName);
                        MessageBox.Show(isRtl ? "تم إنشاء القالب الثابت بنجاح (بدون أوفيس)." : "Blank template created (Office-free).");
                    }
                }
            };

            var lblName = new Label { Text = isRtl ? "اسم الفريق:" : "Team Name:", Bounds = new Rectangle(15, 52, 110, 24) };
            _txtTeamName.Bounds = new Rectangle(130, 49, 220, 26);

            var lblCoach = new Label { Text = isRtl ? "المدرب:" : "Coach:", Bounds = new Rectangle(365, 52, 65, 24) };
            _txtCoach.Bounds = new Rectangle(435, 49, 266, 26);

            var lblLogo = new Label { Text = isRtl ? "الشعار:" : "Logo:", Bounds = new Rectangle(15, 86, 110, 24) };
            _txtLogo.Bounds = new Rectangle(130, 83, 180, 26);
            var btnLogo = new Button { Text = "...", Bounds = new Rectangle(315, 83, 35, 26) };
            btnLogo.Click += (s, e) => BrowseImage(_txtLogo, "Assets\\Logos");

            var lblColor = new Label { Text = isRtl ? "اللون:" : "Color:", Bounds = new Rectangle(365, 86, 65, 24) };
            _txtColor.Bounds = new Rectangle(435, 83, 225, 26);
            var btnColor = new Button { Text = "...", Bounds = new Rectangle(666, 83, 35, 26) };
            btnColor.Click += (s, e) => BrowseImage(_txtColor, "Assets\\Colors");

            _grid.Bounds = new Rectangle(15, 122, 686, 385);
            _grid.AllowUserToAddRows = true;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("No", isRtl ? "الرقم" : "No");
            _grid.Columns.Add("Name", isRtl ? "اسم اللاعب" : "Player Name");
            _grid.Columns.Add("Role", isRtl ? "الدور (Main/Sub/GK)" : "Role (Main/Sub/GK)");
            _grid.Columns[0].FillWeight = 20;
            _grid.Columns[1].FillWeight = 55;
            _grid.Columns[2].FillWeight = 25;

            var btnSave = new Button { Text = isRtl ? "حفظ التعديلات" : "Save Changes", BackColor = Color.FromArgb(235, 130, 25), ForeColor = Color.White, Bounds = new Rectangle(15, 522, 135, 36) };
            btnSave.Click += (s, e) =>
            {
                var r = ReadForm();
                if (string.IsNullOrWhiteSpace(r.TeamName)) { MessageBox.Show(isRtl ? "أدخل اسم الفريق أولاً." : "Enter Team Name first."); return; }
                r.Save(_baseDir);
                RefreshSavedList(r.TeamName);
                MessageBox.Show(isRtl ? "تم حفظ الفريق بنجاح." : "Team saved.");
            };

            var btnUseA = new Button { Text = isRtl ? "تعيين كـ Team A" : "Set as TEAM A", BackColor = Color.LightGreen, Bounds = new Rectangle(420, 522, 135, 36) };
            btnUseA.Click += (s, e) => { SelectedForA = ReadForm(); SelectedForA.Save(_baseDir); DialogResult = DialogResult.OK; Close(); };

            var btnUseB = new Button { Text = isRtl ? "تعيين كـ Team B" : "Set as TEAM B", BackColor = Color.LightSkyBlue, Bounds = new Rectangle(566, 522, 135, 36) };
            btnUseB.Click += (s, e) => { SelectedForB = ReadForm(); SelectedForB.Save(_baseDir); DialogResult = DialogResult.OK; Close(); };

            Controls.AddRange(new Control[] {
                lblSaved, _cmbSaved, btnOpenFile, btnNew, btnTpl,
                lblName, _txtTeamName, lblCoach, _txtCoach,
                lblLogo, _txtLogo, btnLogo, lblColor, _txtColor, btnColor,
                _grid, btnSave, btnUseA, btnUseB
            });
            RefreshSavedList();
        }

        private void OpenExistingFile()
        {
            string teamsDir = Path.Combine(_baseDir, "Data", "Teams");
            Directory.CreateDirectory(teamsDir);
            using (var ofd = new OpenFileDialog
            {
                InitialDirectory = teamsDir,
                Filter = "Team Files (*.json;*.xlsx)|*.json;*.xlsx|JSON (*.json)|*.json|Excel (*.xlsx)|*.xlsx"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                try
                {
                    if (ofd.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        FillForm(ExcelRosterImporter.ImportRoster(ofd.FileName));
                    else
                        FillForm(JsonConvert.DeserializeObject<TeamRoster>(File.ReadAllText(ofd.FileName)));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
            }
        }

        private void BrowseImage(TextBox target, string subDir)
        {
            string initDir = Path.Combine(_baseDir, subDir);
            Directory.CreateDirectory(initDir);
            using (var ofd = new OpenFileDialog { InitialDirectory = initDir, Filter = "Images (*.png;*.jpg)|*.png;*.jpg|All|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                    target.Text = ofd.FileName.Replace("\\", "/");
            }
        }

        private void RefreshSavedList(string selectName = null)
        {
            _savedTeams = TeamRoster.LoadAll(_baseDir);
            _cmbSaved.Items.Clear();
            foreach (var t in _savedTeams) _cmbSaved.Items.Add(t.TeamName);
            if (!string.IsNullOrEmpty(selectName) && _cmbSaved.Items.Contains(selectName))
                _cmbSaved.SelectedItem = selectName;
        }

        private void LoadSelectedTeam()
        {
            if (_cmbSaved.SelectedIndex >= 0 && _cmbSaved.SelectedIndex < _savedTeams.Count)
                FillForm(_savedTeams[_cmbSaved.SelectedIndex]);
        }

        private void FillForm(TeamRoster r)
        {
            if (r == null) return;
            _txtTeamName.Text = r.TeamName;
            _txtCoach.Text = r.Coach;
            _txtLogo.Text = r.LogoPath;
            _txtColor.Text = r.ColorPath;
            _grid.Rows.Clear();
            foreach (var p in r.Players)
                _grid.Rows.Add(p.Number, p.Name, p.Role);
        }

        private TeamRoster ReadForm()
        {
            var r = new TeamRoster
            {
                TeamName = _txtTeamName.Text.Trim(),
                Coach = _txtCoach.Text.Trim(),
                LogoPath = _txtLogo.Text.Trim(),
                ColorPath = _txtColor.Text.Trim()
            };
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                string no = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
                string nm = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
                string rl = Convert.ToString(row.Cells[2].Value)?.Trim() ?? "Main";
                if (!string.IsNullOrWhiteSpace(no) || !string.IsNullOrWhiteSpace(nm))
                    r.Players.Add(new PlayerEntry { Number = no, Name = nm, Role = string.IsNullOrWhiteSpace(rl) ? "Main" : rl });
            }
            return r;
        }
    }
}
