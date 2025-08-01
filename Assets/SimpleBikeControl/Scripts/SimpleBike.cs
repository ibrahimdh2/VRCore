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
        public float maxTorque = 5000f;

        private Transform centerOfMass;
        private Rigidbody m_Rigidbody;

        public Rigidbody GetRigidbody() => m_Rigidbody;

        public Transform leftController;
        public Transform rightController;

        private float temporaryFrontWheelAngle;
        private float handlerBarYLastAngle;
        private float currentLegPower;
        private float reversePower;

        private float rollingResistanceCoefficient;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }

        [SerializeField] private SpeedReceiver speedReceiver;
        [SerializeField] private TextMeshProUGUI sensorSpeedUI;
        [SerializeField] private TextMeshProUGUI currentSpeedUI;
        [SerializeField] private float turnSensitivity;
        [SerializeField] private float rotationMultiplier;
        [SerializeField] private int maxTurnAngle;

        private float powerUp = 1f;

        public bool IsReverse() => false;
        public bool IsMovingToward => speedReceiver.speedKph > 0;
        private bool IsRest() => speedReceiver.speedKph < 0.1f;
        public bool IsMoving() => speedReceiver.speedKph > 0.1f || m_Rigidbody.linearVelocity.sqrMagnitude > 0.01f;
        private bool IsTurning() => frontWheelCollider.steerAngle != 0;
        private bool IsSpeedUp() => false;

        // PID control fields
        public float kp = 10f;
        public float ki = 0.5f;
        public float kd = 5f;

        private float previousError = 0f;
        private float integral = 0f;

        private float smoothedSpeed = 0f;
        private float calculatedTorque;

        [Header("Torque Simulation")]
        public AnimationCurve torqueCurve = AnimationCurve.Linear(0, 150f, 150f, 0f);
        public float wheelRadius = 0.35f;

        void Start()
        {
            CreateCenterOfMass();
            SettingRigidbody();

            currentLegPower = legPower * 10;
            reversePower = legPower * 3;
            rollingResistanceCoefficient = m_Rigidbody.mass * 9.81f;
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

        private void FixedUpdate()
        {
            if (!ReadyToRide()) return;

            // Smooth the target speed (optional)
            float targetSpeed = speedReceiver.speedKph;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, 0.1f);

            calculatedTorque = CalculateTorqueFromSpeed(smoothedSpeed);

            //Debug.Log($"[Speed Sensor: {speedReceiver.speedKph:F1} KPH] | [Unity Speed: {m_Rigidbody.linearVelocity.magnitude * 3.6f:F1} KPH] | [Torque: {calculatedTorque:F1}]");

            if (sensorSpeedUI != null)
            {
                sensorSpeedUI.text = $"{speedReceiver.speedKph:F1} KPH";
                currentSpeedUI.text = $"{m_Rigidbody.linearVelocity.magnitude * 3.6f:F1} KPH"; 
            }


            if (IsRest()) Rest();
            else if (IsMoving()) MovingBike();

            if (IsTurning() || (leftController && rightController)) TurningBike();

            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();

            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0f);
        }

        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = m_Rigidbody.mass > 0 ? GetBikeSpeedMs() / m_Rigidbody.mass * airResistance : 0f;
            m_Rigidbody.angularDamping = 5 + GetBikeSpeedMs() / (m_Rigidbody.mass / 10);

            frontWheelCollider.brakeTorque = 0;
            rearWheelCollider.motorTorque = calculatedTorque;

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

            Quaternion handlerBarLocalRotation = Quaternion.Euler(0, nextAngle - handlerBarYLastAngle, 0);
            handlerBar.rotation = Quaternion.Lerp(handlerBar.rotation, handlerBar.rotation * handlerBarLocalRotation, turningSmooth);
            handlerBarYLastAngle = nextAngle;

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
            rearWheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
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

        private float CalculateTorqueFromSpeed(float targetSpeedKmh)
        {

            float currentSpeedMS = m_Rigidbody.linearVelocity.magnitude; // m/s
            float targetSpeedMS = targetSpeedKmh / 3.6f;

            // Required acceleration to reach target speed in 1 physics frame
            float accelerationNeeded = (targetSpeedMS - currentSpeedMS) / Time.fixedDeltaTime;

            // Torque = mass × acceleration × wheel radius
            float requiredTorque = (m_Rigidbody.mass + (frontWheelCollider.mass * 2f)) * accelerationNeeded * wheelRadius;

            // Clamp torque
            return Mathf.Clamp(requiredTorque, 0f, maxTorque);
        }

        public bool ReadyToRide()
        {
            if (noBikerCtrl) return true;
            if (bikerHolder.childCount == 0) return false;
            return bikerHolder.GetChild(0).CompareTag("Player");
        }
    }
}
