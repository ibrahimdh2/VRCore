#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class TrafficLightsSyncherEditor : EditorWindow
{
    [MenuItem("Tools/Traffic Lights/Set Children")]
    public static void SetChildren()
    {
        int i = 0;
        Debug.Log("TrafficLightsSyncher: Method called");

        var trafficLights = GameObject.FindObjectsByType<TrafficLight>(FindObjectsSortMode.InstanceID);

        foreach (var trafficLight in trafficLights)
        {
            foreach (GameObject g in trafficLight.lightObject)
            {
                if (g == null) continue;

                if (g.transform.parent != trafficLight.transform)
                {
                    Undo.SetTransformParent(g.transform, trafficLight.transform, "Set Parent for Light");
                    g.name = "LightBlocker";
                    EditorUtility.SetDirty(g);
                }
                i++;
            }

            EditorUtility.SetDirty(trafficLight);
        }

        Debug.Log($"TrafficLightsSyncher: Done. Processed {i} light objects.");
    }
}
#endif
