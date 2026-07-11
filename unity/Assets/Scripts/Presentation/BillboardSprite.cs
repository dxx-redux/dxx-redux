using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// A camera-facing vclip sprite (powerups, hostages): a unit quad scaled
    /// to the object's diameter, cycling its animation frames.
    /// </summary>
    public class BillboardSprite : MonoBehaviour
    {
        static Mesh sharedQuad;

        Texture2D[] frames;
        float frameTime;
        Material material;
        int lastFrame = -1;

        public static BillboardSprite Create(string name, Texture2D[] frames, float frameTime,
                                             float radius, Shader shader, float light)
        {
            var go = new GameObject(name);
            var sprite = go.AddComponent<BillboardSprite>();
            sprite.frames = frames;
            sprite.frameTime = Mathf.Max(0.01f, frameTime);

            sprite.material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (sprite.material.HasProperty("_Cull")) sprite.material.SetInt("_Cull", 0);
            if (sprite.material.HasProperty("_AlphaClip")) { sprite.material.SetFloat("_AlphaClip", 1f); sprite.material.EnableKeyword("_ALPHATEST_ON"); }
            if (sprite.material.HasProperty("_Cutoff")) sprite.material.SetFloat("_Cutoff", 0.5f);
            var lightColor = new Color(light, light, light, 1f);
            if (sprite.material.HasProperty("_BaseColor")) sprite.material.SetColor("_BaseColor", lightColor);
            else sprite.material.color = lightColor;
            sprite.SetFrame(0);

            go.AddComponent<MeshFilter>().sharedMesh = GetQuad();
            go.AddComponent<MeshRenderer>().sharedMaterial = sprite.material;
            go.transform.localScale = Vector3.one * (radius * 2f);
            return sprite;
        }

        void SetFrame(int frame)
        {
            if (frame == lastFrame || frames == null || frames.Length == 0)
                return;
            lastFrame = frame;
            var texture = frames[frame % frames.Length];
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
            if (frames != null && frames.Length > 1)
                SetFrame((int)(Time.time / frameTime) % frames.Length);
        }

        void OnDestroy()
        {
            if (material != null)
                DestroyImmediate(material);
        }

        static Mesh GetQuad()
        {
            if (sharedQuad != null)
                return sharedQuad;
            sharedQuad = new Mesh
            {
                name = "d1u_billboard",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
                hideFlags = HideFlags.HideAndDontSave,
            };
            sharedQuad.RecalculateNormals();
            return sharedQuad;
        }
    }
}
