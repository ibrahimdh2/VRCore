using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class MissingTextureGUIDChecker
{
    [MenuItem("Tools/Fix Material Texture References")]
    public static void FindAndFixMissingTextureGUIDs()
    {
        // 1. Build GUID → path map for all texture assets
        var guidToPath = new Dictionary<string, string>();
        string[] textureMetaPaths = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".png.meta") || p.EndsWith(".jpg.meta") ||
                       p.EndsWith(".jpeg.meta") || p.EndsWith(".tga.meta") ||
                       p.EndsWith(".psd.meta") || p.EndsWith(".exr.meta") ||
                       p.EndsWith(".hdr.meta") || p.EndsWith(".tiff.meta"))
            .ToArray();

        foreach (string metaPath in textureMetaPaths)
        {
            string guid = ExtractGuidFromMetaFile(metaPath);
            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = metaPath.Replace(".meta", "").Replace(Application.dataPath, "Assets");
                if (assetPath.StartsWith("Assets"))
                    guidToPath[guid] = assetPath;
            }
        }

        Debug.Log($"Found {guidToPath.Count} texture assets with GUIDs");

        // 2. Process all materials
        int totalReassigned = 0;
        string[] materialPaths = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);

        foreach (string matPath in materialPaths)
        {
            int reassignedForMaterial = ProcessMaterial(matPath, guidToPath);
            totalReassigned += reassignedForMaterial;
        }

        // Force save and reimport to ensure Unity serializes properly
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Force reimport all processed materials to ensure proper serialization
        foreach (string matPath in materialPaths)
        {
            string assetPath = matPath.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        if (totalReassigned == 0)
            Debug.Log("⚠️ No texture properties found in materials or all GUIDs were missing.");
        else
            Debug.Log($"✅ Force reassigned {totalReassigned} textures to fix git sync issues. All materials updated.");
    }

    private static string ExtractGuidFromMetaFile(string metaPath)
    {
        try
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                if (line.StartsWith("guid: "))
                {
                    return line.Substring(6).Trim();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not read meta file {metaPath}: {e.Message}");
        }
        return null;
    }

    private static int ProcessMaterial(string matPath, Dictionary<string, string> guidToPath)
    {
        try
        {
            string materialContent = File.ReadAllText(matPath);
            string assetPath = matPath.Replace(Application.dataPath, "Assets").Replace("\\", "/");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Debug.LogWarning($"Could not load material at {assetPath}");
                return 0;
            }

            int reassignedCount = 0;

            // Parse texture properties from the material file
            var textureProperties = ParseTextureProperties(materialContent);

            foreach (var kvp in textureProperties)
            {
                string propertyName = kvp.Key;
                string guid = kvp.Value;

                if (guidToPath.TryGetValue(guid, out string texturePath))
                {
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                    if (texture != null && material.HasProperty(propertyName))
                    {
                        // Force reassign regardless of current state to fix git sync issues
                        material.SetTexture(propertyName, texture);
                        EditorUtility.SetDirty(material);
                        reassignedCount++;
                        Debug.Log($"🔄 Force assigned {Path.GetFileName(texturePath)} to {material.name}.{propertyName}");
                    }
                }
                else if (!string.IsNullOrEmpty(guid) && guid != "0000000000000000f000000000000000")
                {
                    Debug.LogWarning($"Missing texture with GUID {guid} referenced in {material.name}.{propertyName}");
                }
            }

            return reassignedCount;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing material {matPath}: {e.Message}");
            return 0;
        }
    }

    private static Dictionary<string, string> ParseTextureProperties(string materialContent)
    {
        var textureProperties = new Dictionary<string, string>();

        // Common texture property patterns in Unity material files
        var propertyPatterns = new Dictionary<string, string[]>
        {
            { "_MainTex", new[] { "_MainTex", "m_Texture" } },
            { "_BaseMap", new[] { "_BaseMap", "m_Texture" } },
            { "_BumpMap", new[] { "_BumpMap", "m_Texture" } },
            { "_MetallicGlossMap", new[] { "_MetallicGlossMap", "m_Texture" } },
            { "_OcclusionMap", new[] { "_OcclusionMap", "m_Texture" } },
            { "_EmissionMap", new[] { "_EmissionMap", "m_Texture" } },
            { "_DetailAlbedoMap", new[] { "_DetailAlbedoMap", "m_Texture" } },
            { "_DetailNormalMap", new[] { "_DetailNormalMap", "m_Texture" } },
            { "_ParallaxMap", new[] { "_ParallaxMap", "m_Texture" } },
            { "_SpecGlossMap", new[] { "_SpecGlossMap", "m_Texture" } },
            { "_DetailMask", new[] { "_DetailMask", "m_Texture" } }
        };

        string[] lines = materialContent.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Look for texture property definitions
            foreach (var property in propertyPatterns)
            {
                string propertyName = property.Key;

                // Pattern: m_Textures:
                // Then look for the property name
                if (line.Contains(propertyName + ":"))
                {
                    // Look ahead for the guid in the next few lines
                    for (int j = i + 1; j < Mathf.Min(i + 10, lines.Length); j++)
                    {
                        string nextLine = lines[j].Trim();
                        if (nextLine.StartsWith("guid: "))
                        {
                            string guid = nextLine.Substring(6).Trim();
                            if (!string.IsNullOrEmpty(guid))
                            {
                                textureProperties[propertyName] = guid;
                            }
                            break;
                        }
                        // Stop if we hit another property or section
                        if (nextLine.Contains(":") && !nextLine.StartsWith("m_") &&
                            !nextLine.StartsWith("fileID") && !nextLine.StartsWith("guid"))
                        {
                            break;
                        }
                    }
                }
            }
        }

        return textureProperties;
    }
}