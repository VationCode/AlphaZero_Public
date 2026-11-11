using alpha;
using alpha.scene;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public void NextScene()
    {
        SceneLoader.LoadInGameScene();
    }


    public void SelectArete(int p_num)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectArete(p_num);
        }
    }
}
