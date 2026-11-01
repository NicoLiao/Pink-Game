using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Icon for a lobby team mode (instantiated by MPLobbyUI): four blobs tinted with the team colors they stand
    // for (free-for-all: four teams; 2v2: two blobs per side) around an arrow that shows how they relate.
    public class MPModeIcon : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image[] blobs;
        [Tooltip("Team whose color each blob takes (same order as Blobs).")]
        [SerializeField] int[] blobTeams;

        void OnEnable()
        {
            for (int i = 0; i < blobs.Length && i < blobTeams.Length; i++)
                if (blobs[i] != null) blobs[i].color = MPTeams.ColorOf(blobTeams[i]);
        }
    }
}
