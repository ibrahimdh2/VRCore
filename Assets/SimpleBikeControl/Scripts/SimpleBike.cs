using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class SimpleBike : MonoBehaviour
    {
        [Tooltip("Control without biker")]
        public bool noBikerCtrl;

        private InputManager inputManager;
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
        // Ex:forceRatio = 2f; mean when crankset rotate 1 round the wheels rotate 2 round
        [SerializeField] private float forceRatio = 2f;
        // default: speed [0 -> 50] Angle [1 -> 35]
        [SerializeField] private AnimationCurve frontWheelRestrictCurve = new AnimationCurve(new Keyframe(0f, 35f), new Keyframe(50f, 1f));

        public Transform leftHandTarget, rightHandTarget;
        public Transform leftPendalTarget, rightPendalTarget;
        public Transform leftStandTarget, rightStandTarget;

        private Transform centerOfMass;

        private Rigidbody m_Rigidbody;
        public Rigidbody GetRigidbody() => m_Rigidbody;


        [HideInInspector]
        public bool falling;
        private float fallingDrag = 1;
        private float fallingAngurlarDrag = 0.01f;

        private float temporaryFrontWheelAngle;

        private float handlerBarYLastAngle;
        private float currentLegPower;
        private float reversePower;
        private EventManager eventManager;

        public bool IsReverse() => inputManager.vertical < 0;
        public bool IsMovingToward => inputManager.vertical > 0;
        private bool IsRest() => inputManager.vertical == 0 && inputManager.horizontal == 0 || inputManager.vertical == 0 && inputManager.horizontal != 0;
        public bool IsMoving() => inputManager.vertical != 0;
        private bool IsTurning() => inputManager.horizontal != 0;
        private bool IsSpeedUp() => inputManager.speedUp;

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
            inputManager = GameManager.Instance.GetInputManager;
            eventManager = GameManager.Instance.GetEventManager;

            CreateCenterOfMass();
            SettingRigidbody();

            currentLegPower = legPower * 10;
            reversePower = legPower * 3;

            Freeze = true;

        }

        //creat cemter of mass and add to the bike
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
            m_Rigidbody.centerOfMass = centerOfMass.transform.position;
        }
        float powerUp = 1f;
        private void FixedUpdate()
        {
            // falling when exit bike 
            if (falling) { Falling(); return; };

            // <<< no control handle above
            if (!ReadyToRide()) return;
            // under control handle under >>

            // ready to ride and no key press
            if (IsRest()) Rest();
            if (IsMoving()) MovingBike();
            if (IsTurning()) TurningBike();

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
                //Debug.Log(powerUp);
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
            rearWheelCollider.motorTorque = !IsReverse() ? currentLegPower * inputManager.vertical : reversePower * inputManager.vertical;

            UpdateCenterOfMass();
        }

        private void TurningBike()
        {
            // handlerBar is restricted while speed is high       
            temporaryFrontWheelAngle = frontWheelRestrictCurve.Evaluate(GetBikeSpeedKm());

            //rotate handlerbar by local Y axis
            float nextAngle = temporaryFrontWheelAngle * inputManager.horizontal;
            frontWheelCollider.steerAngle = nextAngle;
            Quaternion handlerBarLocalRotation = Quaternion.Euler(0, nextAngle - handlerBarYLastAngle, 0);
            handlerBar.rotation = Quaternion.Lerp(handlerBar.rotation, handlerBar.rotation * handlerBarLocalRotation, turningSmooth);
            handlerBarYLastAngle = nextAngle;
        }

        // the bike keep moving slowly if we forgot reset motor force
        private void ResetWheelsCollider()
        {
            frontWheelCollider.steerAngle = 0f;
            frontWheelCollider.motorTorque = 0;
            rearWheelCollider.motorTorque = 0;
            rearWheelCollider.brakeTorque = 0;
            frontWheelCollider.brakeTorque = 0;
        }

        // when had biker and no key press
        private void Rest()
        {
            // the bike auto stop due to high drag
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();


        }
        //When biker exit bike it's start falling 
        public void Falling()
        {
            falling = true;
            // the bike auto stop due to high drag
            m_Rigidbody.linearDamping = fallingDrag;
            m_Rigidbody.angularDamping = fallingAngurlarDrag;

            UpdateCenterOfMass();
            UpdateWheelDisplay();
            ResetWheelsCollider();

            //Debug.Log(gameObject.name + "  Falling ");
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
            Vector3 temporaryVector;
            Quaternion temporaryQuaternion;

            rearWheelCollider.GetWorldPose(out temporaryVector, out temporaryQuaternion);
            rearWheel.transform.position = temporaryVector;

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
                if (IsRest())// when bike rest 
                {
                    centerLocal.y = 0;
                    centerLocal.x = TiltToRight() ? .01f : -0.1f;
                }
                else centerLocal.y = -0.8f; //set center local y under ground to make bike tilt while riding 
            }
            else //falling
            {
                centerLocal.y = 0;
                centerLocal.x = TiltToRight() ? .2f : -0.2f;
            }

            // when bike falling
            m_Rigidbody.centerOfMass = centerLocal;
        }

        private bool OnGround(WheelCollider wheelCollider)
        {
            if (Physics.Raycast(wheelCollider.transform.position, -transform.up, out RaycastHit hit, wheelCollider.radius + 0.1f))
            {
                return true;
            }
            return false;
        }
        // convert runtime Euler Angle to angle that showing in Unity Editor 
        private static float WrapAngle(float angle)
        {
            angle %= 360;
            if (angle > 180)
                return angle - 360;

            return angle;
        }

    }
}
