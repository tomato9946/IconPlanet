using System;
using SFS.UI.ModGUI;
using UnityEngine;

namespace PlanetIcons
{
    internal static class SettingsWindow
    {
        private static GameObject holder;
        private static int lastFrame = -100;
        private static bool broken;

        public static void Touch()
        {
            if (broken) return;
            lastFrame = Time.frameCount;
            try
            {
                if (holder == null) Create();
                else if (!holder.activeSelf) holder.SetActive(true);
            }
            catch (Exception e)
            {
                broken = true;
                Debug.LogWarning("[IconPlanet] settings window failed (use config.txt instead): " + e);
            }
        }

        public static void HideIfIdle()
        {
            if (holder != null && holder.activeSelf && Time.frameCount - lastFrame > 3) holder.SetActive(false);
        }

        private static void Create()
        {
            holder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "IconPlanetSettings");

            Window window = Builder.CreateWindow(holder.transform, Builder.GetRandomID(), 320, 190, 260, 420, true, true, 0.95f, "IconPlanet");
            window.CreateLayoutGroup(SFS.UI.ModGUI.Type.Vertical, TextAnchor.UpperLeft, 10f, new RectOffset(10, 10, 10, 10), true);

            Builder.CreateToggleWithLabel(window, 300, 40,
                () => IconConfig.RealPlanet,
                OnToggleReal, 0, 0, "Real Planet");

            Builder.CreateToggleWithLabel(window, 300, 40,
                () => IconConfig.Enabled,
                OnToggleEnabled, 0, 0, "Enable icons");

            Builder.CreateLabel(window, 300, 40, 0, 0, "Real Planet: full textures, water, clouds");
        }

        private static void OnToggleReal()
        {
            IconConfig.RealPlanet = !IconConfig.RealPlanet;
            IconConfig.Save("RealPlanet", IconConfig.RealPlanet ? "true" : "false");
            PlanetIconManager.Reset();
        }

        private static void OnToggleEnabled()
        {
            IconConfig.Enabled = !IconConfig.Enabled;
            IconConfig.Save("Enabled", IconConfig.Enabled ? "true" : "false");
        }
    }

    internal class SettingsWatcher : MonoBehaviour
    {
        private void Update()
        {
            SettingsWindow.HideIfIdle();
        }
    }
}
