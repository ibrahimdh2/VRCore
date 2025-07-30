using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {

        [Header("Player Setting")]
        public bool disable;
        [SerializeField] private string AnimSpeedParaName = "Speed";
        [SerializeField] private float turnSpeed = 10f;
        [SerializeField] private float runSpeed = 3f;
        [SerializeField] private float rotationDamping = 40;
        [SerializeField] private float gravity = -9.8f;
        [SerializeField] private bool stopMoverment = false;

        public bool moving { get; set; }

        private Vector3 m_MoveVector;
        private Vector3 m_Velocity;

        private CharacterController characterCtrl;

        [HideInInspector]
        public Animator m_Animator;
        private Quaternion desiredRotation = Quaternion.identity;
        private Transform camTrans;
        private Vector3 camForward;
        private InputManager inputManager;
        private Vector3 offset;
        private Vector3 gravityMagnitude;

        private void Start()
        {
            inputManager = GameManager.Instance.GetInputManager;
            characterCtrl = GetComponent<CharacterController>();
            m_Animator = GetComponent<Animator>();
            camTrans = Camera.main.transform;
            gravityMagnitude = new Vector3(0, gravity, 0);
        }
        public void DisablePlayerCtrl() { disable = true; characterCtrl.enabled = false; }
        public void EnablePlayerCtrl() { disable = false; characterCtrl.enabled = true; }

        void FixedUpdate()
        {
            if (disable) return;
            // input
            float inputSpeed = Mathf.Clamp01(Mathf.Abs(inputManager.horizontal) + Mathf.Abs(inputManager.vertical));

            bool has_H_Input = !Mathf.Approximately(inputManager.horizontal, 0);
            bool has_V_Input = !Mathf.Approximately(inputManager.vertical, 0);

            if (!stopMoverment) moving = has_H_Input || has_V_Input;
            else moving = false;
            // move vector 
            if (camTrans != null)
            {
                camForward = Vector3.Scale(camTrans.forward, new Vector3(1, 0, 1).normalized);
                m_MoveVector = inputManager.vertical * camForward + inputManager.horizontal * camTrans.right;
                m_MoveVector.Normalize();
            }

            m_Velocity = inputSpeed * m_MoveVector * runSpeed * Time.deltaTime;
            if (!characterCtrl.isGrounded) m_Velocity += gravityMagnitude;

            //animation    




            //m_Animator.SetBool("Moving", moving);
            m_Animator.SetFloat(AnimSpeedParaName, inputSpeed);

            //move and rotate

            Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_MoveVector, turnSpeed * Time.deltaTime, 0f);
            desiredRotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredForward), turnSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * rotationDamping);
            characterCtrl.Move(m_Velocity);


        }

    }
}


