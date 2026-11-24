using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rubban.Core;

namespace Rubban.App
{
    public class DataEntryForm : Form
    {
        private readonly MatchState _state;
        private readonly string _baseDir;

        private readonly TextBox _txtTour = new TextBox();
        private readonly TextBox _txtPlace = new TextBox();
        private readonly TextBox _txtCoachA = new TextBox();
        private readonly TextBox _txtCoachB = new TextBox();
        private readonly TextBox _txtRef1 = new TextBox();
        private readonly TextBox _txtRef2 = new TextBox();
        private readonly TextBox _txtRef3 = new TextBox();
        private readonly TextBox _txtLogoA = new TextBox();
        private readonly TextBox _txtLogoB = new TextBox();
        private readonly TextBox _txtColorA = new TextBox();
        private readonly TextBox _txtColorB = new TextBox();

        public DataEntryForm(MatchState state, string baseDir, bool isRtl)
        {
            _state = state;
            _baseDir = baseDir;
            Text = isRtl ? "إدخال بيانات المباراة والشعارات" : "Match DATA Entry — Logos, Colors & Crew";
            Size = new Size(560, 470);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = isRtl;
            Font = new Font("Segoe UI", 9.5F);

            int y = 18;
            AddRow(isRtl ? "البطولة:" : "Tournament:", _txtTour, _state.Tournament, ref y);
            AddRow(isRtl ? "المكان:" : "Venue / Place:", _txtPlace, _state.Place, ref y);
            AddRow(isRtl ? "مدرب فريق A:" : "Coach A:", _txtCoachA, _state.CoachA, ref y);
            AddRow(isRtl ? "مدرب فريق B:" : "Coach B:", _txtCoachB, _state.CoachB, ref y);
            AddRow(isRtl ? "الحكم 1:" : "Referee 1:", _txtRef1, _state.Ref1, ref y);
            AddRow(isRtl ? "الحكم 2:" : "Referee 2:", _txtRef2, _state.Ref2, ref y);
            AddRow(isRtl ? "الحكم 3:" : "Referee 3:", _txtRef3, _state.Ref3, ref y);
            AddBrowseRow(isRtl ? "شعار فريق A:" : "Team A Logo:", _txtLogoA, _state.TeamALogo, "Assets\\Logos", ref y);
            AddBrowseRow(isRtl ? "شعار فريق B:" : "Team B Logo:", _txtLogoB, _state.TeamBLogo, "Assets\\Logos", ref y);
            AddBrowseRow(isRtl ? "لون فريق A:" : "Team A Color:", _txtColorA, _state.TeamAColor, "Assets\\Colors", ref y);
            AddBrowseRow(isRtl ? "لون فريق B:" : "Team B Color:", _txtColorB, _state.TeamBColor, "Assets\\Colors", ref y);

            var btnSave = new Button { Text = isRtl ? "حفظ وإغلاق" : "SAVE & CLOSE", BackColor = Color.FromArgb(235, 130, 25), ForeColor = Color.White, Bounds = new Rectangle(370, y + 8, 150, 34) };
            btnSave.Click += (s, e) =>
            {
                _state.Tournament = _txtTour.Text.Trim();
                _state.Place = _txtPlace.Text.Trim();
                _state.CoachA = _txtCoachA.Text.Trim();
                _state.CoachB = _txtCoachB.Text.Trim();
                _state.Ref1 = _txtRef1.Text.Trim();
                _state.Ref2 = _txtRef2.Text.Trim();
                _state.Ref3 = _txtRef3.Text.Trim();
                _state.TeamALogo = _txtLogoA.Text.Trim();
                _state.TeamBLogo = _txtLogoB.Text.Trim();
                _state.TeamAColor = _txtColorA.Text.Trim();
                _state.TeamBColor = _txtColorB.Text.Trim();
                _state.SaveAtomic(_baseDir);
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnSave);
        }

        private void AddRow(string label, TextBox box, string val, ref int y)
        {
            Controls.Add(new Label { Text = label, Bounds = new Rectangle(18, y + 3, 120, 24) });
            box.SetBounds(145, y, 375, 26);
            box.Text = val ?? "";
            Controls.Add(box);
            y += 33;
        }

        private void AddBrowseRow(string label, TextBox box, string val, string subDir, ref int y)
        {
            Controls.Add(new Label { Text = label, Bounds = new Rectangle(18, y + 3, 120, 24) });
            box.SetBounds(145, y, 295, 26);
            box.Text = val ?? "";
            var btn = new Button { Text = "...", Bounds = new Rectangle(448, y, 72, 26) };
            btn.Click += (s, e) =>
            {
                string initDir = Path.Combine(_baseDir, subDir);
                Directory.CreateDirectory(initDir);
                using (var ofd = new OpenFileDialog { InitialDirectory = initDir, Filter = "Images (*.png;*.jpg)|*.png;*.jpg|All|*.*" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                        box.Text = ofd.FileName.Replace("\\", "/");
                }
            };
            Controls.AddRange(new Control[] { box, btn });
            y += 33;
        }
    }

    public class MainForm : Form
    {
        private readonly string _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly WorkstationProfile _profile;
        private readonly List<SportDefinition> _sports;
        private readonly LocalizationManager _loc = new LocalizationManager();
        private TemplateEngine _templates;
        private readonly VizTcpClient _viz = new VizTcpClient();
        private MatchState _state;
        private SportDefinition _activeSport;
        private List<TeamRoster> _savedTeams = new List<TeamRoster>();

        private readonly TextBox _txtHost = new TextBox();
        private readonly TextBox _txtPort = new TextBox();
        private readonly Button _btnConnect = new Button();
        private readonly Button _btnLang = new Button();
        private readonly ComboBox _cmbSport = new ComboBox();
        private readonly Label _lblStatus = new Label();

        private readonly ComboBox _cmbTeamA = new ComboBox();
        private readonly ComboBox _cmbTeamB = new ComboBox();
        private readonly Label _lblScoreA = new Label();
        private readonly Label _lblScoreB = new Label();
        private readonly ComboBox _cmbPeriod = new ComboBox();
        private readonly FlowLayoutPanel _pnlButtonsA = new FlowLayoutPanel();
        private readonly FlowLayoutPanel _pnlButtonsB = new FlowLayoutPanel();

        private readonly Button _btnScoreLoad = new Button();
        private readonly Button _btnScoreIn = new Button();
        private readonly Button _btnScoreOut = new Button();
        private readonly Button _btnTitleCard = new Button();
        private readonly Button _btnTeamList = new Button();
        private readonly Button _btnCoach = new Button();
        private readonly Button _btnReferee = new Button();
        private readonly Button _btnDataEntry = new Button();
        private readonly Button _btnTeamsMgr = new Button();

        // Football Panel
        private readonly GroupBox _grpFootball = new GroupBox();
        private readonly Label _lblEt = new Label();
        private readonly TextBox _txtExtraTime = new TextBox();
        private readonly Label _lblPNo = new Label();
        private readonly TextBox _txtPlayerNo = new TextBox();
        private readonly Label _lblPName = new Label();
        private readonly TextBox _txtPlayerName = new TextBox();
        private readonly Label _lblONo = new Label();
        private readonly TextBox _txtOutNo = new TextBox();
        private readonly Label _lblOName = new Label();
        private readonly TextBox _txtOutName = new TextBox();
        private readonly Button _btnYH = new Button();
        private readonly Button _btnYA = new Button();
        private readonly Button _btnRH = new Button();
        private readonly Button _btnRA = new Button();
        private readonly Button _btnSH = new Button();
        private readonly Button _btnSA = new Button();

        // Penalty Panel
        private readonly GroupBox _grpPenalty = new GroupBox();
        private readonly CheckBox _chkExtraPen = new CheckBox();
        private readonly Button _btnPenShow = new Button();
        private readonly Button _btnPenReset = new Button();
        private readonly Label _lblPenHome = new Label();
        private readonly Label _lblPenAway = new Label();
        private readonly List<Button> _penButtons = new List<Button>();

        public MainForm(WorkstationProfile profile)
        {
            _profile = profile;
            _sports = SportDefinition.LoadAll(_baseDir);
            _state = MatchState.Load(_baseDir);

            Size = new Size(940, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildLayout();
            RefreshTeamDropdowns();
            ApplyLanguage(_state.ActiveLang);
            SelectSport(_state.ActiveSportId);

            _viz.ConnectionStateChanged += (ok, msg) =>
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)(() =>
                    {
                        _lblStatus.Text = (ok ? _loc.T("status_connected") : _loc.T("status_offline")) + " " + msg;
                        _lblStatus.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
                    }));
            };
        }

        private void BuildLayout()
        {
            var topBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.WhiteSmoke };
            _txtHost.SetBounds(12, 12, 115, 26);
            _txtPort.SetBounds(133, 12, 55, 26);
            _btnConnect.SetBounds(194, 10, 90, 30);
            _btnConnect.Click += (s, e) => { if (int.TryParse(_txtPort.Text, out int p)) _viz.Start(_txtHost.Text.Trim(), p, _viz.Terminator); };

            _cmbSport.SetBounds(296, 12, 175, 26);
            _cmbSport.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSport.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbSport.SelectedIndex >= 0 && _cmbSport.SelectedIndex < _sports.Count)
                    ApplySport(_sports[_cmbSport.SelectedIndex]);
            };

            _btnLang.SetBounds(480, 10, 85, 30);
            _btnLang.Click += (s, e) => ApplyLanguage(_loc.IsRtl ? "en" : "ar");

            _lblStatus.SetBounds(575, 15, 335, 22);
            topBar.Controls.AddRange(new Control[] { _txtHost, _txtPort, _btnConnect, _cmbSport, _btnLang, _lblStatus });

            _cmbTeamA.SetBounds(30, 70, 260, 28);
            _cmbTeamA.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbTeamA.SelectedIndexChanged += (s, e) => ApplySelectedTeam(true);

            _cmbTeamB.SetBounds(630, 70, 260, 28);
            _cmbTeamB.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbTeamB.SelectedIndexChanged += (s, e) => ApplySelectedTeam(false);

            _lblScoreA.SetBounds(30, 105, 260, 70);
            _lblScoreA.Font = new Font("Segoe UI", 36F, FontStyle.Bold);
            _lblScoreA.TextAlign = ContentAlignment.MiddleCenter;
            _lblScoreA.BackColor = Color.FromArgb(30, 30, 30);
            _lblScoreA.ForeColor = Color.FromArgb(235, 130, 25);

            _lblScoreB.SetBounds(630, 105, 260, 70);
            _lblScoreB.Font = new Font("Segoe UI", 36F, FontStyle.Bold);
            _lblScoreB.TextAlign = ContentAlignment.MiddleCenter;
            _lblScoreB.BackColor = Color.FromArgb(30, 30, 30);
            _lblScoreB.ForeColor = Color.FromArgb(235, 130, 25);

            _pnlButtonsA.SetBounds(30, 182, 260, 92);
            _pnlButtonsB.SetBounds(630, 182, 260, 92);

            _cmbPeriod.SetBounds(385, 120, 150, 28);
            _cmbPeriod.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPeriod.SelectedIndexChanged += (s, e) => { if (_cmbPeriod.SelectedItem != null) { _state.Period = _cmbPeriod.SelectedItem.ToString(); PushScoreBar(); } };

            _btnScoreLoad.SetBounds(30, 285, 160, 38); _btnScoreLoad.Click += (s, e) => { SendTemplate("SCORE_BAR_LOAD"); PushScoreBar(); };
            _btnScoreIn.SetBounds(200, 285, 120, 38);  _btnScoreIn.BackColor = Color.LightGreen; _btnScoreIn.Click += (s, e) => SendTemplate("SCORE_BAR_IN");
            _btnScoreOut.SetBounds(330, 285, 120, 38); _btnScoreOut.BackColor = Color.LightCoral; _btnScoreOut.Click += (s, e) => SendTemplate("SCORE_BAR_OUT");

            _btnTitleCard.SetBounds(30, 332, 135, 36); _btnTitleCard.Click += (s, e) => SendTemplate("TITLE_CARD_SHOW");
            _btnTeamList.SetBounds(172, 332, 135, 36); _btnTeamList.Click += (s, e) => SendTemplate("TEAM_LIST_SHOW");
            _btnCoach.SetBounds(314, 332, 125, 36);    _btnCoach.Click += (s, e) => SendTemplate("COACH_SHOW");
            _btnReferee.SetBounds(446, 332, 125, 36);  _btnReferee.Click += (s, e) => SendTemplate("REFEREE_SHOW");

            _btnDataEntry.SetBounds(630, 285, 125, 38);
            _btnDataEntry.BackColor = Color.FromArgb(235, 130, 25);
            _btnDataEntry.ForeColor = Color.White;
            _btnDataEntry.Click += (s, e) => { using (var f = new DataEntryForm(_state, _baseDir, _loc.IsRtl)) f.ShowDialog(this); };

            _btnTeamsMgr.SetBounds(762, 285, 128, 38);
            _btnTeamsMgr.Click += (s, e) =>
            {
                using (var tf = new TeamRosterForm(_baseDir, _loc.IsRtl))
                {
                    tf.ShowDialog(this);
                    RefreshTeamDropdowns();
                    if (tf.SelectedForA != null) { ApplyRosterToState(true, tf.SelectedForA); }
                    if (tf.SelectedForB != null) { ApplyRosterToState(false, tf.SelectedForB); }
                }
            };

            // Football Panel
            _grpFootball.SetBounds(30, 380, 860, 118);
            _grpFootball.Visible = false;

            _lblEt.Bounds = new Rectangle(15, 28, 42, 24);
            _txtExtraTime.SetBounds(60, 25, 42, 26); _txtExtraTime.Text = "+2";
            _lblPNo.Bounds = new Rectangle(110, 28, 65, 24);
            _txtPlayerNo.SetBounds(178, 25, 45, 26); _txtPlayerNo.Text = "10";
            _lblPName.Bounds = new Rectangle(232, 28, 52, 24);
            _txtPlayerName.SetBounds(288, 25, 155, 26);
            _lblONo.Bounds = new Rectangle(452, 28, 55, 24);
            _txtOutNo.SetBounds(510, 25, 45, 26);
            _lblOName.Bounds = new Rectangle(564, 28, 72, 24);
            _txtOutName.SetBounds(640, 25, 155, 26);

            _btnYH.BackColor = Color.Gold; _btnYH.Bounds = new Rectangle(15, 66, 115, 34); _btnYH.Click += (s, e) => SendTemplate("YELLOW_HOME_SHOW");
            _btnYA.BackColor = Color.Gold; _btnYA.Bounds = new Rectangle(138, 66, 115, 34); _btnYA.Click += (s, e) => SendTemplate("YELLOW_AWAY_SHOW");
            _btnRH.BackColor = Color.IndianRed; _btnRH.ForeColor = Color.White; _btnRH.Bounds = new Rectangle(265, 66, 115, 34); _btnRH.Click += (s, e) => SendTemplate("RED_HOME_SHOW");
            _btnRA.BackColor = Color.IndianRed; _btnRA.ForeColor = Color.White; _btnRA.Bounds = new Rectangle(388, 66, 115, 34); _btnRA.Click += (s, e) => SendTemplate("RED_AWAY_SHOW");
            _btnSH.BackColor = Color.LightSkyBlue; _btnSH.Bounds = new Rectangle(520, 66, 120, 34); _btnSH.Click += (s, e) => SendTemplate("SUB_HOME_SHOW");
            _btnSA.BackColor = Color.LightSkyBlue; _btnSA.Bounds = new Rectangle(648, 66, 120, 34); _btnSA.Click += (s, e) => SendTemplate("SUB_AWAY_SHOW");

            _grpFootball.Controls.AddRange(new Control[] { _lblEt, _txtExtraTime, _lblPNo, _txtPlayerNo, _lblPName, _txtPlayerName, _lblONo, _txtOutNo, _lblOName, _txtOutName, _btnYH, _btnYA, _btnRH, _btnRA, _btnSH, _btnSA });

            // Penalty Panel
            _grpPenalty.SetBounds(30, 506, 860, 108);
            _grpPenalty.Visible = false;

            _chkExtraPen.SetBounds(15, 28, 135, 26);
            _btnPenShow.BackColor = Color.Orange; _btnPenShow.Bounds = new Rectangle(158, 24, 145, 32);
            _btnPenShow.Click += (s, e) => SendTemplate(_chkExtraPen.Checked ? "PENALTY_EXTRA_SHOW" : "PENALTY_SHOW");
            _btnPenReset.Bounds = new Rectangle(312, 24, 80, 32);
            _btnPenReset.Click += (s, e) => ResetPenalties();

            _lblPenHome.Bounds = new Rectangle(15, 68, 55, 22);
            _lblPenAway.Bounds = new Rectangle(320, 68, 55, 22);
            _grpPenalty.Controls.AddRange(new Control[] { _chkExtraPen, _btnPenShow, _btnPenReset, _lblPenHome, _lblPenAway });

            for (int i = 0; i < 5; i++) AddPenaltyCircle(15 + i, $"H{i + 1}", 75 + i * 44, 62);
            for (int i = 0; i < 5; i++) AddPenaltyCircle(10 + i, $"A{i + 1}", 380 + i * 44, 62);

            Controls.AddRange(new Control[] {
                topBar, _cmbTeamA, _cmbTeamB, _lblScoreA, _lblScoreB, _pnlButtonsA, _pnlButtonsB, _cmbPeriod,
                _btnScoreLoad, _btnScoreIn, _btnScoreOut, _btnTitleCard, _btnTeamList, _btnCoach, _btnReferee, _btnDataEntry, _btnTeamsMgr,
                _grpFootball, _grpPenalty
            });

            _txtHost.Text = _profile.Host;
            _txtPort.Text = _profile.Port.ToString();
            _cmbTeamA.Text = _state.TeamAName;
            _cmbTeamB.Text = _state.TeamBName;
            _lblScoreA.Text = _state.ScoreA.ToString();
            _lblScoreB.Text = _state.ScoreB.ToString();
        }

        private void RefreshTeamDropdowns()
        {
            _savedTeams = TeamRoster.LoadAll(_baseDir);
            string curA = _cmbTeamA.Text, curB = _cmbTeamB.Text;
            _cmbTeamA.Items.Clear(); _cmbTeamB.Items.Clear();
            foreach (var t in _savedTeams) { _cmbTeamA.Items.Add(t.TeamName); _cmbTeamB.Items.Add(t.TeamName); }
            _cmbTeamA.Text = curA; _cmbTeamB.Text = curB;
        }

        private void ApplySelectedTeam(bool isA)
        {
            string name = (isA ? _cmbTeamA.Text : _cmbTeamB.Text).Trim();
            var match = _savedTeams.FirstOrDefault(t => t.TeamName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null) ApplyRosterToState(isA, match);
        }

        private void ApplyRosterToState(bool isA, TeamRoster r)
        {
            if (isA)
            {
                _cmbTeamA.Text = r.TeamName; _state.TeamAName = r.TeamName;
                if (!string.IsNullOrWhiteSpace(r.Coach)) _state.CoachA = r.Coach;
                if (!string.IsNullOrWhiteSpace(r.LogoPath)) _state.TeamALogo = r.LogoPath;
                if (!string.IsNullOrWhiteSpace(r.ColorPath)) _state.TeamAColor = r.ColorPath;
            }
            else
            {
                _cmbTeamB.Text = r.TeamName; _state.TeamBName = r.TeamName;
                if (!string.IsNullOrWhiteSpace(r.Coach)) _state.CoachB = r.Coach;
                if (!string.IsNullOrWhiteSpace(r.LogoPath)) _state.TeamBLogo = r.LogoPath;
                if (!string.IsNullOrWhiteSpace(r.ColorPath)) _state.TeamBColor = r.ColorPath;
            }
            _state.SaveAtomic(_baseDir);
            PushScoreBar();
        }

        private void AddPenaltyCircle(int fieldId, string label, int x, int y)
        {
            var btn = new Button
            {
                Text = label,
                Tag = new Tuple<int, int>(fieldId, 0),
                BackColor = Color.White,
                Bounds = new Rectangle(x, y, 38, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btn.Click += (s, e) =>
            {
                var info = (Tuple<int, int>)btn.Tag;
                int next = (info.Item2 + 1) % 3;
                btn.Tag = new Tuple<int, int>(info.Item1, next);
                btn.BackColor = next == 1 ? Color.LimeGreen : next == 2 ? Color.Crimson : Color.White;
                btn.ForeColor = next == 0 ? Color.Black : Color.White;
                SendPenaltyCircle(info.Item1, next);
            };
            _penButtons.Add(btn);
            _grpPenalty.Controls.Add(btn);
        }

        private void SendPenaltyCircle(int fieldId, int state)
        {
            string rgb = state == 1 ? "0.0 1.0 0.0" : state == 2 ? "1.0 0.0 0.0" : "1.0 1.0 1.0";
            if (_viz.Terminator == "\r\n")
                _viz.EnqueueCommand($"page:set_property {fieldId} {rgb}");
            else
            {
                string scene = _chkExtraPen.Checked ? "penalty-extra" : "Penalty";
                string folder = _activeSport?.SceneSubfolder ?? "FUTSAL";
                _viz.EnqueueCommand($"1 SCENE*{_profile.SceneRoot}/{folder}/{scene}*TREE*$object*FUNCTION*ControlObject*in SET ON {fieldId} SET {rgb}");
            }
        }

        private void ResetPenalties()
        {
            foreach (var btn in _penButtons)
            {
                var info = (Tuple<int, int>)btn.Tag;
                btn.Tag = new Tuple<int, int>(info.Item1, 0);
                btn.BackColor = Color.White; btn.ForeColor = Color.Black;
                SendPenaltyCircle(info.Item1, 0);
            }
        }

        private void ApplyLanguage(string langCode)
        {
            _loc.Load(_baseDir, langCode);
            _state.ActiveLang = langCode;
            bool ar = _loc.IsRtl;

            RightToLeft = ar ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = ar;

            Text = $"{_loc.T("app_title")} — {_profile.Name}";
            _btnConnect.Text = _loc.T("btn_connect");
            _btnLang.Text = ar ? "English" : "العربية";
            _btnScoreLoad.Text = _loc.T("btn_score_load");
            _btnScoreIn.Text = _loc.T("btn_score_in");
            _btnScoreOut.Text = _loc.T("btn_score_out");
            _btnTitleCard.Text = _loc.T("btn_title_card");
            _btnTeamList.Text = _loc.T("btn_team_list");
            _btnCoach.Text = _loc.T("btn_coach");
            _btnReferee.Text = _loc.T("btn_referee");
            _btnDataEntry.Text = ar ? "بيانات المباراة" : "DATA ENTRY";
            _btnTeamsMgr.Text = ar ? "الفرق واللاعبين" : "TEAMS & ROSTER";

            // Localize Football & Penalty Panels
            _grpFootball.Text = ar ? "كرة القدم — الإنذارات والتبديلات والوقت الإضافي (تريو)" : "Football — Cards, Substitutions & Extra Time (Viz Trio)";
            _lblEt.Text = ar ? "إضافي:" : "ET:";
            _lblPNo.Text = ar ? "رقم/داخل:" : "In/Card #:";
            _lblPName.Text = ar ? "الاسم:" : "Name:";
            _lblONo.Text = ar ? "خارج #:" : "Out #:";
            _lblOName.Text = ar ? "اسم الخارج:" : "Out Name:";
            _btnYH.Text = ar ? "إنذار أرض" : "Yellow Home";
            _btnYA.Text = ar ? "إنذار ضيف" : "Yellow Away";
            _btnRH.Text = ar ? "طرد أرض" : "Red Home";
            _btnRA.Text = ar ? "طرد ضيف" : "Red Away";
            _btnSH.Text = ar ? "تبديل أرض" : "Sub Home";
            _btnSA.Text = ar ? "تبديل ضيف" : "Sub Away";

            _grpPenalty.Text = ar ? "ركلات الترجيح (اضغط الدائرة: أبيض ← أخضر هدف ← أحمر ضائعة)" : "Penalty Shootout (Click circle: White -> Green Goal -> Red Miss)";
            _chkExtraPen.Text = ar ? "ركلات إضافية" : "Extra Penalty";
            _btnPenShow.Text = ar ? "عرض ركلات الترجيح" : "SHOW PENALTY";
            _btnPenReset.Text = ar ? "تصفير" : "RESET";
            _lblPenHome.Text = ar ? "الأرض:" : "Home:";
            _lblPenAway.Text = ar ? "الضيف:" : "Away:";

            int prevIdx = _cmbSport.SelectedIndex;
            _cmbSport.Items.Clear();
            foreach (var sp in _sports) _cmbSport.Items.Add(ar ? sp.DisplayNameAr : sp.DisplayNameEn);
            if (prevIdx >= 0 && prevIdx < _cmbSport.Items.Count) _cmbSport.SelectedIndex = prevIdx;

            if (_activeSport != null) RefreshPeriods(_activeSport);
        }

        private void RefreshPeriods(SportDefinition sport)
        {
            var list = _loc.IsRtl
                ? (sport.PeriodsAr != null && sport.PeriodsAr.Count > 0 ? sport.PeriodsAr : sport.Periods)
                : (sport.PeriodsEn != null && sport.PeriodsEn.Count > 0 ? sport.PeriodsEn : sport.Periods);

            _cmbPeriod.Items.Clear();
            foreach (var p in list) _cmbPeriod.Items.Add(p);
            if (_cmbPeriod.Items.Count > 0)
                _cmbPeriod.SelectedIndex = _cmbPeriod.Items.Contains(_state.Period) ? _cmbPeriod.Items.IndexOf(_state.Period) : 0;
        }

        private void SelectSport(string sportId)
        {
            int idx = _sports.FindIndex(s => s.Id.Equals(sportId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            _cmbSport.SelectedIndex = idx;
            ApplySport(_sports[idx]);
        }

        private void ApplySport(SportDefinition sport)
        {
            _activeSport = sport;
            _state.ActiveSportId = sport.Id;

            string effectiveMode = !string.IsNullOrWhiteSpace(sport.PreferredControlMode) ? sport.PreferredControlMode : _profile.ControlMode;
            bool isTrio = string.Equals(effectiveMode, "VizTrio", StringComparison.OrdinalIgnoreCase);

            string tplFile = isTrio ? sport.TrioTemplateFile : sport.VizTemplateFile;
            _templates = new TemplateEngine(Path.Combine(_baseDir, "Templates", tplFile));

            int targetPort = sport.PreferredPort > 0 ? sport.PreferredPort : _profile.Port;
            string targetTerm = isTrio ? "\r\n" : "\0";
            if (_viz.Port != targetPort || _viz.Terminator != targetTerm)
            {
                _txtPort.Text = targetPort.ToString();
                _viz.Start(_txtHost.Text.Trim(), targetPort, targetTerm);
            }

            _pnlButtonsA.Controls.Clear();
            _pnlButtonsB.Controls.Clear();
            bool isBigMode = sport.ScoreDeltas.Count <= 2;
            Size btnSize = isBigMode ? new Size(122, 52) : new Size(78, 38);
            Font btnFont = isBigMode ? new Font("Segoe UI", 14F, FontStyle.Bold) : new Font("Segoe UI", 10F, FontStyle.Bold);

            foreach (int delta in sport.ScoreDeltas)
            {
                string lbl = (delta > 0 ? "+" : "") + delta;
                Color bg = delta > 0 ? Color.FromArgb(230, 245, 230) : Color.FromArgb(250, 230, 230);

                var bA = new Button { Text = lbl, Size = btnSize, Font = btnFont, BackColor = bg };
                bA.Click += (s, e) => AdjustScore(true, delta);
                _pnlButtonsA.Controls.Add(bA);

                var bB = new Button { Text = lbl, Size = btnSize, Font = btnFont, BackColor = bg };
                bB.Click += (s, e) => AdjustScore(false, delta);
                _pnlButtonsB.Controls.Add(bB);
            }

            RefreshPeriods(sport);

            bool isFootball = sport.Id.Equals("football", StringComparison.OrdinalIgnoreCase);
            bool isFutsal   = sport.Id.Equals("futsal", StringComparison.OrdinalIgnoreCase);

            _grpFootball.Visible = isFootball;
            _grpPenalty.Visible  = isFootball || isFutsal;

            if (isFutsal) _grpPenalty.Top = 380;
            else if (isFootball) _grpPenalty.Top = 506;

            ClientSize = isFootball ? new Size(920, 630)
                       : isFutsal   ? new Size(920, 505)
                                    : new Size(920, 390);
        }

        private void AdjustScore(bool isTeamA, int delta)
        {
            if (isTeamA) _state.ScoreA = Math.Max(0, _state.ScoreA + delta);
            else _state.ScoreB = Math.Max(0, _state.ScoreB + delta);

            _state.TeamAName = _cmbTeamA.Text.Trim();
            _state.TeamBName = _cmbTeamB.Text.Trim();
            _lblScoreA.Text = _state.ScoreA.ToString();
            _lblScoreB.Text = _state.ScoreB.ToString();
            _state.SaveAtomic(_baseDir);
            PushScoreBar();
        }

        private void PushScoreBar() => SendTemplate("SCORE_BAR_UPDATE");

        private void SendTemplate(string key)
        {
            if (_templates == null) return;
            var tokens = new Dictionary<string, string>
            {
                ["SceneRoot"]   = _profile.SceneRoot,
                ["SportFolder"] = _activeSport?.SceneSubfolder ?? "BASKETBALL",
                ["TeamAName"]   = _cmbTeamA.Text.Trim(),
                ["TeamBName"]   = _cmbTeamB.Text.Trim(),
                ["TeamName"]    = _cmbTeamA.Text.Trim(),
                ["TeamALogo"]   = _state.TeamALogo,
                ["TeamBLogo"]   = _state.TeamBLogo,
                ["TeamLogo"]    = _state.TeamALogo,
                ["TeamAColor"]  = _state.TeamAColor,
                ["TeamBColor"]  = _state.TeamBColor,
                ["TeamColor"]   = _state.TeamAColor,
                ["CoachA"]      = _state.CoachA,
                ["CoachB"]      = _state.CoachB,
                ["CoachName"]   = _state.CoachA,
                ["Ref1"]        = _state.Ref1,
                ["Ref2"]        = _state.Ref2,
                ["Ref3"]        = _state.Ref3,
                ["ScoreA"]      = _state.ScoreA.ToString(),
                ["ScoreB"]      = _state.ScoreB.ToString(),
                ["Period"]      = _state.Period,
                ["ExtraTime"]   = _txtExtraTime.Text.Trim(),
                ["PlayerNo"]    = _txtPlayerNo.Text.Trim(),
                ["PlayerName"]  = _txtPlayerName.Text.Trim(),
                ["InNo"]        = _txtPlayerNo.Text.Trim(),
                ["InName"]      = _txtPlayerName.Text.Trim(),
                ["OutNo"]       = _txtOutNo.Text.Trim(),
                ["OutName"]     = _txtOutName.Text.Trim(),
                ["Tournament"]  = _state.Tournament,
                ["Place"]       = _state.Place
            };
            foreach (var cmd in _templates.Render(key, tokens))
                _viz.EnqueueCommand(cmd);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _state.SaveAtomic(_baseDir);
            _viz.Dispose();
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var cfg = ProfilesConfig.LoadOrCreate(baseDir);

            string req = cfg.DefaultProfile;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--profile") req = args[i + 1];

            var profile = cfg.Profiles.FirstOrDefault(p => p.Name.Equals(req, StringComparison.OrdinalIgnoreCase)) ?? cfg.Profiles.First();
            Application.Run(new MainForm(profile));
        }
    }
}
