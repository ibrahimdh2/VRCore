using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class InputManager : MonoBehaviour
    {
        public float horizontal, vertical;
        public KeyCode enterExitKey = KeyCode.F;
        public KeyCode speedUpKey = KeyCode.LeftShift;
        [HideInInspector]
        public bool enterExitVehicle;
        [HideInInspector]
        public bool speedUp;

        private void Update()
        {
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");

            enterExitVehicle = Input.GetKeyDown(enterExitKey);

            if (Input.GetKeyDown(speedUpKey)) speedUp = true;
            if (Input.GetKeyUp(speedUpKey)) speedUp = false;
        }

    }
}
