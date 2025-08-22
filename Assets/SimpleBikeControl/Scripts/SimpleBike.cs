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
        private AnimationCurve frontWheelRestrictCurve;

        [Header("Speed Reduction on Turns")]
        [Tooltip("How aggressively speed reduces with turn angle (1 = linear, 2 = quadratic, 3 = cubic)")]
        [SerializeField] private float turnReductionCurve = 2f;

        [Tooltip("Minimum turn angle (degrees) before speed reduction starts")]
        [SerializeField] private float minTurnAngleForReduction = 2f;

        [Tooltip("Turn angle (degrees) at which maximum speed reduction occurs")]
        [SerializeField] private float maxTurnAngleForReduction = 60f;

        [Tooltip("Minimum speed multiplier at maximum turn angle (0.1 = 10% of original speed)")]
        [SerializeField] private float minSpeedMultiplier = 0.2f;

        [Header("Automatic Braking")]
        [Tooltip("Automatically apply brakes during sharp turns")]
        public bool autoBrakeOnSharpTurns = true;

        [Tooltip("Turn angle threshold (degrees) to start automatic braking")]
        [SerializeField] private float brakingTurnThreshold = 30f;

        [Tooltip("Maximum brake force applied during sharp turns")]
        [SerializeField] private float maxBrakeForce = 1500f;

        [Tooltip("Rate of turn change (degrees/sec) to detect sharp vs gradual turns")]
        [SerializeField] private float sharpTurnRate = 90f;

        [Tooltip("Only brake on rapid steering changes, not gradual ones")]
        [SerializeField] private bool onlyBrakeOnRapidChanges = true;

        private Transform centerOfMass;
        private Rigidbody m_Rigidbody;

        public Rigidbody GetRigidbody() => m_Rigidbody;
        public float GetBicycleVelocity() => m_Rigidbody.linearVelocity.magnitude * 3.61f;

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

        [Tooltip("Neutral trim if your real rig's straight-ahead isn't exactly 0°.")]
        public float straightAngle = 0f;

        [Tooltip("Ignore tiny jitter in degrees; set 0 for absolute 1:1.")]
        public float deadzoneDegrees = 0.5f;

        [Tooltip("Legacy, unused (kept for inspector back-compat).")]
        public float turnDeadZone = 0f;

        [Header("Speed-based steering")]
        public bool fasterTheSpeedSlowerTheTurn = true;

        // Telemetry
        public float calculatedTurnAngle;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }

        private float smoothedSpeed;
        public float turnSensitivity = 1;

        // Speed reduction variables
        private float currentSpeedMultiplier = 1f;
        private float targetSpeedMultiplier = 1f;

        // Turn rate tracking variables
        private float previousTurnAngle = 0f;
        private float turnRate = 0f;

        private void Awake()
        {
            // Migrate legacy field if set
            if (turnDeadZone > 0f) deadzoneDegrees = turnDeadZone;
        }

        void Start()
        {
            CreateCenterOfMass();
            SettingRigidbody();
            frontWheelRestrictCurve =
       new AnimationCurve(
           new Keyframe(0f, 1f),      // Full turning at 0
           new Keyframe(1f, 0.7f),    // Already reduced at walking speed
           new Keyframe(3f, 0.5f),    // 50% steering at 3 kph
           new Keyframe(10f, 0.35f),  // Still tighter at 10 kph
           new Keyframe(20f, 0.25f),
           new Keyframe(30, 0.2f)// Very limited at max speed
       );

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

            // Calculate speed multiplier based on turn angle (but don't apply to steering yet)
            CalculateSpeedMultiplier();

            // Drive rigidbody forward at target speed (m/s) with turn reduction
            float targetSpeedMS = (smoothedSpeed * currentSpeedMultiplier) / 3.6f;
            m_Rigidbody.linearVelocity = transform.forward * targetSpeedMS;

            // UI
            if (sensorSpeedUI) sensorSpeedUI.text = $"s: {targetSpeed:F1} KPH";
            if (currentSpeedUI) currentSpeedUI.text = $"c: {m_Rigidbody.linearVelocity.magnitude * 3.6f:F1} KPH (×{currentSpeedMultiplier:F2})";

            if (IsRest()) Rest();
            else if (IsMoving()) MovingBike();

            // IMMEDIATE STEERING: Process steering every frame regardless of movement state
            if (leftController && rightController) TurningBike();

            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();

            // Keep upright in Z (as in your original)
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0f);
        }

        // Add these fields to your class (near the other private fields)
        private bool wasBrakingLastFrame = false;
        private float brakeReleaseTimer = 0f;
        private const float BRAKE_RELEASE_DELAY = 0.1f; // Small delay to prevent brake fluttering

        // Add this improved version of the Update method
        private void Update()
        {
            // Handle steering in Update() for responsive input
            if (!ReadyToRide()) return;
            if (leftController && rightController) TurningBike();

            // Handle brake release timer
            if (brakeReleaseTimer > 0)
            {
                brakeReleaseTimer -= Time.deltaTime;
                if (brakeReleaseTimer <= 0)
                {
                    // Force clear brakes after delay
                    frontWheelCollider.brakeTorque = 0f;
                    rearWheelCollider.brakeTorque = 0f;
                    wasBrakingLastFrame = false;
                }
            }
        }

        

        /// <summary>
        /// Calculate the speed multiplier based on current turn angle
        /// The sharper the turn, the more aggressive the speed reduction
        /// </summary>
        private void CalculateSpeedMultiplier()
        {
            float absTurnAngle = Mathf.Abs(calculatedTurnAngle);

            if (absTurnAngle <= minTurnAngleForReduction)
            {
                // No speed reduction for small turns
                targetSpeedMultiplier = 1f;
            }
            else
            {
                // Calculate normalized turn progress (0 to 1)
                float turnProgress = Mathf.Clamp01((absTurnAngle - minTurnAngleForReduction) /
                                                  (maxTurnAngleForReduction - minTurnAngleForReduction));

                // Apply exponential curve for sharper turns = more aggressive reduction
                float curvedProgress = Mathf.Pow(turnProgress, turnReductionCurve);

                // Calculate speed multiplier with aggressive reduction for sharp turns
                targetSpeedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, curvedProgress);
            }

            // FIXED: Faster speed multiplier transition for immediate feel
            currentSpeedMultiplier = Mathf.Lerp(currentSpeedMultiplier, targetSpeedMultiplier, Time.deltaTime * 15f);
        }

        /// <summary>
        /// FIXED: Completely immediate steering response - no delays or smoothing
        /// </summary>
        /// <summary>
        /// FIXED: Completely immediate steering response with improved braking logic
        /// </summary>
        private void TurningBike()
        {
            if (!leftController || !rightController) return;

            // Get direct handlebar angle (wrapped to -180 to 180)
            if (!TryGetHandlebarYawWrapped(out float wrappedYawDeg))
            {
                return;
            }

            // Apply trim and sensitivity directly to wrapped angle
            float rawAngle = (wrappedYawDeg - straightAngle) * turnSensitivity;

            // Apply speed-based reduction if enabled
            float processedAngle = rawAngle;
            if (fasterTheSpeedSlowerTheTurn && frontWheelRestrictCurve != null)
            {
                float speedKph = smoothedSpeed;
                float factor = Mathf.Clamp01(frontWheelRestrictCurve.Evaluate(speedKph));
                processedAngle *= factor;
            }

            // Apply deadzone to both raw and processed angles
            if (Mathf.Abs(rawAngle) <= deadzoneDegrees) rawAngle = 0f;
            if (Mathf.Abs(processedAngle) <= deadzoneDegrees) processedAngle = 0f;

            // Clamp to mechanical limits
            float finalAngle = Mathf.Clamp(processedAngle, -maxTurnAngle, maxTurnAngle);

            // IMPROVED: Calculate turn rate only when we're actively turning (not returning to straight)
            float deltaTime = Time.fixedDeltaTime;
            float angleDelta = Mathf.DeltaAngle(previousTurnAngle, rawAngle);

            // Key fix: Only consider it a "sharp turn" if we're turning INTO a sharp angle, not OUT of it
            bool isTurningIntoSharpAngle = Mathf.Abs(rawAngle) >= brakingTurnThreshold;
            bool wasTurningSharp = Mathf.Abs(previousTurnAngle) >= brakingTurnThreshold;
            bool isReturningToStraight = wasTurningSharp && Mathf.Abs(rawAngle) < Mathf.Abs(previousTurnAngle);

            // Calculate turn rate but distinguish between turning into vs out of sharp turns
            if (deltaTime > 0)
            {
                float rawTurnRate = Mathf.Abs(angleDelta) / deltaTime;

                // If we're returning to straight from a sharp turn, don't use this for braking
                if (isReturningToStraight)
                {
                    turnRate = 0f; // Suppress turn rate when straightening out
                }
                else if (isTurningIntoSharpAngle)
                {
                    turnRate = rawTurnRate; // Only use turn rate when actively turning sharp
                }
                else
                {
                    turnRate = 0f; // No significant turn rate for gentle turns
                }
            }
            else
            {
                turnRate = 0f;
            }

            previousTurnAngle = rawAngle;

            // IMMEDIATE APPLICATION: Apply steering instantly with no lerping or delays
            calculatedTurnAngle = finalAngle;
            frontWheelCollider.steerAngle = finalAngle;

            // Apply automatic braking using improved logic
            ApplyAutomaticBraking(Mathf.Abs(rawAngle), turnRate);

            // IMMEDIATE VISUAL FEEDBACK: Update visual elements instantly
            if (handlerBar) handlerBar.localRotation = Quaternion.Euler(0f, finalAngle, 0f);
            if (handlebarAssembly) handlebarAssembly.localRotation = Quaternion.Euler(0f, finalAngle, 0f);
        }
        /// <summary>
        /// Applies automatic braking based on turn angle and rate of change if enabled
        /// </summary>
        /// <param name="absTurnAngle">Absolute turn angle in degrees</param>
        /// <param name="currentTurnRate">Current rate of turn change in degrees/sec</param>
        /// <summary>
        /// Applies automatic braking based on turn angle and rate of change if enabled
        /// Enhanced to prevent braking when returning to straight from sharp turns
        /// </summary>
        /// <param name="absTurnAngle">Absolute turn angle in degrees</param>
        /// <param name="currentTurnRate">Current rate of turn change in degrees/sec</param>
        private void ApplyAutomaticBraking(float absTurnAngle, float currentTurnRate)
        {
            if (!autoBrakeOnSharpTurns || !IsMoving())
            {
                // Clear any existing brake torque if auto-braking is disabled or not moving
                frontWheelCollider.brakeTorque = 0f;
                rearWheelCollider.brakeTorque = 0f;
                return;
            }

            // Primary condition: Must be in a sharp turn currently
            bool isCurrentlyInSharpTurn = absTurnAngle >= brakingTurnThreshold;

            // Secondary condition: If checking rapid changes, must also have rapid input
            bool hasRapidInput = !onlyBrakeOnRapidChanges || currentTurnRate >= sharpTurnRate;

            // Additional safety: Must be outside deadzone (actually turning)
            bool isActuallyTurning = absTurnAngle > deadzoneDegrees;

            // FIXED: Only brake if ALL conditions are met AND we're currently in a sharp turn
            bool shouldBrake = isActuallyTurning && isCurrentlyInSharpTurn && hasRapidInput;

            if (shouldBrake)
            {
                // Calculate brake force based on how much we exceed the threshold
                float excessAngle = absTurnAngle - brakingTurnThreshold;
                float maxExcess = maxTurnAngle - brakingTurnThreshold;
                float angleIntensity = Mathf.Clamp01(excessAngle / maxExcess);

                // If using rapid change detection, factor in turn rate intensity
                float rateIntensity = 1f;
                if (onlyBrakeOnRapidChanges && currentTurnRate > 0)
                {
                    rateIntensity = Mathf.Clamp01(currentTurnRate / (sharpTurnRate * 2f)); // More gradual ramp-up
                }

                // Combine factors with emphasis on current angle over rate
                float totalIntensity = angleIntensity * (onlyBrakeOnRapidChanges ? (0.7f + 0.3f * rateIntensity) : 1f);
                float brakeForce = totalIntensity * maxBrakeForce;

                // Apply braking
                frontWheelCollider.brakeTorque = brakeForce;
                rearWheelCollider.brakeTorque = brakeForce;

                // Debug info (remove in production)
                Debug.Log($"AUTO BRAKE: Angle={absTurnAngle:F1}° (thresh={brakingTurnThreshold}°), Rate={currentTurnRate:F1}°/s, Force={brakeForce:F0}");
            }
            else
            {
                // Clear brakes immediately when conditions aren't met
                frontWheelCollider.brakeTorque = 0f;
                rearWheelCollider.brakeTorque = 0f;

                // Debug info for when brakes are cleared
                if (absTurnAngle <= deadzoneDegrees)
                {
                    Debug.Log($"BRAKES CLEARED: Going straight (angle={absTurnAngle:F1}°)");
                }
            }
        }
        // Modified MovingBike method to ensure brakes are properly cleared
        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = 0f;
            m_Rigidbody.angularDamping = 0f;

            // Only clear brake torque if auto-braking is disabled
            // AND we're not currently in a braking condition
            if (!autoBrakeOnSharpTurns || Mathf.Abs(calculatedTurnAngle) < brakingTurnThreshold)
            {
                frontWheelCollider.brakeTorque = 0;
                rearWheelCollider.brakeTorque = 0;
                wasBrakingLastFrame = false;
                brakeReleaseTimer = 0f;
            }

            rearWheelCollider.motorTorque = 0;
            UpdateCenterOfMass();
        }

        private void Rest()
        {
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();

            // Reset speed multiplier when at rest
            currentSpeedMultiplier = 1f;
            targetSpeedMultiplier = 1f;

            // Reset turn rate tracking when at rest
            previousTurnAngle = 0f;
            turnRate = 0f;

            // Ensure brakes are cleared when at rest
            frontWheelCollider.brakeTorque = 0f;
            rearWheelCollider.brakeTorque = 0f;
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