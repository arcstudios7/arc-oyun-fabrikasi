using System.Collections.Generic;
using UnityEngine;

namespace Arc
{
    public enum Wave { Sine, Square, Triangle, Saw, Noise }

    /// <summary>
    /// Sound effects synthesized in code (retro "chiptune" style). No audio files needed.
    /// Call Sfx.Coin(), Sfx.Jump() ... or Sfx.Tone(...) for a custom sound.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] sources;
        static int next;
        static AudioSource music;

        public static float Volume = 0.8f;
        public static bool Muted;

        // ---------- ready-made sounds ----------
        public static void Coin() => Tone("coin", 880f, 1760f, 0.12f, Wave.Square, 0.35f);
        public static void Jump() => Tone("jump", 300f, 700f, 0.16f, Wave.Square, 0.35f);
        public static void Hit() => Tone("hit", 220f, 50f, 0.25f, Wave.Saw, 0.5f);
        public static void Explosion() => Tone("explosion", 120f, 30f, 0.6f, Wave.Noise, 0.6f);
        public static void Click() => Tone("click", 1200f, 1000f, 0.04f, Wave.Square, 0.25f);
        public static void PowerUp() => Tone("powerup", 400f, 1600f, 0.45f, Wave.Triangle, 0.45f);
        public static void Lose() => Tone("lose", 520f, 90f, 0.8f, Wave.Triangle, 0.5f);
        public static void Win() => Tone("win", 523f, 1046f, 0.6f, Wave.Square, 0.35f);
        public static void Shoot() => Tone("shoot", 1400f, 300f, 0.12f, Wave.Saw, 0.3f);

        /// <summary>
        /// Play a synthesized sound: frequency slides from f0 to f1 (Hz) over duration seconds.
        /// Clips are cached by key, so call it every time you want the sound.
        /// pitchJitter adds small random variation so repeated sounds feel alive.
        /// </summary>
        public static void Tone(string key, float f0, float f1, float duration, Wave wave, float volume = 0.5f, float pitchJitter = 0.06f)
        {
            if (Muted) return;
            string id = key + "|" + f0 + "|" + f1 + "|" + duration + "|" + wave;
            if (!clips.TryGetValue(id, out var clip) || clip == null)
            {
                clip = Synth(id, f0, f1, duration, wave);
                clips[id] = clip;
            }
            Play(clip, volume, 1f + Random.Range(-pitchJitter, pitchJitter));
        }

        /// <summary>Play any AudioClip through the shared voice pool.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (Muted || clip == null) return;
            EnsureSources();
            var src = sources[next];
            next = (next + 1) % sources.Length;
            src.pitch = pitch;
            src.volume = volume * Volume;
            src.clip = clip;
            src.Play();
        }

        /// <summary>
        /// Simple looping background melody from note names, e.g. "C4 E4 G4 E4 A4 G4 E4 D4". "-" is a rest.
        /// bpm = speed. Call once in Setup(). Use Sfx.StopMusic() to stop.
        /// </summary>
        public static void Music(string notes, float bpm = 120f, float volume = 0.18f, Wave wave = Wave.Triangle)
        {
            EnsureSources();
            string id = "music|" + notes + "|" + bpm + "|" + wave;
            if (!clips.TryGetValue(id, out var clip) || clip == null)
            {
                clip = SynthMelody(id, notes.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries), bpm, wave);
                clips[id] = clip;
            }
            music.clip = clip;
            music.loop = true;
            music.volume = Muted ? 0f : volume * Volume;
            music.Play();
        }

        public static void StopMusic()
        {
            if (music != null) music.Stop();
        }

        // ---------- synthesis ----------

        static void EnsureSources()
        {
            if (sources != null && sources[0] != null) return;
            var go = new GameObject("ArcSfx");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[8];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            music = go.AddComponent<AudioSource>();
            music.playOnAwake = false;
        }

        static AudioClip Synth(string name, float f0, float f1, float duration, Wave wave)
        {
            int n = Mathf.Max(1, (int)(duration * Rate));
            var data = new float[n];
            double phase = 0;
            float lastNoise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += f / Rate;
                float s = Osc(wave, phase, ref lastNoise, i);
                float attack = Mathf.Clamp01(i / (Rate * 0.005f));
                float release = 1f - t;
                data[i] = s * attack * release * release;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip SynthMelody(string name, string[] notes, float bpm, Wave wave)
        {
            float beat = 60f / Mathf.Max(30f, bpm) * 0.5f; // eighth notes
            int perNote = (int)(beat * Rate);
            int count = Mathf.Max(1, notes.Length);
            var data = new float[perNote * count];
            float lastNoise = 0f;
            for (int k = 0; k < count; k++)
            {
                float f = k < notes.Length ? NoteHz(notes[k]) : 0f;
                if (f <= 0f) continue;
                double phase = 0;
                for (int i = 0; i < perNote; i++)
                {
                    phase += f / Rate;
                    float t = (float)i / perNote;
                    float env = Mathf.Clamp01(i / (Rate * 0.01f)) * (1f - t * 0.85f);
                    data[k * perNote + i] = Osc(wave, phase, ref lastNoise, i) * env * 0.8f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Osc(Wave wave, double phase, ref float lastNoise, int i)
        {
            float p = (float)(phase - System.Math.Floor(phase));
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 0.6f : -0.6f;
                case Wave.Triangle: return (p < 0.5f ? p * 4f - 1f : 3f - p * 4f) * 0.9f;
                case Wave.Saw: return (p * 2f - 1f) * 0.6f;
                case Wave.Noise:
                    if (i % 4 == 0) lastNoise = Random.value * 2f - 1f;
                    return lastNoise * 0.8f;
                default: return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        /// <summary>"A4" → 440 Hz. Supports sharps like "C#5". Returns 0 for rests ("-").</summary>
        public static float NoteHz(string note)
        {
            if (string.IsNullOrEmpty(note) || note == "-") return 0f;
            string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string letter = note.Length > 2 && note[1] == '#' ? note.Substring(0, 2) : note.Substring(0, 1);
            int octave;
            if (!int.TryParse(note.Substring(letter.Length), out octave)) octave = 4;
            int idx = System.Array.IndexOf(names, letter.ToUpperInvariant());
            if (idx < 0) return 0f;
            int midi = (octave + 1) * 12 + idx;
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }
    }
}
