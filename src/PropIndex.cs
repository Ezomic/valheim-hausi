using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// Finds a loaded prefab by name, so a material can be lifted off it.
    ///
    /// Lifted from Stow, which lifted it from Stoker. Nothing here grafts a vanilla prop
    /// onto anything - the shapes in this mod are all hand-modelled. The only thing
    /// borrowed is the *surface*, one real material per group, so a mesh of ours is made
    /// out of the game's own materials rather than an approximation of them.
    /// </summary>
    internal static class PropIndex
    {
        private static Dictionary<string, GameObject> _index;

        /// <summary>
        /// ZNetScene only knows prefabs that carry a ZNetView, and most dressing props do
        /// not - they live as children inside location prefabs. So the scene is asked
        /// first, and everything Unity currently has loaded is searched second.
        /// </summary>
        public static GameObject Find(string name)
        {
            if (ZNetScene.instance != null)
            {
                var registered = ZNetScene.instance.GetPrefab(name);
                if (registered != null) return registered;
            }

            if (_index == null) BuildIndex();

            GameObject found;
            return _index.TryGetValue(name, out found) ? found : null;
        }

        private static void BuildIndex()
        {
            _index = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

            // Expensive, so it happens once. Prefabs are preferred over scene instances:
            // an instance carries whatever state the world has put on it.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate == null || candidate.transform.parent != null) continue;
                if (candidate.GetComponentInChildren<MeshRenderer>(true) == null) continue;

                var inScene = candidate.scene.IsValid();
                GameObject existing;

                if (!_index.TryGetValue(candidate.name, out existing) || (!inScene && existing.scene.IsValid()))
                    _index[candidate.name] = candidate;
            }

            HausiPlugin.Log.LogInfo("Prop index built: " + _index.Count + " candidates with meshes.");
        }

        /// <summary>
        /// Lists everything loaded whose name contains one of the given words, which is how
        /// to find a prefab worth borrowing a material from.
        ///
        /// Asking the index rather than the asset manifest, because the manifest catalogues
        /// every asset that exists rather than every asset that is loaded - Stoker's first
        /// candidate list came off the manifest and two of sixteen names resolved.
        /// </summary>
        /// <summary>Noise when hunting a material donor. Not noise when hunting an effect.</summary>
        private static readonly string[] Chaff =
            { "destruction", "broken", "lod", "vfx", "sfx" };

        public static void Search(string keywords)
        {
            if (string.IsNullOrEmpty(keywords)) return;
            if (_index == null) BuildIndex();

            // Both lists. The index only holds prefabs with a MeshRenderer, which is
            // right for its own job and useless for anything made of particles - so a
            // search for vfx_ came back with nothing at all, from a game that is full
            // of them. ZNetScene's list is what "loaded prefab" actually means.
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _index.Keys) names.Add(key);

            if (ZNetScene.instance != null)
                foreach (var prefab in ZNetScene.instance.m_prefabs)
                    if (prefab != null) names.Add(prefab.name);

            foreach (var raw in keywords.Split(','))
            {
                var word = raw.Trim();
                if (word.Length == 0) continue;

                var hits = new List<string>();
                foreach (var name in names)
                {
                    if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Broken and destruction variants are the same prop in pieces - but
                    // only chaff when they are not what was asked for. This filter used
                    // to be unconditional, so a search for 'vfx_' threw away every hit
                    // it found on the grounds that it contained 'vfx'.
                    var lower = name.ToLowerInvariant();
                    var asked = word.ToLowerInvariant();

                    var junk = false;
                    foreach (var chaff in Chaff)
                        if (lower.Contains(chaff) && !asked.Contains(chaff)) junk = true;

                    if (junk) continue;

                    hits.Add(name);
                }

                hits.Sort();
                HausiPlugin.Log.LogInfo(
                    "Prefabs matching '" + word + "' (" + hits.Count + "): "
                    + string.Join(", ", hits.GetRange(0, Math.Min(40, hits.Count)).ToArray()));
            }
        }
    }
}
