using UnityEngine;
using UnityEngine.SceneManagement;

namespace VectorCabinet
{
    /// <summary>Games own their scene lifetime. No game object or input handler persists across selection.</summary>
    public static class GameCollection
    {
        public const string LauncherScene = "Cabinet";
        public const string HouseScene = "HauntedHouse";
        public const string AirScene = "Meridian1944";
        public const string HockeyScene = "ColdSnap";
        public static void Open(string scene)
        {
            Time.timeScale=1f;
            Cursor.lockState=CursorLockMode.None;
            Cursor.visible=true;
            SceneManager.LoadScene(scene,LoadSceneMode.Single);
        }
        public static void ReturnToMenu() { Open(LauncherScene); }
    }
}
