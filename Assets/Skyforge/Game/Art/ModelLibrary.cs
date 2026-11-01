using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skyforge.Game
{
    /// <summary>
    /// Loads the Blender-made models from <c>Assets/Skyforge/Resources/Models/</c> (see docs/BLENDER.md).
    /// A model that doesn't exist yet returns null, and the caller falls back to its code-built
    /// version, so models can be replaced one at a time.
    /// <para>
    /// Every loaded model is wrapped in an empty "mount" object: the game moves and spins the mount,
    /// never the imported root, whose own rotation corrects Blender's Z-up axes. Imported materials
    /// are swapped, by material name, for the game's shared materials (<see cref="Mats"/>), so
    /// lighting and glow match everywhere.
    /// </para>
    /// </summary>
    public static class ModelLibrary
    {
        public const string Folder = "Models/";

        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Prefabs.Clear();

        /// <summary>True if a Blender model with this file name (no extension) is in the project.</summary>
        public static bool Has(string name) => Load(name) != null;

        static GameObject Load(string name)
        {
            if (!Prefabs.TryGetValue(name, out var prefab))
            {
                prefab = Resources.Load<GameObject>(Folder + name);
                Prefabs[name] = prefab;
            }
            return prefab;
        }

        /// <summary>
        /// Instantiates model <paramref name="name"/> inside a new mount object under
        /// <paramref name="parent"/>, or returns null if there's no such model.
        /// </summary>
        public static GameObject TryCreate(string name, Transform parent)
        {
            var prefab = Load(name);
            if (prefab == null) return null;
            var mount = new GameObject(name);
            mount.transform.SetParent(parent, false);
            var model = Object.Instantiate(prefab, mount.transform, false);
            model.name = name + " (model)";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Remap(mats[i]);
                r.sharedMaterials = mats;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return mount;
        }

        /// <summary>
        /// The game material for a Blender material slot, by name (docs/BLENDER.md lists them).
        /// Blender's ".001" suffixes and case are ignored. Unknown names get the plain hull.
        /// </summary>
        public static Material Remap(Material imported)
        {
            string n = imported == null ? "" : imported.name;
            int dot = n.IndexOf('.');
            if (dot >= 0) n = n.Substring(0, dot);
            n = n.Replace(" (Instance)", "").Trim().ToLowerInvariant();
            switch (n)
            {
                case "hull": return Mats.Hull;
                case "hulllight": return Mats.HullLight;
                case "hulldark": return Mats.HullDark;
                case "canopy": return Mats.Canopy;
                case "glow": return Mats.PlayerGlow;
                case "glowsoft": return Mats.PlayerGlowSoft;
                case "enemyhull": return Mats.EnemyHull;
                case "enemyplate": return Mats.EnemyPlate;
                case "enemyglow": return Mats.EnemyGlow;
                default: return Mats.Hull;
            }
        }
    }
}
