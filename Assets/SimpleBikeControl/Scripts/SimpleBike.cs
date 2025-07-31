using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class SimpleBike : MonoBehaviour
    {
        [Tooltip("Control without biker")]
        public bool noBikerCtrl;

        public Transform bikerHolder;

        public WheelCollider frontWheelCollider;
        public WheelCollider rearWheelCollider;
        public GameObject frontWheel;
        public GameObject rearWheel;

        public Transform handlerBar;
        public Transform cranksetTransform;

        [SerializeField] private float legPower = 10;
        [Tooltip("speed multiply max")]
        [SerializeField] private float powerUpMax = 2;
        [Tooltip("define how fast to reach power up max")]
        [SerializeField] private float powerUpSpeed = .5f;
        [SerializeField] private float airResistance = 6;
        [SerializeField] private float turningSmooth = .8f;
        [Tooltip("Rigidbody Drag while standing")]
        [SerializeField] private float restDrag = 2f;
        [Tooltip("Rigidbody AngularDrag while standing")]
        [SerializeField] private float restAngularDrag = .2f;
        [Tooltip("ratio of wheels and crankset rotation ")]
        [SerializeField] private float forceRatio = 2f;
        [SerializeField] private AnimationCurve frontWheelRestrictCurve = new AnimationCurve(new Keyframe(0f, 35f), new Keyframe(50f, 1f));


        private Transform centerOfMass;
        private Rigidbody m_Rigidbody;

        public Rigidbody GetRigidbody() => m_Rigidbody;

        // VR Controllers
        public Transform leftController;
        public Transform rightController;

        [HideInInspector]
        public bool falling;
        private float fallingDrag = 1;
        private float fallingAngurlarDrag = 0.01f;

        private float temporaryFrontWheelAngle;
        private float handlerBarYLastAngle;
        private float currentLegPower;
        private float reversePower;
        private EventManager eventManager;

        public bool IsReverse() => false;
        public bool IsMovingToward => speedReceiver.speedKph > 0;
        private bool IsRest() => speedReceiver.speedKph == 0;
        public bool IsMoving() => true;
        private bool IsTurning() => frontWheelCollider.steerAngle != 0;
        private bool IsSpeedUp() => false;

        private float GetBikeSpeedKm() => GetBikeSpeedMs() * 3.6f;
        private float GetBikeSpeedMs() => m_Rigidbody.linearVelocity.magnitude;
        private float GetBikeAngle() => WrapAngle(transform.eulerAngles.z);

        public bool TiltToRight() => WrapAngle(transform.eulerAngles.z) <= 0;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }

        public bool ReadyToRide()
        {
            if (noBikerCtrl) return true;
            if (bikerHolder.childCount == 0) return false;
            if (bikerHolder.GetChild(0).CompareTag("Player")) return true;
            return false;
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
            centerOfMass = new GameObject().transform;
            centerOfMass.name = "CenterOfMass";
            Vector3 center = new Vector3();
            Vector3 rearPosition = rearWheelCollider.transform.position;
            center.x = rearPosition.x;
            center.y = 0;
            center.z = rearPosition.z + (frontWheelCollider.transform.position.z - rearPosition.z) / 2;

            centerOfMass.transform.position = center;
            centerOfMass.parent = transform;
        }

        private void SettingRigidbody()
        {
            m_Rigidbody = transform.GetComponent<Rigidbody>();
            m_Rigidbody.centerOfMass = centerOfMass.transform.localPosition;
        }

        float powerUp = 1f;
        [SerializeField] private SpeedReceiver speedReceiver;
        [SerializeField] private float turnSensitivity;
        [SerializeField] private float rotationMultiplier;
        [SerializeField] private int maxTurnAngle;

        private void FixedUpdate()
        {
            if (falling) { Falling(); return; }
            ;

            if (!ReadyToRide()) return;

            if (IsRest()) Rest();
            if (IsMoving()) MovingBike();
            if (IsTurning() || (leftController && rightController)) TurningBike();

            UpdateLegPower(IsSpeedUp());
            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();
        }

        private void UpdateLegPower(bool speedUp)
        {
            if (speedUp)
            {
                powerUp += powerUpSpeed * Time.deltaTime;
                if (powerUp >= powerUpMax) powerUp = powerUpMax;
                currentLegPower = legPower * 10 * powerUp;
                eventManager?.OnSpeedUp();
                return;
            }
            eventManager?.OnNormalSpeed();
            powerUp = 1f;
            currentLegPower = legPower * 10 * powerUp;
        }

        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = GetBikeSpeedMs() / m_Rigidbody.mass * airResistance;
            m_Rigidbody.angularDamping = 5 + GetBikeSpeedMs() / (m_Rigidbody.mass / 10);

            frontWheelCollider.brakeTorque = 0;
            rearWheelCollider.motorTorque = speedReceiver.speedKph;

            UpdateCenterOfMass();
        }

        private void TurningBike()
        {
            temporaryFrontWheelAngle = frontWheelRestrictCurve.Evaluate(GetBikeSpeedKm());

            float inputAngle = default;

            // === VR Integration ===
            if (leftController != null && rightController != null)
            {
                inputAngle = CalculateHandlebarAngle() / maxTurnAngle;
                Debug.Log($"{inputAngle}");
            }

            float nextAngle = temporaryFrontWheelAngle * inputAngle;
            frontWheelCollider.steerAngle = nextAngle;

            Quaternion handlerBarLocalRotation = Quaternion.Euler(0, nextAngle - handlerBarYLastAngle, 0);
            handlerBar.rotation = Quaternion.Lerp(handlerBar.rotation, handlerBar.rotation * handlerBarLocalRotation, turningSmooth);
            handlerBarYLastAngle = nextAngle;
        }

        private void ResetWheelsCollider()
        {
            frontWheelCollider.steerAngle = 0f;
            frontWheelCollider.motorTorque = 0;
            rearWheelCollider.motorTorque = 0;
            rearWheelCollider.brakeTorque = 0;
            frontWheelCollider.brakeTorque = 0;
        }

        private void Rest()
        {
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();
        }

        public void Falling()
        {
            falling = true;
            m_Rigidbody.linearDamping = fallingDrag;
            m_Rigidbody.angularDamping = fallingAngurlarDrag;

            UpdateCenterOfMass();
            UpdateWheelDisplay();
            ResetWheelsCollider();

            float angle = GetBikeAngle();
            if (angle < -75 || angle > 75) { Freeze = true; falling = false; }
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
            var centerLocal = centerOfMass.localPosition;

            if (!falling)
            {
                centerLocal.y = IsRest() ? 0 : -0.8f;
            }
            else
            {
                centerLocal.y = 0;
            }

            m_Rigidbody.centerOfMass = centerLocal;
        }

        private bool OnGround(WheelCollider wheelCollider)
        {
            return Physics.Raycast(wheelCollider.transform.position, -transform.up, out RaycastHit hit, wheelCollider.radius + 0.1f);
        }

        private static float WrapAngle(float angle)
        {
            angle %= 360;
            return angle > 180 ? angle - 360 : angle;
        }

        private float CalculateHandlebarAngle()
        {
            if (leftController == null || rightController == null)
                return 0f;

            // Get positions in world space
            Vector3 leftPos = leftController.position;
            Vector3 rightPos = rightController.position;

            // Vector from left to right controller
            Vector3 handlebarVector = rightPos - leftPos;

            // Project onto bike's local XZ plane
            Vector3 localHandlebarVector = transform.InverseTransformDirection(handlebarVector);
            localHandlebarVector.y = 0;

            if (localHandlebarVector.magnitude == 0) return 0f;

            // Calculate angle in degrees: left-right vector relative to local X axis
            float angle = Mathf.Atan2(localHandlebarVector.z, localHandlebarVector.x) * Mathf.Rad2Deg;

            // Normalize: zero = perfectly horizontal handlebar
            float handlebarTurnAngle = angle - 0f; // Straight = 0 degrees

            // Clamp
            handlebarTurnAngle = Mathf.Clamp(handlebarTurnAngle, -maxTurnAngle, maxTurnAngle);
            Debug.Log($"handlebar turn {handlebarTurnAngle}");
            return -handlebarTurnAngle;
        }

    }
}
