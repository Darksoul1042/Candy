using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PetaloPop.Core
{
    /// <summary>Datos que se conservan al cerrar el juego.</summary>
    public sealed class SaveData
    {
        public int Version = 1;
        public int CurrentLevel = 1;
        public int HighestUnlocked = 1;
        public bool SoundOn = true;
        public bool MusicOn = true;
        public Dictionary<int, int> Stars = new Dictionary<int, int>();
        public Dictionary<int, int> BestScore = new Dictionary<int, int>();
        public Dictionary<BoosterKind, int> Boosters = new Dictionary<BoosterKind, int>();
    }

    public interface ISaveStorage
    {
        string Read();   // null si no existe
        void Write(string text);
    }

    public sealed class MemoryStorage : ISaveStorage
    {
        public string Text;
        public string Read() => Text;
        public void Write(string text) => Text = text;
    }

    /// <summary>Guarda en un archivo con escritura atómica (temporal + reemplazo) para no corromper el progreso.</summary>
    public sealed class FileStorage : ISaveStorage
    {
        private readonly string path;
        public FileStorage(string path) { this.path = path; }

        public string Read() => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;

        public void Write(string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    /// <summary>Formato de texto clave=valor, sin dependencias externas. Tolerante a datos dañados.</summary>
    public static class SaveSerializer
    {
        public static string Serialize(SaveData d)
        {
            var sb = new StringBuilder();
            sb.Append("version=").Append(d.Version).Append('\n');
            sb.Append("current=").Append(d.CurrentLevel).Append('\n');
            sb.Append("unlocked=").Append(d.HighestUnlocked).Append('\n');
            sb.Append("sound=").Append(d.SoundOn ? 1 : 0).Append('\n');
            sb.Append("music=").Append(d.MusicOn ? 1 : 0).Append('\n');
            foreach (var kv in d.Stars) sb.Append("stars.").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            foreach (var kv in d.BestScore) sb.Append("best.").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            foreach (var kv in d.Boosters) sb.Append("booster.").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            return sb.ToString();
        }

        public static SaveData Deserialize(string text)
        {
            var d = new SaveData();
            if (string.IsNullOrWhiteSpace(text)) return d;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq), val = line.Substring(eq + 1);
                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) continue;
                if (key == "version") d.Version = n;
                else if (key == "current") d.CurrentLevel = n;
                else if (key == "unlocked") d.HighestUnlocked = n;
                else if (key == "sound") d.SoundOn = n != 0;
                else if (key == "music") d.MusicOn = n != 0;
                else if (key.StartsWith("stars.") && int.TryParse(key.Substring(6), out int ls)) d.Stars[ls] = Math.Max(0, Math.Min(3, n));
                else if (key.StartsWith("best.") && int.TryParse(key.Substring(5), out int lb)) d.BestScore[lb] = Math.Max(0, n);
                else if (key.StartsWith("booster.") && Enum.TryParse(key.Substring(8), out BoosterKind bk)) d.Boosters[bk] = Math.Max(0, n);
            }
            d.CurrentLevel = Math.Max(1, Math.Min(LevelCatalog.Count, d.CurrentLevel));
            d.HighestUnlocked = Math.Max(1, Math.Min(LevelCatalog.Count, Math.Max(d.HighestUnlocked, d.CurrentLevel)));
            return d;
        }
    }

    /// <summary>Progreso del jugador: desbloqueo de niveles, estrellas, mejores puntuaciones e inventario.</summary>
    public sealed class SaveSystem
    {
        private readonly ISaveStorage storage;
        public SaveData Data { get; private set; }

        public SaveSystem(ISaveStorage storage)
        {
            this.storage = storage;
            string text = null;
            try { text = storage.Read(); } catch (Exception) { /* archivo ilegible: se empieza de cero */ }
            Data = SaveSerializer.Deserialize(text);
            if (text == null) // primera ejecución: regalo inicial de potenciadores
            {
                Data.Boosters[BoosterKind.Hammer] = 3;
                Data.Boosters[BoosterKind.FreeSwap] = 2;
                Data.Boosters[BoosterKind.ObstacleRemover] = 2;
                Data.Boosters[BoosterKind.ExtraMoves] = 2;
                Data.Boosters[BoosterKind.SpecialMaker] = 2;
            }
        }

        public bool IsUnlocked(int level) => level >= 1 && level <= Data.HighestUnlocked;
        public int StarsOf(int level) { Data.Stars.TryGetValue(level, out int s); return s; }
        public int BestOf(int level) { Data.BestScore.TryGetValue(level, out int s); return s; }

        public BoosterManager CreateInventory()
        {
            var m = new BoosterManager();
            foreach (var kv in Data.Boosters) m.Add(kv.Key, kv.Value);
            return m;
        }

        /// <summary>Registra el final de un nivel. Solo mejora estrellas y puntuación; ganar desbloquea el siguiente.</summary>
        public void RecordResult(int level, bool won, int score, int stars, BoosterManager remaining)
        {
            if (level >= 1 && level <= LevelCatalog.Count)
            {
                if (score > BestOf(level)) Data.BestScore[level] = score;
                if (won)
                {
                    if (stars > StarsOf(level)) Data.Stars[level] = Math.Max(0, Math.Min(3, stars));
                    Data.HighestUnlocked = Math.Max(Data.HighestUnlocked, Math.Min(LevelCatalog.Count, level + 1));
                    Data.CurrentLevel = Math.Min(LevelCatalog.Count, level + 1);
                }
            }
            if (remaining != null)
                foreach (BoosterKind k in Enum.GetValues(typeof(BoosterKind))) Data.Boosters[k] = remaining.Count(k);
            Save();
        }

        public void SetCurrent(int level) { Data.CurrentLevel = Math.Max(1, Math.Min(LevelCatalog.Count, level)); Save(); }
        public void SetSound(bool on, bool music) { Data.SoundOn = on; Data.MusicOn = music; Save(); }
        /// <summary>Si no se puede escribir (disco lleno, permisos), el juego sigue: solo se pierde ese guardado.</summary>
        public bool Save()
        {
            try { storage.Write(SaveSerializer.Serialize(Data)); return true; }
            catch (Exception) { return false; }
        }
    }
}
