using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms
{
    /// <summary>
    /// Creates runtime materials that work in the Built-in pipeline, URP and HDRP.
    /// </summary>
    public static class MaterialFactory
    {
        static bool UsingScriptablePipeline => GraphicsSettings.currentRenderPipeline != null;

        static Shader FindFirst(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            Debug.LogWarning("Backrooms: none of the shaders [" + string.Join(", ", names) +
                             "] were found. Add one to Project Settings > Graphics > Always Included Shaders.");
            return Shader.Find("Hidden/InternalErrorShader");
        }

        public static Material Lit(string name, Texture2D texture, float smoothness)
        {
            var shader = UsingScriptablePipeline
                ? FindFirst("Universal Render Pipeline/Lit", "HDRP/Lit", "Standard")
                : FindFirst("Standard", "Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name, mainTexture = texture, color = Color.white };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.enableInstancing = true;
            return m;
        }

        public static Material Unlit(string name, Color color)
        {
            var shader = UsingScriptablePipeline
                ? FindFirst("Universal Render Pipeline/Unlit", "HDRP/Unlit", "Unlit/Color")
                : FindFirst("Unlit/Color", "Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        public static void SetColor(MaterialPropertyBlock block, Color color)
        {
            block.SetColor("_BaseColor", color);
            block.SetColor("_UnlitColor", color);
            block.SetColor("_Color", color);
        }
    }
}
