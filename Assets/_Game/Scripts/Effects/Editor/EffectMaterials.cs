using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RushedCrops.Editor
{
    // The particle look shared by every effect: a soft round dot, drawn either glowing (light adds up, for
    // fire and fireflies) or soft (blends over the scene, for smoke, dust, chips and leaves). Made once.
    public static class EffectMaterials
    {
        private const string TexturePath = "Assets/_Game/Textures/Effects/TEX_SoftDot.png";
        private const string MaterialFolder = "Assets/_Game/Materials/Effects";
        public const string GlowPath = MaterialFolder + "/MAT_ParticleGlow.mat";
        public const string SoftPath = MaterialFolder + "/MAT_ParticleSoft.mat";

        public static Material Glow() => Get(GlowPath, true);
        public static Material Soft() => Get(SoftPath, false);

        private static Material Get(string path, bool additive)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            EnsureFolder(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", Dot());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // White, with alpha falling smoothly from the middle to the edge.
        private static Texture2D Dot()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null) return existing;
            EnsureFolder(Path.GetDirectoryName(TexturePath).Replace('\\', '/'));
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
