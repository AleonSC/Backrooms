using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Generates the Backrooms textures at runtime so the project needs no art assets.
    /// All textures tile horizontally; the wallpaper is mapped once over the wall height.
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>World-space size of one wallpaper repeat (width, height in metres).</summary>
        public const float WallpaperWidth = 1.4f;
        /// <summary>World-space size of one carpet repeat in metres.</summary>
        public const float CarpetSize = 2f;
        /// <summary>World-space size of one ceiling repeat in metres (5 x 5 tiles of 0.6 m).</summary>
        public const float CeilingSize = 3f;

        static Texture2D Create(string name, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            return tex;
        }

        static Color32 Rgb(float r, float g, float b)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b), 0, 255),
                255);
        }

        /// <summary>Value noise that wraps at the lattice size.</summary>
        static float TileNoise(float[,] lattice, float x, float y)
        {
            int px = lattice.GetLength(0);
            int py = lattice.GetLength(1);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int ax = ((x0 % px) + px) % px, bx = (ax + 1) % px;
            int ay = ((y0 % py) + py) % py, by = (ay + 1) % py;
            float a = Mathf.Lerp(lattice[ax, ay], lattice[bx, ay], fx);
            float b = Mathf.Lerp(lattice[ax, by], lattice[bx, by], fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float[,] Lattice(System.Random rng, int w, int h)
        {
            var l = new float[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    l[x, y] = (float)rng.NextDouble();
            return l;
        }

        static float Fbm(float[][,] octaves, float u, float v)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < octaves.Length; i++)
            {
                int n = octaves[i].GetLength(0);
                sum += TileNoise(octaves[i], u * n, v * octaves[i].GetLength(1)) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float[][,] Octaves(System.Random rng, int baseW, int baseH, int count)
        {
            var o = new float[count][,];
            for (int i = 0; i < count; i++)
                o[i] = Lattice(rng, baseW << i, baseH << i);
            return o;
        }

        /// <summary>Mono-yellow striped wallpaper with a baseboard and grime near the floor.</summary>
        public static Texture2D Wallpaper()
        {
            const int W = 512, H = 512;
            var tex = Create("Backrooms Wallpaper", W, H);
            var rng = new System.Random(1337);
            var px = new Color32[W * H];
            var grime = Octaves(rng, 4, 4, 4);

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)H; // 0 = floor, 1 = ceiling
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float r = 201, g = 183, b = 100;

                    // Vertical stripes, 8 per repeat.
                    int sx = x % 64;
                    if (sx >= 20 && sx < 44) { r += 7; g += 7; b += 4; }
                    if (sx == 19 || sx == 44) { r -= 16; g -= 16; b -= 12; }

                    // Small diamond motif between the stripes (texture is stretched 2:1 vertically).
                    int my = y % 48;
                    int dx = Mathf.Abs(sx - 6), dy = Mathf.Abs(my - 24);
                    if (sx < 13 && dx + dy / 2 <= 3) { r -= 12; g -= 13; b -= 10; }

                    // Grime and water stains, heavier towards the floor.
                    float n = Fbm(grime, u, v);
                    float stain = Mathf.Clamp01((n - 0.58f) * 4f) * (0.4f + (1f - v) * 0.6f);
                    float floorDirt = Mathf.Clamp01(1f - v * 6f) * 0.35f;
                    float dirt = Mathf.Clamp01(stain + floorDirt);
                    r = Mathf.Lerp(r, 140, dirt);
                    g = Mathf.Lerp(g, 118, dirt);
                    b = Mathf.Lerp(b, 60, dirt);

                    // Baseboard (~10 cm of a 2.8 m wall).
                    if (y < 18) { r = 112; g = 94; b = 52; }
                    else if (y == 18) { r = 150; g = 130; b = 76; }

                    float grain = (float)rng.NextDouble() * 10f - 5f;
                    px[y * W + x] = Rgb(r + grain, g + grain, b + grain * 0.7f);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Damp, slightly blotchy mustard carpet.</summary>
        public static Texture2D Carpet()
        {
            const int S = 256;
            var tex = Create("Backrooms Carpet", S, S);
            var rng = new System.Random(4242);
            var px = new Color32[S * S];
            var blotch = Octaves(rng, 3, 3, 4);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(blotch, x / (float)S, y / (float)S);
                    float shade = Mathf.Lerp(0.78f, 1.08f, n);
                    float fibre = (float)rng.NextDouble() * 34f - 17f;
                    px[y * S + x] = Rgb(150 * shade + fibre, 128 * shade + fibre, 72 * shade + fibre * 0.6f);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Off-white acoustic ceiling tiles, 5 x 5 tiles per repeat.</summary>
        public static Texture2D CeilingTiles()
        {
            const int S = 640, Tile = 128;
            var tex = Create("Backrooms Ceiling", S, S);
            var rng = new System.Random(777);
            var px = new Color32[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int tx = x % Tile, ty = y % Tile;
                    float r = 222, g = 216, b = 190;
                    if (tx < 3 || ty < 3)
                    {
                        r = 168; g = 162; b = 140;
                    }
                    else
                    {
                        if (rng.NextDouble() < 0.06) { r -= 30; g -= 30; b -= 30; }
                        float grain = (float)rng.NextDouble() * 8f - 4f;
                        r += grain; g += grain; b += grain;
                    }
                    px[y * S + x] = Rgb(r, g, b);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
