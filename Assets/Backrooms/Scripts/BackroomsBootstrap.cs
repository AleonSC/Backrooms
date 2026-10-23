using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Drop this on an empty GameObject in an empty scene and press Play. It sets up the
    /// atmosphere, spawns the first person player and starts the infinite generator.
    /// </summary>
    public class BackroomsBootstrap : MonoBehaviour
    {
        [Header("Seed")]
        public bool randomSeed = true;
        public int seed = 12345;

        [Header("World")]
        public int viewRadius = 4;

        [Header("Atmosphere")]
        public Color ambientColor = new Color(0.36f, 0.33f, 0.21f);
        public Color fogColor = new Color(0.3f, 0.27f, 0.15f);
        public float fogStart = 2f;
        public float fogEnd = 30f;
        [Range(0f, 1f)] public float humVolume = 0.12f;

        [Header("Optional material overrides")]
        public Material wallMaterial;
        public Material floorMaterial;
        public Material ceilingMaterial;
        public Material fixtureOnMaterial;
        public Material fixtureOffMaterial;

        public BackroomsWorld World { get; private set; }
        public FirstPersonController Player { get; private set; }

        void Awake()
        {
            int s = randomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
            var generator = new BackroomsGenerator(s);

            DisableSceneCamerasAndSun();
            SetupAtmosphere();

            var playerGo = new GameObject("Player");
            playerGo.transform.SetPositionAndRotation(BackroomsGenerator.SpawnPoint + Vector3.up * 0.05f, Quaternion.Euler(0f, 45f, 0f));
            Player = playerGo.AddComponent<FirstPersonController>();
            Player.footstepClips = new AudioClip[4];
            for (int i = 0; i < Player.footstepClips.Length; i++)
                Player.footstepClips[i] = ProceduralAudio.Footstep(i);

            var cam = Player.playerCamera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
            cam.farClipPlane = fogEnd + 15f;

            var hum = cam.gameObject.AddComponent<AudioSource>();
            hum.clip = ProceduralAudio.FluorescentHum();
            hum.loop = true;
            hum.volume = humVolume;
            hum.spatialBlend = 0f;
            hum.Play();

            var worldGo = new GameObject("Backrooms World");
            World = worldGo.AddComponent<BackroomsWorld>();
            World.viewRadius = viewRadius;
            World.wallMaterial = wallMaterial;
            World.floorMaterial = floorMaterial;
            World.ceilingMaterial = ceilingMaterial;
            World.fixtureOnMaterial = fixtureOnMaterial;
            World.fixtureOffMaterial = fixtureOffMaterial;
            World.Initialize(generator, playerGo.transform);
            Player.world = World;

            // Make sure the freshly created colliders exist before the player's first move.
            Physics.SyncTransforms();

            Debug.Log("Backrooms: entering level with seed " + s);
        }

        static void DisableSceneCamerasAndSun()
        {
#if UNITY_2022_2_OR_NEWER
            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var cameras = FindObjectsOfType<Camera>();
            var lights = FindObjectsOfType<Light>();
#endif
            foreach (var cam in cameras)
                cam.gameObject.SetActive(false);
            foreach (var light in lights)
                if (light.type == LightType.Directional) light.enabled = false;
        }

        void SetupAtmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
