using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Synthesises the fluorescent hum and carpet footsteps so no audio assets are needed.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;

        /// <summary>A seamlessly looping mains hum with buzzy harmonics and a little air noise.</summary>
        public static AudioClip FluorescentHum()
        {
            const float Seconds = 2f; // every partial completes a whole number of cycles
            int n = Mathf.RoundToInt(SampleRate * Seconds);
            var data = new float[n];
            var rng = new System.Random(99);
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0.5f * Mathf.Sin(2 * Mathf.PI * 60f * t)
                        + 0.35f * Mathf.Sin(2 * Mathf.PI * 120f * t)
                        + 0.18f * Mathf.Sin(2 * Mathf.PI * 180f * t)
                        + 0.12f * Mathf.Sin(2 * Mathf.PI * 240f * t)
                        + 0.06f * Mathf.Sin(2 * Mathf.PI * 360f * t)
                        + 0.03f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 120f * t)); // ballast buzz
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.05f;
                s += lp * 0.6f;
                data[i] = s * 0.25f;
            }

            var clip = AudioClip.Create("Fluorescent Hum", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A muffled footstep on damp carpet; vary <paramref name="variant"/> for different clips.</summary>
        public static AudioClip Footstep(int variant)
        {
            const float Seconds = 0.18f;
            int n = Mathf.RoundToInt(SampleRate * Seconds);
            var data = new float[n];
            var rng = new System.Random(500 + variant);
            float cutoff = 0.08f + variant * 0.015f;
            float thump = 70f + variant * 9f;
            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 32f) * Mathf.Clamp01(t * 600f);
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp1 += (white - lp1) * cutoff;
                lp2 += (lp1 - lp2) * cutoff;
                float body = Mathf.Sin(2 * Mathf.PI * thump * t) * Mathf.Exp(-t * 45f);
                data[i] = (lp2 * 2.2f + body * 0.5f) * env;
            }

            var clip = AudioClip.Create("Footstep " + variant, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
