using UnityEngine;
using UnityEngine.Events;

namespace KikiNgao.SimpleBikeControl
{
    public class Biker : MonoBehaviour
    {
        [SerializeField] SimpleBike currentBike;
        [SerializeField] float standBodyOffset = 0.15f;
        [SerializeField] float standAngle = 15f;

        [SerializeField] float enterExitDuration = 1;
       
        public UnityEvent OnEnter, OnExit, OnRidding;
        private bool MovingBike => currentBike.IsMoving();

        private Animator m_Animator;
        private InputManager inputManager;
        private EventManager eventManager;
        private GameObject leftLegTargetObj, rightLegTargetObj;

        private bool riding;
        private bool enterExitLock;

        private float movingBodySmooth = 0.3f;
        private float rotateBodySmooth = 0.1f;
        private float movingLegSmooth = 0.1f;

        private float IKWeight = 1f;

        public SimpleBike GetCurrentBike() => currentBike;

        void Start()
        {
            inputManager = GameManager.Instance.GetInputManager;
            eventManager = GameManager.Instance.GetEventManager;
            m_Animator = GetComponent<Animator>();
        }
        // leg target when stand
        void InitLegTarget(bool init)
        {
            if (!init && rightLegTargetObj && leftLegTargetObj)
            {
                Destroy(rightLegTargetObj);
                Destroy(leftLegTargetObj);
                return;
            }
            rightLegTargetObj = new GameObject();
            rightLegTargetObj.name = "Right Leg Target";
            rightLegTargetObj.transform.parent = currentBike.rightStandTarget.parent;
            rightLegTargetObj.transform.position = currentBike.rightPendalTarget.position;

            leftLegTargetObj = new GameObject();
            leftLegTargetObj.name = "Left Leg Target";
            leftLegTargetObj.transform.parent = currentBike.leftStandTarget.parent;
            leftLegTargetObj.transform.position = currentBike.leftPendalTarget.position;


        }

        private void MovingLegToPendal(Transform standPoint, Transform pendalPoint, float smooth)
        {
            standPoint.position = Vector3.Lerp(standPoint.position, pendalPoint.position, smooth);
            if (inputManager.vertical == 1) standPoint.position = pendalPoint.position;
        }

        private void FixedUpdate()
        {
            if (!currentBike) return;
            if (!enterExitLock && inputManager.enterExitVehicle) EnterExitBike();
            if (riding) RidingBike();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!riding || !currentBike) return;

            if (currentBike.leftHandTarget)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, IKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, IKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, currentBike.leftHandTarget.position);
                m_Animator.SetIKRotation(AvatarIKGoal.LeftHand, currentBike.leftHandTarget.rotation);
            }
            if (currentBike.rightHandTarget)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, IKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, IKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.RightHand, currentBike.rightHandTarget.position);
                m_Animator.SetIKRotation(AvatarIKGoal.RightHand, currentBike.rightHandTarget.rotation);
            }

            if (currentBike.leftPendalTarget && leftLegTargetObj && !currentBike.IsReverse())
            {

                m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, IKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, IKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftLegTargetObj.transform.position);
                if (MovingBike || !MovingBike && currentBike.TiltToRight())
                    m_Animator.SetIKRotation(AvatarIKGoal.LeftFoot, currentBike.leftPendalTarget.rotation);

            }

            if (currentBike.rightPendalTarget && rightLegTargetObj && !currentBike.IsReverse())
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, IKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, IKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.RightFoot, rightLegTargetObj.transform.position);
                if (MovingBike || !MovingBike && !currentBike.TiltToRight())
                    m_Animator.SetIKRotation(AvatarIKGoal.RightFoot, currentBike.rightPendalTarget.rotation);
            }
        }


        private void OnTriggerExit(Collider other)
        {
            if (!other.transform.parent) return;
            if (other.transform.parent.GetComponent<SimpleBike>() && currentBike)
            {              
                Physics.IgnoreLayerCollision(gameObject.layer, currentBike.gameObject.layer, false);
                currentBike = null;
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            if (!other.transform.parent) return;
            if (other.transform.parent.GetComponent<SimpleBike>() && !currentBike)
            {
                currentBike = other.transform.parent.GetComponent<SimpleBike>();
                //Debug.Log(currentBike.gameObject.name);
                //fix bug impact and go crazy bike when exit        
                Physics.IgnoreLayerCollision(gameObject.layer, currentBike.gameObject.layer, true);
            }

        }
        private void MovingBody(bool toRight, float distance, float smooth)
        {
            //// moving right
            if (toRight && transform.localPosition.x < distance)
            {
                Vector3 movingTo = transform.InverseTransformDirection(transform.right) * Time.deltaTime * smooth;
                transform.localPosition += movingTo;
            }
            // moving left
            if (!toRight && transform.localPosition.x > -distance)
            {
                Vector3 movingTo = transform.InverseTransformDirection(-transform.right) * Time.deltaTime * smooth;
                transform.localPosition += movingTo;
            }
        }

        private void RidingBike()
        {
            // Stand
            if (!MovingBike)
            {
                m_Animator.SetBool("Reverse", false);
                if (currentBike.TiltToRight())
                {
                    //body logic when stand : 
                    //Move body to right 
                    MovingBody(true, standBodyOffset, movingBodySmooth);
                    //and update body rotation logic
                    transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0, 0, standAngle), rotateBodySmooth);
                    // left leg to pendal 
                    MovingLegToPendal(leftLegTargetObj.transform, currentBike.leftPendalTarget, movingLegSmooth);
                    // right leg to ground
                    rightLegTargetObj.transform.position = Vector3.Lerp(rightLegTargetObj.transform.position, currentBike.rightStandTarget.position, movingLegSmooth);
                }
                else
                {
                    // left side
                    MovingBody(false, standBodyOffset, movingBodySmooth);
                    transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0, 0, -standAngle), rotateBodySmooth);
                    // right leg to pendal 
                    MovingLegToPendal(rightLegTargetObj.transform, currentBike.rightPendalTarget, movingLegSmooth);
                    // Left leg to ground
                    leftLegTargetObj.transform.position = Vector3.Lerp(leftLegTargetObj.transform.position, currentBike.leftStandTarget.position, movingLegSmooth);
                }
                return;
            }
            // riding
            //reset body position and rotation
            transform.localPosition = Vector3.Lerp(transform.localPosition, Vector3.zero, rotateBodySmooth);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0, 0, 0), rotateBodySmooth);

            if (currentBike.IsMovingToward)
            {
                currentBike.FreezeCrankset = false;
                m_Animator.SetBool("Reverse", false);
                MovingLegToPendal(leftLegTargetObj.transform, currentBike.leftPendalTarget, movingLegSmooth);
                MovingLegToPendal(rightLegTargetObj.transform, currentBike.rightPendalTarget, movingLegSmooth);
            }

            if (currentBike.IsReverse())
            {
                currentBike.FreezeCrankset = true;
                //right
                if (currentBike.TiltToRight())
                {
                    m_Animator.SetBool("Reverse", true);
                    m_Animator.SetBool("Left", false);
                }
                //left
                else
                {
                    m_Animator.SetBool("Reverse", true);
                    m_Animator.SetBool("Left", true);
                }
            }

            OnRidding?.Invoke();
            eventManager?.OnRiding();
        }

        private void EnterBike()
        {
            enterExitLock = true;
            currentBike.Freeze = false;

            if (!currentBike.bikerHolder)
                Debug.LogError("Missing Biker holder");

            InitLegTarget(true);

            OnEnter?.Invoke();
            eventManager?.OnEnter();

            transform.parent = currentBike.bikerHolder;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            m_Animator.SetBool("Riding", true);            
            riding = true;
            Invoke("EnterExitUnlock", enterExitDuration);
            //  Debug.Log("Enter Bike");
        }

        private void ExitBike()
        {
            enterExitLock = true;

            OnExit?.Invoke();
            eventManager?.OnExit();

            transform.parent = null;
            m_Animator.SetBool("Riding", false);
            riding = false;

            //remove LegTarget
            InitLegTarget(false);
            currentBike.Falling();
            Invoke("EnterExitUnlock", enterExitDuration);
            // Debug.Log("Exit Bike");
        }

        private void EnterExitBike()
        {
            if (riding) ExitBike();
            else EnterBike();
        }

        private void EnterExitUnlock() { enterExitLock = false; }

    }
}
