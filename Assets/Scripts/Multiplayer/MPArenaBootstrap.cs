using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Multiplayer
{
    // Scene entry point for the multiplayer arena. The host spawns the networked match (which spawns players
    // and NPCs); clients wait for it to arrive. Opening the scene without a session goes back to the menu.
    public class MPArenaBootstrap : MonoBehaviour
    {
        [SerializeField] NetworkObject matchPrefab;
        [SerializeField] string menuSceneName = "MainMenu";

        void Start()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                Debug.LogWarning("[LoveShot] The multiplayer arena needs a session. Start it from Online Multiplayer in the main menu.");
                SceneManager.LoadScene(menuSceneName);
                return;
            }
            if (network.IsServer && MPMatch.Instance == null)
                Instantiate(matchPrefab).Spawn(destroyWithScene: true);
        }
    }
}
