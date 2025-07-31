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

        public Transform handlerBar;
        public Transform cranksetTransform;

        [SerializeField] private float legPower = 10;
        [SerializeField] private float powerUpMax = 2;
        [SerializeField] private float powerUpSpeed = .5f;
        [SerializeField] private float airResistance = 6;
        [SerializeField] private float turningSmooth = .8f;
        [SerializeField] private float restDrag = 2f;
        [SerializeField] private float restAngularDrag = .2f;
        [SerializeField] private float forceRatio = 2f;
        [SerializeField] private AnimationCurve frontWheelRestrictCurve = new AnimationCurve(new Keyframe(0f, 35f), new Keyframe(50f, 1f));
        float maxTorque = 5000f;
        float maxEffectiveSpeed = 60f; // km/h where torque starts to drop
        private Transform centerOfMass;
        private Rigidbody m_Rigidbody;

        public Rigidbody GetRigidbody() => m_Rigidbody;

        public Transform leftController;
        public Transform rightController;

        private float temporaryFrontWheelAngle;
        private float handlerBarYLastAngle;
        private float currentLegPower;
        private float reversePower;
        private EventManager eventManager;

        public bool IsReverse() => false;
        public bool IsMovingToward => speedReceiver.speedKph > 0;
        private bool IsRest() => speedReceiver.speedKph < 0.1f; // Changed threshold
        public bool IsMoving() => speedReceiver.speedKph > 0.1f || m_Rigidbody.linearVelocity.sqrMagnitude > 0.01f;
        private bool IsTurning() => frontWheelCollider.steerAngle != 0;
        private bool IsSpeedUp() => false;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }

        public bool ReadyToRide()
        {
            if (noBikerCtrl) return true;
            if (bikerHolder.childCount == 0) return false;
            return bikerHolder.GetChild(0).CompareTag("Player");
        }

        void Start()
        {
            CreateCenterOfMass();
            SettingRigidbody();

            currentLegPower = legPower * 10;
            reversePower = legPower * 3;

            Freeze = true;
        }

        private void CreateCenterOfMass()
        {
            centerOfMass = new GameObject("CenterOfMass").transform;
            Vector3 center = rearWheelCollider.transform.position;
            center.z += (frontWheelCollider.transform.position.z - center.z) / 2;
            center.y = 0;
            centerOfMass.position = center;
            centerOfMass.parent = transform;
        }

        private void SettingRigidbody()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Rigidbody.centerOfMass = centerOfMass.localPosition;
        }

        float powerUp = 1f;
        [SerializeField] private SpeedReceiver speedReceiver;
        [SerializeField] private float turnSensitivity;
        [SerializeField] private float rotationMultiplier;
        [SerializeField] private int maxTurnAngle;



        private void FixedUpdate()
        {
            if (!ReadyToRide()) return;

            if (IsRest()) Rest();
            else if (IsMoving()) MovingBike();

            if (IsTurning() || (leftController && rightController)) TurningBike();

            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();

            // Prevent bicycle tilt (lock Z-axis rotation)
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0f);
        }

        private void UpdateLegPower(bool speedUp)
        {

        }

        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = m_Rigidbody.mass > 0 ? GetBikeSpeedMs() / m_Rigidbody.mass * airResistance : 0f;
            m_Rigidbody.angularDamping = 5 + GetBikeSpeedMs() / (m_Rigidbody.mass / 10);

            frontWheelCollider.brakeTorque = 0;

            // Apply torque based on movement and speed
            rearWheelCollider.motorTorque = GetSimulatedTorqueFromSpeed(speedReceiver.speedKph);

            UpdateCenterOfMass();
        }

        private void TurningBike()
        {
            temporaryFrontWheelAngle = frontWheelRestrictCurve.Evaluate(GetBikeSpeedKm());

            float inputAngle = 0f;
            if (leftController != null && rightController != null)
            {
                inputAngle = CalculateHandlebarAngle() / maxTurnAngle;
            }

            float nextAngle = temporaryFrontWheelAngle * inputAngle;
            frontWheelCollider.steerAngle = nextAngle;

            // Visually rotate handlerBar (grips)
            Quaternion handlerBarLocalRotation = Quaternion.Euler(0, nextAngle - handlerBarYLastAngle, 0);
            handlerBar.rotation = Quaternion.Lerp(handlerBar.rotation, handlerBar.rotation * handlerBarLocalRotation, turningSmooth);
            handlerBarYLastAngle = nextAngle;

            // Visually rotate the handlebar assembly (optional visual fork turning)
            if (handlebarAssembly != null)
            {
                Quaternion targetRotation = Quaternion.Euler(0, nextAngle, 0);
                handlebarAssembly.localRotation = Quaternion.Lerp(handlebarAssembly.localRotation, targetRotation, turningSmooth);
            }
        }

        private void Rest()
        {
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();
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
            cranksetTransform.rotation *= Quaternion.Euler(GetBikeSpeedKm() / forceRatio, 0, 0);
            Quaternion ro = Quaternion.Euler(-GetBikeSpeedKm() / forceRatio, 0, 0);
            cranksetTransform.GetChild(0).rotation *= ro;
            cranksetTransform.GetChild(1).rotation *= ro;
        }

        private void UpdateWheelDisplay()
        {
            Vector3 pos;
            Quaternion rot;

            rearWheelCollider.GetWorldPose(out pos, out rot);
            rearWheel.transform.position = pos;

            Quaternion rearWheelRot = rearWheel.transform.rotation;
            rearWheel.transform.rotation = IsReverse() ? rearWheelRot * Quaternion.Euler(-GetBikeSpeedKm(), 0, 0)
                                                        : rearWheelRot * Quaternion.Euler(GetBikeSpeedKm(), 0, 0);

            frontWheel.transform.localRotation = rearWheel.transform.localRotation;
        }

        private void UpdateCenterOfMass()
        {
            centerOfMass.localPosition = new Vector3(0, -0.8f, 0);
            m_Rigidbody.centerOfMass = centerOfMass.localPosition;
        }

        private float GetBikeSpeedKm() => GetBikeSpeedMs() * 3.6f;
        private float GetBikeSpeedMs() => m_Rigidbody.linearVelocity.magnitude;

        private float CalculateHandlebarAngle()
        {
            if (leftController == null || rightController == null)
                return 0f;

            Vector3 leftPos = leftController.position;
            Vector3 rightPos = rightController.position;

            Vector3 handlebarVector = rightPos - leftPos;
            Vector3 localHandlebarVector = transform.InverseTransformDirection(handlebarVector);
            localHandlebarVector.y = 0;

            if (localHandlebarVector.magnitude == 0) return 0f;

            float angle = Mathf.Atan2(localHandlebarVector.z, localHandlebarVector.x) * Mathf.Rad2Deg;
            float handlebarTurnAngle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);
            return -handlebarTurnAngle;
        }

        [Header("Torque Simulation")]
        public AnimationCurve torqueCurve = AnimationCurve.Linear(0, 150f, 150f, 0f);
        public float wheelRadius = 0.35f; // meters

        public float GetSimulatedTorqueFromSpeed(float speedKmh)
        {
            // Convert speed sensor reading to the torque needed to maintain that speed

            // If no speed detected, no torque needed
            if (speedKmh < 0.1f)
            {
                return 0f;
            }

            // Calculate the torque needed to overcome resistance at this speed
            float speedMps = speedKmh / 3.6f;

            // Estimate resistance forces that need to be overcome:
            // 1. Air resistance (increases with speed squared)
            float airResistanceForce = airResistance * speedMps * speedMps;

            // 2. Rolling resistance (roughly constant)
            float rollingResistance = m_Rigidbody.mass * 9.81f * 0.01f; // ~1% of weight

            // 3. Additional drag from Unity's physics
            float unityDrag = m_Rigidbody.linearDamping * speedMps * m_Rigidbody.mass;

            // Total force needed to maintain this speed
            float totalResistanceForce = airResistanceForce + rollingResistance + unityDrag;

            // Convert force to torque (Force = Torque / wheelRadius)
            float requiredTorque = totalResistanceForce * wheelRadius;

            // Add some extra torque for acceleration/maintaining momentum
            float momentumTorque = speedKmh * legPower * 0.5f;

            float finalTorque = requiredTorque + momentumTorque;

            return Mathf.Clamp(finalTorque, 0f, maxTorque);
        }
    }
}