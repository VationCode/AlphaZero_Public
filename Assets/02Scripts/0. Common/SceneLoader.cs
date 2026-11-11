using UnityEngine;
using UnityEngine.SceneManagement;


namespace alpha.scene
{
    public enum ESceneType
    {
        Title = 0,
        Lobby = 1,
        InGame = 2
    }
    public static class SceneLoader
    {
        public static void LoadScene(int p_sceneNum)
        {
            SceneManager.LoadScene(p_sceneNum);
        }

        public static void LoadLobbyScene()
        {
            SceneManager.LoadScene((int)ESceneType.Lobby);
        }
        public static void LoadInGameScene()
        {
            SceneManager.LoadScene((int)ESceneType.InGame);
        }
        
    }
}