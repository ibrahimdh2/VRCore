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
        // 1. Build GUID → path map for all texture assets
        var guidToPath = new Dictionary<string, string>();
        string[] textureMetaPaths = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".png.meta") || p.EndsWith(".jpg.meta") || p.EndsWith(".tga.meta") || p.EndsWith(".psd.meta"))
            .ToArray();

        foreach (string metaPath in textureMetaPaths)
        {
            string guid = null;
            foreach (string line in File.ReadLines(metaPath))
            {
                if (line.StartsWith("guid: "))
                {
                    guid = line.Substring(6).Trim();
                    break;
                }
            }

            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = metaPath.Replace(".meta", "");
                guidToPath[guid] = assetPath;
            }
        }

        // 2. Find all materials and check for texture GUIDs
        int reassignedCount = 0;
        string[] materialPaths = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories);

        foreach (string matPath in materialPaths)
        {
            string[] matLines = File.ReadAllLines(matPath);
            foreach (string line in matLines)
            {
                if (line.Trim().StartsWith("guid: "))
                {
                    string guid = line.Trim().Substring(6).Trim();

                    if (guidToPath.TryGetValue(guid, out string texPath))
                    {
                        // Load both material and texture
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath.Replace(Application.dataPath, "Assets"));
                        Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);

                        if (mat != null && tex != null)
                        {
                            // Assign texture to main property slot
                            if (mat.HasProperty("_BaseMap"))
                                mat.SetTexture("_BaseMap", tex);
                            else if (mat.HasProperty("_MainTex"))
                                mat.SetTexture("_MainTex", tex);

                            EditorUtility.SetDirty(mat);
                            reassignedCount++;
                            Debug.Log($"🔄 Reassigned texture to material: {mat.name} → {Path.GetFileName(texPath)}");
                        }
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (reassignedCount == 0)
            Debug.Log("✅ No missing assignments detected — all materials already linked.");
        else
            Debug.Log($"✅ Reassigned {reassignedCount} textures to materials automatically.");
    }
}
