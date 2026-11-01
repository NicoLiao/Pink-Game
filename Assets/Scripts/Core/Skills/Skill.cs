using UnityEngine;
using UnityEngine.InputSystem;

namespace LoveShot
{
    // Base class for a player ability shown in the HUD skill bar. Controllers activate skills by key;
    // the skill bar reads name, key, icon and cooldown. Add new abilities by subclassing this.
    public abstract class Skill : MonoBehaviour
    {
        [SerializeField] string displayName = "Skill";
        [Tooltip("Key that activates the skill.")]
        [SerializeField] Key key = Key.Space;
        [Tooltip("Text on the key badge in the HUD. Empty uses the key name.")]
        [SerializeField] string keyLabel = "";
        [SerializeField] Sprite icon;

        public virtual string DisplayName => displayName;
        public Key Key => key;
        public string KeyLabel => string.IsNullOrEmpty(keyLabel) ? key.ToString().ToUpperInvariant() : keyLabel;
        public virtual Sprite Icon => icon;

        // Full cooldown length and time left, in seconds.
        public abstract float Cooldown { get; }
        public abstract float CooldownRemaining { get; }
        public virtual bool IsReady => CooldownRemaining <= 0f;
        // True while the skill can't be used at all (the HUD greys its slot out).
        public virtual bool IsDisabled => false;

        // direction: the way the player is moving, or last moved. Returns false if the skill can't fire now.
        public abstract bool TryActivate(Vector2 direction);
    }
}
