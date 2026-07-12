using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// All game materials are created at runtime, so player builds must ship
    /// the shader VARIANTS they need (URP strips keyword variants no built
    /// asset uses — runtime EnableKeyword can't bring them back; symptom:
    /// alpha-tested pixels render opaque white). BuildScript generates a
    /// template material asset in Resources with _ALPHATEST_ON baked in;
    /// cloning it guarantees the variant exists in the build.
    /// </summary>
    public static class RuntimeMaterials
    {
        const string TemplatePath = "D1U/LevelCutout";
        const string LitTemplatePath = "D1U/LevelLit";
        static Material template;
        static Material litTemplate;
        static bool searched;
        static bool litSearched;

        /// <summary>Clone the shipped cutout template (fall back to a bare
        /// shader material in-editor before the template asset exists).</summary>
        public static Material Cutout(Shader fallbackShader)
        {
            if (!searched)
            {
                template = Resources.Load<Material>(TemplatePath);
                searched = true;
                if (template == null)
                    Debug.LogWarning("D1U: Resources/D1U/LevelCutout.mat missing — " +
                                     "alpha-tested surfaces will break in player builds");
            }
            return template != null ? new Material(template) : new Material(fallbackShader);
        }

        /// <summary>Level-geometry material on the D1U/Level shader (per-vertex
        /// dynamic lighting). Falls back to the cutout template if the shader
        /// or template is missing, so worst case is the old static-lit look.</summary>
        public static Material Level(Shader fallbackShader)
        {
            if (!litSearched)
            {
                litTemplate = Resources.Load<Material>(LitTemplatePath);
                litSearched = true;
                if (litTemplate == null)
                {
                    var shader = Shader.Find("D1U/Level");
                    if (shader != null)
                        litTemplate = new Material(shader);
                    else
                        Debug.LogWarning("D1U: LevelLit template and D1U/Level shader both " +
                                         "missing — dynamic lighting disabled");
                }
            }
            return litTemplate != null ? new Material(litTemplate) : Cutout(fallbackShader);
        }
    }
}
