using HarmonyLib;

namespace Hausi
{
    /// <summary>
    /// Offering at a boss's altar in that biome's sash asks Vandi for the harder fight.
    ///
    /// <b>On vanilla's own method.</b> InitiateSpawnBoss is what every altar goes through, offering
    /// items or bells on stands alike, and it runs on the client that made the offering, which is the
    /// only machine that knows what they wear. Utangard has no hook to offer: its altar code is a
    /// refusing prefix on this same method. So this checks __runOriginal, as Vandi's own postfix does,
    /// and a refused offering asks for nothing.
    ///
    /// <b>Stars are Vandi's.</b> This asks and Vandi decides, so its boss health, specials and credit all
    /// apply as they do to any starred boss, and there is no second difficulty system here. The sash is
    /// only looked at, never taken. The offering itself costs what it always costs.
    ///
    /// <b>The Queen has no altar.</b> She waits behind a sealed door, so no offering reaches her, and
    /// the Mistlands relic cannot be earned this way.
    /// </summary>
    internal static class Altar
    {
        [HarmonyPatch(typeof(OfferingBowl), "InitiateSpawnBoss")]
        [HarmonyPostfix]
        private static void Offered(OfferingBowl __instance, bool __runOriginal)
        {
            if (!__runOriginal || !HausiConfig.Enabled.Value || __instance == null || __instance.m_bossPrefab == null) return;

            ItemDrop.ItemData worn = Slot.Worn(Player.m_localPlayer);
            if (worn == null || !Sashes.Is(worn)) return;

            if (!__instance.m_bossPrefab.TryGetComponent(out Character boss)) return;

            string key = boss.m_defeatSetGlobalKey;
            if (Sashes.BiomeOf(worn.m_dropPrefab.name) != Biomes.BiomeOfBossKey(key)) return;

            VandiLink.AskHarder(key, HausiConfig.HarderStars.Value);

            if (HausiConfig.Verbose.Value)
                HausiPlugin.Log.LogInfo("Offered for " + key + " in " + worn.m_dropPrefab.name + ": asked Vandi for "
                                        + HausiConfig.HarderStars.Value + " more star(s). Vandi available: " + VandiLink.Available);
        }
    }
}
