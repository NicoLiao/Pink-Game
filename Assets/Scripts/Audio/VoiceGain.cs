using UnityEngine;

namespace LoveShot.Audio
{
    // Multiplies the audio of the voice on this object by a gain above 1, which an AudioSource's own volume can't do; added to each voice by GameAudio.
    // Runs only while the gain is above 1, and eases peaks past full scale into it instead of cutting them.
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceGain : MonoBehaviour
    {
        // Level above which a boosted signal starts to be compressed, and the headroom it is compressed into.
        const float Knee = 0.8f;

        // Read on the audio thread.
        volatile float gain = 1f;

        // Sets the gain; 1 or less leaves the audio alone and turns the filter off.
        public void SetGain(float value)
        {
            gain = Mathf.Max(value, 1f);
            enabled = gain > 1f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float amount = gain;
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i] * amount;
                float magnitude = Mathf.Abs(sample);
                if (magnitude > Knee) sample = Mathf.Sign(sample) * (Knee + (1f - Knee) * (float)System.Math.Tanh((magnitude - Knee) / (1f - Knee)));
                data[i] = sample;
            }
        }
    }
}
