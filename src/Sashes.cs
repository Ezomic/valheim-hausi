using System;
using System.Collections.Generic;
using System.IO;
using Ezomic.Shared;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// The sashes and the relics: one of each per biome that has a boss with a trophy.
    ///
    /// <b>Prefab names are permanent.</b> ZNetScene, ObjectDB and every saved inventory and ZDO store
    /// the hash of these names, so renaming one destroys every copy in the world and in every pack.
    /// The display names are not part of that and are free to change.
    ///
    /// <b>Eight, not nine.</b> A sash needs a boss trophy. The Ocean has no boss, so it gets no recipe
    /// and its trophies still work one at a time. The Frozen King drops no trophy either, and the Deep
    /// North was left out on that rule until LHM-98 gave it his Sacrificial Blood in its place, through
    /// BossItems. The prefabs are registered regardless of what this world's tables say,
    /// because a host changing BossBiomes must not be able to delete sashes that are already in packs.
    ///
    /// <b>The upgrade is the item's quality, vanilla's own path.</b> One recipe holds both prices.
    /// Requirement.GetAmount reads m_amount at quality 1 and (quality - 1) * m_amountPerLevel above it,
    /// so the trophies are the craft and the relic and the material are the upgrade, with neither
    /// charged for the other. m_amount is also what recipe discovery reads, so a player learns the
    /// recipe from holding the trophies and is not asked to have seen a relic first.
    ///
    /// <b>Misc, not Trophy.</b> Trophy would put a sash on item stands, where it has no attach child to
    /// show, and on the compendium's trophy page, as a trophy no creature drops.
    ///
    /// <b>Two forms of one mesh.</b> The models are authored standing on the body, at chest height. On
    /// the ground that would float a metre up, so the dropped form is centred on the item and laid
    /// flat at a smaller scale. Worn draws the same mesh back at the place it was modelled for.
    /// </summary>
    internal static class Sashes
    {
        private const string Prefix = "HausiSash_";
        private const string RelicPrefix = "HausiRelic_";

        private const string RecipePrefix = "Recipe_";
        private const string Bench = "piece_workbench";

        /// <summary>The dropped sash as a fraction of the worn one: a metre-tall loop is a hoop.</summary>
        private const float DroppedScale = 0.45f;

        private static readonly Dictionary<string, string> Display = new Dictionary<string, string>
        {
            { "Meadows", "Meadows" },
            { "BlackForest", "Black Forest" },
            { "Swamp", "Swamp" },
            { "Mountain", "Mountain" },
            { "Plains", "Plains" },
            { "Mistlands", "Mistlands" },
            { "Ashlands", "Ashlands" },
            { "DeepNorth", "Deep North" },
        };

        /// <summary>The material group the token and its clasp are modelled in, which is what glows.</summary>
        private const string TokenGroup = "bone";

        /// <summary>Per sash prefab, the submesh of its token. Filled as each sash is built.</summary>
        private static readonly Dictionary<string, int> TokenSubmesh = new Dictionary<string, int>();

        private static ObjectDB _writtenTo;
        private static int _writtenGeneration = -1;

        internal static void Keep()
        {
            WarnMissingAssets();

            foreach (KeyValuePair<string, string> biome in Display)
            {
                string key = biome.Key;
                string title = biome.Value;

                Prefabs.Keep(Prefix + key, () => BuildSash(key, title), item: true);
                Prefabs.Keep(RelicPrefix + key, () => BuildRelic(key, title), item: true);
            }
        }

        /// <summary>
        /// A sash or relic with no .obj beside the dll is never registered, and with it no recipe is
        /// written, so one line at load names every file that is missing and what it costs.
        /// </summary>
        private static void WarnMissingAssets()
        {
            string directory = Path.GetDirectoryName(typeof(Sashes).Assembly.Location);
            var models = new List<string>();
            var icons = new List<string>();

            foreach (string key in Display.Keys)
                foreach (string file in new[] { "sash_" + key.ToLowerInvariant(), "relic_" + key.ToLowerInvariant() })
                {
                    if (!File.Exists(Path.Combine(directory, file + ".obj"))) models.Add(file + ".obj");
                    if (!File.Exists(Path.Combine(directory, file + ".png"))) icons.Add(file + ".png");
                }

            if (models.Count > 0)
                HausiPlugin.Log.LogWarning("Missing beside the dll in " + directory + ": " + string.Join(", ", models.ToArray())
                                           + ". Without its model a sash or relic is not registered and its sash recipe is never written. "
                                           + "Reinstall with every .obj and .png from the package.");

            if (icons.Count > 0)
                HausiPlugin.Log.LogWarning("Missing beside the dll in " + directory + ": " + string.Join(", ", icons.ToArray())
                                           + ". Those items fall back to the donor's icon.");
        }

        internal static bool Is(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>The biome a sash prefab belongs to, or null for anything that is not one.</summary>
        internal static string BiomeOf(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) || !prefabName.StartsWith(Prefix, StringComparison.Ordinal)) return null;

            return prefabName.Substring(Prefix.Length).ToLowerInvariant();
        }

        /// <summary>The chance per unit for a worn item at this quality.</summary>
        internal static float Chance(string prefabName, int quality)
        {
            if (BiomeOf(prefabName) == null) return HausiConfig.TrophyChance.Value;

            return quality >= 2 ? HausiConfig.UpgradedChance.Value : HausiConfig.SashChance.Value;
        }

        /// <summary>The submesh of a sash's token, or -1 for anything that is not a built sash.</summary>
        internal static int TokenSubmeshOf(string prefabName)
        {
            return prefabName != null && TokenSubmesh.TryGetValue(prefabName, out int index) ? index : -1;
        }

        /// <summary>The relic prefab of a biome, or null. Biome in the index's spelling.</summary>
        internal static GameObject RelicOf(string biome)
        {
            if (string.IsNullOrEmpty(biome) || ObjectDB.instance == null) return null;

            foreach (string key in Display.Keys)
                if (string.Equals(key, biome, StringComparison.OrdinalIgnoreCase))
                    return ObjectDB.instance.GetItemPrefab(RelicPrefix + key);

            return null;
        }

        // ---------------------------------------------------------------- prefabs

        private static GameObject BuildSash(string key, string title)
        {
            GameObject clone = BuildItem(Prefix + key, "sash_" + key.ToLowerInvariant(), out ModelData model);
            if (clone == null) return null;

            TokenSubmesh[Prefix + key] = Array.IndexOf(model.Groups, TokenGroup);

            ItemDrop.ItemData.SharedData shared = clone.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = title + " Sash";
            shared.m_description = "Made from every trophy of the " + title + " and its boss. Worn in the trophy "
                                   + "slot, its creatures give more, and never their trophy.";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = 2;
            shared.m_weight = 1f;

            // Laid flat, centred, smaller. Worn.Build undoes all three for the body.
            Transform visual = clone.transform.Find(VisualName);
            Bounds bounds = model.Mesh.bounds;
            visual.localScale = Vector3.one * DroppedScale;
            visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.localPosition = visual.localRotation * (-bounds.center * DroppedScale);

            return clone;
        }

        private static GameObject BuildRelic(string key, string title)
        {
            GameObject clone = BuildItem(RelicPrefix + key, "relic_" + key.ToLowerInvariant(), out ModelData _);
            if (clone == null) return null;

            ItemDrop.ItemData.SharedData shared = clone.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = title + " Relic";
            shared.m_description = "Taken from the " + title + " boss, summoned at its hardest. With the right "
                                   + "material it upgrades the " + title + " Sash at a workbench.";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            shared.m_maxStackSize = 10;
            shared.m_weight = 1f;

            return clone;
        }

        private const string VisualName = "hausi_visual";

        /// <summary>
        /// A vanilla item for its machinery, the hand-built mesh for its looks. Components are removed
        /// rather than their GameObjects, because on some donors the collider shares an object with the
        /// renderer, and an ItemDrop with nothing to rest on falls through the world.
        /// </summary>
        private static GameObject BuildItem(string name, string file, out ModelData model)
        {
            model = null;

            string directory = Path.GetDirectoryName(typeof(Sashes).Assembly.Location);
            model = ObjMesh.Load(Path.Combine(directory, file + ".obj"));
            if (model == null) return null;

            GameObject source = Prefabs.Donor("LeatherScraps, Wood", out string _);
            if (source == null) return null;

            GameObject clone = Prefabs.Clone(source, name);
            if (clone == null) return null;

            clone.transform.localRotation = Quaternion.identity;

            foreach (MeshRenderer renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.TryGetComponent(out MeshFilter filter)) UnityEngine.Object.DestroyImmediate(filter);
                UnityEngine.Object.DestroyImmediate(renderer);
            }

            foreach (ParticleSystemRenderer drawn in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
                UnityEngine.Object.DestroyImmediate(drawn);
            foreach (ParticleSystem particles in clone.GetComponentsInChildren<ParticleSystem>(true))
                UnityEngine.Object.DestroyImmediate(particles);

            var visual = new GameObject(VisualName);
            visual.transform.SetParent(clone.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            MeshRenderer added = visual.AddComponent<MeshRenderer>();
            added.sharedMaterials = Skins.Skin(model.Groups);
            Skins.Remap(model.Mesh, model.Groups);

            ItemDrop drop = clone.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;

            Sprite icon = Icons.Load(file + ".png", name);
            if (icon != null) shared.m_icons = new[] { icon };

            shared.m_teleportable = true;
            shared.m_questItem = false;
            drop.m_itemData.m_stack = 1;
            drop.m_itemData.m_quality = 1;
            drop.m_itemData.m_dropPrefab = clone;

            return clone;
        }

        // ---------------------------------------------------------------- recipes

        /// <summary>
        /// From the plugin's Update. ObjectDB is rebuilt per world and swapped on joining a server, and
        /// the sets change when a host's config arrives, so this checks both and rewrites only then.
        /// A recipe is resolved ItemDrops and the first ObjectDB of a session has no items, which is why
        /// it waits for the biome sets, and why nothing is written with any name unresolved: half a
        /// recipe is a discount, and an empty one is a free sash.
        /// </summary>
        internal static void Tick()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || !Biomes.Ready) return;
            if (db == _writtenTo && _writtenGeneration == Biomes.Generation) return;

            foreach (string key in Display.Keys)
                if (db.GetItemPrefab(Prefix + key) == null || db.GetItemPrefab(RelicPrefix + key) == null) return;

            _writtenTo = db;
            _writtenGeneration = Biomes.Generation;

            db.m_recipes.RemoveAll(recipe => recipe != null && recipe.name.StartsWith(RecipePrefix + Prefix));

            foreach (string key in Display.Keys) Write(db, key);
        }

        private static void Write(ObjectDB db, string key)
        {
            Biomes.BiomeSet set = Biomes.SetOf(key.ToLowerInvariant());
            if (set == null || set.BossTrophy == null)
            {
                HausiPlugin.Log.LogInfo("No sash recipe for " + key + ": BossBiomes and the game's tables give it no boss trophy.");
                return;
            }

            GameObject station = ZNetScene.instance == null ? null : ZNetScene.instance.GetPrefab(Bench);
            ItemDrop material = Material(db, key.ToLowerInvariant(), out int amount);
            ItemDrop relic = db.GetItemPrefab(RelicPrefix + key).GetComponent<ItemDrop>();

            if (station == null || !station.TryGetComponent(out CraftingStation bench) || material == null)
            {
                HausiPlugin.LogOnce("No sash recipe for " + key + ": the workbench or its UpgradeMaterials entry "
                                    + "did not resolve.");
                return;
            }

            var resources = new List<Piece.Requirement>();
            foreach (ItemDrop trophy in set.Trophies)
                resources.Add(new Piece.Requirement { m_resItem = trophy, m_amount = 1, m_amountPerLevel = 0, m_recover = false });

            // A BossItems stand-in is no trophy, so it is not among the ones above.
            if (!set.Trophies.Contains(set.BossTrophy))
                resources.Add(new Piece.Requirement { m_resItem = set.BossTrophy, m_amount = 1, m_amountPerLevel = 0, m_recover = false });

            resources.Add(new Piece.Requirement { m_resItem = relic, m_amount = 0, m_amountPerLevel = 1, m_recover = false });
            resources.Add(new Piece.Requirement { m_resItem = material, m_amount = 0, m_amountPerLevel = amount, m_recover = false });

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = RecipePrefix + Prefix + key;
            recipe.m_item = db.GetItemPrefab(Prefix + key).GetComponent<ItemDrop>();
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = bench;
            recipe.m_minStationLevel = 1;
            recipe.m_resources = resources.ToArray();

            db.m_recipes.Add(recipe);
        }

        /// <summary>The upgrade material a biome names in UpgradeMaterials, or null.</summary>
        private static ItemDrop Material(ObjectDB db, string biome, out int amount)
        {
            amount = 0;

            foreach (string entry in (HausiConfig.UpgradeMaterials.Value ?? "").Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 3 || parts[0].Trim().ToLowerInvariant() != biome) continue;
                if (!int.TryParse(parts[2].Trim(), out amount) || amount <= 0) return null;

                GameObject prefab = db.GetItemPrefab(parts[1].Trim());
                return prefab != null && prefab.TryGetComponent(out ItemDrop item) ? item : null;
            }

            return null;
        }
    }
}
