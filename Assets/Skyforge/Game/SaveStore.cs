using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Keeps the save between sessions. The text comes from <see cref="Progress.Save"/>; it's stored
    /// in Unity's PlayerPrefs, which works the same on Windows (registry), the web (browser storage)
    /// and phones, so there are no file paths to worry about. The key carries a version so a future
    /// format can live alongside this one.
    /// </summary>
    public static class SaveStore
    {
        const string Key = "skyforge.save.v1";

        public static Progress Load()
        {
            string text = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
            return Progress.Load(text);
        }

        public static void Save(Progress progress)
        {
            PlayerPrefs.SetString(Key, progress.Save());
            PlayerPrefs.Save();
        }
    }
}
