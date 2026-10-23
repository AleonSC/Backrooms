using UnityEngine;

namespace Backrooms
{
    public enum Zone
    {
        PillarHall,
        Maze,
        Corridors,
    }

    public enum LightState
    {
        None,
        On,
        Off,
        Flicker,
    }

    /// <summary>
    /// Deterministic, stateless layout generator for an infinite Backrooms level.
    ///
    /// The world is a grid of square cells. Every cell may have a wall on its east
    /// edge and on its south (+Z) edge, a pillar on its south-west (min) corner and
    /// a ceiling light fixture. Everything is derived from a hash of (seed, cell),
    /// so any part of the world can be queried in any order and always comes out
    /// the same, which is what lets the world stream in chunks forever.
    /// </summary>
    public sealed class BackroomsGenerator
    {
        public const float Cell = 3f;
        public const float WallHeight = 2.8f;
        public const float WallThickness = 0.2f;
        public const float PillarSize = 0.6f;
        public const int ChunkCells = 4;
        public const float ChunkSize = Cell * ChunkCells;

        // Salts keep the different hash streams independent of each other.
        const int SaltZone = 101;
        const int SaltDensity = 103;
        const int SaltCorridorAxis = 104;
        const int SaltDarkness = 105;
        const int SaltWallEast = 11;
        const int SaltWallSouth = 12;
        const int SaltPillar = 13;
        const int SaltLight = 21;
        const int SaltFlickerPhase = 22;
        const int SaltFlicker = 77;

        readonly uint seed;

        public int Seed { get; }

        public BackroomsGenerator(int seed)
        {
            Seed = seed;
            this.seed = (uint)seed;
        }

        /// <summary>Spawn position (feet) at the centre of cell (0, 0).</summary>
        public static Vector3 SpawnPoint => new Vector3(Cell * 0.5f, 0f, Cell * 0.5f);

        public static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        public static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        /// <summary>Uniform hash in [0, 1).</summary>
        public float Hash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = seed ^ ((uint)x * 0x27d4eb2dU) ^ ((uint)z * 0x165667b1U) ^ ((uint)salt * 0x9e3779b9U);
                h ^= h >> 15;
                h *= 0x85ebca6bU;
                h ^= h >> 13;
                h *= 0xc2b2ae35U;
                h ^= h >> 16;
                return (h >> 8) / 16777216f;
            }
        }

        /// <summary>Smooth value noise in [0, 1).</summary>
        public float Noise(float x, float z, int salt)
        {
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            float fx = x - x0;
            float fz = z - z0;
            float u = fx * fx * (3f - 2f * fx);
            float v = fz * fz * (3f - 2f * fz);
            float a = Hash(x0, z0, salt);
            float b = Hash(x0 + 1, z0, salt);
            float c = Hash(x0, z0 + 1, salt);
            float d = Hash(x0 + 1, z0 + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public Zone GetZone(int cx, int cz)
        {
            float n = Noise(cx / 12f, cz / 12f, SaltZone);
            if (n < 0.3f) return Zone.PillarHall;
            if (n > 0.68f) return Zone.Corridors;
            return Zone.Maze;
        }

        /// <summary>The 3x3 block of cells around the spawn cell is always open.</summary>
        static bool InSpawn(int cx, int cz)
        {
            return cx >= -1 && cx <= 1 && cz >= -1 && cz <= 1;
        }

        /// <summary>Wall between cell (cx, cz) and cell (cx + 1, cz), at x = (cx + 1) * Cell.</summary>
        public bool WallEast(int cx, int cz)
        {
            if (InSpawn(cx, cz) && InSpawn(cx + 1, cz)) return false;
            return EdgeWall(cx, cz, true);
        }

        /// <summary>Wall between cell (cx, cz) and cell (cx, cz + 1), at z = (cz + 1) * Cell.</summary>
        public bool WallSouth(int cx, int cz)
        {
            if (InSpawn(cx, cz) && InSpawn(cx, cz + 1)) return false;
            return EdgeWall(cx, cz, false);
        }

        bool EdgeWall(int cx, int cz, bool east)
        {
            float r = Hash(cx, cz, east ? SaltWallEast : SaltWallSouth);
            switch (GetZone(cx, cz))
            {
                case Zone.PillarHall:
                    return r < 0.03f;

                case Zone.Maze:
                    float density = Mathf.Lerp(0.2f, 0.45f, Noise(cx / 5f, cz / 5f, SaltDensity));
                    return r < density;

                default: // Corridors: long parallel walls two cells apart with doorways.
                    bool alongX = Noise(cx / 20f, cz / 20f, SaltCorridorAxis) > 0.5f;
                    bool parallel = alongX ? !east : east;
                    if (!parallel) return r < 0.06f;
                    bool wallRow = Mod(alongX ? cz : cx, 2) == 0;
                    return wallRow ? r > 0.12f : r < 0.05f;
            }
        }

        /// <summary>Pillar centred on the corner at world (px * Cell, pz * Cell).</summary>
        public bool Pillar(int px, int pz)
        {
            if (px >= 0 && px <= 1 && pz >= 0 && pz <= 1) return false; // spawn interior
            float r = Hash(px, pz, SaltPillar);
            switch (GetZone(px, pz))
            {
                case Zone.PillarHall:
                    return Mod(px, 2) == 0 && Mod(pz, 2) == 0 && r < 0.85f;
                case Zone.Maze:
                    return r < 0.06f;
                default:
                    return false;
            }
        }

        /// <summary>True if any wall touches the corner at world (px * Cell, pz * Cell).</summary>
        public bool CornerHasWall(int px, int pz)
        {
            return WallEast(px - 1, pz - 1) || WallEast(px - 1, pz)
                || WallSouth(px - 1, pz - 1) || WallSouth(px, pz - 1);
        }

        public LightState GetLight(int cx, int cz)
        {
            if (Mod(cx + cz, 2) != 0) return LightState.None;
            if (cx == 0 && cz == 0) return LightState.On;

            bool dark = Noise(cx / 16f, cz / 16f, SaltDarkness) > 0.72f;
            float r = Hash(cx, cz, SaltLight);
            float offChance = dark ? 0.75f : 0.08f;
            if (r < offChance) return LightState.Off;
            if (r < offChance + 0.06f) return LightState.Flicker;
            return LightState.On;
        }

        /// <summary>World-space centre of the fixture in cell (cx, cz), on the ceiling plane.</summary>
        public static Vector3 FixturePosition(int cx, int cz)
        {
            // Offset so the 0.6 x 1.2 m panel lines up with the 0.6 m ceiling tile grid.
            return new Vector3(cx * Cell + 1.2f, WallHeight, cz * Cell + 1.5f);
        }

        /// <summary>Brightness in [0, 1] of a flickering fixture at time t.</summary>
        public float FlickerLevel(float t, int cx, int cz)
        {
            float phase = Hash(cx, cz, SaltFlickerPhase) * 100f;
            int step = Mathf.FloorToInt(t * 11f + phase);
            float r = Hash(step, cx * 31 + cz, SaltFlicker);
            if (r < 0.12f) return 0.05f;
            if (r < 0.22f) return 0.45f;
            if (r < 0.3f) return 0.8f;
            return 1f;
        }
    }
}
