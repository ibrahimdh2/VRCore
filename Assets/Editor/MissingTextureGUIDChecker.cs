using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MissingTextureGUIDChecker
{
    [MenuItem("Tool/Check Missing Texture GUIDs in Materials")]
    public static void FindMissingTextureGUIDs()
    {
        // 1. Collect all texture GUIDs from .meta files in Assets
        var allTextureMetaGUIDs = new HashSet<string>();
        string[] texturePaths = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".png.meta") || p.EndsWith(".jpg.meta") || p.EndsWith(".tga.meta") || p.EndsWith(".psd.meta"))
            .ToArray();

        foreach (var metaPath in texturePaths)
        {
            string[] lines = File.ReadAllLines(metaPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("guid: "))
                {
                    allTextureMetaGUIDs.Add(line.Substring(6).Trim());
                    break;
                }
            }
        }

        // 2. Scan all materials for texture GUIDs
        string[] materialPaths = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);
        var missingGUIDs = new Dictionary<string, string>(); // guid => material

        foreach (string matPath in materialPaths)
        {
            string[] lines = File.ReadAllLines(matPath);
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("guid: "))
                {
                    string guid = line.Trim().Substring(6).Trim();
                    if (!allTextureMetaGUIDs.Contains(guid))
                    {
                        if (!missingGUIDs.ContainsKey(guid))
                            missingGUIDs[guid] = matPath;
                    }
                }
            }
        }

        // 3. Report results
        if (missingGUIDs.Count == 0)
        {
            Debug.Log("✅ All texture GUIDs referenced by materials are valid.");
        }
        else
        {
            Debug.LogWarning($"⚠️ {missingGUIDs.Count} missing texture GUID(s) found in materials:");
            foreach (var kv in missingGUIDs)
            {
                Debug.LogWarning($"Missing GUID: {kv.Key} in material: {kv.Value}");
            }
        }
    }
}
