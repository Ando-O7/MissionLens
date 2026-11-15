using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionLens
{
    /// <summary>
    /// Interface sounds, synthesised at start so the app ships no audio files. These are feedback tones of ours;
    /// none of them is a recording from Mars.
    /// </summary>
    public class MLAudio
    {
        const int Rate = 44100;
        readonly AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public MLAudio(GameObject host)
        {
            src = host.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0; src.volume = .7f;
            // name, seconds, sample function (t seconds -> -1..1)
            Make("hover", .05f, t => Sin(1760, t) * Env(t, .002f, .04f) * .5f);
            Make("press", .14f, t => (Sin(880, t) + .5f * Sin(1320, t)) * Env(t, .002f, .11f) * .55f);
            Make("toggle", .16f, t => Sin(Mathf.Lerp(620, 930, t / .16f), t) * Env(t, .003f, .13f) * .5f);
            Make("deny", .22f, t => Sin(196, t) * (Env(t, .004f, .08f) + Env(t - .11f, .004f, .08f)) * .55f);
            Make("mark", .7f, t => (Sin(1318.5f, t) + .45f * Sin(1975.5f, t) + .2f * Sin(2637, t)) * Env(t, .002f, .6f) * .42f);
            Make("shutter", .2f, t => (Sin(2900, t) + .6f * Sin(1700, t)) * (Env(t, .001f, .02f) + .8f * Env(t - .085f, .001f, .035f)) * .42f);   // two short clicks: a view was taken
            Make("undo", .25f, t => Sin(Mathf.Lerp(880, 520, t / .25f), t) * Env(t, .004f, .2f) * .4f);
            Make("done", .9f, t => (Bell(1046.5f, t) + Bell(1318.5f, t - .11f) + Bell(1568, t - .22f)) * .34f);
            Make("ready", 1.3f, t => (Bell(523.25f, t) + Bell(784, t - .12f) + Bell(1046.5f, t - .26f)) * .32f);
            Make("complete", 2.2f, t => (Bell(523.25f, t) + Bell(659.25f, t - .16f) + Bell(784, t - .32f) + Bell(1046.5f, t - .5f) + Bell(1318.5f, t - .7f)) * .3f);
            Make("arrive", 1.6f, t => (Sin(220, t) + .7f * Sin(330, t) + .5f * Sin(440, t)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)) * .2f);
            Make("whoosh", 3.0f, Noise(3.0f, 180, 1400, .34f));
            Make("travel", 1.2f, Noise(1.2f, 240, 700, .2f));
            // The walk between stations: two soft, muffled steps that alternate. Ours, for the pace of walking; not a recording.
            Make("stepA", .24f, Step(96, 11)); Make("stepB", .24f, Step(84, 23));
            Make("intro", 4.2f, t =>
            {   // a slow rising shimmer under the opening
                float k = Mathf.Clamp01(t / 4.2f), e = Mathf.Sin(Mathf.PI * k); e *= e;
                return (Sin(Mathf.Lerp(110, 165, k), t) * .5f + Sin(Mathf.Lerp(220, 330, k), t) * .32f + Sin(Mathf.Lerp(440, 660, k), t) * .16f * (0.6f + 0.4f * Sin(5, t))) * e * .5f;
            });
        }

        public void Play(string name, float volume = 1) { if (clips.TryGetValue(name, out var c)) src.PlayOneShot(c, volume); }

        void Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1) * Mathf.Min(1, (n - i) / 400f);   // last 9 ms always fades to zero
            var c = AudioClip.Create(name, n, 1, Rate, false); c.SetData(d, 0); clips[name] = c;
        }

        static float Sin(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Env(float t, float attack, float decay) => t < 0 ? 0 : t < attack ? t / attack : Mathf.Exp(-(t - attack) / (decay * .35f));
        static float Bell(float hz, float t) => t < 0 ? 0 : (Sin(hz, t) + .3f * Sin(hz * 2.01f, t) + .12f * Sin(hz * 3.02f, t)) * Env(t, .004f, .9f);

        // A low thump that drops in pitch as it lands, with a short scuff of low-passed noise.
        static Func<float, float> Step(float hz, int seed)
        {
            var rng = new System.Random(seed); float lp = 0;
            return t =>
            {
                float x = (float)rng.NextDouble() * 2 - 1; lp += (x - lp) * .07f;
                float f = Mathf.Lerp(hz * 1.6f, hz, Mathf.Clamp01(t / .05f));
                return ((Sin(f, t) + .35f * Sin(f * 2, t)) * Env(t, .004f, .17f) * .62f + lp * Env(t, .002f, .1f) * 1.7f) * .6f;
            };
        }

        // Band-limited noise that sweeps from one cutoff to another and swells in the middle.
        static Func<float, float> Noise(float seconds, float hz0, float hz1, float gain)
        {
            var rng = new System.Random(7); float lp = 0, hp = 0;
            return t =>
            {
                float k = Mathf.Clamp01(t / seconds), cut = Mathf.Lerp(hz0, hz1, Mathf.Sin(Mathf.PI * k)), a = 1 - Mathf.Exp(-2 * Mathf.PI * cut / Rate);
                float x = (float)rng.NextDouble() * 2 - 1; lp += (x - lp) * a; hp += (lp - hp) * .002f;
                float e = Mathf.Sin(Mathf.PI * k);
                return (lp - hp) * e * e * gain * 4;
            };
        }
    }
}
