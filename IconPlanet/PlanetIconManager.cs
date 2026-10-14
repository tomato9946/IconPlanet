using System;
using System.Collections.Generic;
using System.Threading;
using SFS.World.Maps;
using SFS.WorldBase;
using UnityEngine;

namespace PlanetIcons
{
    internal static class IconWorker
    {
        private static readonly Queue<IconJob> queue = new Queue<IconJob>();
        private static readonly object gate = new object();
        private static Thread thread;
        private static int pending;

        public static int PendingCount { get { lock (gate) { return pending; } } }

        public static void Enqueue(IconJob job)
        {
            lock (gate)
            {
                queue.Enqueue(job);
                pending++;
                if (thread == null)
                {
                    thread = new Thread(Loop);
                    thread.IsBackground = true;
                    thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                    thread.Name = "IconPlanetWorker";
                    thread.Start();
                }
                Monitor.Pulse(gate);
            }
        }

        private static void Loop()
        {
            while (true)
            {
                IconJob job;
                lock (gate)
                {
                    while (queue.Count == 0) Monitor.Wait(gate);
                    job = queue.Dequeue();
                }
                try { job.Run(); }
                catch (Exception) { }
                lock (gate) { pending--; }
            }
        }
    }

    internal class WaterRaise
    {
        public Renderer renderer;
        public int layerId;
        public int order;
    }

    internal class IconInstance
    {
        public GameObject go;
        public SpriteRenderer renderer;
        public GameObject cloudGo;
        public SpriteRenderer cloudRenderer;
        public GeneratedIcon icon;
        public IconJob job;
        public int sortFrame = -1000;
        public int builtSize;
        public int pendingSize;
        public List<Renderer> hiddenMeshes;
        public List<WaterRaise> raisedWater;
        public bool sortLogged;
        public bool failed;
        public int usedFrame = -1;
    }

    public static class PlanetIconManager
    {
        private static readonly Dictionary<Planet, IconInstance> icons = new Dictionary<Planet, IconInstance>();
        private const int MaxPreparePerFrame = 1;
        private const int MaxFinishPerFrame = 1;
        private const int MaxConcurrentJobs = 2;
        private static float lastBuildTime = -100f;
        private static int prepareFrame = -1, preparedThisFrame;
        private static int finishFrame = -1, finishedThisFrame;

        private static Camera cachedCamera;
        private static int cameraFrame = -1;

        public static bool DrawAll()
        {
            Keybindings.Poll();

            if (!IconConfig.Enabled)
            {
                HideAll();
                return false;
            }

            int frame = Time.frameCount;
            double viewDistance = Map.view.view.distance.Value;

            foreach (Planet planet in SFS.Base.planetLoader.planets.Values)
            {
                float gameAlpha = 1f;
                if (IconConfig.FadeWhenZoomedOut)
                {
                    if (!planet.data.basics.significant)
                    {
                        gameAlpha = Mathf.Lerp(0.3f, 1f,
                            MapDrawer.GetFadeOut(viewDistance, planet.Radius * 30000.0, planet.Radius * 30000.0 * 1.5));
                    }
                    if (planet.HasParent)
                    {
                        double m = Math.Min(planet.orbit.apoapsis, planet.orbit.Planet.SOI);
                        gameAlpha *= MapDrawer.GetFadeIn(viewDistance / 70.0, m, m * 0.75);
                    }
                }

                float dotFraction;
                if (!TryDrawIcon(planet, 1f, frame, out dotFraction))
                {
                    if (gameAlpha > 0f) DrawFallbackDot(planet, gameAlpha);
                }
                else if (dotFraction > 0.002f && gameAlpha > 0f)
                {
                    DrawFallbackDot(planet, gameAlpha * dotFraction);
                }
            }

            List<Planet> dead = null;
            foreach (KeyValuePair<Planet, IconInstance> kv in icons)
            {
                IconInstance inst = kv.Value;
                if (kv.Key == null)
                {
                    if (dead == null) dead = new List<Planet>();
                    dead.Add(kv.Key);
                    continue;
                }
                if (inst.usedFrame != frame && inst.go != null && inst.go.activeSelf)
                {
                    inst.go.SetActive(false);
                    RestoreMeshes(inst);
                }
            }
            if (dead != null)
            {
                foreach (Planet p in dead)
                {
                    DestroyInstance(icons[p]);
                    icons.Remove(p);
                }
            }

            return true;
        }

        private static void FitSorting(Planet planet, IconInstance inst)
        {
            int bestLayerVal = int.MinValue, bestOrder = int.MinValue, bestLayerId = 0;
            bool found = false;
            bool log = !inst.sortLogged;
            inst.sortLogged = true;
            int logged = 0;
            List<Renderer> gameMeshes = new List<Renderer>();
            List<Renderer> waterMeshes = new List<Renderer>();
            int terrLayerVal = int.MinValue, terrOrder = int.MinValue, terrLayerId = 0;
            bool terrFound = false;

            try
            {
                Renderer[] all = planet.mapHolder.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in all)
                {
                    if (r == null || r == inst.renderer || r == inst.cloudRenderer) continue;

                    bool skip = r is LineRenderer || r is TrailRenderer || r.GetType().Name == "ParticleSystemRenderer";

                    if (log && logged < 6)
                    {
                        logged++;
                    }
                    if (skip || !r.enabled) continue;
                    gameMeshes.Add(r);

                    bool isWater = r.gameObject.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isWater) waterMeshes.Add(r);
                    else
                    {
                        int tlv = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
                        if (tlv > terrLayerVal || (tlv == terrLayerVal && r.sortingOrder > terrOrder))
                        {
                            terrLayerVal = tlv; terrOrder = r.sortingOrder; terrLayerId = r.sortingLayerID; terrFound = true;
                        }
                    }

                    int lv = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
                    if (lv > bestLayerVal || (lv == bestLayerVal && r.sortingOrder > bestOrder))
                    {
                        bestLayerVal = lv; bestOrder = r.sortingOrder; bestLayerId = r.sortingLayerID; found = true;
                    }
                }
            }
            catch (Exception e)
            {
                if (log) Debug.LogWarning("[IconPlanet] sorting scan failed: " + e.Message);
            }

            if (IconConfig.HideGameMesh)
            {
                if (inst.hiddenMeshes == null) inst.hiddenMeshes = new List<Renderer>();
                foreach (Renderer gm in gameMeshes)
                {
                    if (gm != null && !gm.forceRenderingOff) { gm.forceRenderingOff = true; inst.hiddenMeshes.Add(gm); }
                }
            }
            else RestoreMeshes(inst);

            int waterOrderUsed = int.MinValue;
            if (IconConfig.RaiseGameWater && terrFound)
            {
                if (inst.raisedWater == null) inst.raisedWater = new List<WaterRaise>();
                foreach (Renderer wr in waterMeshes)
                {
                    if (wr == null) continue;
                    bool known = false;
                    foreach (WaterRaise wrr in inst.raisedWater) { if (wrr.renderer == wr) { known = true; break; } }
                    if (!known)
                    {
                        WaterRaise rec = new WaterRaise();
                        rec.renderer = wr; rec.layerId = wr.sortingLayerID; rec.order = wr.sortingOrder;
                        inst.raisedWater.Add(rec);
                    }
                    wr.sortingLayerID = terrLayerId;
                    wr.sortingOrder = terrOrder + 1;
                    waterOrderUsed = terrOrder + 1;
                }
            }
            else RestoreWater(inst);

            if (found)
                inst.renderer.sortingLayerID = bestLayerId;
            else
                inst.renderer.sortingLayerName = "Map";
            inst.renderer.sortingOrder = Mathf.Max(IconConfig.SortingOrder, waterOrderUsed + 1);

            if (inst.cloudRenderer != null)
            {
                inst.cloudRenderer.sortingLayerID = inst.renderer.sortingLayerID;
                inst.cloudRenderer.sortingOrder = inst.renderer.sortingOrder + 1;
            }
        }

        private static void RestoreWater(IconInstance inst)
        {
            if (inst == null || inst.raisedWater == null) return;
            foreach (WaterRaise w in inst.raisedWater)
            {
                if (w.renderer != null) { w.renderer.sortingLayerID = w.layerId; w.renderer.sortingOrder = w.order; }
            }
            inst.raisedWater.Clear();
        }

        private static void RestoreMeshes(IconInstance inst)
        {
            if (inst == null || inst.hiddenMeshes == null) return;
            foreach (Renderer r in inst.hiddenMeshes) { if (r != null) r.forceRenderingOff = false; }
            inst.hiddenMeshes.Clear();
        }

        public static void Reset()
        {
            foreach (KeyValuePair<Planet, IconInstance> kv in icons) DestroyInstance(kv.Value);
            icons.Clear();
        }

        public static void HideAll()
        {
            foreach (KeyValuePair<Planet, IconInstance> kv in icons)
            {
                if (kv.Value.go != null && kv.Value.go.activeSelf) kv.Value.go.SetActive(false);
                RestoreMeshes(kv.Value);
            }
        }

        private static bool TryDrawIcon(Planet planet, float alpha, int frame, out float dotAlpha)
        {
            dotAlpha = 0f;
            if (planet.mapHolder == null) return false;

            IconInstance inst;
            if (!icons.TryGetValue(planet, out inst))
            {
                inst = new IconInstance();
                icons[planet] = inst;
            }
            if (inst.failed) return false;

            if (inst.go == null && inst.icon != null)
            {
                inst.icon.DestroyAll();
                inst.icon = null;
                inst.renderer = null;
                inst.cloudGo = null;
                inst.cloudRenderer = null;
            }

            float unitsPerPixel = GetUnitsPerPixel(planet.mapHolder.position);
            float realRadiusPx = (float)(planet.Radius / 1000.0) / unitsPerPixel;

            if (inst.icon != null && inst.job == null && inst.builtSize < IconConfig.TextureSize)
            {
                int want = WantedSize(realRadiusPx, inst.icon.discFraction);
                if (want >= inst.builtSize * 1.2f && IsOnScreen(planet, realRadiusPx))
                {
                    TryStartJob(planet, inst, want, frame, true);
                }
            }

            if (inst.job != null)
            {
                if (inst.job.done)
                {
                    bool upgrade = inst.icon != null;
                    GeneratedIcon fresh = TryFinishJob(planet, inst, frame, upgrade);
                    if (fresh != null)
                    {
                        if (upgrade) SwapIcon(inst, fresh);
                        else { inst.icon = fresh; inst.builtSize = inst.pendingSize; }
                    }
                }
            }
            else if (inst.icon == null)
            {
                float startPx = Mathf.Max(2f, IconConfig.MinVisiblePx * 0.5f);
                if (2f * realRadiusPx < startPx) return false;
                if (!IsOnScreen(planet, realRadiusPx)) return false;
                TryStartJob(planet, inst, WantedSize(realRadiusPx, 0.9f), frame, false);
            }
            if (inst.icon == null) return false;

            if (inst.go == null)
            {
                inst.go = new GameObject("PlanetIcon_" + planet.codeName);
                inst.go.transform.SetParent(planet.mapHolder, false);
                inst.go.transform.localPosition = Vector3.zero;

                int mapLayer = LayerMask.NameToLayer("Map");
                inst.go.layer = mapLayer >= 0 ? mapLayer : planet.mapHolder.gameObject.layer;

                inst.renderer = inst.go.AddComponent<SpriteRenderer>();
                inst.renderer.sprite = inst.icon.sprite;
                inst.renderer.sortingLayerName = "Map";

                if (inst.icon.cloudSprite != null)
                {
                    inst.cloudGo = new GameObject("PlanetClouds_" + planet.codeName);
                    inst.cloudGo.transform.SetParent(inst.go.transform, false);
                    inst.cloudGo.transform.localPosition = Vector3.zero;
                    inst.cloudGo.layer = inst.go.layer;
                    inst.cloudRenderer = inst.cloudGo.AddComponent<SpriteRenderer>();
                    inst.cloudRenderer.sprite = inst.icon.cloudSprite;
                    inst.cloudRenderer.sortingLayerName = "Map";
                }
            }
            if (frame - inst.sortFrame >= 600)
            {
                inst.sortFrame = frame;
                FitSorting(planet, inst);
            }

            float disc = Mathf.Max(0.05f, inst.icon.discFraction);
            float iconPx = 2f * realRadiusPx / disc;

            float show = 1f;
            if (IconConfig.MinVisiblePx > 0f)
            {
                show = Mathf.InverseLerp(IconConfig.MinVisiblePx * 0.5f, IconConfig.MinVisiblePx, 2f * realRadiusPx);
                dotAlpha = alpha * (1f - show);
            }

            // Fade toward real surface when zoomed in (default ~1000px radius → gone at 1.5x)
            float fade = 1f;
            if (IconConfig.FadeStartPx > 0f)
            {
                float start = IconConfig.FadeStartPx;
                float end = start * 1.5f;
                if (realRadiusPx >= end)
                {
                    if (inst.go != null && inst.go.activeSelf) inst.go.SetActive(false);
                    RestoreMeshes(inst);
                    RestoreWater(inst);
                    inst.usedFrame = frame;
                    return true;
                }
                float t = Mathf.InverseLerp(start, end, realRadiusPx);
                fade = 1f - Mathf.SmoothStep(0f, 1f, t);
            }
            float a = alpha * show * Mathf.Clamp01(fade);
            if (!(a > 0.002f))
            {
                if (inst.go != null && inst.go.activeSelf) inst.go.SetActive(false);
                RestoreMeshes(inst);
                RestoreWater(inst);
                inst.usedFrame = frame;
                return true;
            }

            float parentScale = planet.mapHolder.lossyScale.x;
            if (!(parentScale > 1e-6f)) parentScale = 1f;
            inst.go.transform.localScale = Vector3.one * (iconPx * unitsPerPixel / parentScale);
            inst.renderer.color = new Color(1f, 1f, 1f, a);
            if (inst.cloudRenderer != null)
            {
                inst.cloudRenderer.enabled = IconConfig.ShowClouds;
                inst.cloudRenderer.color = new Color(1f, 1f, 1f, a);
            }

            if (!inst.go.activeSelf) inst.go.SetActive(true);
            inst.usedFrame = frame;
            return true;
        }

        private static void DrawFallbackDot(Planet planet, float alpha)
        {
            if (planet.mapHolder == null) return;
            Map.elementDrawer.DrawTextElement(
                "\u25CF",
                Vector2.zero,
                35,
                planet.data.basics.mapColor * new Color(1f, 1f, 1f, alpha),
                (Vector2)planet.mapHolder.position,
                -1,
                false,
                1);
        }

        private static Camera GetMapCamera()
        {
            if (cameraFrame == Time.frameCount && cachedCamera != null) return cachedCamera;
            cameraFrame = Time.frameCount;

            int layer = LayerMask.NameToLayer("Map");
            Camera best = null;
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null || !c.enabled) continue;
                if (layer >= 0 && (c.cullingMask & (1 << layer)) == 0) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            if (best == null) best = Camera.main;
            cachedCamera = best;
            return best;
        }

        private static int WantedSize(float realRadiusPx, float disc)
        {
            float need = 2f * realRadiusPx / Mathf.Max(0.05f, disc) * 1.2f;
            int max = Mathf.Clamp(IconConfig.TextureSize, 256, 4096);
            int s = 256;
            while (s < need && s < max) s *= 2;
            return Mathf.Min(s, max);
        }

        private static bool IsOnScreen(Planet planet, float radiusPx)
        {
            try
            {
                Camera cam = GetMapCamera();
                if (cam == null) return true;
                Vector3 p = cam.WorldToScreenPoint(planet.mapHolder.position);
                float margin = radiusPx * 1.1f + 64f;
                return p.x > -margin && p.x < cam.pixelWidth + margin && p.y > -margin && p.y < cam.pixelHeight + margin;
            }
            catch (Exception) { return true; }
        }

        private static bool BudgetOk()
        {
            return Time.unscaledDeltaTime < 0.05f || Time.unscaledTime - lastBuildTime > 0.75f;
        }

        private static void TryStartJob(Planet planet, IconInstance inst, int size, int frame, bool upgrade)
        {
            if (IconWorker.PendingCount >= MaxConcurrentJobs) return;
            if (prepareFrame != frame) { prepareFrame = frame; preparedThisFrame = 0; }
            if (preparedThisFrame >= MaxPreparePerFrame) return;
            if (!BudgetOk()) return;
            preparedThisFrame++;

            try
            {
                IconJob job = PlanetIconGenerator.Prepare(planet, size);
                inst.job = job;
                inst.pendingSize = size;
                lastBuildTime = Time.unscaledTime;
                IconWorker.Enqueue(job);
            }
            catch (Exception e)
            {
                if (upgrade) inst.builtSize = int.MaxValue; else inst.failed = true;
                Debug.LogWarning("[IconPlanet] failed to start icon for " + planet.codeName + ": " + e);
            }
        }

        private static GeneratedIcon TryFinishJob(Planet planet, IconInstance inst, int frame, bool upgrade)
        {
            if (finishFrame != frame) { finishFrame = frame; finishedThisFrame = 0; }
            if (finishedThisFrame >= MaxFinishPerFrame) return null;
            if (!BudgetOk()) return null;
            finishedThisFrame++;

            IconJob job = inst.job;
            inst.job = null;
            try
            {
                GeneratedIcon fresh = job.Finish();
                lastBuildTime = Time.unscaledTime;
                return fresh;
            }
            catch (Exception e)
            {
                if (upgrade) inst.builtSize = int.MaxValue; else inst.failed = true;
                Debug.LogWarning("[IconPlanet] failed to generate icon for " + planet.codeName + ": " + e);
                return null;
            }
        }

        private static void SwapIcon(IconInstance inst, GeneratedIcon fresh)
        {
            GeneratedIcon old = inst.icon;
            inst.icon = fresh;
            inst.builtSize = inst.pendingSize;
            if (inst.renderer != null) inst.renderer.sprite = fresh.sprite;
            if (inst.cloudRenderer != null) inst.cloudRenderer.sprite = fresh.cloudSprite;
            if (old != null) old.DestroyAll();
        }

        private static float GetUnitsPerPixel(Vector3 worldPos)
        {
            Camera cam = GetMapCamera();
            if (cam != null)
            {
                Vector3 a = cam.WorldToScreenPoint(worldPos);
                Vector3 b = cam.WorldToScreenPoint(worldPos + cam.transform.right);
                float pixelsPerUnit = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                if (pixelsPerUnit > 1e-4f && !float.IsNaN(pixelsPerUnit) && !float.IsInfinity(pixelsPerUnit))
                {
                    return 1f / pixelsPerUnit;
                }
            }
            return Mathf.Max(1e-9f, Map.view.ToConstantSize(0.001f));
        }

        private static void DestroyInstance(IconInstance inst)
        {
            if (inst == null) return;
            if (inst.job != null) inst.job.cancelled = true;
            RestoreMeshes(inst);
            RestoreWater(inst);
            if (inst.go != null) UnityEngine.Object.Destroy(inst.go);
            if (inst.icon != null) inst.icon.DestroyAll();
        }
    }
}
