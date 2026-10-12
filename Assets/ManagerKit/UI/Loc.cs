using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit.UI
{
    /// <summary>
    /// Minimal localisation: the English string IS the key. Translation tables live in Resources/Loc/{lang}.json
    /// ({"entries":[{"k":"English","v":"Terjemahan"}]}). Missing keys fall back to English.
    /// </summary>
    public static class L
    {
        [Serializable] class Table { public List<KVS> entries = new List<KVS>(); }
        static Dictionary<string, string> map;
        public static string Lang { get; private set; } = "en";
        public static event Action Changed;
        public static readonly string[] Languages = { "en", "id" };
        public static string DisplayName(string code) => code == "id" ? "Bahasa Indonesia" : "English";

        public static void Load(string lang)
        {
            Lang = lang; map = null;
            if (lang != "en")
            {
                var ta = Resources.Load<TextAsset>("Loc/" + lang);
                if (ta != null)
                {
                    var t = JsonUtility.FromJson<Table>(ta.text);
                    map = new Dictionary<string, string>(t.entries.Count);
                    foreach (var e in t.entries) map[e.k] = e.v;
                }
            }
            Changed?.Invoke();
        }

        public static string T(string s) { if (string.IsNullOrEmpty(s) || map == null) return s; return map.TryGetValue(s, out var v) ? v : s; }
        public static string T(string s, params object[] a) { try { return string.Format(T(s), a); } catch { return s; } }
        public static string SystemLang()
        {
            switch (Application.systemLanguage) { case SystemLanguage.Indonesian: return "id"; default: return "en"; }
        }
    }
}
