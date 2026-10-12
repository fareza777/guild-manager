using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit.UI
{
    /// <summary>
    /// Fully procedural chiptune audio: every sound effect and music loop is synthesised at start-up,
    /// so the game ships zero audio files (tiny APK) and works as a drop-in for any other manager game.
    /// </summary>
    public static class Audio
    {
        const int SR = 22050;
        public static float SfxVolume = 1f, MusicVolume = 0.55f;
        static AudioSource sfx, music;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, Func<float[]>> musicGen = new Dictionary<string, Func<float[]>>();
        static string curTrack;
        public static bool Ready { get; private set; }

        public static void Init(GameObject host)
        {
            sfx = host.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false;
            DefineSfx();
            Ready = true;
            UButton.PlaySound = n => { if (!string.IsNullOrEmpty(n)) Play(n); };
        }

        public static void SetVolumes(float s, float m) { SfxVolume = s; MusicVolume = m; if (music) music.volume = m; }

        public static void Play(string name, float vol = 1f, float pitch = 1f)
        {
            if (!Ready || SfxVolume <= 0.001f || !clips.TryGetValue(name, out var c)) return;
            sfx.pitch = pitch; sfx.PlayOneShot(c, SfxVolume * vol);
        }

        public static void PlayMusic(string track)
        {
            if (!Ready || track == curTrack) return;
            curTrack = track;
            if (!clips.TryGetValue("m_" + track, out var c))
            {
                if (!musicGen.TryGetValue(track, out var g)) return;
                var data = g(); c = AudioClip.Create("m_" + track, data.Length, 1, SR, false); c.SetData(data, 0); clips["m_" + track] = c;
            }
            var old = music.clip != null && music.isPlaying;
            music.clip = c; music.volume = MusicVolume; music.Play();
        }
        public static void StopMusic() { curTrack = null; if (music) music.Stop(); }

        // =============================== SFX ===============================
        enum W { Sq, Sq25, Tri, Sin, Noise }
        struct N { public float f, ms, vol, slide; public W w; public float delay; }
        static N Nt(float f, float ms, W w = W.Sq, float vol = 0.5f, float slide = 0, float delay = 0) => new N { f = f, ms = ms, w = w, vol = vol, slide = slide == 0 ? f : slide, delay = delay };

        static void Def(string name, params N[] notes)
        {
            float end = 0; foreach (var n in notes) end = Mathf.Max(end, n.delay + n.ms);
            var buf = new float[(int)(end / 1000f * SR) + 64];
            var rng = new System.Random(name.GetHashCode());
            foreach (var n in notes) Render(buf, n, rng);
            var c = AudioClip.Create(name, buf.Length, 1, SR, false); c.SetData(buf, 0); clips[name] = c;
        }

        static float Osc(W w, float ph, System.Random rng)
        {
            ph -= Mathf.Floor(ph);
            switch (w)
            {
                case W.Sq: return Mathf.Sin(ph * Mathf.PI * 2f) * 0.85f + (ph < 0.5f ? 0.15f : -0.15f);
                case W.Sq25: return Mathf.Sin(ph * Mathf.PI * 2f);
                case W.Tri: return Mathf.Sin(ph * Mathf.PI * 2f) * 0.65f;
                case W.Sin: return Mathf.Sin(ph * Mathf.PI * 2f);
                default: return (float)(rng.NextDouble() * 2 - 1);
            }
        }

        static void Render(float[] buf, N n, System.Random rng)
        {
            int start = (int)(n.delay / 1000f * SR), len = Mathf.Max(1, (int)(n.ms / 1000f * SR));
            float ph = 0, held = 0, lp = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)len;
                float f = Mathf.Lerp(n.f, n.slide, t);
                ph += f / SR;
                float attack = Mathf.Clamp01(i / (SR * 0.012f));
                float env = attack * Mathf.Pow(1f - t, n.w == W.Noise ? 2.4f : 1.8f);
                float s = Osc(n.w, ph, rng);
                if (n.w == W.Noise)
                {
                    if (i % 4 == 0) held = s;
                    lp += (held - lp) * 0.18f;
                    s = lp;
                }
                buf[start + i] += s * n.vol * env * 0.72f;
            }
        }

        static void DefineSfx()
        {
            Def("click", Nt(520, 36, W.Sin, 0.16f, 340));
            Def("deny", Nt(311, 140, W.Sin, 0.18f, 196));
            Def("pop", Nt(440, 80, W.Sin, 0.2f, 660));
            Def("swoosh", Nt(420, 180, W.Noise, 0.12f, 180), Nt(280, 160, W.Sin, 0.08f, 520));
            Def("coin", Nt(880, 90, W.Sin, 0.16f), Nt(1174, 160, W.Sin, 0.12f, 0, 70));
            Def("gem", Nt(659, 120, W.Sin, 0.16f), Nt(880, 180, W.Sin, 0.12f, 0, 90));
            Def("equip", Nt(220, 40, W.Noise, 0.1f), Nt(494, 120, W.Sin, 0.16f, 659, 20));
            Def("hit", Nt(140, 90, W.Sin, 0.42f, 55), Nt(240, 70, W.Noise, 0.16f));
            Def("crit", Nt(180, 110, W.Sin, 0.4f, 70), Nt(320, 80, W.Noise, 0.14f), Nt(740, 140, W.Sin, 0.12f, 520, 30));
            Def("miss", Nt(360, 90, W.Noise, 0.08f), Nt(520, 100, W.Sin, 0.08f, 300));
            Def("heal", Nt(523, 320, W.Sin, 0.16f, 784), Nt(659, 280, W.Sin, 0.1f, 988, 40));
            Def("magic", Nt(392, 280, W.Sin, 0.14f, 784), Nt(588, 240, W.Sin, 0.08f, 880, 80));
            Def("arrow", Nt(1400, 50, W.Noise, 0.08f), Nt(880, 90, W.Sin, 0.1f, 320));
            Def("death", Nt(220, 360, W.Sin, 0.22f, 70), Nt(140, 240, W.Noise, 0.08f, 0, 40));
            Def("build", Nt(160, 50, W.Noise, 0.14f), Nt(140, 50, W.Noise, 0.12f, 0, 140), Nt(494, 220, W.Sin, 0.12f, 659, 220));
            Def("forge", Nt(180, 40, W.Noise, 0.16f), Nt(660, 280, W.Sin, 0.14f, 440, 20));
            Def("quest", Nt(523, 140, W.Sin, 0.14f), Nt(659, 180, W.Sin, 0.12f, 0, 120));
            Def("hire", Nt(392, 120, W.Sin, 0.14f), Nt(523, 160, W.Sin, 0.12f, 0, 100), Nt(659, 220, W.Sin, 0.1f, 0, 200));
            Def("levelup", Nt(392, 140, W.Sin, 0.14f), Nt(494, 140, W.Sin, 0.12f, 0, 130), Nt(587, 220, W.Sin, 0.12f, 0, 260));
            Def("win", Nt(392, 180, W.Sin, 0.14f), Nt(494, 180, W.Sin, 0.12f, 0, 180), Nt(587, 360, W.Sin, 0.12f, 0, 360));
            Def("lose", Nt(349, 240, W.Sin, 0.16f, 294), Nt(247, 420, W.Sin, 0.14f, 196, 220));
            Def("star", Nt(784, 160, W.Sin, 0.14f, 988));
            Def("rank", Nt(392, 160, W.Sin, 0.12f), Nt(494, 160, W.Sin, 0.12f, 0, 150), Nt(587, 280, W.Sin, 0.12f, 0, 300));
            Def("tick", Nt(660, 24, W.Sin, 0.06f));
            Def("alert", Nt(440, 160, W.Sin, 0.14f), Nt(554, 220, W.Sin, 0.1f, 0, 140));

            musicGen["town"] = () => Song.Render(new Song { bpm = 78, bars = 16, key = 60, minor = false, prog = new[] { 0, 5, 3, 4 }, density = 1, drums = false, lead = true, seed = 11 });
            musicGen["battle"] = () => Song.Render(new Song { bpm = 112, bars = 16, key = 57, minor = true, prog = new[] { 0, 5, 3, 4 }, density = 1, drums = true, lead = true, seed = 5 });
            musicGen["title"] = () => Song.Render(new Song { bpm = 76, bars = 16, key = 60, minor = false, prog = new[] { 0, 3, 5, 4 }, density = 1, drums = false, lead = true, seed = 29 });
            musicGen["story"] = () => Song.Render(new Song { bpm = 80, bars = 16, key = 60, minor = false, prog = new[] { 0, 3, 5, 4 }, density = 1, drums = false, lead = true, seed = 43 });
        }

        class Song
        {
            public int bpm, bars, key, density, seed; public bool minor, drums, lead; public int[] prog;
            static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 }, Minor = { 0, 2, 3, 5, 7, 8, 10 };
            static float Hz(int midi) => 440f * Mathf.Pow(2, (midi - 69) / 12f);
            int Deg(int d) { var sc = minor ? Minor : Major; int oct = Mathf.FloorToInt(d / 7f); int i = ((d % 7) + 7) % 7; return key + sc[i] + 12 * oct; }

            public static float[] Render(Song s)
            {
                float beat = 60f / s.bpm; int spb = (int)(beat * SR); int total = spb * 4 * s.bars;
                var b = new float[total]; var rng = new System.Random(s.seed);
                // melody motif (8 notes over 2 bars) built once and varied
                var motif = new int[8]; int cur = 4; for (int i = 0; i < 8; i++) { cur = Mathf.Clamp(cur + rng.Next(-2, 3), 0, 9); motif[i] = cur; }
                for (int bar = 0; bar < s.bars; bar++)
                {
                    int chordDeg = s.prog[bar % s.prog.Length];
                    int root = s.Deg(chordDeg), third = s.Deg(chordDeg + 2), fifth = s.Deg(chordDeg + 4);
                    int barStart = bar * 4 * spb;
                    // bass
                    for (int q = 0; q < 4; q++)
                    {
                        int st = barStart + q * spb; int step = s.density > 1 ? 2 : 1;
                        for (int e = 0; e < step; e++)
                            Tone(b, st + e * spb / step, spb / step * 0.85f, Hz(root - 24), 0.16f, W.Sin, rng);
                    }
                    int[] arp = { root, fifth, third + 12, fifth };
                    int half = spb * 2;
                    for (int i = 0; i < 4; i++)
                        Tone(b, barStart + i * half, half * 0.9f, Hz(arp[i]), 0.045f, W.Sin, rng);
                    if (s.lead && bar >= 4 && bar % 2 == 0)
                    {
                        int n = motif[(bar / 2) % 8];
                        Tone(b, barStart, spb * 3f, Hz(s.Deg(n) + 12), 0.06f, W.Sin, rng, 0.004f);
                    }
                    if (s.drums && bar % 2 == 0)
                    {
                        Kick(b, barStart);
                        if (s.density > 0) Hat(b, barStart + spb * 2, rng, 0.035f);
                    }
                }
                // soft limiter + fade tail to avoid click at loop
                float peak = 0; for (int i = 0; i < b.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
                float g = peak > 0 ? 0.8f / peak : 1; for (int i = 0; i < b.Length; i++) b[i] *= g;
                return b;
            }

            static void Tone(float[] b, int start, float len, float f, float vol, W w, System.Random rng, float vib = 0)
            {
                int n = (int)len; float ph = 0;
                for (int i = 0; i < n && start + i < b.Length; i++)
                {
                    float t = i / (float)n; float ff = f * (1 + vib * Mathf.Sin(i * 0.002f));
                    ph += ff / SR; float env = Mathf.Min(1, i / 120f) * Mathf.Pow(1 - t, 0.7f);
                    b[start + i] += Osc(w, ph, rng) * vol * env;
                }
            }
            static void Kick(float[] b, int start) { int n = (int)(0.22f * SR); float ph = 0; for (int i = 0; i < n && start + i < b.Length; i++) { float t = i / (float)n; ph += Mathf.Lerp(90, 42, t) / SR; b[start + i] += Mathf.Sin(ph * 6.2831f) * 0.28f * (1 - t) * (1 - t); } }
            static void Hat(float[] b, int start, System.Random rng, float v) { int n = (int)(0.03f * SR); float lp = 0; for (int i = 0; i < n && start + i < b.Length; i++) { float nse = (float)(rng.NextDouble() * 2 - 1); lp += (nse - lp) * 0.25f; b[start + i] += lp * v * (1 - i / (float)n); } }
            static void Snare(float[] b, int start, System.Random rng) { }
        }
    }
}
