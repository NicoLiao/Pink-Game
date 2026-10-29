using UnityEngine;

namespace LoveShot
{
    // Cupid: a flighty wanderer that, once recruited, fires a fan of hearts instead of a single one.
    public class CupidNPC : NPCController
    {
        [Header("Cupid")]
        [SerializeField, Min(1)] int heartsPerShot = 3;
        [SerializeField, Range(0f, 90f)] float spreadAngle = 24f;

        protected override void Fire(Vector2 direction) => Shooter.TryShootSpread(direction, heartsPerShot, spreadAngle);
    }
}
