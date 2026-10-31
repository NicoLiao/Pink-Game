using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Small per-type animations for special NPCs, on every machine (all fields optional):
    // a Drama Queen aura that glows while her burst is ready and the burst shockwave itself, a Celebrity's
    // spinning glow, Cupid's flapping wings, and the Hugger's waddle, charge stretch, open arms and hug hearts.
    [RequireComponent(typeof(MPNpc))]
    public class MPNpcFlair : MonoBehaviour
    {
        [SerializeField] MPNpcVisuals visuals;
        [SerializeField] HeartRing ringPrefab;
        [SerializeField] HeartBurst burstPrefab;

        [Header("Drama Queen")]
        [SerializeField] SpriteRenderer aura;
        [SerializeField, Min(0f)] float burstShake = 0.2f;

        [Header("Celebrity")]
        [SerializeField] Transform spinner;
        [SerializeField] float spinSpeed = 45f;

        [Header("Cupid")]
        [SerializeField] Transform[] wings;
        [SerializeField, Min(0f)] float flapSpeed = 14f;

        [Header("Hugger")]
        [SerializeField] bool waddle;
        [SerializeField] Transform[] arms;
        [SerializeField, Min(0f)] float waddleSpeed = 11f;
        [SerializeField, Min(0f)] float waddleAmount = 0.12f;
        [SerializeField, Min(0f)] float armSpread = 0.25f;

        MPNpc npc;
        MPDramaQueenNpc queen;
        MPHuggerNpc hugger;
        Vector3[] wingScales;
        Vector3[] armPositions;
        Vector3 lastPosition;
        float speed;
        float waddlePhase;

        void Awake()
        {
            npc = GetComponent<MPNpc>();
            queen = npc as MPDramaQueenNpc;
            hugger = npc as MPHuggerNpc;
            wingScales = SaveScales(wings);
            if (arms != null)
            {
                armPositions = new Vector3[arms.Length];
                for (int i = 0; i < arms.Length; i++) armPositions[i] = arms[i].localPosition;
            }
            lastPosition = transform.position;
        }

        static Vector3[] SaveScales(Transform[] parts)
        {
            if (parts == null) return null;
            var scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) scales[i] = parts[i].localScale;
            return scales;
        }

        void OnEnable()
        {
            if (queen != null) queen.Burst += OnBurst;
            if (hugger != null) hugger.Hugged += OnHugged;
        }

        void OnDisable()
        {
            if (queen != null) queen.Burst -= OnBurst;
            if (hugger != null) hugger.Hugged -= OnHugged;
        }

        void OnBurst(int team)
        {
            HeartRing.Play(ringPrefab, transform.position, MPTeams.ColorOf(team), queen.BurstRadius, 20);
            HeartBurst.Play(burstPrefab, transform.position, MPTeams.ColorOf(team), 1.6f, 14);
            if (MPPlayer.Local != null && Vector2.Distance(MPPlayer.Local.Position, transform.position) < queen.BurstRadius * 2f)
                CameraShake.Shake(burstShake);
        }

        void OnHugged(Vector2 point, int team)
        {
            HeartBurst.Play(burstPrefab, point, MPTeams.ColorOf(team), 1.3f, 12);
            HeartBurst.Play(burstPrefab, point, Color.white, 0.6f, 5);
        }

        void LateUpdate()
        {
            // Clients only see interpolated positions, so speed comes from movement, not the rigidbody.
            Vector3 position = transform.position;
            float frameSpeed = Time.deltaTime > 0f ? (position - lastPosition).magnitude / Time.deltaTime : 0f;
            speed = Mathf.Lerp(speed, frameSpeed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            lastPosition = position;

            if (aura != null && queen != null)
            {
                Color color = npc.Team == MPTeams.Neutral ? Color.white : MPTeams.ColorOf(npc.Team);
                float alpha = queen.BurstReady ? 0.45f + 0.3f * Mathf.Sin(Time.time * 5f) : 0.12f;
                aura.color = new Color(color.r, color.g, color.b, alpha);
                aura.transform.localScale = Vector3.one * (1.5f + (queen.BurstReady ? 0.12f * Mathf.Sin(Time.time * 5f) : 0f));
            }

            if (spinner != null) spinner.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            if (wings != null)
            {
                float flap = 1f + 0.25f * Mathf.Sin(Time.time * flapSpeed);
                for (int i = 0; i < wings.Length; i++) wings[i].localScale = new Vector3(wingScales[i].x * flap, wingScales[i].y, 1f);
            }

            if (waddle && visuals != null)
            {
                bool charging = npc.Behavior == MPNpc.NpcBehavior.Charge;
                waddlePhase += Time.deltaTime * waddleSpeed * Mathf.Clamp01(speed / 2f);
                float bob = Mathf.Sin(waddlePhase) * waddleAmount * Mathf.Clamp01(speed / 2f);
                visuals.ExtraScale = charging ? new Vector3(0.85f, 1.25f, 1f) : new Vector3(1f + bob, 1f - bob, 1f);
                if (arms != null)
                    for (int i = 0; i < arms.Length; i++)
                    {
                        float side = Mathf.Sign(armPositions[i].x);
                        float open = charging || npc.Behavior == MPNpc.NpcBehavior.Attack ? armSpread : 0f;
                        arms[i].localPosition = armPositions[i] + new Vector3(side * open, open * 0.6f + Mathf.Abs(bob), 0f);
                    }
            }
        }
    }
}
