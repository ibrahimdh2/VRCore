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
        Debug.Log("🔧 Starting complete material and texture reference fix...");

        // 1. Build GUID maps for all assets
        var textureGuidToPath = BuildTextureGuidMap();
        var materialGuidToPath = BuildMaterialGuidMap();

        Debug.Log($"Found {textureGuidToPath.Count} textures and {materialGuidToPath.Count} materials");

        // 2. Fix all material texture assignments by reading .mat files directly
        int textureReassignments = FixMaterialTextureReferences(textureGuidToPath);

        // 3. Fix missing material references in mesh renderers
        int materialReassignments = FixMeshRendererMaterialReferences(materialGuidToPath);

        // 4. Force Unity to save and refresh everything
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5. Force reimport all materials to ensure proper serialization
        ForceReimportAllMaterials();

        Debug.Log($"✅ COMPLETE: Fixed {textureReassignments} texture references and {materialReassignments} material references");
    }

    private static Dictionary<string, string> BuildTextureGuidMap()
    {
        var guidToPath = new Dictionary<string, string>();

        string[] textureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr", ".tiff", ".bmp" };

        foreach (string ext in textureExtensions)
        {
            string[] metaPaths = Directory.GetFiles("Assets", $"*{ext}.meta", SearchOption.AllDirectories);
            foreach (string metaPath in metaPaths)
            {
                string guid = ExtractGuidFromMetaFile(metaPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    string assetPath = metaPath.Replace(".meta", "").Replace("\\", "/");
                    if (assetPath.StartsWith("Assets/"))
                        guidToPath[guid] = assetPath;
                }
            }
        }

        return guidToPath;
    }

    private static Dictionary<string, string> BuildMaterialGuidMap()
    {
        var guidToPath = new Dictionary<string, string>();

        string[] matMetaPaths = Directory.GetFiles("Assets", "*.mat.meta", SearchOption.AllDirectories);
        foreach (string metaPath in matMetaPaths)
        {
            string guid = ExtractGuidFromMetaFile(metaPath);
            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = metaPath.Replace(".meta", "").Replace("\\", "/");
                if (assetPath.StartsWith("Assets/"))
                    guidToPath[guid] = assetPath;
            }
        }

        return guidToPath;
    }

    private static string ExtractGuidFromMetaFile(string metaPath)
    {
        try
        {
            string[] lines = File.ReadAllLines(metaPath);
            foreach (string line in lines)
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

    private static int FixMaterialTextureReferences(Dictionary<string, string> textureGuidToPath)
    {
        int totalFixed = 0;
        string[] materialPaths = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);

        foreach (string matPath in materialPaths)
        {
            try
            {
                string materialContent = File.ReadAllText(matPath);
                string assetPath = matPath.Replace("\\", "/");

                // Load the material
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null) continue;

                // Parse texture references from the .mat file
                var textureReferences = ParseTextureReferencesFromMatFile(materialContent);

                foreach (var texRef in textureReferences)
                {
                    string propertyName = texRef.Key;
                    string guid = texRef.Value;

                    if (textureGuidToPath.TryGetValue(guid, out string texturePath))
                    {
                        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                        if (texture != null && material.HasProperty(propertyName))
                        {
                            // FORCE reassign regardless of current state
                            material.SetTexture(propertyName, texture);
                            EditorUtility.SetDirty(material);
                            totalFixed++;
                            Debug.Log($"🔄 Force assigned {Path.GetFileName(texturePath)} → {material.name}.{propertyName}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(guid) && guid != "0000000000000000f000000000000000")
                    {
                        Debug.LogWarning($"❌ Missing texture GUID {guid} in {material.name}.{propertyName}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing material {matPath}: {e.Message}");
            }
        }

        return totalFixed;
    }

    private static Dictionary<string, string> ParseTextureReferencesFromMatFile(string materialContent)
    {
        var textureRefs = new Dictionary<string, string>();
        string[] lines = materialContent.Split('\n');

        // Common texture properties in Unity materials
        string[] textureProperties = {
            "_MainTex", "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap",
            "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_ParallaxMap",
            "_SpecGlossMap", "_DetailMask", "_Cube", "_CubeMap"
        };

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            foreach (string property in textureProperties)
            {
                // Look for property definition
                if (line.Contains($"{property}:") || line.Contains($"name: {property}"))
                {
                    // Search for the guid in the next few lines
                    for (int j = i + 1; j < Mathf.Min(i + 15, lines.Length); j++)
                    {
                        string nextLine = lines[j].Trim();

                        if (nextLine.StartsWith("guid: "))
                        {
                            string guid = nextLine.Substring(6).Trim();
                            if (!string.IsNullOrEmpty(guid) && guid != "0000000000000000f000000000000000")
                            {
                                textureRefs[property] = guid;
                            }
                            break;
                        }

                        // Stop if we hit another property section
                        if (nextLine.Contains("serializedVersion:") && j > i + 5)
                            break;
                    }
                }
            }
        }

        return textureRefs;
    }

    private static int FixMeshRendererMaterialReferences(Dictionary<string, string> materialGuidToPath)
    {
        int totalFixed = 0;

        // Find all prefabs and scene objects with MeshRenderer/SkinnedMeshRenderer
        string[] prefabPaths = Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories);

        foreach (string prefabPath in prefabPaths)
        {
            try
            {
                string prefabContent = File.ReadAllText(prefabPath);
                string assetPath = prefabPath.Replace("\\", "/");

                // Load prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                // Get all renderers in prefab
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;

                    // Parse material GUIDs from prefab file
                    var materialGuids = ParseMaterialGuidsFromPrefab(prefabContent, renderer);

                    if (materialGuids.Count > 0)
                    {
                        Material[] materials = new Material[materialGuids.Count];
                        bool hasChanges = false;

                        for (int i = 0; i < materialGuids.Count; i++)
                        {
                            string guid = materialGuids[i];
                            if (materialGuidToPath.TryGetValue(guid, out string matPath))
                            {
                                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                                materials[i] = mat;
                                if (mat != null) hasChanges = true;
                            }
                        }

                        if (hasChanges)
                        {
                            renderer.materials = materials;
                            EditorUtility.SetDirty(prefab);
                            totalFixed++;
                            Debug.Log($"🔄 Fixed materials on {renderer.name} in {prefab.name}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing prefab {prefabPath}: {e.Message}");
            }
        }

        return totalFixed;
    }

    private static List<string> ParseMaterialGuidsFromPrefab(string prefabContent, Renderer renderer)
    {
        var guids = new List<string>();

        // This is a simplified parser - in practice you'd need more robust YAML parsing
        // Look for m_Materials section and extract GUIDs
        string[] lines = prefabContent.Split('\n');
        bool inMaterialsSection = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Contains("m_Materials:"))
            {
                inMaterialsSection = true;
                continue;
            }

            if (inMaterialsSection)
            {
                if (line.StartsWith("guid: "))
                {
                    string guid = line.Substring(6).Trim();
                    if (!string.IsNullOrEmpty(guid))
                        guids.Add(guid);
                }
                else if (line.StartsWith("-") || (!line.StartsWith(" ") && !line.StartsWith("guid")))
                {
                    if (!line.StartsWith("- {fileID:"))
                        inMaterialsSection = false;
                }
            }
        }

        return guids;
    }

    private static void ForceReimportAllMaterials()
    {
        string[] materialPaths = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);

        foreach (string matPath in materialPaths)
        {
            string assetPath = matPath.Replace("\\", "/");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        Debug.Log($"🔄 Force reimported {materialPaths.Length} materials");
    }
}