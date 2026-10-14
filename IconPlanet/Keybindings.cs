using ModLoader;
using SFS.Input;
using UnityEngine;

namespace PlanetIcons
{
    public class Keybindings : ModKeybindings
    {
        public static Keybindings main;

        public static void LoadKeybindings()
        {
            main = SetupKeybindings<Keybindings>(Main.Instance);
        }

        public KeybindingsPC.Key CycleMode = KeyCode.P;
        private static readonly KeybindingsPC.Key DefaultCycleMode = KeyCode.P;

        public override void CreateUI()
        {
            CreateUI_Text("IconPlanet");
            CreateUI_Keybinding(CycleMode, DefaultCycleMode, "Mode (Smooth / High / Default)");
        }

        private static int lastPollFrame = -1;

        public static void Poll()
        {
            if (main == null || lastPollFrame == Time.frameCount) return;
            lastPollFrame = Time.frameCount;
            if (((I_Key)main.CycleMode).IsKeyDown()) IconConfig.CycleMode();
        }
    }
}
