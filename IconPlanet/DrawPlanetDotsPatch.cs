using System;
using HarmonyLib;
using SFS.World.Maps;
using UnityEngine;

namespace PlanetIcons
{
    [HarmonyPatch(typeof(MapManager), "DrawPlanetDots")]
    public static class DrawPlanetDotsPatch
    {
        private static bool errorLogged;

        static bool Prefix()
        {
            try
            {
                return !PlanetIconManager.DrawAll();
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[IconPlanet] DrawAll failed, falling back to the original dots: " + e);
                }
                IconConfig.Enabled = false;
                try { PlanetIconManager.HideAll(); } catch (Exception) { }
                return true;
            }
        }
    }
}
