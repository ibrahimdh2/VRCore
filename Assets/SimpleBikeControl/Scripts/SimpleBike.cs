using System.IO;
using TMPro;
using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class SimpleBike : MonoBehaviour
    {
        [Tooltip("Control without biker")]
        public bool noBikerCtrl;
        public Transform handlebarAssembly;

        public Transform bikerHolder;
        public WheelCollider frontWheelCollider;
        public WheelCollider rearWheelCollider;
        public GameObject frontWheel;
        public GameObject rearWheel;
        public Transform handlerBar;              // visual bar
        public Transform cranksetTransform;

        [Header("Physics")]
        [SerializeField] private float legPower = 10;
        [SerializeField] private float airResistance = 6;
        [SerializeField] private float restDrag = 2f;
        [SerializeField] private float restAngularDrag = .2f;
        [SerializeField] private float forceRatio = 2f;
        [SerializeField]
        private AnimationCurve frontWheelRestrictCurve =
            new AnimationCurve(new Keyframe(0f, 35f), new Keyframe(50f, 1f));

        private Transform centerOfMass;
        private Rigidbody m_Rigidbody;

        public Rigidbody GetRigidbody() => m_Rigidbody;

        [Header("VR Inputs (mounted to the real handlebar)")]
        public Transform leftController;
        public Transform rightController;

        [Header("Speed/Debug UI")]
        [SerializeField] private SpeedReceiver speedReceiver;
        [SerializeField] private TextMeshProUGUI sensorSpeedUI;
        [SerializeField] private TextMeshProUGUI currentSpeedUI;

        [Header("Steering (1:1 mapping)")]
        [Tooltip("If true, the wheel steer angle equals the handlebar angle exactly.")]
        public bool oneToOneSteer = true;

        [Tooltip("Maximum allowed steer in degrees (hard mechanical stop).")]
        public float maxTurnAngle = 85f;

        [Tooltip("Neutral trim if your real rig’s straight-ahead isn’t exactly 0°.")]
        public float straightAngle = 0f;

        [Tooltip("Ignore tiny jitter in degrees; set 0 for absolute 1:1.")]
        public float deadzoneDegrees = 0.5f;

        [Tooltip("Legacy, unused (kept for inspector back-compat).")]
        public float turnDeadZone = 0f;

        // Telemetry
        public float calculatedTurnAngle;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }

        private float smoothedSpeed;
        public float turnSensitivity = 1;

        // ==== NEW: steering unwrap state ====
        private float _prevWrappedYaw;   // last [-180..180] sample
        private float _unwrappedYaw;     // accumulated continuous yaw
        private bool _havePrevYaw;

        private void Awake()
        {
            // Migrate legacy field if set
            if (turnDeadZone > 0f) deadzoneDegrees = turnDeadZone;
        }

        void Start()
        {
            CreateCenterOfMass();
            SettingRigidbody();
            Freeze = true;
        }

        private void CreateCenterOfMass()
        {
            centerOfMass = new GameObject("CenterOfMass").transform;
            Vector3 center = rearWheelCollider.transform.position;
            center.z += (frontWheelCollider.transform.position.z - center.z) / 2f;
            center.y = 0f;
            centerOfMass.position = center;
            centerOfMass.parent = transform;
        }

        private void SettingRigidbody()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Rigidbody.centerOfMass = centerOfMass.localPosition;
        }

        private void FixedUpdate()
        {
            if (!ReadyToRide()) return;

            // Smooth the reported speed a touch (optional)
            float targetSpeed = speedReceiver != null ? speedReceiver.speedKph : 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, 0.1f);

            // Drive rigidbody forward at target speed (m/s)
            float targetSpeedMS = smoothedSpeed / 3.6f;
            m_Rigidbody.linearVelocity = transform.forward * targetSpeedMS;

            // UI
            if (sensorSpeedUI) sensorSpeedUI.text = $"s: {targetSpeed:F1} KPH";
            if (currentSpeedUI) currentSpeedUI.text = $"c: {m_Rigidbody.linearVelocity.magnitude * 3.6f:F1} KPH";

            if (IsRest()) Rest();
            else if (IsMoving()) MovingBike();

            // === CHANGED: steering now uses unwrapped yaw ===
            if (leftController && rightController) TurningBike();
            else _havePrevYaw = false; // lost controllers → reset unwrap state

            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();

            // Keep upright in Z (as in your original)
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0f);
        }

        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = 0f;
            m_Rigidbody.angularDamping = 0f;

            frontWheelCollider.brakeTorque = 0;
            rearWheelCollider.brakeTorque = 0;
            rearWheelCollider.motorTorque = 0;

            UpdateCenterOfMass();
        }

        // === REPLACED: continuous steering with unwrap ===
        private void TurningBike()
        {
            if (!leftController || !rightController) return;

            // 1) Read wrapped yaw in [-180..180]
            if (!TryGetHandlebarYawWrapped(out float wrappedYawDeg))
            {
                _havePrevYaw = false;
                return;
            }

            // 2) Unwrap to make it continuous (no jumps at ±180)
            if (!_havePrevYaw)
            {
                _prevWrappedYaw = wrappedYawDeg;
                _unwrappedYaw = 0f;   // start from zero relative to first reading
                _havePrevYaw = true;
            }
            float delta = Mathf.DeltaAngle(_prevWrappedYaw, wrappedYawDeg); // smallest signed delta
            _prevWrappedYaw = wrappedYawDeg;
            _unwrappedYaw += delta;

            // 3) Apply trim/deadzone/sensitivity on the continuous value
            float raw = (_unwrappedYaw - straightAngle) * turnSensitivity;
            if (Mathf.Abs(raw) <= deadzoneDegrees) raw = 0f;

            // 4) Mechanical limits and apply
            float finalAngle = Mathf.Clamp(raw, -maxTurnAngle, maxTurnAngle);
            calculatedTurnAngle = wrappedYawDeg; // telemetry (wrapped sample if you want to display it)

            frontWheelCollider.steerAngle = finalAngle;

            if (handlerBar) handlerBar.localRotation = Quaternion.Euler(0f, finalAngle, 0f);
            if (handlebarAssembly) handlebarAssembly.localRotation = Quaternion.Euler(0f, finalAngle, 0f);
        }

        private void Rest()
        {
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();

            // Optional: slowly relax unwrap when fully at rest
            // Keeps state sane if the rider dismounts after many turns
            if (!_havePrevYaw) _unwrappedYaw = 0f;
        }

        private void ResetWheelsCollider()
        {
            frontWheelCollider.steerAngle = 0f;
            frontWheelCollider.motorTorque = 0;
            rearWheelCollider.motorTorque = 0;
            rearWheelCollider.brakeTorque = 0;
            frontWheelCollider.brakeTorque = 0;
        }

        private void UpdateCranksetRotation()
        {
            float kmh = GetBikeSpeedKm();
            cranksetTransform.rotation *= Quaternion.Euler(kmh / forceRatio, 0, 0);
            Quaternion ro = Quaternion.Euler(-kmh / forceRatio, 0, 0);
            if (cranksetTransform.childCount >= 2)
            {
                cranksetTransform.GetChild(0).rotation *= ro;
                cranksetTransform.GetChild(1).rotation *= ro;
            }
        }

        private void UpdateWheelDisplay()
        {
            rearWheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            rearWheel.transform.position = pos;

            Quaternion rearWheelRot = rearWheel.transform.rotation;
            float kmh = GetBikeSpeedKm();
            rearWheel.transform.rotation = IsReverse()
                ? rearWheelRot * Quaternion.Euler(-kmh, 0, 0)
                : rearWheelRot * Quaternion.Euler(kmh, 0, 0);

            frontWheel.transform.localRotation = rearWheel.transform.localRotation;
        }

        private void UpdateCenterOfMass()
        {
            centerOfMass.localPosition = new Vector3(0, -0.8f, 0);
            m_Rigidbody.centerOfMass = centerOfMass.localPosition;
        }

        private float GetBikeSpeedKm() => GetBikeSpeedMs() * 3.6f;
        private float GetBikeSpeedMs() => m_Rigidbody.linearVelocity.magnitude;

        // === REPLACED: stable handlebar yaw (wrapped to [-180..180]) ===
        /// <summary>
        /// Returns the handlebar yaw in [-180..180] degrees (wrapped).
        /// Positive = left turn, Negative = right turn (bike-local space).
        /// Derived from the perpendicular of the grips line (left→right).
        /// </summary>
        private bool TryGetHandlebarYawWrapped(out float angleDeg)
        {
            angleDeg = 0f;
            if (!leftController || !rightController) return false;

            // Controller positions in bike-local space, ignore vertical
            Vector3 leftLocal = transform.InverseTransformPoint(leftController.position);
            Vector3 rightLocal = transform.InverseTransformPoint(rightController.position);
            leftLocal.y = 0f; rightLocal.y = 0f;

            Vector3 barRight = rightLocal - leftLocal;           // grips line (left→right)
            if (barRight.sqrMagnitude < 1e-6f) return false;     // hands coincide: no reliable reading

            // Perpendicular gives the bar's forward in bike space:
            // right × up = forward (so straight bar reads +Z)
            Vector3 barForward = Vector3.Cross(barRight, Vector3.up).normalized;

            
            // Yaw relative to bike forward (Z+)
            angleDeg = Vector3.SignedAngle(Vector3.forward, barForward, Vector3.up);
            return true;
        }

        public bool ReadyToRide()
        {
            if (noBikerCtrl) return true;
            if (bikerHolder.childCount == 0) return false;
            return bikerHolder.GetChild(0).CompareTag("Player");
        }

        public bool IsReverse() => false;
        public bool IsMovingToward => speedReceiver != null && speedReceiver.speedKph > 0;
        private bool IsRest() => speedReceiver == null || speedReceiver.speedKph < 0.1f;
        public bool IsMoving() => (speedReceiver != null && speedReceiver.speedKph > 0.1f) || m_Rigidbody.linearVelocity.sqrMagnitude > 0.01f;
    }
}
