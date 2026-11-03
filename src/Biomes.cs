using System;
using System.Collections.Generic;
using Ezomic.Shared;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// Which biome a trophy, a creature and a set of trophies belong to, read off the game's own
    /// tables through BiomeIndex rather than listed here. That is what makes the rule cover a mod's
    /// creature for free, and it means a sash's recipe is whatever the world actually holds.
    ///
    /// <b>A creature's biome is its own trophy's.</b> BiomeIndex classifies items and not creatures,
    /// so the question goes through the one item that can only mean that creature. It also makes
    /// the rule exactly the sentence a player reads on the trophy: this skull's biome is the biome
    /// of the creatures it helps with. A creature with no trophy has no biome and gets no boost.
    /// That fails by doing less, and the alternatives were worse: the biome it died in would make a
    /// Greydwarf in the Meadows a Meadows creature, and the biome of its other drops would make
    /// everything that drops resin a Meadows creature.
    ///
    /// <b>The index is per world.</b> ZoneSystem, SpawnSystem and ZNetScene only exist once a world
    /// is loading, and a new world is a new ZNetScene. Until it is complete, everything answers
    /// "none", which is a kill with nothing changed and never a wrong biome.
    /// </summary>
    internal static class Biomes
    {
        private const float RetrySeconds = 5f;

        private static ZNetScene _scene;
        private static float _nextTry;

        /// <summary>What the last build found. Null until the index is complete for this world.</summary>
        private static Dictionary<string, BiomeSet> _sets;

        private static readonly Dictionary<string, string> Creatures = new Dictionary<string, string>();

        /// <summary>Every trophy of one biome, and the boss's. The makings of its sash.</summary>
        internal sealed class BiomeSet
        {
            internal string Biome;
            internal readonly List<ItemDrop> Trophies = new List<ItemDrop>();

            /// <summary>
            /// The boss's trophy, which is also in Trophies, or for a boss with none the BossItems
            /// stand-in, which is not: it is no trophy, so it can be worn by nobody and blocks nothing.
            /// </summary>
            internal ItemDrop BossTrophy;
        }

        internal static void Wire()
        {
            BiomeIndex.Log = HausiPlugin.Log;
            BiomeIndex.BiomeForKey = BiomeOfBossKey;
            BiomeIndex.Overrides = () => HausiConfig.BiomeOverrides.Value;

            // A joining client has these swapped for the host's, and that swap has to reach the
            // recipes. The first version read them at build time and a client kept its own set of
            // ingredients for the whole visit.
            HausiConfig.BossBiomes.SettingChanged += (sender, args) => Forget();
            HausiConfig.BiomeOverrides.SettingChanged += (sender, args) => Forget();
            HausiConfig.BossItems.SettingChanged += (sender, args) => Forget();

            // Read only when a recipe is written, so it only needs the recipes to notice.
            HausiConfig.UpgradeMaterials.SettingChanged += (sender, args) => Generation++;
        }

        private static void Forget()
        {
            BiomeIndex.Invalidate();
            _sets = null;
            Generation++;
            _nextTry = 0f;
            Creatures.Clear();
        }

        /// <summary>
        /// From the plugin's Update. A lookup once it has an answer, and one attempt every few
        /// seconds until then, because Prepare walks the vegetation table and every prefab.
        /// </summary>
        internal static void Tick()
        {
            if (_scene != ZNetScene.instance)
            {
                _scene = ZNetScene.instance;
                Forget();
            }

            if (_scene == null || _sets != null) return;
            if (Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + RetrySeconds;

            // The stub ObjectDB of a session has no items, so a set built off it would be empty.
            if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0) return;

            BiomeIndex.Prepare();
            if (BiomeIndex.Complete) _sets = BuildSets();
        }

        /// <summary>Moves whenever the sets are thrown away, so a recipe knows it is out of date.</summary>
        internal static int Generation;

        internal static bool Ready
        {
            get { return _sets != null; }
        }

        /// <summary>The sets by biome, or an empty table before the index is complete.</summary>
        internal static IEnumerable<BiomeSet> Sets
        {
            get { return _sets != null ? _sets.Values : (IEnumerable<BiomeSet>)new BiomeSet[0]; }
        }

        internal static BiomeSet SetOf(string biome)
        {
            BiomeSet set;
            return _sets != null && _sets.TryGetValue(biome, out set) ? set : null;
        }

        /// <summary>The biome a worn item helps with, or BiomeIndex.None.</summary>
        internal static string OfWorn(GameObject prefab)
        {
            if (prefab == null || _sets == null) return BiomeIndex.None;

            return Sashes.BiomeOf(prefab.name) ?? BiomeIndex.BiomeOf(prefab.name);
        }

        /// <summary>The biome of a creature's own trophy, or BiomeIndex.None.</summary>
        internal static string OfCreature(Character character)
        {
            if (character == null || _sets == null) return BiomeIndex.None;

            string name = Utils.GetPrefabName(character.gameObject);

            string biome;
            if (Creatures.TryGetValue(name, out biome)) return biome;

            CharacterDrop drops;
            ItemDrop trophy = character.TryGetComponent(out drops) ? TrophyOf(drops) : null;

            biome = trophy == null ? BiomeIndex.None : BiomeIndex.BiomeOf(trophy.gameObject.name);
            Creatures[name] = biome;

            return biome;
        }

        private static ItemDrop TrophyOf(CharacterDrop drops)
        {
            foreach (CharacterDrop.Drop drop in drops.m_drops)
            {
                if (drop == null || drop.m_prefab == null) continue;

                ItemDrop item;
                if (drop.m_prefab.TryGetComponent(out item) && IsTrophy(item)) return item;
            }

            return null;
        }

        internal static bool IsTrophy(ItemDrop item)
        {
            return item != null && item.m_itemData != null && item.m_itemData.m_shared != null
                   && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }

        private static Dictionary<string, BiomeSet> BuildSets()
        {
            var sets = new Dictionary<string, BiomeSet>();

            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop item;
                if (prefab == null || !prefab.TryGetComponent(out item) || !IsTrophy(item)) continue;

                string biome = BiomeIndex.BiomeOf(prefab.name);
                if (biome == BiomeIndex.None) continue;

                BiomeSet set;
                if (!sets.TryGetValue(biome, out set)) sets[biome] = set = new BiomeSet { Biome = biome };
                set.Trophies.Add(item);
            }

            // In the prefab table, not the spawn table: a boss stands in a location. When two
            // prefabs carry keys of one biome the Frozen King has three, with two keys between
            // them, and the first that drops a trophy of that biome is the boss's. The check against
            // the set is what stops a modded add with a boss's key naming somebody else's trophy.
            foreach (GameObject prefab in _scene.m_prefabs)
            {
                Character character;
                if (prefab == null || !prefab.TryGetComponent(out character)) continue;

                string biome = BiomeOfBossKey(character.m_defeatSetGlobalKey);
                if (biome == null) continue;

                CharacterDrop drops;
                if (!prefab.TryGetComponent(out drops)) continue;

                ItemDrop trophy = TrophyOf(drops);

                BiomeSet set;
                if (trophy == null || !sets.TryGetValue(biome, out set) || set.BossTrophy != null) continue;

                if (set.Trophies.Contains(trophy)) set.BossTrophy = trophy;
            }

            foreach (BiomeSet set in sets.Values)
                if (set.BossTrophy == null) set.BossTrophy = BossItem(set.Biome);

            HausiPlugin.Log.LogInfo("Biome sets built: " + Describe(sets));

            return sets;
        }

        private static string Describe(Dictionary<string, BiomeSet> sets)
        {
            var parts = new List<string>();

            foreach (BiomeSet set in sets.Values)
                parts.Add(set.Biome + " " + set.Trophies.Count + " trophies, boss "
                          + (set.BossTrophy != null ? set.BossTrophy.gameObject.name : "none"));

            return string.Join("; ", parts.ToArray());
        }

        /// <summary>The first BossItems entry for a biome, resolved, or null.</summary>
        private static ItemDrop BossItem(string biome)
        {
            foreach (string entry in (HausiConfig.BossItems.Value ?? "").Split(','))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2 || pair[0].Trim().ToLowerInvariant() != biome) continue;

                GameObject prefab = ObjectDB.instance.GetItemPrefab(pair[1].Trim());
                if (prefab != null && prefab.TryGetComponent(out ItemDrop item)) return item;

                HausiPlugin.Log.LogWarning("BossItems names " + pair[1].Trim() + " for " + biome
                                           + ", which is no item, so that biome has no sash recipe.");
                return null;
            }

            return null;
        }

        /// <summary>The biome a boss key belongs to in BossBiomes, or null. BiomeIndex asks this too.</summary>
        internal static string BiomeOfBossKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            foreach (string entry in (HausiConfig.BossBiomes.Value ?? "").Split(','))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2) continue;

                if (string.Equals(pair[0].Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
                    return pair[1].Trim().ToLowerInvariant();
            }

            return null;
        }
    }
}
