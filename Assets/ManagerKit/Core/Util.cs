using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ManagerKit
{
    /// <summary>Small shared helpers: time, number/duration formatting.</summary>
    public static class Util
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        /// <summary>Tests / debug tools can override the clock.</summary>
        public static Func<double> ClockOverride;
        public static double Now => ClockOverride != null ? ClockOverride() : (DateTime.UtcNow - Epoch).TotalSeconds;

        static readonly string[] Suffix = { "", "K", "M", "B", "T", "Qa", "Qi" };

        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            bool neg = v < 0; v = Math.Abs(v);
            string s;
            if (v < 1000) s = Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);
            else
            {
                int i = 0;
                while (v >= 1000 && i < Suffix.Length - 1) { v /= 1000; i++; }
                s = (v < 10 ? v.ToString("0.00", CultureInfo.InvariantCulture) : v < 100 ? v.ToString("0.0", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture)) + Suffix[i];
            }
            return neg ? "-" + s : s;
        }

        public static string Dur(double sec)
        {
            if (sec < 0) sec = 0;
            long s = (long)Math.Ceiling(sec);
            if (s < 60) return s + "s";
            if (s < 3600) return (s / 60) + "m " + (s % 60).ToString("00") + "s";
            if (s < 86400) return (s / 3600) + "h " + ((s % 3600) / 60).ToString("00") + "m";
            return (s / 86400) + "d " + ((s % 86400) / 3600) + "h";
        }

        public static string Pct(double v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static Color Col(string hex, Color? fallback = null)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) return c;
            return fallback ?? Color.white;
        }
        public static string Uid() => Guid.NewGuid().ToString("N").Substring(0, 10);
        public static double Day(double utc, double tzHours = 0) => Math.Floor((utc + tzHours * 3600) / 86400.0);
    }

    /// <summary>Tiny deterministic RNG (xorshift64*) so battles/loot can be seeded and replayed.</summary>
    public sealed class Rng
    {
        ulong s;
        public Rng(int seed) { s = (ulong)(seed == 0 ? 0x9E3779B9u : (uint)seed) * 0x2545F4914F6CDD1DUL; if (s == 0) s = 88172645463325252UL; Next(); Next(); }
        public Rng() : this(Environment.TickCount ^ Guid.NewGuid().GetHashCode()) { }
        public ulong Next() { s ^= s >> 12; s ^= s << 25; s ^= s >> 27; return s * 2685821657736338717UL; }
        /// <summary>Float in [0,1).</summary>
        public float F() => (Next() >> 40) / 16777216f;
        public double D() => (Next() >> 11) / 9007199254740992.0;
        /// <summary>Int in [min,max) .</summary>
        public int Int(int min, int maxExcl) => maxExcl <= min ? min : min + (int)(Next() % (ulong)(maxExcl - min));
        public int Range(int minIncl, int maxIncl) => Int(minIncl, maxIncl + 1);
        public float Range(float a, float b) => a + (b - a) * F();
        public bool Chance(double p) => D() < p;
        public T Pick<T>(IList<T> l) => l == null || l.Count == 0 ? default : l[Int(0, l.Count)];
        public int Weighted(IList<double> w)
        {
            double t = 0; for (int i = 0; i < w.Count; i++) t += Math.Max(0, w[i]);
            if (t <= 0) return -1;
            double r = D() * t;
            for (int i = 0; i < w.Count; i++) { r -= Math.Max(0, w[i]); if (r < 0) return i; }
            return w.Count - 1;
        }
        public void Shuffle<T>(IList<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = Int(0, i + 1); (l[i], l[j]) = (l[j], l[i]); } }
    }

    // ---- JsonUtility-friendly key/value containers (JsonUtility cannot serialize Dictionary) ----
    [Serializable] public class KVD { public string k; public double v; public KVD() { } public KVD(string k, double v) { this.k = k; this.v = v; } }
    [Serializable] public class KVS { public string k; public string v; public KVS() { } public KVS(string k, string v) { this.k = k; this.v = v; } }

    public static class DictExt
    {
        public static List<KVD> ToList(this Dictionary<string, double> d) { var l = new List<KVD>(d.Count); foreach (var p in d) l.Add(new KVD(p.Key, p.Value)); return l; }
        public static Dictionary<string, double> ToDict(this List<KVD> l) { var d = new Dictionary<string, double>(); if (l != null) foreach (var e in l) d[e.k] = e.v; return d; }
        public static double Get(this Dictionary<string, double> d, string k, double def = 0) => d.TryGetValue(k, out var v) ? v : def;
    }
}
