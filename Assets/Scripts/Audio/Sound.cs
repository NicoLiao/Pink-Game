using System;
using UnityEngine;

namespace LoveShot.Audio
{
    // A sound effect authored inline on its owner: clip variants picked at random, with a volume, a pitch range, whether it loops and how far it carries in 3D.
    [Serializable]
    public sealed class Sound
    {
        [SerializeField] AudioClip[] clips;
        [Tooltip("Above 1 boosts the sound past the voice's full volume, softly limited.")]
        [SerializeField, Range(0f, 3f)] float volume = 1f;
        [Tooltip("Pitch is picked at random between x and y each play.")]
        [SerializeField] Vector2 pitch = Vector2.one;
        [Tooltip("Repeats until stopped; only a held sound can be stopped.")]
        [SerializeField] bool loop;
        [Tooltip("Most voices this sound plays on at once; past it a new play takes over its oldest unheld voice, or is skipped if all are held. 0 is no cap.")]
        [SerializeField, Min(0)] int maxVoices;

        [Header("3D")]
        [Tooltip("Distance (m) within which a positional play is at full volume.")]
        [SerializeField, Min(0.01f)] float minDistance = 1f;
        [Tooltip("Distance (m) past which a positional play stops getting quieter.")]
        [SerializeField, Min(0.01f)] float maxDistance = 500f;
        [Tooltip("How much a positional play shifts in pitch as it moves relative to the listener.")]
        [SerializeField, Range(0f, 5f)] float doppler;

        public float Volume => volume;
        public bool Loop => loop;
        public int MaxVoices => maxVoices;
        public float MinDistance => minDistance;
        public float MaxDistance => Mathf.Max(maxDistance, minDistance);
        public float Doppler => doppler;

        // Picks a random clip variant, or null when none are set.
        public AudioClip PickClip() => clips == null || clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];

        public float PickPitch() => UnityEngine.Random.Range(pitch.x, pitch.y);
    }
}
