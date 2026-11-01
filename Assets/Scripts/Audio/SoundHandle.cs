using UnityEngine;

namespace LoveShot.Audio
{
    // A sound held on one of GameAudio's voices: it can be moved, scaled in volume and pitch while it plays, and stopped.
    // It goes dead when stopped or when its clip ends; a dead handle ignores every call, so it never touches a voice that was handed on.
    public sealed class SoundHandle
    {
        readonly GameAudio owner;
        readonly int index;
        readonly AudioSource voice;
        readonly float baseVolume;
        readonly float basePitch;
        bool held = true;
        float volume = 1f;
        float pitch = 1f;

        internal SoundHandle(GameAudio owner, int index, AudioSource voice, float baseVolume, float basePitch)
        {
            this.owner = owner;
            this.index = index;
            this.voice = voice;
            this.baseVolume = baseVolume;
            this.basePitch = basePitch;
        }

        // Whether the sound is still playing on its voice.
        public bool IsAlive => held && voice != null && voice.isPlaying;

        // Moves a positional sound to a world position.
        public Vector3 Position
        {
            set
            {
                if (IsAlive) voice.transform.position = value;
            }
        }

        // Scales the sound's authored volume; above full it becomes gain on the voice.
        public float Volume
        {
            set
            {
                volume = Mathf.Max(value, 0f);
                Refresh();
            }
        }

        // Scales the sound's picked pitch.
        public float Pitch
        {
            set
            {
                pitch = value;
                if (IsAlive) voice.pitch = basePitch * pitch;
            }
        }

        // Stops the sound and frees its voice.
        public void Stop()
        {
            if (!IsAlive) return;
            voice.Stop();
            held = false;
        }

        // Marks the handle dead without touching the voice.
        internal void Release() => held = false;

        // Applies the volume with the config's sound volume.
        internal void Refresh()
        {
            if (IsAlive) owner.ApplyVolume(index, baseVolume * owner.SfxVolume * volume);
        }
    }
}
