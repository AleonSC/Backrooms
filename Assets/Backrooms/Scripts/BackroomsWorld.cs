using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Streams Backrooms chunks around a viewer, animates flickering fixtures and
    /// moves a small pool of real-time point lights onto the nearest working fixtures.
    /// </summary>
    public class BackroomsWorld : MonoBehaviour
    {
        [Header("Streaming")]
        [Tooltip("Chunks are loaded within this many chunks of the viewer.")]
        public int viewRadius = 4;
        [Tooltip("Maximum number of chunks built per frame, to avoid hitches.")]
        public int chunksPerFrame = 2;

        [Header("Lighting")]
        public int lightPoolSize = 8;
        public float lightRange = 7.5f;
        public float lightIntensity = 1.6f;
        public Color lightColor = new Color(1f, 0.95f, 0.8f);
        public Color fixtureColor = new Color(1f, 0.98f, 0.9f);
        public Color deadFixtureColor = new Color(0.36f, 0.35f, 0.32f);
        [Tooltip("Seconds between re-assigning pooled lights to the nearest fixtures.")]
        public float lightReassignInterval = 0.15f;

        [Header("Materials (optional, generated if empty)")]
        public Material wallMaterial;
        public Material floorMaterial;
        public Material ceilingMaterial;
        public Material fixtureOnMaterial;
        public Material fixtureOffMaterial;

        public BackroomsGenerator Generator { get; private set; }
        public Transform Viewer { get; set; }

        sealed class FlickerFixture
        {
            public ChunkMeshBuilder.Fixture Info;
            public MeshRenderer Renderer;
            public float Level = 1f;
        }

        sealed class Chunk
        {
            public GameObject Root;
            public Mesh Mesh;
            public readonly List<ChunkMeshBuilder.Fixture> Fixtures = new List<ChunkMeshBuilder.Fixture>();
            public readonly List<FlickerFixture> Flickers = new List<FlickerFixture>();
        }

        struct LightCandidate
        {
            public Vector3 Position;
            public float DistanceSq;
            public FlickerFixture Flicker;
        }

        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> pending = new List<Vector2Int>();
        readonly List<Vector2Int> toRemove = new List<Vector2Int>();
        readonly List<LightCandidate> candidates = new List<LightCandidate>();
        Light[] lightPool;
        FlickerFixture[] lightFlicker;
        Material[] chunkMaterials;
        Mesh fixtureMesh;
        MaterialPropertyBlock block;
        Vector2Int lastCentre = new Vector2Int(int.MinValue, int.MinValue);
        float nextLightAssign;

        public int LoadedChunkCount => chunks.Count;

        public void Initialize(BackroomsGenerator generator, Transform viewer)
        {
            Generator = generator;
            Viewer = viewer;

            if (wallMaterial == null) wallMaterial = MaterialFactory.Lit("Backrooms Wall", ProceduralTextures.Wallpaper(), 0.12f);
            if (floorMaterial == null) floorMaterial = MaterialFactory.Lit("Backrooms Carpet", ProceduralTextures.Carpet(), 0.02f);
            if (ceilingMaterial == null) ceilingMaterial = MaterialFactory.Lit("Backrooms Ceiling", ProceduralTextures.CeilingTiles(), 0.05f);
            if (fixtureOnMaterial == null) fixtureOnMaterial = MaterialFactory.Unlit("Backrooms Fixture On", fixtureColor);
            if (fixtureOffMaterial == null) fixtureOffMaterial = MaterialFactory.Unlit("Backrooms Fixture Off", deadFixtureColor);

            chunkMaterials = new Material[ChunkMeshBuilder.SubmeshCount];
            chunkMaterials[ChunkMeshBuilder.SubWall] = wallMaterial;
            chunkMaterials[ChunkMeshBuilder.SubFloor] = floorMaterial;
            chunkMaterials[ChunkMeshBuilder.SubCeiling] = ceilingMaterial;
            chunkMaterials[ChunkMeshBuilder.SubFixtureOn] = fixtureOnMaterial;
            chunkMaterials[ChunkMeshBuilder.SubFixtureOff] = fixtureOffMaterial;

            fixtureMesh = ChunkMeshBuilder.BuildFixtureMesh();
            block = new MaterialPropertyBlock();

            lightPool = new Light[lightPoolSize];
            lightFlicker = new FlickerFixture[lightPoolSize];
            for (int i = 0; i < lightPoolSize; i++)
            {
                var go = new GameObject("Fixture Light " + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = lightRange;
                l.color = lightColor;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;
                lightPool[i] = l;
            }

            // Build everything around the viewer synchronously so the player has ground to stand on.
            UpdateStreaming(int.MaxValue);
            AssignLights();
        }

        void Update()
        {
            if (Generator == null || Viewer == null) return;

            UpdateStreaming(chunksPerFrame);
            UpdateFlicker(Time.time);

            if (Time.time >= nextLightAssign)
            {
                nextLightAssign = Time.time + lightReassignInterval;
                AssignLights();
            }
            UpdateLightIntensities();
        }

        Vector2Int ChunkOf(Vector3 p)
        {
            return new Vector2Int(
                Mathf.FloorToInt(p.x / BackroomsGenerator.ChunkSize),
                Mathf.FloorToInt(p.z / BackroomsGenerator.ChunkSize));
        }

        bool InRadius(Vector2Int c, Vector2Int centre, int radius)
        {
            int dx = c.x - centre.x, dz = c.y - centre.y;
            return dx * dx + dz * dz <= radius * radius + 1;
        }

        void UpdateStreaming(int budget)
        {
            Vector2Int centre = ChunkOf(Viewer.position);
            if (centre != lastCentre)
            {
                lastCentre = centre;

                toRemove.Clear();
                foreach (var key in chunks.Keys)
                    if (!InRadius(key, centre, viewRadius + 1)) toRemove.Add(key);
                foreach (var key in toRemove) Unload(key);

                pending.Clear();
                for (int dx = -viewRadius; dx <= viewRadius; dx++)
                {
                    for (int dz = -viewRadius; dz <= viewRadius; dz++)
                    {
                        var key = new Vector2Int(centre.x + dx, centre.y + dz);
                        if (InRadius(key, centre, viewRadius) && !chunks.ContainsKey(key)) pending.Add(key);
                    }
                }
                // Furthest first, so the nearest chunks are popped off the end first.
                pending.Sort((a, b) => (b - centre).sqrMagnitude.CompareTo((a - centre).sqrMagnitude));
            }

            while (budget-- > 0 && pending.Count > 0)
            {
                var key = pending[pending.Count - 1];
                pending.RemoveAt(pending.Count - 1);
                if (!chunks.ContainsKey(key)) Load(key);
            }
        }

        void Load(Vector2Int key)
        {
            var chunk = new Chunk();
            chunk.Mesh = ChunkMeshBuilder.Build(Generator, key.x, key.y, chunk.Fixtures);

            chunk.Root = new GameObject("Chunk " + key.x + "," + key.y);
            chunk.Root.transform.SetParent(transform, false);
            chunk.Root.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
            var mr = chunk.Root.AddComponent<MeshRenderer>();
            mr.sharedMaterials = chunkMaterials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            chunk.Root.AddComponent<MeshCollider>().sharedMesh = chunk.Mesh;

            foreach (var f in chunk.Fixtures)
            {
                if (f.State != LightState.Flicker) continue;
                var go = new GameObject("Flickering Fixture");
                go.transform.SetParent(chunk.Root.transform, false);
                go.transform.position = f.Position;
                go.AddComponent<MeshFilter>().sharedMesh = fixtureMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fixtureOnMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                chunk.Flickers.Add(new FlickerFixture { Info = f, Renderer = r });
            }

            chunks.Add(key, chunk);
        }

        void Unload(Vector2Int key)
        {
            if (!chunks.TryGetValue(key, out var chunk)) return;
            for (int i = 0; i < lightFlicker.Length; i++)
                if (lightFlicker[i] != null && chunk.Flickers.Contains(lightFlicker[i])) lightFlicker[i] = null;
            Destroy(chunk.Root);
            Destroy(chunk.Mesh);
            chunks.Remove(key);
        }

        void UpdateFlicker(float t)
        {
            foreach (var chunk in chunks.Values)
            {
                foreach (var f in chunk.Flickers)
                {
                    float level = Generator.FlickerLevel(t, f.Info.Cx, f.Info.Cz);
                    if (Mathf.Approximately(level, f.Level)) continue;
                    f.Level = level;
                    MaterialFactory.SetColor(block, Color.Lerp(deadFixtureColor, fixtureColor, level));
                    f.Renderer.SetPropertyBlock(block);
                }
            }
        }

        void AssignLights()
        {
            Vector3 viewer = Viewer.position;
            float maxDistSq = (lightRange * 2.5f) * (lightRange * 2.5f);
            candidates.Clear();

            foreach (var chunk in chunks.Values)
            {
                foreach (var f in chunk.Fixtures)
                {
                    if (f.State != LightState.On) continue;
                    float d = (f.Position - viewer).sqrMagnitude;
                    if (d < maxDistSq) candidates.Add(new LightCandidate { Position = f.Position, DistanceSq = d });
                }
                foreach (var f in chunk.Flickers)
                {
                    float d = (f.Info.Position - viewer).sqrMagnitude;
                    if (d < maxDistSq) candidates.Add(new LightCandidate { Position = f.Info.Position, DistanceSq = d, Flicker = f });
                }
            }

            candidates.Sort((a, b) => a.DistanceSq.CompareTo(b.DistanceSq));

            for (int i = 0; i < lightPool.Length; i++)
            {
                var l = lightPool[i];
                if (i < candidates.Count)
                {
                    l.transform.position = candidates[i].Position + Vector3.down * 0.3f;
                    lightFlicker[i] = candidates[i].Flicker;
                    l.enabled = true;
                }
                else
                {
                    lightFlicker[i] = null;
                    l.enabled = false;
                }
            }
        }

        void UpdateLightIntensities()
        {
            for (int i = 0; i < lightPool.Length; i++)
            {
                var l = lightPool[i];
                if (!l.enabled) continue;
                float level = lightFlicker[i] != null ? lightFlicker[i].Level : 1f;
                l.intensity = lightIntensity * level;
            }
        }

        void OnDestroy()
        {
            foreach (var chunk in chunks.Values)
                if (chunk.Mesh != null) Destroy(chunk.Mesh);
            chunks.Clear();
            if (fixtureMesh != null) Destroy(fixtureMesh);
        }
    }
}
