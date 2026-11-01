using System;
using UnityEngine;

namespace LoveShot.Audio
{
    // Audio manager of a scene: crossfades looping music with optional intros between two sides and plays sound effects on a set of voices that grows when all are busy, at its own volumes scaled by the player's saved ones.
    public sealed class GameAudio : MonoBehaviour
    {
        // Seconds ahead an intro is scheduled, so it and its loop share an exact start time.
        const double ScheduleLead = 0.1;

        [Header("Music")]
        [Tooltip("Played once before the music loop when the scene starts.")]
        [SerializeField] AudioClip musicIntro;
        [Tooltip("Track started on enable and looped; empty starts silent.")]
        [SerializeField] AudioClip music;
        [Tooltip("Seconds a music change crossfades over.")]
        [SerializeField, Min(0f)] float musicFade = 1f;
        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
        [Tooltip("Prefix of the player's saved volumes.")]
        [SerializeField] string saveKey = "LoveShot";
        [Header("Sources")]
        [Tooltip("Two sources the looping music crossfades between.")]
        [SerializeField] AudioSource musicA;
        [SerializeField] AudioSource musicB;
        [Tooltip("Sources that play each side's intro once before its loop.")]
        [SerializeField] AudioSource introA;
        [SerializeField] AudioSource introB;
        [Tooltip("Sources sound effects play on, each on its own object so positional sounds can move it.")]
        [SerializeField] AudioSource[] voices;
        [Tooltip("Most voices there can be; while under it, clones of the first voice are added when all are busy.")]
        [SerializeField, Min(1)] int maxVoices = 64;

        VoiceGain[] gains;
        readonly float[] weights = new float[2];
        readonly bool[] introHeld = new bool[2];
        AudioClip currentIntro;
        int active;
        float target;
        float fadeTime;
        bool paused;
        float[] started;
        SoundHandle[] held;
        Sound[] owners;
        AudioListener listener;
        float playerMusic = 1f;
        float playerSfx = 1f;

        // Raised when the music track changes; null when it stops.
        public event Action<AudioClip> MusicChanged;

        // Looping track playing or fading in, including during its intro, or null when stopped.
        public AudioClip CurrentMusic { get; private set; }

        // Where the scene's audio listener is, or the origin when there is none.
        public Vector3 ListenerPosition
        {
            get
            {
                if (listener == null) listener = FindFirstObjectByType<AudioListener>();
                return listener != null ? listener.transform.position : Vector3.zero;
            }
        }

        // Player's music volume from 0 to 1, saved and applied at once.
        public float PlayerMusicVolume
        {
            get => playerMusic;
            set
            {
                playerMusic = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, playerMusic);
                ApplyMusicVolume();
            }
        }

        // Player's sound effect volume from 0 to 1, saved and applied at once to held sounds.
        public float PlayerSfxVolume
        {
            get => playerSfx;
            set
            {
                playerSfx = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxKey, playerSfx);
                RefreshHeld();
            }
        }

        string MusicKey => saveKey + ".MusicVolume";
        string SfxKey => saveKey + ".SfxVolume";

        float MusicVolume => musicVolume * playerMusic;
        internal float SfxVolume => sfxVolume * playerSfx;
        float DefaultFade => musicFade;

        void OnEnable()
        {
            playerMusic = PlayerPrefs.GetFloat(MusicKey, 1f);
            playerSfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            Apply();
            if (music != null) PlayMusic(musicIntro, music);
        }

        void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled) Apply();
        }

        void Update()
        {
            if (paused) return;
            Step(fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f);
        }

        // Crossfades to the looping track over the fade time.
        public void PlayMusic(AudioClip clip) => PlayMusic(null, clip, DefaultFade);

        // Crossfades to the looping track over this many seconds; null fades the music out.
        public void PlayMusic(AudioClip clip, float fade) => PlayMusic(null, clip, fade);

        // Crossfades to the intro, then the looping track, over the fade time.
        public void PlayMusic(AudioClip intro, AudioClip clip) => PlayMusic(intro, clip, DefaultFade);

        // Crossfades over this many seconds to the intro, played once with no gap before the looping track; a null track fades the music out.
        public void PlayMusic(AudioClip intro, AudioClip clip, float fade)
        {
            if (clip == null)
            {
                StopMusic(fade);
                return;
            }
            if (clip == CurrentMusic && intro == currentIntro) return;

            active = 1 - active;
            StopSide(active);
            weights[active] = 0f;
            AudioSource source = Music(active);
            source.clip = clip;
            source.loop = true;
            if (intro != null) StartIntro(intro, clip);
            else
            {
                source.Play();
                if (paused) source.Pause();
            }

            currentIntro = intro;
            target = 1f;
            ChangeMusic(clip, fade);
        }

        // Fades the music out over the fade time.
        public void StopMusic() => StopMusic(DefaultFade);

        // Fades the music out over this many seconds.
        public void StopMusic(float fade)
        {
            if (CurrentMusic == null) return;
            currentIntro = null;
            target = 0f;
            ChangeMusic(null, fade);
        }

        // Pauses both music sides where they are, holding any fade in progress and cancelling any loop scheduled after an intro.
        public void PauseMusic()
        {
            if (paused) return;
            paused = true;
            for (int i = 0; i < 2; i++)
            {
                if (Intro(i).isPlaying)
                {
                    Intro(i).Pause();
                    Music(i).Stop();
                    introHeld[i] = true;
                }
                else if (Music(i).isPlaying) Music(i).Pause();
            }
        }

        // Resumes both music sides, scheduling the loop again after any intro that was held.
        public void ResumeMusic()
        {
            if (!paused) return;
            paused = false;
            for (int i = 0; i < 2; i++)
            {
                if (weights[i] <= 0f && (i != active || target <= 0f)) continue;
                if (!introHeld[i])
                {
                    Music(i).UnPause();
                    continue;
                }

                AudioSource intro = Intro(i);
                introHeld[i] = false;
                intro.UnPause();
                Music(i).PlayScheduled(AudioSettings.dspTime + (intro.clip.samples - intro.timeSamples) / (double)intro.clip.frequency);
            }
        }

        // Plays the sound flat, ignoring position.
        public void Play(Sound sound) => Play(sound, false, default, false);

        // Plays the sound in 3D from a world position.
        public void PlayAt(Sound sound, Vector3 position) => Play(sound, true, position, false);

        // Plays the sound in 3D from a world position, with its volume and picked pitch scaled.
        public void PlayAt(Sound sound, Vector3 position, float volumeScale, float pitchScale) => Play(sound, true, position, false, volumeScale, pitchScale);

        // Plays the sound flat on a voice no other sound can take until it is stopped or ends; null when it has no clip or every voice is held.
        public SoundHandle Hold(Sound sound) => Play(sound, false, default, true);

        // Plays the sound in 3D from a world position on a held voice; null when it has no clip or every voice is held.
        public SoundHandle HoldAt(Sound sound, Vector3 position) => Play(sound, true, position, true);

        SoundHandle Play(Sound sound, bool spatial, Vector3 position, bool hold, float volumeScale = 1f, float pitchScale = 1f)
        {
            AudioClip clip = sound?.PickClip();
            if (clip == null) return null;
            int index = NextVoice(sound);
            if (index < 0) return null;

            AudioSource voice = voices[index];
            if (held[index] != null) held[index].Release();
            held[index] = null;
            owners[index] = sound;

            float pitch = sound.PickPitch() * pitchScale;
            float volume = sound.Volume * Mathf.Max(volumeScale, 0f);
            if (spatial) voice.transform.position = position;
            voice.spatialBlend = spatial ? 1f : 0f;
            voice.minDistance = sound.MinDistance;
            voice.maxDistance = sound.MaxDistance;
            voice.dopplerLevel = sound.Doppler;
            voice.loop = sound.Loop;
            voice.clip = clip;
            voice.pitch = pitch;
            ApplyVolume(index, volume * SfxVolume);
            voice.Play();
            started[index] = Time.unscaledTime;
            if (!hold) return null;

            held[index] = new SoundHandle(this, index, voice, volume, pitch);
            return held[index];
        }

        // Sets a voice to a total volume: the source takes up to full, and anything above it is applied as gain by a VoiceGain added to the voice the first time it is needed.
        internal void ApplyVolume(int index, float total)
        {
            voices[index].volume = Mathf.Clamp01(total);

            if (gains[index] == null)
            {
                if (total <= 1f) return;
                if (!voices[index].TryGetComponent(out gains[index])) gains[index] = voices[index].gameObject.AddComponent<VoiceGain>();
            }
            gains[index].SetGain(total);
        }

        // Index of the voice to play this sound on: its own oldest unheld one at its cap (-1 if all are held), else a free one, else a new one under the limit, else the unheld one playing longest; -1 when there are none.
        int NextVoice(Sound sound)
        {
            if (voices == null || voices.Length == 0) return -1;
            if (started == null) started = new float[voices.Length];
            if (held == null) held = new SoundHandle[voices.Length];
            if (owners == null) owners = new Sound[voices.Length];
            if (gains == null) gains = new VoiceGain[voices.Length];

            if (sound.MaxVoices > 0)
            {
                int playing = 0;
                int oldestOwn = -1;
                for (int i = 0; i < voices.Length; i++)
                {
                    if (owners[i] != sound || !voices[i].isPlaying) continue;
                    playing++;
                    if (held[i] != null && held[i].IsAlive) continue;
                    if (oldestOwn < 0 || started[i] < started[oldestOwn]) oldestOwn = i;
                }
                if (playing >= sound.MaxVoices) return oldestOwn;
            }

            int oldest = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) return i;
                if (held[i] != null && held[i].IsAlive) continue;
                if (oldest < 0 || started[i] < started[oldest]) oldest = i;
            }
            return voices.Length < maxVoices ? AddVoice() : oldest;
        }

        // Clones the first voice beside it and grows the voice state to match; returns its index.
        int AddVoice()
        {
            int index = voices.Length;
            int size = index + 1;
            Array.Resize(ref voices, size);
            Array.Resize(ref started, size);
            Array.Resize(ref held, size);
            Array.Resize(ref owners, size);
            Array.Resize(ref gains, size);

            voices[index] = Instantiate(voices[0], voices[0].transform.parent);
            voices[index].TryGetComponent(out gains[index]);
            return index;
        }

        // Starts the intro on the active side with its loop scheduled at the intro's exact end; while paused it waits for ResumeMusic.
        void StartIntro(AudioClip intro, AudioClip clip)
        {
            intro.LoadAudioData();
            clip.LoadAudioData();

            AudioSource source = Intro(active);
            source.clip = intro;
            source.loop = false;
            if (paused)
            {
                source.Play();
                source.Pause();
                introHeld[active] = true;
                return;
            }

            double start = AudioSettings.dspTime + ScheduleLead;
            source.PlayScheduled(start);
            Music(active).PlayScheduled(start + intro.samples / (double)intro.frequency);
        }

        // Stops the side's loop and intro, including anything scheduled or held.
        void StopSide(int index)
        {
            Music(index).Stop();
            Intro(index).Stop();
            introHeld[index] = false;
        }

        void ChangeMusic(AudioClip clip, float fade)
        {
            fadeTime = fade;
            CurrentMusic = clip;
            if (fade <= 0f) Step(1f);
            else ApplyMusicVolume();
            MusicChanged?.Invoke(clip);
        }

        // Moves each source's weight toward its goal by this much, stopping sources that faded out.
        void Step(float step)
        {
            for (int i = 0; i < 2; i++)
            {
                float goal = i == active ? target : 0f;
                weights[i] = Mathf.MoveTowards(weights[i], goal, step);
                if (weights[i] <= 0f && (Music(i).isPlaying || Intro(i).isPlaying || introHeld[i])) StopSide(i);
            }
            ApplyMusicVolume();
        }

        void ApplyMusicVolume()
        {
            for (int i = 0; i < 2; i++)
            {
                float volume = weights[i] * MusicVolume;
                Music(i).volume = volume;
                Intro(i).volume = volume;
            }
        }

        AudioSource Music(int index) => index == 0 ? musicA : musicB;

        AudioSource Intro(int index) => index == 0 ? introA : introB;

        // Applies the volumes to the music and to every held sound.
        void Apply()
        {
            ApplyMusicVolume();
            RefreshHeld();
        }

        // Applies the current sound effect volume to every held sound.
        void RefreshHeld()
        {
            if (held == null) return;
            foreach (SoundHandle handle in held) handle?.Refresh();
        }
    }
}
