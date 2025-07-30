using UnityEngine;
using UnityEngine.UI;

namespace KikiNgao.SimpleBikeControl
{
    public class SpeedUpText : MonoBehaviour
    {
        Text text;
        EventManager eventManager;

        private void Start()
        {
            eventManager = GameManager.Instance.GetEventManager;

            eventManager.onSpeedUp += TextWhenSpeedUp;
            eventManager.onNormalSpeed += TextWhenNormalSpeed;
            text = GetComponent<Text>();
            text.enabled = false;
        }

        void TextWhenSpeedUp() => text.enabled = true;

        void TextWhenNormalSpeed() => text.enabled = false;

        private void OnDestroy()
        {
            eventManager.onSpeedUp -= TextWhenSpeedUp;
            eventManager.onNormalSpeed -= TextWhenNormalSpeed;
        }
    }
}
