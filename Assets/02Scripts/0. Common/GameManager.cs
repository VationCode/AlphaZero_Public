using UnityEngine;

namespace alpha
{
    public enum EAreteType
    {
        Heruna = 0,
        Arin = 1,
        Silyon = 2,
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance {  get; private set; }
        public EAreteType AreteType {  get; private set; }

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }

            AreteType = EAreteType.Silyon;
        }

        public void SelectArete(int p_num)
        {
            AreteType = (EAreteType)p_num;
        }

        private void Update()
        {
            
        }

        private void OnApplicationQuit()
        {
            Application.Quit();
        }
    }
}