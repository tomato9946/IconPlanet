using System;
using System.Reflection;
using SFS.WorldBase;
using UnityEngine;

namespace PlanetIcons
{
    public class GeneratedIcon
    {
        public Texture2D texture;
        public Sprite sprite;

        public Texture2D cloudTexture;
        public Sprite cloudSprite;

        public float discFraction = 0.96f;

        public void DestroyAll()
        {
            if (sprite != null) UnityEngine.Object.Destroy(sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            if (cloudSprite != null) UnityEngine.Object.Destroy(cloudSprite);
            if (cloudTexture != null) UnityEngine.Object.Destroy(cloudTexture);
            sprite = null;
            texture = null;
            cloudSprite = null;
            cloudTexture = null;
        }
    }

    internal class TexSampler
    {
        private readonly Color32[] px;
        public readonly int w, h;
        public float meanA, meanL, varL, varA;
        public bool UseAlpha;

        private TexSampler(Color32[] p, int width, int height)
        {
            px = p; w = width; h = height;
            ComputeStats();
            UseAlpha = varA > 0.01f && varA > varL * 2f;
        }

        public bool Wide { get { return w >= h; } }

        public bool IsFlat { get { return (w <= 4 && h <= 4) || (varL < 0.0004f && varA < 0.0004f); } }

        public string Describe(string label)
        {
            return label + " " + w + "x" + h + " meanA=" + meanA.ToString("0.00") + " meanL=" + meanL.ToString("0.00")
                   + " varL=" + varL.ToString("0.0000") + " varA=" + varA.ToString("0.0000") + (UseAlpha ? " ALPHA-CHANNEL" : "") + (IsFlat ? " FLAT" : "");
        }

        private void ComputeStats()
        {
            int n = px.Length;
            int step = Math.Max(1, n / 20000);
            double sA = 0, sL = 0, sA2 = 0, sL2 = 0; int c = 0;
            for (int i = 0; i < n; i += step)
            {
                Color32 p = px[i];
                double l = (p.r + p.g + p.b) / (3.0 * 255.0);
                double a = p.a / 255.0;
                sA += a; sL += l; sA2 += a * a; sL2 += l * l; c++;
            }
            if (c == 0) return;
            meanA = (float)(sA / c); meanL = (float)(sL / c);
            varA = (float)(sA2 / c - (sA / c) * (sA / c));
            varL = (float)(sL2 / c - (sL / c) * (sL / c));
        }

        public static TexSampler Create(Texture src, int maxDim)
        {
            if (src == null) return null;
            RenderTexture rt = null;
            Texture2D tmp = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                int sw = src.width, sh = src.height;
                if (sw <= 0 || sh <= 0) return null;
                float k = Mathf.Min(1f, (float)maxDim / Mathf.Max(sw, sh));
                int tw = Mathf.Max(1, Mathf.RoundToInt(sw * k));
                int th = Mathf.Max(1, Mathf.RoundToInt(sh * k));

                rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                tmp = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                Color32[] data = tmp.GetPixels32();
                return new TexSampler(data, tw, th);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (tmp != null) UnityEngine.Object.Destroy(tmp);
            }
        }

        private Color Px(int x, int y)
        {
            if (x < 0) x = 0; else if (x >= w) x = w - 1;
            if (y < 0) y = 0; else if (y >= h) y = h - 1;
            Color32 p = px[y * w + x];
            return new Color(p.r / 255f, p.g / 255f, p.b / 255f, p.a / 255f);
        }

        public Color Sample(float u, float v)
        {
            float x = u * w - 0.5f, y = v * h - 0.5f;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            Color a = Px(x0, y0), b = Px(x0 + 1, y0), c = Px(x0, y0 + 1), d = Px(x0 + 1, y0 + 1);
            float r = (a.r * (1 - fx) + b.r * fx) * (1 - fy) + (c.r * (1 - fx) + d.r * fx) * fy;
            float g = (a.g * (1 - fx) + b.g * fx) * (1 - fy) + (c.g * (1 - fx) + d.g * fx) * fy;
            float bl = (a.b * (1 - fx) + b.b * fx) * (1 - fy) + (c.b * (1 - fx) + d.b * fx) * fy;
            float al = (a.a * (1 - fx) + b.a * fx) * (1 - fy) + (c.a * (1 - fx) + d.a * fx) * fy;
            return new Color(r, g, bl, al);
        }
    }

    internal class IconJob
    {
        public volatile bool done;
        public Exception error;

        public string codeName;
        public int size;
        public double radius, extent, pxMeters;
        public double[] groundR, topR;
        public bool hasWater, hasRings, fullDisc, sharpen, real, oceanIsLight, oceanFromTexture;
        public Color sand, floor, waterMapColor, waterSolid, fog;
        public bool hasFog;
        public volatile bool cancelled;
        public bool distortTex;
        public float[] oceanCov, dLandPx, dOceanPx;
        public float shelfPx = 10f, sandPx = 5f;
        public float oceanT = 0.5f;
        public int debugView;
        public double[] rawHeights, rawAngles;
        public float cloudAlpha = 1f;
        public double ringInner, ringOuter;
        public float cutout, rotRad, cloudCutout, oceanDepth;
        public Color mapColor, shallow, deep, ringMapColor;
        public TexSampler planetTex, waterTex, ringTex, cloudTex;

        public Color32[] pixels;
        public Color32[] cloudPixels;

        private const double TwoPi = Math.PI * 2.0;

        public void Run()
        {
            if (cancelled) { done = true; return; }
            try
            {
                FillPlanet();
                if (cloudTex != null) FillClouds();
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                done = true;
            }
        }

        public float MaskValue(float u, float v)
        {
            if (oceanFromTexture && planetTex != null)
            {
                Color tc = planetTex.Sample(u, v);
                if (tc.a < 0.02f) return 0f;
                float blue = tc.b - Mathf.Max(tc.r, tc.g);
                return 1f - Mathf.Clamp01((blue - 0.05f) / 0.2f);
            }
            Color c = waterTex.Sample(u, v);
            float m = waterTex.UseAlpha ? c.a : (c.r + c.g + c.b) / 3f;
            return oceanIsLight ? 1f - m : m;
        }

        private Color OceanColor(float depth)
        {
            depth = Mathf.Clamp01(depth);
            Color seabed = Color.Lerp(sand, floor, Mathf.SmoothStep(0f, 0.3f, depth));
            Color sh = new Color(shallow.r, shallow.g, shallow.b, 1f);
            Color dp = new Color(deep.r, deep.g, deep.b, 1f);
            float sa = shallow.a > 0.01f ? Mathf.Clamp01(shallow.a + 0.35f) : 1f;
            Color shallowOver = Color.Lerp(seabed, sh, Mathf.Min(1f, sa));
            Color c = Color.Lerp(shallowOver, dp, Mathf.SmoothStep(0.05f, 0.7f, depth));
            c.a = 1f;
            return c;
        }

        private void BuildOceanFields()
        {
            int total = size * size;
            int n = groundR.Length;
            float[] m = new float[total];
            bool[] forced = new bool[total];
            bool[] inside = new bool[total];
            double cosRot = Math.Cos(rotRad), sinRot = Math.Sin(rotRad);

            for (int j = 0; j < size; j++)
            {
                if (cancelled) return;
                double y = ((j + 0.5) / size * 2.0 - 1.0) * extent;
                for (int i = 0; i < size; i++)
                {
                    double x = ((i + 0.5) / size * 2.0 - 1.0) * extent;
                    double r = Math.Sqrt(x * x + y * y);
                    int idx = j * size + i;

                    inside[idx] = r <= radius;
                    forced[idx] = false;

                    double dxn = r > 1e-9 ? x / r : 1.0, dyn = r > 1e-9 ? y / r : 0.0;
                    double cxr = dxn * cosRot + dyn * sinRot;
                    double cyr = dyn * cosRot - dxn * sinRot;
                    double rr = fullDisc ? Math.Min(r / radius, 1.0) : 1.0;
                    float u = (float)(cxr * rr * cutout * 0.5 + 0.5);
                    float v = (float)(cyr * rr * cutout * 0.5 + 0.5);
                    m[idx] = MaskValue(u, v);
                }
            }

            float[] mb = BoxBlur(m, size, 2);
            float tHi = Mathf.Clamp01(oceanT + (1f - oceanT) * 0.35f);
            float tLo = Mathf.Clamp01(oceanT - 0.04f);
            oceanCov = new float[total];
            bool[] isOcean = new bool[total];
            bool[] isLand = new bool[total];
            for (int k = 0; k < total; k++)
            {
                if (!inside[k]) { oceanCov[k] = 0f; continue; }
                if (forced[k]) { oceanCov[k] = 1f; isOcean[k] = true; continue; }
                float cov = 1f - Mathf.SmoothStep(tLo, tHi, mb[k]);
                oceanCov[k] = cov;
                if (cov > 0.35f) isOcean[k] = true; else isLand[k] = true;
            }
            dLandPx = DistanceTo(isLand, size);
            dOceanPx = DistanceTo(isOcean, size);

            float radiusPx = (float)(radius / extent * (size * 0.5));
            shelfPx = Math.Max(2f, radiusPx * 0.022f);
            sandPx = Math.Max(1.5f, radiusPx * 0.012f);
        }

        private static float[] BoxBlur(float[] src, int sz, int rad)
        {
            float[] tmp = new float[src.Length];
            float[] dst = new float[src.Length];
            float inv = 1f / (2 * rad + 1);
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float s = 0f;
                    for (int k = -rad; k <= rad; k++)
                    {
                        int xx = x + k; if (xx < 0) xx = 0; else if (xx >= sz) xx = sz - 1;
                        s += src[y * sz + xx];
                    }
                    tmp[y * sz + x] = s * inv;
                }
            }
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float s = 0f;
                    for (int k = -rad; k <= rad; k++)
                    {
                        int yy = y + k; if (yy < 0) yy = 0; else if (yy >= sz) yy = sz - 1;
                        s += tmp[yy * sz + x];
                    }
                    dst[y * sz + x] = s * inv;
                }
            }
            return dst;
        }

        private static float[] DistanceTo(bool[] seed, int sz)
        {
            int total = sz * sz;
            const int INF = 1 << 28;
            int[] d = new int[total];
            for (int i = 0; i < total; i++) d[i] = seed[i] ? 0 : INF;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    int idx = y * sz + x;
                    int v = d[idx];
                    if (v == 0) continue;
                    if (x > 0) v = Math.Min(v, d[idx - 1] + 3);
                    if (y > 0)
                    {
                        v = Math.Min(v, d[idx - sz] + 3);
                        if (x > 0) v = Math.Min(v, d[idx - sz - 1] + 4);
                        if (x < sz - 1) v = Math.Min(v, d[idx - sz + 1] + 4);
                    }
                    d[idx] = v;
                }
            }
            for (int y = sz - 1; y >= 0; y--)
            {
                for (int x = sz - 1; x >= 0; x--)
                {
                    int idx = y * sz + x;
                    int v = d[idx];
                    if (v == 0) continue;
                    if (x < sz - 1) v = Math.Min(v, d[idx + 1] + 3);
                    if (y < sz - 1)
                    {
                        v = Math.Min(v, d[idx + sz] + 3);
                        if (x < sz - 1) v = Math.Min(v, d[idx + sz + 1] + 4);
                        if (x > 0) v = Math.Min(v, d[idx + sz - 1] + 4);
                    }
                    d[idx] = v;
                }
            }
            float[] f = new float[total];
            for (int i = 0; i < total; i++) f[i] = d[i] >= INF / 2 ? 1e6f : d[i] / 3f;
            return f;
        }

        private static byte B(float v)
        {
            int i = (int)Math.Round(v * 255f);
            return (byte)(i < 0 ? 0 : (i > 255 ? 255 : i));
        }

        private void FillPlanet()
        {
            int n = groundR.Length;
            Color32[] outPx = new Color32[size * size];
            if (debugView == 0 && hasWater && (waterTex != null || oceanFromTexture)) BuildOceanFields();

            double cosRot = Math.Cos(rotRad), sinRot = Math.Sin(rotRad);
            double minEdge = double.MaxValue;
            for (int q = 0; q < n; q++)
            {
                if (topR[q] < minEdge) minEdge = topR[q];
                if (groundR[q] < minEdge) minEdge = groundR[q];
            }
            double innerR = distortTex ? -1.0 : minEdge * 0.995 - pxMeters * 2.0;

            for (int j = 0; j < size; j++)
            {
                if (cancelled) return;
                double y = ((j + 0.5) / size * 2.0 - 1.0) * extent;
                for (int i = 0; i < size; i++)
                {
                    double x = ((i + 0.5) / size * 2.0 - 1.0) * extent;
                    double r = Math.Sqrt(x * x + y * y);
                    double ground, top;
                    if (r < innerR)
                    {
                        ground = double.MaxValue;
                        top = r + pxMeters * 8.0;
                    }
                    else
                    {
                        double theta = Math.Atan2(y, x);
                        if (theta < 0) theta += TwoPi;
                        double f = theta * (n / TwoPi);
                        int i0 = (int)f;
                        if (i0 >= n) i0 = 0;
                        int i1 = (i0 + 1) % n;
                        double fr = f - i0;
                        ground = groundR[i0] * (1.0 - fr) + groundR[i1] * fr;
                        top = topR[i0] * (1.0 - fr) + topR[i1] * fr;
                    }

                    float R = 0f, G = 0f, Bc = 0f, A = 0f;

                    if (hasRings)
                    {
                        float cov = Mathf.Clamp01((float)((r - ringInner) / pxMeters + 0.5)) *
                                    Mathf.Clamp01((float)((ringOuter - r) / pxMeters + 0.5));
                        if (cov > 0f)
                        {
                            float t = Mathf.Clamp01((float)((r - ringInner) / (ringOuter - ringInner)));
                            Color rc = ringMapColor;
                            if (real)
                            {
                                if (ringTex != null)
                                {
                                    rc = ringTex.Wide ? ringTex.Sample(t, 0.5f) : ringTex.Sample(0.5f, t);
                                }
                            }
                            else
                            {
                                float ra = 0.6f;
                                if (ringTex != null)
                                {
                                    Color tcRing = ringTex.Wide ? ringTex.Sample(t, 0.5f) : ringTex.Sample(0.5f, t);
                                    ra *= tcRing.a;
                                }
                                rc = new Color(ringMapColor.r, ringMapColor.g, ringMapColor.b, ra);
                            }
                            R = rc.r; G = rc.g; Bc = rc.b; A = rc.a * cov;
                        }
                    }

                    float planetCov = Mathf.Clamp01((float)((top - r) / pxMeters + 0.5));
                    if (planetCov > 0f)
                    {
                        Color baseC = mapColor;
                        baseC.a = 1f;

                        double dxn = r > 1e-9 ? x / r : 1.0, dyn = r > 1e-9 ? y / r : 0.0;
                        double cxr = dxn * cosRot + dyn * sinRot;
                        double cyr = dyn * cosRot - dxn * sinRot;
                        double rr = fullDisc ? Math.Min(r / (distortTex ? Math.Max(top, radius * 0.05) : radius), 1.0) : 1.0;
                        float u = (float)(cxr * rr * cutout * 0.5 + 0.5);
                        float v = (float)(cyr * rr * cutout * 0.5 + 0.5);

                        if (debugView != 0)
                        {
                            if (debugView == 1 && waterTex != null)
                            {
                                Color dc = waterTex.Sample(u, v);
                                float gray = waterTex.UseAlpha ? dc.a : (dc.r + dc.g + dc.b) / 3f;
                                baseC = new Color(gray, gray, gray, 1f);
                            }
                            else if (debugView == 2 && planetTex != null)
                            {
                                Color dc = planetTex.Sample(u, v);
                                baseC = new Color(dc.r, dc.g, dc.b, 1f);
                            }
                            else
                            {
                                baseC = new Color(0.1f, 0.1f, 0.15f, 1f);
                            }
                        }
                        else if (real)
                        {
                            bool textured = false;
                            if (planetTex != null)
                            {
                                Color tc = planetTex.Sample(u, v);
                                if (tc.a > 0.02f)
                                {
                                    baseC = new Color(tc.r, tc.g, tc.b, 1f);
                                    textured = true;
                                }
                            }

                            if (!textured)
                            {
                                float rn = Mathf.Min(1f, (float)(r / radius));
                                float shade = 0.8f + 0.2f * (float)Math.Sqrt(Math.Max(0.0, 1.0 - rn * rn));
                                baseC = new Color(baseC.r * shade, baseC.g * shade, baseC.b * shade, 1f);
                            }

                            if (hasWater && oceanCov != null)
                            {
                                int px0 = j * size + i;
                                float cov = oceanCov[px0];
                                if (cov > 0.35f)
                                {
                                    float depthC = Mathf.Clamp01(dLandPx[px0] / Mathf.Max(1f, shelfPx));
                                    Color wc = OceanColor(Mathf.Max(depthC, 0.08f));
                                    float b = Mathf.Clamp01(0.75f + 0.25f * cov);
                                    baseC = new Color(
                                        Mathf.Lerp(baseC.r, wc.r, b),
                                        Mathf.Lerp(baseC.g, wc.g, b),
                                        Mathf.Lerp(baseC.b, wc.b, b), 1f);
                                }
                                else
                                {
                                    float sandAmt = (1f - Mathf.Clamp01(dOceanPx[px0] / Mathf.Max(1f, sandPx))) * 0.5f;
                                    if (sandAmt > 0.02f)
                                        baseC = new Color(
                                            Mathf.Lerp(baseC.r, sand.r, sandAmt),
                                            Mathf.Lerp(baseC.g, sand.g, sandAmt),
                                            Mathf.Lerp(baseC.b, sand.b, sandAmt), 1f);
                                }
                            }
                        }
                        else
                        {
                            float rn = Mathf.Min(1f, (float)(r / radius));
                            float shade = 0.85f + 0.15f * (float)Math.Sqrt(Math.Max(0.0, 1.0 - rn * rn));
                            float detail = 1f;
                            if (planetTex != null && planetTex.meanL > 0.01f)
                            {
                                Color tc = planetTex.Sample(u, v);
                                if (tc.a > 0.02f)
                                {
                                    float lum = (tc.r + tc.g + tc.b) / 3f;
                                    float rel = Mathf.Clamp(lum / planetTex.meanL, 0.7f, 1.25f);
                                    detail = Mathf.Lerp(1f, rel, 0.45f);
                                }
                            }
                            baseC = new Color(
                                Mathf.Clamp01(mapColor.r * shade * detail),
                                Mathf.Clamp01(mapColor.g * shade * detail),
                                Mathf.Clamp01(mapColor.b * shade * detail), 1f);

                            if (hasWater && oceanCov != null && oceanCov[j * size + i] > 0.35f)
                            {
                                baseC = new Color(waterSolid.r, waterSolid.g, waterSolid.b, 1f);
                            }
                        }

                        if (real && hasFog)
                        {
                            float rnf = Mathf.Min(1f, (float)(r / radius));
                            float fogAmt = Mathf.Clamp01(fog.a * (float)Math.Pow(rnf, 3.0) * 0.8f);
                            baseC = new Color(
                                Mathf.Lerp(baseC.r, fog.r, fogAmt),
                                Mathf.Lerp(baseC.g, fog.g, fogAmt),
                                Mathf.Lerp(baseC.b, fog.b, fogAmt), 1f);
                        }

                        float outA = planetCov + A * (1f - planetCov);
                        if (outA > 0.0001f)
                        {
                            R = (baseC.r * planetCov + R * A * (1f - planetCov)) / outA;
                            G = (baseC.g * planetCov + G * A * (1f - planetCov)) / outA;
                            Bc = (baseC.b * planetCov + Bc * A * (1f - planetCov)) / outA;
                            A = outA;
                        }
                    }

                    outPx[j * size + i] = new Color32(B(R), B(G), B(Bc), B(A));
                }
            }
            pixels = outPx;
        }

        private void FillClouds()
        {
            Color32[] outPx = new Color32[size * size];
            for (int j = 0; j < size; j++)
            {
                if (cancelled) return;
                double y = ((j + 0.5) / size * 2.0 - 1.0) * extent;
                for (int i = 0; i < size; i++)
                {
                    double x = ((i + 0.5) / size * 2.0 - 1.0) * extent;
                    double r = Math.Sqrt(x * x + y * y);
                    float cov = Mathf.Clamp01((float)((radius - r) / pxMeters + 0.5));
                    if (cov <= 0f) { outPx[j * size + i] = new Color32(0, 0, 0, 0); continue; }

                    double dxn = r > 1e-9 ? x / r : 1.0, dyn = r > 1e-9 ? y / r : 0.0;
                    double rr = Math.Min(r / radius, 1.0);
                    float u = (float)(dxn * rr * cloudCutout * 0.5 + 0.5);
                    float v = (float)(dyn * rr * cloudCutout * 0.5 + 0.5);
                    Color tc = cloudTex.Sample(u, v);
                    float ca = tc.a;
                    if (sharpen) ca = Mathf.Clamp01((ca - 0.25f) / 0.5f);
                    outPx[j * size + i] = new Color32(B(tc.r), B(tc.g), B(tc.b), B(ca * cov * cloudAlpha));
                }
            }
            cloudPixels = outPx;
        }

        private static Texture2D MakeTex(Color32[] px, int size, string name)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.name = name;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        // main thread
        public GeneratedIcon Finish()
        {
            if (error != null) throw new Exception("icon job failed: " + error.Message, error);
            GeneratedIcon icon = new GeneratedIcon();
            icon.texture = MakeTex(pixels, size, "PlanetIcon_" + codeName);
            icon.sprite = Sprite.Create(icon.texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            icon.sprite.name = icon.texture.name;
            icon.discFraction = (float)(radius / extent);
            if (cloudPixels != null)
            {
                icon.cloudTexture = MakeTex(cloudPixels, size, "PlanetClouds_" + codeName);
                icon.cloudSprite = Sprite.Create(icon.cloudTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                icon.cloudSprite.name = icon.cloudTexture.name;
            }
            pixels = null; cloudPixels = null;
            return icon;
        }
    }

    public static class PlanetIconGenerator
    {
        private const int AngleSamples = 1440;
        private static readonly System.Collections.Generic.Dictionary<string, double[]> HeightCache = new System.Collections.Generic.Dictionary<string, double[]>();
        private const double TwoPi = Math.PI * 2.0;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static object Get(object o, params string[] names)
        {
            if (o == null) return null;
            Type t = o.GetType();
            foreach (string n in names)
            {
                FieldInfo f = t.GetField(n, Flags);
                if (f != null) return f.GetValue(o);
                PropertyInfo p = t.GetProperty(n, Flags);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) return p.GetValue(o, null);
            }
            return null;
        }

        internal static IconJob Prepare(Planet planet, int size)
        {
            PlanetData data = planet.data;
            double radius = planet.Radius;

            bool hasTerrain = data.hasTerrain && data.terrain != null;
            bool hasWater = IconConfig.ShowWater && data.hasWater && data.water != null;
            bool hasRings = IconConfig.ShowRings && data.hasRings && data.rings != null;

            double[] angles = new double[AngleSamples];
            for (int i = 0; i < AngleSamples; i++) angles[i] = i * TwoPi / AngleSamples;

            double[] heights = null;
            if (!HeightCache.TryGetValue(planet.codeName, out heights))
            {
                try { heights = planet.GetTerrainHeightAtAngles(angles, false); }
                catch (Exception) { }
                if (heights != null && heights.Length == AngleSamples) HeightCache[planet.codeName] = heights;
            }
            if (heights == null || heights.Length != AngleSamples) heights = new double[AngleSamples];

            double exaggeration = IconConfig.TerrainExaggeration;
            double[] groundR = new double[AngleSamples];
            double[] topR = new double[AngleSamples];
            double maxTop = radius;
            for (int i = 0; i < AngleSamples; i++)
            {
                double h = heights[i];
                if (double.IsNaN(h) || double.IsInfinity(h)) h = 0.0;
                double limHi = radius * 3.0, limLo = -radius * 0.98;
                if (h > limHi) h = limHi; else if (h < limLo) h = limLo;
                double g = radius + h * exaggeration;
                double t = hasWater ? Math.Max(g, radius) : g;
                groundR[i] = g;
                topR[i] = t;
                if (t > maxTop) maxTop = t;
            }

            double ringInner = 0.0, ringOuter = 0.0;
            if (hasRings)
            {
                ringInner = data.rings.startRadius;
                double width = data.rings.endRadius - data.rings.startRadius;
                ringOuter = data.rings.endRadius + width * 0.01;
                if (ringOuter <= ringInner) hasRings = false;
            }

            double extent = Math.Max(maxTop, hasRings ? ringOuter : 0.0) * 1.04;

            IconJob job = new IconJob();
            job.codeName = planet.codeName;
            job.rawHeights = heights;
            job.rawAngles = angles;
            job.size = size;
            job.radius = radius;
            job.extent = extent;
            job.pxMeters = 2.0 * extent / size;
            job.groundR = groundR;
            job.topR = topR;
            job.hasWater = hasWater;
            job.hasRings = hasRings;
            job.ringInner = ringInner;
            job.ringOuter = ringOuter;
            job.fullDisc = IconConfig.FullDiscTexture;
            job.real = IconConfig.RealPlanet;
            job.debugView = IconConfig.DebugView;
            job.mapColor = data.basics.mapColor;
            job.ringMapColor = hasRings ? data.rings.mapColor : Color.clear;

            float cutout = 1f, rotRad = 0f;
            if (hasTerrain && data.terrain.TERRAIN_TEXTURE_DATA != null)
            {
                cutout = data.terrain.TERRAIN_TEXTURE_DATA.planetTextureCutout;
                rotRad = data.terrain.TERRAIN_TEXTURE_DATA.planetTextureRotation * Mathf.Deg2Rad;
            }
            if (cutout <= 0.0001f) cutout = 1f;
            job.cutout = cutout;
            job.rotRad = rotRad;
            {
                object dd = hasTerrain ? Get(data.terrain.TERRAIN_TEXTURE_DATA, "planetTextureDontDistort") : null;
                job.distortTex = dd is bool && !(bool)dd;
            }

            job.planetTex = TexSampler.Create(planet.planetTexture, size);
            if (job.planetTex != null)
            {
                if (job.planetTex.IsFlat) job.planetTex = null;
            }
            if (hasWater)
            {
                job.waterTex = TexSampler.Create(planet.waterTexture, size);
                job.shallow = data.water.shallow;
                job.deep = data.water.deep;
                job.oceanDepth = Mathf.Max(1f, data.water.oceanDepth);
                object so = Get(data.water, "sand"), fl = Get(data.water, "floor"), wm = Get(data.water, "mapColor");
                job.sand = so is Color ? (Color)so : new Color(0.9f, 0.86f, 0.81f, 1f);
                job.floor = fl is Color ? (Color)fl : new Color(0.25f, 0.25f, 0.25f, 1f);
                job.waterMapColor = wm is Color ? (Color)wm : job.deep;
            }
            if (hasRings && planet.ringsMaterial != null)
            {
                try { job.ringTex = TexSampler.Create(planet.ringsMaterial.GetTexture("_RingsTex"), 2048); }
                catch (Exception) { }
            }

            if (hasWater)
            {
                if (IconConfig.WaterMaskMode == 3 && job.planetTex != null) job.oceanFromTexture = true;
                else if (job.waterTex != null)
                {
                    job.oceanIsLight = DetectOceanIsLight(job);
                }
                else if (job.planetTex != null)
                {
                    job.oceanFromTexture = true;
                }
                if (job.waterTex != null && !job.oceanFromTexture) job.oceanT = CalibrateThreshold(job);
            }

            if (hasWater)
            {
                object wmc = Get(data.water, "mapColor");
                Color wc0 = wmc is Color ? (Color)wmc : job.deep;
                float wa = wc0.a > 0.01f ? Mathf.Clamp01(wc0.a) : 1f;
                if (wa >= 0.99f)
                    job.waterSolid = new Color(wc0.r, wc0.g, wc0.b, 1f);
                else
                {
                    Color dark = new Color(0.08f, 0.10f, 0.14f, 1f);
                    Color solid = Color.Lerp(dark, new Color(wc0.r, wc0.g, wc0.b, 1f), wa);
                    job.waterSolid = new Color(solid.r, solid.g, solid.b, 1f);
                }
            }

            if (IconConfig.RealPlanet)
            {
                try
                {
                    object av = Get(data, "atmosphereVisuals");
                    object fogGrad = Get(av, "FOG", "fog");
                    object keys = Get(fogGrad, "keys");
                    System.Collections.IEnumerable fogList = keys as System.Collections.IEnumerable;
                    object last = null;
                    if (fogList != null) foreach (object fogKey in fogList) last = fogKey;
                    object fc = Get(last, "color");
                    if (fc is Color) { job.fog = (Color)fc; job.hasFog = job.fog.a > 0.01f; }
                }
                catch (Exception) { }
            }
            job.cloudAlpha = IconConfig.RealPlanet ? 1f : 0.65f;
            if (job.debugView == 3) job.cloudAlpha = 1f;

            if (IconConfig.ShowClouds && job.debugView != 1 && job.debugView != 2)
            {
                bool allowFrontClouds = IconConfig.RealPlanet || !hasTerrain;
                if (allowFrontClouds)
                {
                    try { PrepareClouds(planet, job); }
                    catch (Exception e) { Debug.LogWarning("[IconPlanet] clouds skipped for " + planet.codeName + ": " + e.Message); }
                }
            }
            return job;
        }

        private static bool DetectOceanIsLight(IconJob job)
        {
            if (IconConfig.WaterMaskMode == 1) return false;
            if (IconConfig.WaterMaskMode == 2) return true;
            if (IconConfig.WaterMaskMode == 3) { job.oceanFromTexture = true; return false; }
            if (job.waterTex == null) return false;

            if (job.rawHeights != null && job.rawAngles != null && job.rawHeights.Length == job.rawAngles.Length)
            {
                int n = job.rawHeights.Length;
                double[] sorted = (double[])job.rawHeights.Clone();
                Array.Sort(sorted);
                double p25 = sorted[n / 4], p75 = sorted[(3 * n) / 4];
                if (p75 - p25 > 1.0)
                {
                    double sumLow = 0, sumHigh = 0; int nLow = 0, nHigh = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double ang = job.rawAngles[i] - job.rotRad;
                        float u = (float)(Math.Cos(ang) * job.cutout * 0.5 + 0.5);
                        float v = (float)(Math.Sin(ang) * job.cutout * 0.5 + 0.5);
                        Color mc = job.waterTex.Sample(u, v);
                        float m = job.waterTex.UseAlpha ? mc.a : (mc.r + mc.g + mc.b) / 3f;
                        if (job.rawHeights[i] <= p25) { sumLow += m; nLow++; }
                        else if (job.rawHeights[i] >= p75) { sumHigh += m; nHigh++; }
                    }
                    if (nLow >= 20 && nHigh >= 20)
                    {
                        double mLow = sumLow / nLow, mHigh = sumHigh / nHigh;
                        if (Math.Abs(mLow - mHigh) >= 0.12) return mLow > mHigh;
                    }
                }
            }

            if (job.planetTex == null) return false;

            double sumBlue = 0, sumOther = 0; int nBlue = 0, nOther = 0;
            const int N = 70;
            for (int iy = -N; iy <= N; iy++)
            {
                for (int ix = -N; ix <= N; ix++)
                {
                    double x = ix / (double)N, y = iy / (double)N;
                    double r = Math.Sqrt(x * x + y * y);
                    if (r > 0.98) continue;
                    double ang = Math.Atan2(y, x) - job.rotRad;
                    double rr = job.fullDisc ? r : 1.0;
                    float u = (float)(Math.Cos(ang) * rr * job.cutout * 0.5 + 0.5);
                    float v = (float)(Math.Sin(ang) * rr * job.cutout * 0.5 + 0.5);
                    Color tc = job.planetTex.Sample(u, v);
                    Color mc = job.waterTex.Sample(u, v);
                    float m = job.waterTex.UseAlpha ? mc.a : (mc.r + mc.g + mc.b) / 3f;
                    if (tc.a < 0.02f) { sumBlue += m; nBlue++; continue; }
                    float blue = tc.b - Mathf.Max(tc.r, tc.g);
                    if (blue > 0.2f) { sumBlue += m; nBlue++; }
                    else if (blue < 0.05f) { sumOther += m; nOther++; }
                }
            }
            if (nBlue < 50 || nOther < 50) return false;
            double mb = sumBlue / nBlue, mo = sumOther / nOther;

            if (Math.Abs(mb - mo) < 0.15) { job.oceanFromTexture = true; return false; }
            return mb > mo;
        }

        private static float CalibrateThreshold(IconJob job)
        {
            if (job.rawHeights == null || job.rawAngles == null || job.oceanFromTexture || job.waterTex == null) return 0.5f;
            int n = job.rawHeights.Length;
            if (n < 100 || job.rawAngles.Length != n) return 0.5f;

            float[] ms = new float[n];
            int oceanCount = 0;
            for (int i = 0; i < n; i++)
            {
                double ang = job.rawAngles[i] - job.rotRad;
                float u = (float)(Math.Cos(ang) * 0.99 * job.cutout * 0.5 + 0.5);
                float v = (float)(Math.Sin(ang) * 0.99 * job.cutout * 0.5 + 0.5);
                ms[i] = job.MaskValue(u, v);
                if (job.rawHeights[i] < 0.0) oceanCount++;
            }
            int landCount = n - oceanCount;
            if (oceanCount < 20 || landCount < 20) return 0.5f;

            int bestAcc = -1;
            System.Collections.Generic.List<float> bestTs = new System.Collections.Generic.List<float>();
            for (int k = 2; k <= 38; k++)
            {
                float t = k * 0.025f;
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    bool predOcean = ms[i] < t;
                    bool isOcean = job.rawHeights[i] < 0.0;
                    if (predOcean == isOcean) acc++;
                }
                if (acc > bestAcc) { bestAcc = acc; bestTs.Clear(); bestTs.Add(t); }
                else if (acc == bestAcc) bestTs.Add(t);
            }
            float chosen = bestTs[bestTs.Count / 2];
            chosen = Mathf.Clamp01(chosen + 0.12f);
            if (bestAcc < n * 0.75) return Mathf.Clamp01(0.5f + 0.12f);
            return chosen;
        }

        private static void PrepareClouds(Planet planet, IconJob job)
        {
            PlanetData data = planet.data;
            object frontClouds = Get(data, "frontClouds");
            object has = Get(data, "hasFrontClouds");
            bool hasClouds = has is bool ? (bool)has : frontClouds != null;
            if (!hasClouds) return;

            Material mat = Get(planet, "frontCloudsMaterial") as Material;
            Texture src = null;
            if (mat != null && mat.HasProperty("_CloudsTex")) src = mat.GetTexture("_CloudsTex");
            if (src == null) src = Get(frontClouds, "cloudsTexture") as Texture;

            TexSampler tex = TexSampler.Create(src, job.size);
            if (tex == null) return;
            if (tex.IsFlat) return;

            float cutout = 1f;
            if (mat != null && mat.HasProperty("_TextureCutout")) cutout = mat.GetFloat("_TextureCutout");
            else
            {
                object c = Get(frontClouds, "cloudTextureCutout");
                if (c is float) cutout = (float)c;
            }
            if (cutout <= 0.0001f) cutout = 1f;

            bool sharpen = false;
            if (mat != null && mat.HasProperty("_SharpenAlpha")) sharpen = mat.GetFloat("_SharpenAlpha") > 0.5f;
            else
            {
                object s = Get(frontClouds, "sharpenAlpha");
                if (s is bool) sharpen = (bool)s;
            }

            job.cloudTex = tex;
            job.cloudCutout = cutout;
            job.sharpen = sharpen;
        }
    }
}
