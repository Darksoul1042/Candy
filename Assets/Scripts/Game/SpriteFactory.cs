using System;
using UnityEngine;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Genera sprites propios por código (sin recursos externos). Cada tipo tiene forma y color distintos.</summary>
    public static class SpriteFactory
    {
        private const int Size = 128;

        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.36f, 0.45f), // 1 coral
            new Color(0.98f, 0.76f, 0.22f), // 2 ámbar
            new Color(0.35f, 0.80f, 0.55f), // 3 menta
            new Color(0.34f, 0.62f, 0.95f), // 4 azul
            new Color(0.70f, 0.48f, 0.92f), // 5 violeta
            new Color(0.98f, 0.60f, 0.30f), // 6 naranja
        };

        private static Sprite[] tileSprites;
        private static Sprite square, crate, stone, ice, jelly;
        private static readonly Sprite[] overlays = new Sprite[5];

        public static Color ColorOf(int type) => Palette[Mathf.Clamp(type - 1, 0, Palette.Length - 1)];

        public static Sprite Ice() => Cached(ref ice, IceColor);
        public static Sprite Jelly() => Cached(ref jelly, JellyColor);

        public static Sprite Overlay(SpecialKind k)
        {
            int i = (int)k;
            if (overlays[i] == null) overlays[i] = FromFunc((u, v) => OverlayColor(k, u, v));
            return overlays[i];
        }

        private static Sprite Cached(ref Sprite slot, Func<float, float, Color> f)
        {
            if (slot == null) slot = FromFunc(f);
            return slot;
        }

        private static Sprite FromFunc(Func<float, float, Color> f)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = f((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        private static float RoundedBox(float u, float v, float half, float corner)
        {
            float qx = Mathf.Abs(u) - (half - corner), qy = Mathf.Abs(v) - (half - corner);
            return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - corner;
        }

        private static float Alpha(float d) => Mathf.Clamp01(0.5f - d * Size * 0.5f);

        private static Color IceColor(float u, float v)
        {
            float d = RoundedBox(u, v, 0.92f, 0.25f);
            float a = Alpha(d);
            if (a <= 0f) return Color.clear;
            var c = new Color(0.78f, 0.93f, 1f, 0.5f);
            if (d > -0.07f) c = new Color(1f, 1f, 1f, 0.9f);
            else if (Mathf.Abs(u - v * 0.6f + 0.15f) < 0.025f || Mathf.Abs(u + v * 1.4f - 0.3f) < 0.02f) c = new Color(1f, 1f, 1f, 0.85f);
            c.a *= a;
            return c;
        }

        private static Color JellyColor(float u, float v)
        {
            float d = RoundedBox(u, v, 0.98f, 0.2f);
            float a = Alpha(d);
            if (a <= 0f) return Color.clear;
            var c = d > -0.1f ? new Color(1f, 0.45f, 0.78f, 0.75f) : new Color(1f, 0.62f, 0.86f, 0.5f);
            c.a *= a;
            return c;
        }

        private static Color CrateColor(float u, float v)
        {
            float d = RoundedBox(u, v, 0.9f, 0.12f);
            float a = Alpha(d);
            if (a <= 0f) return Color.clear;
            var c = new Color(0.66f, 0.45f, 0.24f);
            if (d > -0.1f) c = new Color(0.36f, 0.22f, 0.11f);
            else if (Mathf.Abs(Mathf.Abs(u) - Mathf.Abs(v)) < 0.07f) c = new Color(0.45f, 0.29f, 0.14f);
            c.a = a;
            return c;
        }

        private static Color StoneColor(float u, float v)
        {
            float d = RoundedBox(u, v, 0.92f, 0.38f);
            float a = Alpha(d);
            if (a <= 0f) return Color.clear;
            float speck = Mathf.Sin(u * 23f) * Mathf.Sin(v * 19f);
            var c = Color.Lerp(new Color(0.46f, 0.50f, 0.55f), new Color(0.62f, 0.66f, 0.7f), Mathf.Clamp01(0.5f + 0.4f * speck + 0.25f * (v - u)));
            if (d > -0.09f) c = new Color(0.27f, 0.3f, 0.34f);
            c.a = a;
            return c;
        }

        private static Color OverlayColor(SpecialKind k, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            bool on = false;
            switch (k)
            {
                case SpecialKind.LineH: on = Mathf.Abs(u) < 0.62f && (Mathf.Abs(v - 0.22f) < 0.07f || Mathf.Abs(v + 0.22f) < 0.07f || Mathf.Abs(v) < 0.07f); break;
                case SpecialKind.LineV: on = Mathf.Abs(v) < 0.62f && (Mathf.Abs(u - 0.22f) < 0.07f || Mathf.Abs(u + 0.22f) < 0.07f || Mathf.Abs(u) < 0.07f); break;
                case SpecialKind.Bomb: on = Mathf.Abs(r - 0.46f) < 0.09f || r < 0.14f; break;
                case SpecialKind.ColorBurst: on = (Mathf.Abs(u) * Mathf.Abs(v) < 0.02f && r < 0.74f) || (Mathf.Abs(Mathf.Abs(u) - Mathf.Abs(v)) < 0.06f && r < 0.5f); break;
            }
            return on ? new Color(1f, 1f, 1f, 0.95f) : Color.clear;
        }

        public static int MaxTypes => Palette.Length;

        public static Sprite Tile(int type)
        {
            if (type == Board.Crate) return Cached(ref crate, CrateColor);
            if (type == Board.Stone) return Cached(ref stone, StoneColor);
            if (tileSprites == null) tileSprites = new Sprite[Palette.Length];
            int i = Mathf.Clamp(type - 1, 0, Palette.Length - 1);
            if (tileSprites[i] == null) tileSprites[i] = Build(i);
            return tileSprites[i];
        }

        public static Sprite Square()
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return square;
        }

        private static Sprite Build(int index)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color[Size * Size];
            Color baseCol = Palette[index];
            Color dark = baseCol * 0.62f; dark.a = 1f;
            Color light = Color.Lerp(baseCol, Color.white, 0.45f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;   // -1..1
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float d = Distance(index, u, v);          // <0 dentro
                    float edge = Mathf.Clamp01(0.5f - d * Size * 0.5f);
                    if (edge <= 0f) { px[y * Size + x] = Color.clear; continue; }

                    Color c = d > -0.09f ? dark : baseCol;
                    float shine = Mathf.Clamp01(1f - Mathf.Sqrt((u + 0.35f) * (u + 0.35f) + (v - 0.4f) * (v - 0.4f)) * 2.2f);
                    if (d < -0.09f) c = Color.Lerp(c, light, shine * 0.8f);
                    c.a = edge;
                    px[y * Size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // Distancia aproximada con signo al borde de cada forma (radio ~0.82).
        private static float Distance(int shape, float u, float v)
        {
            const float r = 0.82f;
            switch (shape % 6)
            {
                case 0: return Mathf.Sqrt(u * u + v * v) - r;                                       // círculo
                case 1: // cuadrado redondeado
                    {
                        float qx = Mathf.Abs(u) - (r - 0.25f), qy = Mathf.Abs(v) - (r - 0.25f);
                        return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                               + Mathf.Min(Mathf.Max(qx, qy), 0) - 0.25f;
                    }
                case 2: return (Mathf.Abs(u) + Mathf.Abs(v) - r * 1.18f) * 0.707f;                  // rombo
                case 3: // triángulo redondeado
                    {
                        float a = -v * 0.9f - 0.45f * r;                       // base
                        float b = (v * 0.5f + u * 0.866f) - 0.45f * r;         // lado derecho
                        float c = (v * 0.5f - u * 0.866f) - 0.45f * r;         // lado izquierdo
                        return Mathf.Max(a, Mathf.Max(b, c));
                    }
                case 4: // hexágono
                    return Mathf.Max(Mathf.Abs(u) * 0.866f + Mathf.Abs(v) * 0.5f, Mathf.Abs(v)) - r * 0.95f;
                default: // flor de 5 pétalos
                    {
                        float ang = Mathf.Atan2(v, u);
                        float rad = 0.64f + 0.2f * Mathf.Cos(5f * ang);
                        return Mathf.Sqrt(u * u + v * v) - rad;
                    }
            }
        }
    }
}
