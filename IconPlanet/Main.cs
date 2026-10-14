using System;
using System.Reflection;
using HarmonyLib;
using ModLoader;
using UnityEngine;

namespace PlanetIcons
{
    public class Main : Mod
    {
        public override string ModNameID => "IconPlanet";
        public override string DisplayName => "Icon Planet";
        public override string Author => "Luna Tomato";
        public override string MinimumGameVersionNecessary => "1.5.9.8";
        public override string ModVersion => "v1.0.0";

        public override string Description =>
            "This mod nice the planet icons on the map. Press \"P\" on your keyboard to switch modes\nThis mod supports custom solar systems";

        public override string IconLink =>
            "https://raw.githubusercontent.com/tomato9946/icon-sfs-/refs/heads/main/icon-planet.png";

        public static Main Instance;

        public override Action LoadKeybindings => Keybindings.LoadKeybindings;

        public override void Early_Load()
        {
            Instance = this;
        }

        public override void Load()
        {
            Instance = this;
            IconConfig.Load(ModFolder);
            new Harmony(ModNameID).PatchAll(Assembly.GetExecutingAssembly());
            Debug.Log("[IconPlanet] " + ModVersion + " loaded");
        }
    }
}
