using HarmonyLib;

namespace Hausi
{
    /// <summary>
    /// Offering at a boss's altar in that biome's sash asks Vandi for the harder fight.
    ///
    /// <b>When the altar has said yes, on the machine that made the offering.</b> Every altar goes
    /// through InitiateSpawnBoss, offering items or bells on stands alike, but that only sends a request:
    /// the altar's owner decides, and says nothing to a player it turns away because a summon is already
    /// on its way. Asking from there is how one player's sash would harden somebody else's boss, since an
    /// ask names a boss and not a fight. RPC_BossSpawnInitiated reaches only the sender of an accepted
    /// offering, which is the one machine that also knows what they wear, and it lands inside the altar's
    /// spawn delay. Utangard has no hook to offer: its altar code is a refusing prefix on
    /// InitiateSpawnBoss, so a refused offering never gets this far.
    ///
    /// <b>Stars are Vandi's.</b> This asks and Vandi decides, so its boss health, specials and credit all
    /// apply as they do to any starred boss, and there is no second difficulty system here. The sash is
    /// only looked at, never taken. The offering itself costs what it always costs.
    ///
    /// <b>The Queen's room is an altar like any other.</b> She stands there the first time, behind a sealed
    /// door, and comes back through offeraltar_queen in the room, for 3 Seeker Soldier trophies. The room
    /// generates at y 5066, which changes nothing here: this reads no position. The Frozen King's bowl is
    /// the same again: his altar's boss prefab carries defeated_frozenking, which BossBiomes puts in the
    /// Deep North. He fights in phases, each its own prefab, and which one's death Vandi has stamped as
    /// harder, and so which drops the relic, is unproven until a real kill.
    /// </summary>
    internal static class Altar
    {
        [HarmonyPatch(typeof(OfferingBowl), "RPC_BossSpawnInitiated")]
        [HarmonyPostfix]
        private static void Accepted(OfferingBowl __instance)
        {
            if (!HausiConfig.Enabled.Value || __instance == null || __instance.m_bossPrefab == null) return;

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
