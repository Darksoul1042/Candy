using UnityEngine;

namespace PetaloPop.Game
{
    /// <summary>Sonidos propios generados por código (tonos sintéticos), sin archivos de terceros.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public bool SoundOn = true;
        private AudioSource source, musicSource;
        private AudioClip swap, invalid, win, lose, click, special;
        private AudioClip[] match;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.clip = MakeMusic();
            musicSource.volume = 0.5f;
            musicSource.Play();
            swap = Tone("swap", new[] { 440f }, 0.07f, 0.35f);
            invalid = Tone("invalid", new[] { 220f, 180f }, 0.16f, 0.4f);
            click = Tone("click", new[] { 660f }, 0.05f, 0.3f);
            special = Tone("special", new[] { 520f, 780f, 1040f }, 0.28f, 0.4f);
            win = Tone("win", new[] { 523f, 659f, 784f, 1047f }, 0.7f, 0.45f);
            lose = Tone("lose", new[] { 392f, 330f, 262f }, 0.7f, 0.4f);
            match = new AudioClip[5];
            for (int i = 0; i < match.Length; i++) match[i] = Tone("match" + i, new[] { 392f * Mathf.Pow(1.122f, i * 2), 523f * Mathf.Pow(1.122f, i * 2) }, 0.14f, 0.4f);
        }

        // Una secuencia de notas: cada frecuencia ocupa una parte igual de la duración, con caída de volumen suave.
        private static AudioClip Tone(string name, float[] freqs, float duration, float volume)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = freqs[Mathf.Min(freqs.Length - 1, (int)(t * freqs.Length))];
                float env = Mathf.Min(1f, i / 200f) * (1f - t);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * i / rate) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Activa o silencia efectos y música a la vez.</summary>
        public void SetEnabled(bool on)
        {
            SoundOn = on;
            if (musicSource != null) musicSource.mute = !on;
        }

        // Arpegio suave en escala pentatónica (16 notas de 0,5 s = 8 s en bucle). Composición propia, sin archivos de terceros.
        private static AudioClip MakeMusic()
        {
            const int rate = 22050;
            float[] scale = { 261.6f, 293.7f, 329.6f, 392f, 440f, 523.3f };
            int[] melody = { 0, 2, 4, 2, 3, 4, 5, 4, 3, 2, 4, 3, 2, 1, 0, 2 };
            int perNote = rate / 2, n = perNote * melody.Length;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                int note = i / perNote;
                float t = (float)(i % perNote) / perNote;
                float env = Mathf.Min(1f, t * 20f) * (1f - t) * (1f - t);
                float f = scale[melody[note]];
                float bass = scale[melody[note / 4 * 4] % 3] * 0.5f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * i / rate) * env * 0.18f) + (Mathf.Sin(2f * Mathf.PI * bass * i / rate) * 0.06f);
            }
            var clip = AudioClip.Create("music", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Play(AudioClip c) { if (SoundOn && c != null) source.PlayOneShot(c); }

        public void Swap() => Play(swap);
        public void Invalid() => Play(invalid);
        public void Click() => Play(click);
        public void Special() => Play(special);
        public void Win() => Play(win);
        public void Lose() => Play(lose);
        public void Match(int cascade) => Play(match[Mathf.Clamp(cascade - 1, 0, match.Length - 1)]);
    }
}
