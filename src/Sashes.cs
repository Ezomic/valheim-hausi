using System.Collections.Generic;

namespace Hausi
{
    /// <summary>
    /// The sashes as names and tiers. The prefabs and recipes wait for the model pick.
    /// </summary>
    internal static class Sashes
    {
        private const string Prefix = "HausiSash_";

        internal static bool Is(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(Prefix);
        }

        /// <summary>The biome a sash prefab belongs to, or null for anything that is not one.</summary>
        internal static string BiomeOf(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) || !prefabName.StartsWith(Prefix)) return null;

            return prefabName.Substring(Prefix.Length).ToLowerInvariant();
        }

        /// <summary>The chance per unit for a worn item at this quality.</summary>
        internal static float Chance(string prefabName, int quality)
        {
            if (BiomeOf(prefabName) == null) return HausiConfig.TrophyChance.Value;

            return quality >= 2 ? HausiConfig.UpgradedChance.Value : HausiConfig.SashChance.Value;
        }
    }
}
