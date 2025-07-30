using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace KikiNgao.SimpleBikeControl
{
    public class SettingWindow : EditorWindow
    {
        private readonly static int maxLayers = 31;
        private GameObject bike;
        private GameObject frontWheel, rearWheel;
        private Transform handlebarTrans;
        private Transform bodyTrans;
        private Transform cranksetTrans;
        private Transform pendalLeftTrans,pendalRightTrans;

        private GameObject biker;
        private Transform footLeftTrans, footRightTrans;
        
        private readonly string bikeLayerName = "Bike";
        private readonly string playerLayerName = "Player";

        [MenuItem("Tool/SimpleBike/Setting")]
        public static void ShowWindow()
        {
            GetWindow<SettingWindow>("Setting");
        }

        [MenuItem("Tool/Remove all Component")]
        public static void RemoveComponents()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Component [] components = obj.GetComponents<Component>();
                foreach(Component com in components)
                {
                    DestroyImmediate(com);
                }
            }
        }

        private void OnGUI()
        {
            // Layer
            GUILayout.Label("Layer Setting", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Layer", GUILayout.Width(150)))
            {
                CreateLayer(playerLayerName);
                CreateLayer(bikeLayerName);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Bike
            
            GUILayout.Label("Bike Setting", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            bike = EditorGUILayout.ObjectField("Bike", bike, typeof(GameObject), true) as GameObject;
            frontWheel = EditorGUILayout.ObjectField("Front Wheel", frontWheel, typeof(GameObject), true) as GameObject;
            rearWheel = EditorGUILayout.ObjectField("Rear Wheel", rearWheel, typeof(GameObject), true) as GameObject;
            handlebarTrans = EditorGUILayout.ObjectField("Handlebar", handlebarTrans, typeof(Transform), true) as Transform;
            bodyTrans = EditorGUILayout.ObjectField("Body", bodyTrans, typeof(Transform), true) as Transform;
            cranksetTrans = EditorGUILayout.ObjectField("Crankset", cranksetTrans, typeof(Transform), true) as Transform;
            pendalLeftTrans = EditorGUILayout.ObjectField("Pendal Left",pendalLeftTrans, typeof(Transform), true) as Transform;
            pendalRightTrans = EditorGUILayout.ObjectField("Pendal Right", pendalRightTrans, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();
           
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Setup Bike", GUILayout.Width(150)))
            {
                SetUpBike();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
          

            //Biker
            
            GUILayout.Label("Biker Setting", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            biker = EditorGUILayout.ObjectField("Biker", biker, typeof(GameObject), true) as GameObject;
            footLeftTrans = EditorGUILayout.ObjectField("Foot Left", footLeftTrans, typeof(Transform), true) as Transform;
            footRightTrans = EditorGUILayout.ObjectField("Foot Right", footRightTrans, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Setup Biker" ,GUILayout.Width(150)))
            {
                SetUpBiker();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void SetUpBike()
        {
            if (!bike || !frontWheel || !rearWheel || !handlebarTrans || !bodyTrans || !cranksetTrans || !pendalLeftTrans || !pendalRightTrans)
            {
                Debug.LogWarning("Missing some part"); return;
            }
            if (!LayerExists(bikeLayerName)) { Debug.LogWarning("Missing Bike layer"); return; }
            
            // Setup Rigidbody
            PrefabUtility.UnpackPrefabInstance(bike, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Rigidbody m_Rigidbody = bike.AddComponent<Rigidbody>();
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.mass = 200;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            //Add and setup BoxCollider
            #region BoxCollider
            BoxCollider bodyCollider = bike.AddComponent<BoxCollider>();

            Vector3 boxSize = new Vector3();
            Vector3 boxCenter = new Vector3();

            MeshRenderer bodyMesh = bodyTrans.GetComponent<MeshRenderer>();
            MeshRenderer wheelMesh = rearWheel.GetComponent<MeshRenderer>();
            if (bodyMesh)
            {
                boxSize.x = bodyTrans.GetComponent<MeshRenderer>().bounds.size.x;
                boxCenter.y = wheelMesh != null ? bodyMesh.bounds.size.y / 2 + wheelMesh.bounds.size.y / 2 : 0.5f;
            }


            else boxSize.x = 0.1f;

            boxSize.z = Vector3.Distance(frontWheel.transform.position, rearWheel.transform.position) + 0.1f;
            boxSize.y = 0.3f;

            bodyCollider.center = boxCenter;
            bodyCollider.size = boxSize;
            #endregion

            #region SimpleBike
            SimpleBike simpleBike = bike.AddComponent<SimpleBike>();

            //Create Biker Holder , add to SimpleBike
            GameObject bikerHolder = new GameObject("Biker Holder");
            bikerHolder.transform.parent = bike.transform;
            bikerHolder.transform.localPosition = new Vector3(0, 0.04f, -0.2f);
            simpleBike.bikerHolder = bikerHolder.transform;

            // Create Biker Detector Box collider
            GameObject bikerDetector = new GameObject("Biker Detector");
            bikerDetector.transform.parent = bike.transform;
            bikerDetector.transform.localPosition = Vector3.zero;
            BoxCollider detectorBoxCol = bikerDetector.AddComponent<BoxCollider>();
            detectorBoxCol.center = new Vector3(0,0.5f,0);
            detectorBoxCol.size = new Vector3(0.6f, 1, 1);
            detectorBoxCol.isTrigger = true;

            //Create Wheels Collider
            GameObject frontWheelColliderObj = CreateWheelCollider("FrontWheelCollider");
            GameObject rearWheelColliderObj = CreateWheelCollider("RearWheelCollider");

            var frontWheelCollider = frontWheelColliderObj.GetComponent<WheelCollider>();
            frontWheelCollider.radius = frontWheel.GetComponent<MeshRenderer>().bounds.size.z/2;

            var rearWheelCollider = rearWheelColliderObj.GetComponent<WheelCollider>();
            rearWheelCollider.radius = rearWheel.GetComponent<MeshRenderer>().bounds.size.z / 2;

            // Set parent and position Wheels
            if(frontWheel.transform.parent != handlebarTrans) frontWheel.transform.parent = handlebarTrans;
            if(frontWheelCollider.transform.parent != handlebarTrans) frontWheelCollider.transform.parent = handlebarTrans;
            rearWheelCollider.transform.parent = rearWheel.transform.parent;

            frontWheelColliderObj.transform.position = frontWheel.transform.position;
            rearWheelColliderObj.transform.position = rearWheel.transform.position;

            //add Wheels and Wheels Collider to simple Bike
            simpleBike.frontWheelCollider = frontWheelCollider;
            simpleBike.rearWheelCollider = rearWheelCollider;
            simpleBike.frontWheel = frontWheel;
            simpleBike.rearWheel = rearWheel;
            simpleBike.handlerBar = handlebarTrans;
            simpleBike.cranksetTransform = cranksetTrans;
            if (cranksetTrans.childCount < 2) Debug.LogError("Missing Pendal in " + cranksetTrans.name);

            //creat Impact box
            GameObject f_ImpactBoxObj = new GameObject("Front Impact Box");
            GameObject r_ImpactBoxObj = new GameObject("Rear Impact Box");

            f_ImpactBoxObj.transform.position = frontWheel.transform.position;
            f_ImpactBoxObj.transform.parent = handlebarTrans;

            BoxCollider f_ImpactBoxCol = f_ImpactBoxObj.AddComponent<BoxCollider>();
            f_ImpactBoxCol.size = new Vector3(0.05f, 0.05f, frontWheelCollider.radius * 2);

            r_ImpactBoxObj.transform.position = rearWheel.transform.position;
            r_ImpactBoxObj.transform.parent = bodyTrans.transform;

            BoxCollider r_ImpactBoxCol = r_ImpactBoxObj.AddComponent<BoxCollider>();
            r_ImpactBoxCol.size = new Vector3(0.05f, 0.05f, rearWheelCollider.radius * 2);

            #region IK Target
            // Create and Add Ik Target
            GameObject lefHandTarget = new GameObject("Left hand target");
            GameObject rightHandTarget = new GameObject("Right hand target");
            GameObject leftPendalTarget = new GameObject("Left Pendal");
            GameObject rightPendalTarget = new GameObject("Right Pendal");
            GameObject leftStandingTarget = new GameObject("Left Standing Point");
            GameObject rightStandingTarget = new GameObject("Right Standing Point");

            // hand target parent and position
            lefHandTarget.transform.parent = handlebarTrans;
            rightHandTarget.transform.parent = handlebarTrans;

            var handlebarSize = handlebarTrans.GetComponent<MeshRenderer>().bounds.size;
            
            lefHandTarget.transform.localPosition = new Vector3(-handlebarSize.x/2, 0.35f,0);
            rightHandTarget.transform.localPosition = new Vector3(handlebarSize.x/2, 0.35f, 0);

            // pendalTarget
            leftPendalTarget.transform.parent = pendalLeftTrans;
            leftPendalTarget.transform.localPosition = Vector3.zero ;

            rightPendalTarget.transform.parent = pendalRightTrans;
            rightPendalTarget.transform.localPosition = Vector3.zero;

            // StandingTarget
            leftStandingTarget.transform.parent = bodyTrans;
            leftStandingTarget.transform.localPosition = new Vector3(-0.2f, 0.1f, -0.15f);

            rightStandingTarget.transform.parent = bodyTrans;
            rightStandingTarget.transform.localPosition = new Vector3(0.2f, 0.1f, -0.15f);

            //add IKTarget to SimpleBike
            simpleBike.leftHandTarget = lefHandTarget.transform;
            simpleBike.rightHandTarget = rightHandTarget.transform;
            simpleBike.leftPendalTarget = leftPendalTarget.transform;
            simpleBike.rightPendalTarget = rightPendalTarget.transform;
            simpleBike.leftStandTarget = leftStandingTarget.transform;
            simpleBike.rightStandTarget = rightStandingTarget.transform;

            #endregion
            #endregion

            // Setup layer 
            //Add Layer to Bike and collider type object

            int bikeLayerInt = LayerMask.NameToLayer(bikeLayerName);
            bike.layer = bikeLayerInt;

            frontWheelCollider.gameObject.layer = bikeLayerInt;
            rearWheelCollider.gameObject.layer = bikeLayerInt;
            r_ImpactBoxObj.layer = bikeLayerInt;
            f_ImpactBoxObj.layer = bikeLayerInt;

            Debug.Log("Setup bike done!");
        }

        // Creat Wheels collider and copy Wheel Collider Component value from prefab
        GameObject CreateWheelCollider(string name)
        {
            GameObject wheelColObj = new GameObject(name);
            

            var defaultWheel = Resources.Load<GameObject>("WheelData/RearWheel Collider");            
            if (!defaultWheel) Debug.LogWarning("Can't find default wheel data from Resources");

            WheelCollider defaultWheelCol = defaultWheel.GetComponent<WheelCollider>();

            CopyWheelColliderComponent(defaultWheelCol, wheelColObj);

            return wheelColObj;
        }

        private void CopyWheelColliderComponent(WheelCollider from, GameObject to )
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(from);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to);
        }

        private void SetUpBiker()
        {
            if (!biker) { Debug.LogWarning("Missing biker"); return; }
            if (!footLeftTrans || !footRightTrans) { Debug.LogWarning("Missing foot Transform"); return; }

            //add foot collider
            GameObject footColHolder_L = new GameObject("Foot Collider L");
            GameObject footColHolder_R = new GameObject("Foot Collider R");

            footColHolder_L.transform.position = footLeftTrans.position;
            footColHolder_R.transform.position = footRightTrans.position;

            footColHolder_L.transform.parent = footLeftTrans;
            footColHolder_R.transform.parent = footRightTrans;

            BoxCollider footCollider_L = footColHolder_L.AddComponent<BoxCollider>();
            BoxCollider footCollider_R = footColHolder_R.AddComponent<BoxCollider>();

            footCollider_L.size = footCollider_L.size / 10;
            footCollider_R.size = footCollider_R.size / 10;

            footCollider_L.center = new Vector3(0, -footCollider_L.size.y / 2, 0);
            footCollider_R.center = new Vector3(0, -footCollider_R.size.y / 2, 0);

            biker.AddComponent<Biker>();

            // Animator 
            Animator bikerAnim = biker.GetComponent<Animator>();
            if (!bikerAnim) Debug.LogWarning("Missing biker Animator");
            else
            {
                bikerAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bikerAnim.updateMode = AnimatorUpdateMode.Fixed;
            }

            //add Tag and layer
            biker.tag = "Player";

            int bikerLayerInt = LayerMask.NameToLayer(playerLayerName);

            biker.layer = bikerLayerInt;

            Collider[] allBikerCollider = biker.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in allBikerCollider)
            {
                col.gameObject.layer = bikerLayerInt;
            }
            Debug.Log("Setup biker done.");
        }

        #region Layer

        /// <summary>
        /// Adds the layer.
        /// </summary>
        /// <returns><c>true</c>, if layer was added, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool CreateLayer(string layerName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (!PropertyExists(layersProp, 0, maxLayers, layerName))
            {
                SerializedProperty sp;
                // Start at layer 9th index -> 8 (zero based) => first 8 reserved for unity / greyed out
                for (int i = 8, j = maxLayers; i < j; i++)
                {
                    sp = layersProp.GetArrayElementAtIndex(i);
                    if (sp.stringValue == "")
                    {
                        // Assign string value to layer
                        sp.stringValue = layerName;
                        Debug.Log("Layer: " + layerName + " has been added");
                        // Save settings
                        tagManager.ApplyModifiedProperties();
                        
                        return true;
                    }
                    if (i == j)
                        Debug.Log("All allowed layers have been filled");
                }
            }
            else
            {
                Debug.Log("Layer: " + layerName + " already exists");
            }
           
            return false;
        }

        /// <summary>
        /// Checks to see if layer exists.
        /// </summary>
        /// <returns><c>true</c>, if layer exists, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool LayerExists(string layerName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            return PropertyExists(layersProp, 0, maxLayers, layerName);
        }
        /// <summary>
        /// Checks if the value exists in the property.
        /// </summary>
        /// <returns><c>true</c>, if exists was propertyed, <c>false</c> otherwise.</returns>
        /// <param name="property">Property.</param>
        /// <param name="start">Start.</param>
        /// <param name="end">End.</param>
        /// <param name="value">Value.</param>
        private static bool PropertyExists(SerializedProperty property, int start, int end, string value)
        {
            for (int i = start; i < end; i++)
            {
                SerializedProperty t = property.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}

