using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;

        public static GameManager Instance { get => instance; }

        [SerializeField] InputManager inputManager;
        [SerializeField] EventManager eventManager;

        public InputManager GetInputManager => inputManager;
        public EventManager GetEventManager => eventManager;

        private void Awake()
        {
            if (instance != null) Destroy(gameObject);
            else
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }
        protected void OnDestroy()
        {
            //Don't forget remove it OnDestroy because it's static
            if (Instance == this) instance = null;
        }
    }
}
