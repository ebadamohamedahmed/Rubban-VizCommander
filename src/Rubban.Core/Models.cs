using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rubban.Core
{
    public class WorkstationProfile
    {
        public string Name { get; set; } = "viz_6800";
        public string Description { get; set; } = "Viz Engine Local Preview (Port 6800)";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 6800;
        public string Terminator { get; set; } = "\0";
        public string SceneRoot { get; set; } = "2024_SPORTS";
        public string ControlMode { get; set; } = "VizEngine";
        public List<string> MatchMachineNames { get; set; } = new List<string>();
    }

    public class ProfilesConfig
    {
        public string DefaultProfile { get; set; } = "viz_6800";
        public int PromptTimeoutSeconds { get; set; } = 3;
        public List<WorkstationProfile> Profiles { get; set; } = new List<WorkstationProfile>();

        public static ProfilesConfig LoadOrCreate(string baseDir)
        {
            string path = Path.Combine(baseDir, "Profiles", "profiles.json");
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<ProfilesConfig>(File.ReadAllText(path));

            var cfg = new ProfilesConfig
            {
                DefaultProfile = "viz_6800",
                PromptTimeoutSeconds = 3,
                Profiles = new List<WorkstationProfile>
                {
                    new WorkstationProfile { Name = "viz_6800", Description = "Viz Engine Preview (Port 6800)", Host = "127.0.0.1", Port = 6800, Terminator = "\0", SceneRoot = "2024_SPORTS", ControlMode = "VizEngine" },
                    new WorkstationProfile { Name = "viz_6100", Description = "Viz Engine Program (Port 6100)", Host = "127.0.0.1", Port = 6100, Terminator = "\0", SceneRoot = "2024_SPORTS", ControlMode = "VizEngine" },
                    new WorkstationProfile { Name = "trio_6200", Description = "Viz Trio Macro Socket (Port 6200)", Host = "127.0.0.1", Port = 6200, Terminator = "\r\n", SceneRoot = "2024_SPORTS", ControlMode = "VizTrio" }
                }
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(cfg, Formatting.Indented));
            return cfg;
        }
    }

    public class SportDefinition
    {
        public string Id { get; set; } = "basketball";
        public string DisplayNameEn { get; set; } = "Basketball";
        public string DisplayNameAr { get; set; } = "كرة السلة";
        public string SceneSubfolder { get; set; } = "BASKETBALL";
        public string VizTemplateFile { get; set; } = "viz_basketball.json";
        public string TrioTemplateFile { get; set; } = "trio_basketball.json";
        public string PreferredControlMode { get; set; } = "";
        public int PreferredPort { get; set; } = 0;
        public List<int> ScoreDeltas { get; set; } = new List<int>();
        public List<string> Periods { get; set; } = new List<string>();
        public List<string> PeriodsEn { get; set; } = new List<string>();
        public List<string> PeriodsAr { get; set; } = new List<string>();

        public static List<SportDefinition> LoadAll(string baseDir)
        {
            var list = new List<SportDefinition>();
            string dir = Path.Combine(baseDir, "Sports");
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
                {
                    try
                    {
                        var sport = JsonConvert.DeserializeObject<SportDefinition>(File.ReadAllText(file));
                        if (sport != null) list.Add(sport);
                    }
                    catch { }
                }
            }
            if (list.Count == 0) list.Add(new SportDefinition());
            return list;
        }
    }

    public class LocalizationManager
    {
        private Dictionary<string, string> _strings = new Dictionary<string, string>();
        public string CurrentLang { get; private set; } = "en";
        public bool IsRtl => CurrentLang.Equals("ar", StringComparison.OrdinalIgnoreCase);

        public void Load(string baseDir, string langCode)
        {
            CurrentLang = langCode;
            string path = Path.Combine(baseDir, "Lang", $"{langCode}.json");
            if (File.Exists(path))
                _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
        }

        public string T(string key, string fallback = null)
            => _strings.TryGetValue(key, out var val) ? val : (fallback ?? key);
    }

    public class MatchState
    {
        public string ActiveSportId { get; set; } = "basketball";
        public string ActiveLang { get; set; } = "en";
        public string Tournament { get; set; } = "CHAMPIONSHIP";
        public string Place { get; set; } = "ARENA";
        public string TeamAName { get; set; } = "TEAM A";
        public string TeamBName { get; set; } = "TEAM B";
        public string CoachA { get; set; } = "";
        public string CoachB { get; set; } = "";
        public string TeamALogo { get; set; } = "";
        public string TeamBLogo { get; set; } = "";
        public string TeamAColor { get; set; } = "";
        public string TeamBColor { get; set; } = "";
        public int ScoreA { get; set; } = 0;
        public int ScoreB { get; set; } = 0;
        public string Period { get; set; } = "1st";
        public string Ref1 { get; set; } = "";
        public string Ref2 { get; set; } = "";
        public string Ref3 { get; set; } = "";

        public void SaveAtomic(string baseDir)
        {
            string dir = Path.Combine(baseDir, "Data");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "live_state.json");
            string temp = target + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
        }

        public static MatchState Load(string baseDir)
        {
            string target = Path.Combine(baseDir, "Data", "live_state.json");
            if (File.Exists(target))
            {
                try { return JsonConvert.DeserializeObject<MatchState>(File.ReadAllText(target)) ?? new MatchState(); }
                catch { }
            }
            return new MatchState();
        }
    }

    public class PlayerEntry
    {
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "Main"; // Main / Sub / GK
    }

    public class TeamRoster
    {
        public string TeamName { get; set; } = "";
        public string Coach { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string ColorPath { get; set; } = "";
        public List<PlayerEntry> Players { get; set; } = new List<PlayerEntry>();

        public void Save(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(TeamName)) return;
            string dir = Path.Combine(baseDir, "Data", "Teams");
            Directory.CreateDirectory(dir);
            string safe = string.Concat(TeamName.Split(Path.GetInvalidFileNameChars()));
            File.WriteAllText(Path.Combine(dir, safe + ".json"), JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static List<TeamRoster> LoadAll(string baseDir)
        {
            var list = new List<TeamRoster>();
            string dir = Path.Combine(baseDir, "Data", "Teams");
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x))
            {
                try { var t = JsonConvert.DeserializeObject<TeamRoster>(File.ReadAllText(f)); if (t != null) list.Add(t); }
                catch { }
            }
            return list;
        }
    }
}

