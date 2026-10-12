using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ManagerKit
{
    /// <summary>
    /// Disk persistence: atomic write (tmp + move), rolling .bak, corruption-safe load.
    /// The payload is the GameContext envelope (one blob per ISaveable system).
    /// </summary>
    public class SaveSystem
    {
        readonly string dir, file;
        public string Path => file;
        public SaveSystem(string slot = "save", string directory = null)
        {
            dir = directory ?? Application.persistentDataPath;
            file = System.IO.Path.Combine(dir, slot + ".json");
        }
        string Bak => file + ".bak";
        string Tmp => file + ".tmp";

        public bool Exists => File.Exists(file) || File.Exists(Bak);

        public bool Save(GameContext c)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var json = c.SaveBlobs(out _);
                File.WriteAllText(Tmp, json, Encoding.UTF8);
                if (File.Exists(file)) File.Copy(file, Bak, true);
                if (File.Exists(file)) File.Delete(file);
                File.Move(Tmp, file);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); return false; }
        }

        public bool Load(GameContext c)
        {
            foreach (var p in new[] { file, Bak })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    if (c.LoadBlobs(File.ReadAllText(p, Encoding.UTF8))) return true;
                }
                catch (Exception e) { Debug.LogWarning($"Load failed ({p}): {e.Message}"); }
            }
            return false;
        }

        public void Delete() { foreach (var p in new[] { file, Bak, Tmp }) try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}
