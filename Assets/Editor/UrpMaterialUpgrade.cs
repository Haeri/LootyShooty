using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LootyShooty.Editor
{
    /// <summary>
    /// Converts legacy Built-in materials to the URP version installed by this project.
    /// The automatic pass is idempotent; the menu item can be used after importing old assets.
    /// </summary>
    internal static class UrpMaterialUpgrade
    {
        [MenuItem("Tools/LootyShooty/Upgrade All Materials to URP")]
        private static void UpgradeFromMenu()
        {
            UpgradeAllMaterials();
        }

        private static void UpgradeAllMaterials()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
            {
                Debug.LogWarning("Material upgrade skipped because URP is not the default render pipeline.");
                return;
            }

            int upgraded = 0;
            int customFallbacks = 0;
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in materialGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null)
                        continue;

                    string shaderName = material.shader.name;
                    if (TryUpgradeCustomShader(material, shaderName))
                    {
                        EditorUtility.SetDirty(material);
                        customFallbacks++;
                        continue;
                    }

                    if (TryUpgradeLegacyBuiltInShader(material, shaderName))
                    {
                        EditorUtility.SetDirty(material);
                        upgraded++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"URP material upgrade complete: {upgraded} legacy materials converted, " +
                      $"{customFallbacks} unsupported custom materials mapped to URP shaders, " +
                      $"{materialGuids.Length} materials checked.");
        }

        private static bool TryUpgradeLegacyBuiltInShader(Material material, string shaderName)
        {
            MaterialUpgrader upgrader;
            if (shaderName == "Standard" || shaderName == "Standard (Specular setup)")
                upgrader = new StandardUpgrader(shaderName);
            else if (shaderName == "Particles/Standard Surface" ||
                     shaderName == "Particles/Standard Unlit" ||
                     shaderName == "Legacy Shaders/Particles/VertexLit Blended")
                upgrader = new ParticleUpgrader(shaderName);
            else
                return false;

            MaterialUpgrader.Upgrade(material, upgrader, MaterialUpgrader.UpgradeFlags.None);
            return true;
        }

        private static bool TryUpgradeCustomShader(Material material, string shaderName)
        {
            bool transparent;
            bool alphaClip;
            string targetShader;
            float blendMode = 0f;

            switch (shaderName)
            {
                case "Custom/Ice":
                    targetShader = "Universal Render Pipeline/Lit";
                    transparent = true;
                    alphaClip = false;
                    break;
                case "Custom/Dissolve":
                case "Custom/Respawn":
                    targetShader = "Universal Render Pipeline/Lit";
                    transparent = false;
                    alphaClip = true;
                    break;
                case "Unlit/FireFlyNew":
                    targetShader = "Universal Render Pipeline/Unlit";
                    transparent = true;
                    alphaClip = false;
                    break;
                case "Universal Render Pipeline/2D/Mesh2D-Lit-Default":
                    targetShader = "Universal Render Pipeline/Lit";
                    transparent = false;
                    alphaClip = false;
                    break;
                case "Legacy Shaders/Particles/Additive":
                    targetShader = "Universal Render Pipeline/Particles/Unlit";
                    transparent = true;
                    alphaClip = false;
                    blendMode = 2f;
                    break;
                case "Legacy Shaders/Particles/Alpha Blended Premultiply":
                    targetShader = "Universal Render Pipeline/Particles/Unlit";
                    transparent = true;
                    alphaClip = false;
                    blendMode = 1f;
                    break;
                default:
                    if (shaderName.StartsWith("Legacy Shaders/Particles/", StringComparison.Ordinal))
                    {
                        targetShader = "Universal Render Pipeline/Particles/Unlit";
                        transparent = true;
                        alphaClip = false;
                        blendMode = shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0
                            ? 2f
                            : shaderName.IndexOf("Premultiply", StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : 0f;
                    }
                    else if (shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal))
                    {
                        targetShader = "Universal Render Pipeline/Lit";
                        transparent = shaderName.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
                        alphaClip = shaderName.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }

            Texture baseTexture = GetTexture(material, "_MainTex");
            Vector2 baseScale = material.HasProperty("_MainTex")
                ? material.GetTextureScale("_MainTex")
                : Vector2.one;
            Vector2 baseOffset = material.HasProperty("_MainTex")
                ? material.GetTextureOffset("_MainTex")
                : Vector2.zero;
            Texture normalTexture = GetTexture(material, "_BumpMap") ?? GetTexture(material, "_Normal");
            Color baseColor = material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : InferDemoColor(material.name, GetColor(material, "_White", Color.white));
            Color emission = GetColor(material, "_Emission", Color.black);
            float metallic = GetFloat(material, "_Metallic", 0f);
            float smoothness = GetFloat(material, "_Glossiness", 0.5f);
            float cutoff = GetFloat(material, "_Cutoff", 0.5f);

            Shader shader = Shader.Find(targetShader);
            if (shader == null)
            {
                Debug.LogError($"Could not find target shader '{targetShader}' for material '{material.name}'.");
                return false;
            }

            material.shader = shader;
            SetTexture(material, "_BaseMap", baseTexture, baseScale, baseOffset);
            SetColor(material, "_BaseColor", baseColor);
            SetTexture(material, "_BumpMap", normalTexture, Vector2.one, Vector2.zero);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", smoothness);
            SetColor(material, "_EmissionColor", emission);
            SetFloat(material, "_Cutoff", cutoff);
            SetFloat(material, "_Surface", transparent ? 1f : 0f);
            SetFloat(material, "_AlphaClip", alphaClip ? 1f : 0f);
            SetFloat(material, "_Blend", blendMode);
            ConfigureBlendState(material, transparent, blendMode);

            CoreUtils.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            CoreUtils.SetKeyword(material, "_ALPHATEST_ON", alphaClip);
            CoreUtils.SetKeyword(material, "_NORMALMAP", normalTexture != null);
            CoreUtils.SetKeyword(material, "_EMISSION", emission.maxColorComponent > 0f);

            material.SetOverrideTag("RenderType", transparent
                ? "Transparent"
                : alphaClip ? "TransparentCutout" : "Opaque");
            material.renderQueue = transparent
                ? (int)RenderQueue.Transparent
                : alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            return true;
        }

        private static Color InferDemoColor(string materialName, Color fallback)
        {
            string name = materialName.ToLowerInvariant();
            if (name.Contains("red") || name.Contains("server") || name.Contains("target"))
                return new Color(0.8f, 0.1f, 0.1f, 1f);
            if (name.Contains("blue") || name.Contains("client"))
                return new Color(0.1f, 0.3f, 0.9f, 1f);
            if (name.Contains("green"))
                return new Color(0.1f, 0.7f, 0.2f, 1f);
            if (name.Contains("orange"))
                return new Color(1f, 0.35f, 0.05f, 1f);
            if (name.Contains("pink"))
                return new Color(1f, 0.2f, 0.55f, 1f);
            if (name.Contains("purple"))
                return new Color(0.5f, 0.15f, 0.8f, 1f);
            if (name.Contains("yellow"))
                return new Color(0.85f, 0.75f, 0.15f, 1f);
            if (name.Contains("black"))
                return new Color(0.05f, 0.05f, 0.05f, 1f);
            if (name.Contains("gray") || name.Contains("ground") || name.Contains("wall") || name.Contains("wheel"))
                return new Color(0.45f, 0.45f, 0.45f, 1f);

            return fallback;
        }

        private static void ConfigureBlendState(Material material, bool transparent, float blendMode)
        {
            if (!transparent)
            {
                SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                SetFloat(material, "_ZWrite", 1f);
                return;
            }

            BlendMode source = blendMode == 1f ? BlendMode.One : BlendMode.SrcAlpha;
            BlendMode destination = blendMode == 2f ? BlendMode.One : BlendMode.OneMinusSrcAlpha;
            SetFloat(material, "_SrcBlend", (float)source);
            SetFloat(material, "_DstBlend", (float)destination);
            SetFloat(material, "_ZWrite", 0f);
        }

        private static Texture GetTexture(Material material, string property) =>
            material.HasProperty(property) ? material.GetTexture(property) : null;

        private static Color GetColor(Material material, string property, Color fallback) =>
            material.HasProperty(property) ? material.GetColor(property) : fallback;

        private static float GetFloat(Material material, string property, float fallback) =>
            material.HasProperty(property) ? material.GetFloat(property) : fallback;

        private static void SetTexture(
            Material material,
            string property,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!material.HasProperty(property))
                return;

            material.SetTexture(property, texture);
            material.SetTextureScale(property, scale);
            material.SetTextureOffset(property, offset);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }
}
