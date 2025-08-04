using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MissingTextureGUIDChecker
{
    [MenuItem("Tools/Fix Material Texture References")]
    public static void FindAndFixMissingTextureGUIDs()
    {
        Debug.Log("🔧 Starting material texture reference fix...");

        // 1. Build GUID to path mapping for textures
        var textureGuidToPath = BuildTextureGuidMap();
        Debug.Log($"Found {textureGuidToPath.Count} texture assets");

        // 2. Process each material file directly
        int totalFixed = 0;
        string[] materialFiles = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);

        foreach (string matFile in materialFiles)
        {
            totalFixed += ProcessMaterialFile(matFile, textureGuidToPath);
        }

        // 3. Save everything
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ Successfully processed {materialFiles.Length} materials, fixed {totalFixed} texture assignments");
    }

    private static Dictionary<string, string> BuildTextureGuidMap()
    {
        var guidToPath = new Dictionary<string, string>();

        // Find all texture meta files
        string[] allFiles = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
        string[] textureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr", ".tiff", ".bmp" };

        foreach (string metaFile in allFiles)
        {
            string assetFile = metaFile.Replace(".meta", "");

            // Check if this is a texture file
            bool isTexture = textureExtensions.Any(ext => assetFile.ToLower().EndsWith(ext));
            if (!isTexture) continue;

            // Extract GUID from meta file
            string guid = ExtractGuidFromMetaFile(metaFile);
            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = assetFile.Replace("\\", "/");
                if (assetPath.StartsWith("Assets/"))
                {
                    guidToPath[guid] = assetPath;
                }
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

    private static int ProcessMaterialFile(string matFile, Dictionary<string, string> textureGuidToPath)
    {
        try
        {
            string assetPath = matFile.Replace("\\", "/");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material == null)
            {
                Debug.LogWarning($"Could not load material: {assetPath}");
                return 0;
            }

            // Read the raw material file content
            string materialContent = File.ReadAllText(matFile);

            // Parse texture assignments from the material file
            var textureAssignments = ParseMaterialTextureAssignments(materialContent);

            int fixedCount = 0;
            foreach (var assignment in textureAssignments)
            {
                string propertyName = assignment.Key;
                string textureGuid = assignment.Value;

                // Skip empty or null GUIDs
                if (string.IsNullOrEmpty(textureGuid) || textureGuid == "0000000000000000f000000000000000")
                    continue;

                // Find the texture by GUID
                if (textureGuidToPath.TryGetValue(textureGuid, out string texturePath))
                {
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);

                    if (texture != null && material.HasProperty(propertyName))
                    {
                        // Get current texture to compare
                        Texture currentTexture = material.GetTexture(propertyName);

                        // Only assign if different or if current is null (broken reference)
                        if (currentTexture != texture)
                        {
                            material.SetTexture(propertyName, texture);
                            EditorUtility.SetDirty(material);
                            fixedCount++;

                            string currentName = currentTexture != null ? currentTexture.name : "null";
                            Debug.Log($"🔄 {material.name}.{propertyName}: {currentName} → {texture.name}");
                        }
                    }
                    else if (!material.HasProperty(propertyName))
                    {
                        Debug.LogWarning($"Property {propertyName} not found on material {material.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Texture with GUID {textureGuid} not found for {material.name}.{propertyName}");
                }
            }

            return fixedCount;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing material {matFile}: {e.Message}");
            return 0;
        }
    }

    private static Dictionary<string, string> ParseMaterialTextureAssignments(string materialContent)
    {
        var assignments = new Dictionary<string, string>();
        string[] lines = materialContent.Split('\n');

        // State tracking for parsing
        string currentProperty = null;
        bool inTexturesSection = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmedLine = line.Trim();

            // Check if we're entering the textures section
            if (trimmedLine.StartsWith("m_SavedProperties:"))
            {
                // Look for m_TexEnvs in the next few lines
                for (int j = i + 1; j < Mathf.Min(i + 10, lines.Length); j++)
                {
                    if (lines[j].Trim().StartsWith("m_TexEnvs:"))
                    {
                        inTexturesSection = true;
                        i = j;
                        break;
                    }
                }
                continue;
            }

            if (!inTexturesSection) continue;

            // Check if we've left the textures section
            if (trimmedLine.StartsWith("m_Floats:") || trimmedLine.StartsWith("m_Colors:"))
            {
                inTexturesSection = false;
                continue;
            }

            // Look for texture property names
            if (trimmedLine.StartsWith("- _"))
            {
                // Extract property name (e.g., "- _MainTex:" becomes "_MainTex")
                int colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex > 0)
                {
                    currentProperty = trimmedLine.Substring(2, colonIndex - 2); // Remove "- " prefix
                }
            }

            // Look for GUID within a property section
            if (!string.IsNullOrEmpty(currentProperty) && trimmedLine.StartsWith("guid: "))
            {
                string guid = trimmedLine.Substring(6).Trim();
                if (!string.IsNullOrEmpty(guid))
                {
                    assignments[currentProperty] = guid;
                    currentProperty = null; // Reset for next property
                }
            }
        }

        return assignments;
    }
}