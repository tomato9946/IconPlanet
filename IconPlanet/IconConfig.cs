using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PlanetIcons
{
    public static class IconConfig
    {
        public static bool Enabled = true;

        public static int TextureSize = 2048;

        public static float MinVisiblePx = 14f;

        public static float FadeStartPx = 1000f;

        public static bool FadeWhenZoomedOut = true;

        public static bool ShowClouds = true;

        public static int SortingOrder = 11;

        public static float TerrainExaggeration = 1f;

        public const int ModeSimple = 0, ModeReal = 1, ModeOff = 2; // Simple = Smooth, Real = High, Off = Default
        public static int Mode = ModeSimple;

        public static bool RealPlanet { get { return Mode == ModeReal; } }

        private static string ModeName(int mode)
        {
            return mode == ModeReal ? "high" : (mode == ModeOff ? "default" : "smooth");
        }

        public static string StatusText()
        {
            string dbg = DebugView == 1 ? "mask" : (DebugView == 2 ? "texture" : (DebugView == 3 ? "clouds" : "none"));
            return "IconPlanet " + Version + " " + ModeLabel(Mode);
        }

        private static string ModeLabel(int mode)
        {
            return mode == ModeReal ? "High" : (mode == ModeOff ? "Default" : "Smooth");
        }

        public static void CycleMode()
        {
            Mode = (Mode + 1) % 3;
            Enabled = Mode != ModeOff;
            Save("Mode", ModeName(Mode));
            PlanetIconManager.Reset();
            Debug.Log("[IconPlanet] mode -> " + ModeName(Mode));
            try { SFS.UI.MsgDrawer.main.Log("IconPlanet: " + ModeLabel(Mode)); }
            catch (Exception) { }
        }

        public static int WaterMaskMode = 0;

        public static bool HideGameMesh = false;

        public static bool RaiseGameWater = true;

        public static int DebugView = 0;

        public static bool ShowWater = true;
        public static bool ShowRings = true;

        public static bool FullDiscTexture = true;

        public const string Version = "3.1";
        public static string ConfigPath = "(not loaded)";
        public static bool ConfigFound;
        private static string folder;

        public static void Save(string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(folder)) return;
                string path = Path.Combine(folder, "config.txt");
                System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                if (File.Exists(path)) lines.AddRange(File.ReadAllLines(path));
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    if (l.StartsWith("#") || l.StartsWith("//")) continue;
                    int eq = l.IndexOf('=');
                    if (eq > 0 && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + value;
                        found = true;
                        break;
                    }
                }
                if (!found) lines.Add(key + "=" + value);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IconPlanet] config.txt save failed: " + e.Message);
            }
        }

        public static void Load(string modFolder)
        {
            folder = modFolder;
            ConfigPath = Path.Combine(modFolder, "config.txt");
            ConfigFound = File.Exists(ConfigPath);

            if (!ConfigFound)
            {
                try
                {
                    System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string>();
                    string asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(asmDir)) candidates.Add(asmDir);
                    if (!string.IsNullOrEmpty(modFolder))
                    {
                        DirectoryInfo parent = Directory.GetParent(modFolder);
                        if (parent != null) candidates.Add(Path.Combine(parent.FullName, "IconPlanet"));
                    }
                    foreach (string dir in candidates)
                    {
                        string p = Path.Combine(dir, "config.txt");
                        if (File.Exists(p)) { ConfigPath = p; ConfigFound = true; folder = dir; break; }
                    }
                }
                catch (Exception) { }
            }

            try
            {
                if (!ConfigFound) return;
                string path = ConfigPath;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    Apply(key, val);
                }

                if (!Enabled) Mode = ModeOff;
                Enabled = Mode != ModeOff;
                TextureSize = Mathf.Clamp(TextureSize, 64, 4096);
                MinVisiblePx = Mathf.Max(0f, MinVisiblePx);
                FadeStartPx = Mathf.Max(0f, FadeStartPx);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IconPlanet] config.txt read failed, using defaults: " + e.Message);
            }
        }

        private static void Apply(string key, string val)
        {
            switch (key.ToLowerInvariant())
            {
                case "enabled": Enabled = ParseBool(val, Enabled); break;
                case "texturesize": TextureSize = ParseInt(val, TextureSize); break;
                case "minvisiblepx": MinVisiblePx = ParseFloat(val, MinVisiblePx); break;
                case "terrainfadepx": FadeStartPx = ParseFloat(val, FadeStartPx); break;
                case "fadewhenzoomedout": FadeWhenZoomedOut = ParseBool(val, FadeWhenZoomedOut); break;
                case "showclouds": ShowClouds = ParseBool(val, ShowClouds); break;
                case "sortingorder": SortingOrder = ParseInt(val, SortingOrder); break;
                case "terrainexaggeration": TerrainExaggeration = ParseFloat(val, TerrainExaggeration); break;
                case "mode":
                    {
                        string v = val.ToLowerInvariant();
                        if (v == "high" || v == "real" || v == "1") Mode = ModeReal;
                        else if (v == "default" || v == "off" || v == "2") Mode = ModeOff;
                        else Mode = ModeSimple;
                        break;
                    }
                case "realplanet": if (ParseBool(val, false)) Mode = ModeReal; break;
                case "watermask":
                    {
                        string wv = val.ToLowerInvariant();
                        WaterMaskMode = wv == "dark" ? 1 : (wv == "light" ? 2 : (wv == "texture" ? 3 : 0));
                        break;
                    }
                case "raisegamewater": RaiseGameWater = ParseBool(val, RaiseGameWater); break;
                case "hidegamemesh": HideGameMesh = ParseBool(val, HideGameMesh); break;
                case "debugview":
                    {
                        string dv = val.ToLowerInvariant();
                        DebugView = dv == "mask" ? 1 : (dv == "texture" ? 2 : (dv == "clouds" ? 3 : 0));
                        break;
                    }
                case "showwater": ShowWater = ParseBool(val, ShowWater); break;
                case "showrings": ShowRings = ParseBool(val, ShowRings); break;
                case "fulldisctexture": FullDiscTexture = ParseBool(val, FullDiscTexture); break;
            }
        }

        private static bool ParseBool(string s, bool fallback)
        {
            bool b;
            return bool.TryParse(s, out b) ? b : fallback;
        }

        private static int ParseInt(string s, int fallback)
        {
            int i;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : fallback;
        }

        private static float ParseFloat(string s, float fallback)
        {
            float f;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : fallback;
        }
    }
}
