using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One row of the live leaderboard (instantiated by MPHUD): team symbol, team + members text, score.
    public class MPLeaderboardRow : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image swatch;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text countText;

        public void Show(Sprite symbol, Color swatchColor, string names, string count, Color textColor)
        {
            if (swatch != null)
            {
                swatch.color = swatchColor;
                if (symbol != null) swatch.sprite = symbol;
            }
            if (nameText != null)
            {
                nameText.text = names;
                nameText.color = textColor;
            }
            if (countText != null)
            {
                countText.text = count;
                countText.color = textColor;
            }
        }
    }
}
