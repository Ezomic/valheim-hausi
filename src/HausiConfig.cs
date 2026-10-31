using BepInEx.Configuration;

namespace Hausi
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// Note the standing BepInEx trap: every entry is written to disk on first run and the
    /// saved value beats a new default in code. Changing a default here does nothing on a
    /// machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.hausi.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg
    /// before reading any code.
    /// </summary>
    internal static class HausiConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> TrophyChance, SashChance, UpgradedChance;
        internal static ConfigEntry<string> BossBiomes, BiomeOverrides, UpgradeMaterials;
        internal static ConfigEntry<int> HarderStars;
        internal static ConfigEntry<string> WornJoint;
        internal static ConfigEntry<float> WornSize, WornRight, WornUp, WornForward, WornTurn;
        internal static ConfigEntry<bool> Verbose;

        internal static void Bind(ConfigFile cfg)
        {
            // Every mod here has one, and it means the same thing every time: loaded, bound,
            // patched, and deciding nothing. Not "unloaded" - a plugin cannot unload itself,
            // and a switch that pretends otherwise is a lie somebody will debug.
            Enabled = cfg.Bind("Hausi", "Enabled", true,
                "Off leaves the plugin loaded and changing nothing: trophies cannot be worn, one "
                + "already worn comes off, and every kill drops what the game says.");

            // Chances rather than multipliers, because the rule is per unit: a Boar dropping 2
            // meat rolls twice, each for one more. A multiplier on the stack would have to round,
            // and rounding 1 x 1.25 gives nothing at all on the commonest drop in the game.
            TrophyChance = cfg.Bind("Tiers", "TrophyChance", 0.25f,
                "Worn trophy: the chance, 0 to 1, that each unit a creature of its biome drops comes "
                + "with one more. 0.25 is what a player reads as 1.25x.");

            SashChance = cfg.Bind("Tiers", "SashChance", 0.5f,
                "The same for that biome's sash. 0.5 is 1.5x.");

            UpgradedChance = cfg.Bind("Tiers", "UpgradedChance", 1f,
                "The same for an upgraded sash. 1 is a true double, every unit, every time.");

            // The one fact the game's tables do not hold, as Vandi's and Yoke's copies of this
            // same row say: nothing reads "defeated_gdking belongs to the Black Forest" off a
            // prefab. A key may name one biome only here, unlike Vandi's table, because a boss's
            // trophy goes into exactly one sash. So Fader is the Ashlands and not the Deep North
            // as well, where the Frozen King has his own keys.
            BossBiomes = cfg.Bind("Biomes", "BossBiomes",
                "defeated_eikthyr:meadows, defeated_gdking:blackforest, defeated_bonemass:swamp, "
                + "defeated_dragon:mountain, defeated_goblinking:plains, defeated_queen:mistlands, "
                + "defeated_fader:ashlands, defeated_frozenking:deepnorth, "
                + "defeated_frozenking_p3:deepnorth",
                "boss defeat key:biome, comma separated. A boss's trophy belongs to that biome and is "
                + "the last ingredient of its sash. A biome with no boss here has no sash; its "
                + "trophies still work one at a time. The Ocean is that case in vanilla.");

            // Ordinary creatures that only ever stand in a location or a dungeon, so no table the
            // game keeps can place them, and without this a Fuling shaman is nobody's. Read off a
            // full dump of what resolved to no biome on game 1.0.21. Hildir's quest bosses and the
            // rest are left out on purpose: they stay no biome, and that fails by doing less. Each
            // name here is also one more trophy in that biome's sash, for every player on the server.
            BiomeOverrides = cfg.Bind("Biomes", "BiomeOverrides",
                "TrophyGoblinShaman:plains, TrophyGrowth:plains, TrophyUlv:mountain, TrophyCultist:mountain, "
                + "TrophySkeletonPoison:swamp, TrophySurtling:ashlands, TrophyCharredMage:ashlands",
                "prefab:biome, comma separated, for an item the game's tables cannot place: a trophy "
                + "of a creature that only ever stands in a location or a dungeon. A trophy not placed "
                + "either way can be worn and does nothing, and naming one adds it to that biome's "
                + "sash. `hausi sets` in the console lists what resolved to nothing. Biomes: meadows, "
                + "blackforest, ocean, swamp, mountain, plains, mistlands, ashlands, deepnorth.");

            UpgradeMaterials = cfg.Bind("Biomes", "UpgradeMaterials",
                "meadows:DeerHide:20, blackforest:TrollHide:10, swamp:Ooze:20, mountain:WolfPelt:10, "
                + "plains:LoxPelt:5, mistlands:Carapace:10, ashlands:AskHide:5",
                "biome:prefab:amount, comma separated. What upgrading that biome's sash costs at a "
                + "level 2 workbench, on top of its relic. A biome with a missing or misspelled entry "
                + "has no sash recipe at all, and the log says so.");

            HarderStars = cfg.Bind("Tiers", "HarderStars", 1,
                "How many stars above what Vandi would give you a boss comes back with, when you offer "
                + "at its altar in that biome's sash. It may pass Vandi's own cap by this much, because "
                + "somebody already at the cap would otherwise get no harder a fight for it.");

            // Where it hangs is Local (see the plugin): nothing about it can desync anything, and
            // a host moving the trophy on your hip is not a rule of the server.
            //
            // The hips and not the chest, though a brooch would sit on the chest. The camera stands
            // behind the player, so anything on the breastbone is the one accessory its own wearer
            // never sees, and the iron trinket this was asked to look like is no guide: its rip is
            // a strap skinned to the whole body rig (attach_skin, under the name belt1), not a
            // thing on a joint.
            WornJoint = cfg.Bind("Worn", "WornJoint", "Hips",
                "The bone a worn trophy hangs from. Any name in the player's rig: Hips, Spine, "
                + "Spine1, Spine2, LeftUpLeg and so on.");

            WornSize = cfg.Bind("Worn", "WornSize", 0.24f,
                "How long the longest side of a worn trophy is, in metres. A boss's trophy is nearly "
                + "a metre and a Neck's is a hand's width, so every one is scaled to this.");

            WornRight = cfg.Bind("Worn", "WornRight", -0.25f,
                "Where it hangs, measured on the body standing straight with feet at zero: metres to "
                + "the wearer's right. Negative is their left.");

            WornUp = cfg.Bind("Worn", "WornUp", 0.98f, "Metres up from the feet. The belt is near 0.95.");

            WornForward = cfg.Bind("Worn", "WornForward", -0.18f,
                "Metres in front of the body's centre. Negative is behind it, where the arm does not "
                + "swing through it.");

            WornTurn = cfg.Bind("Worn", "WornTurn", -90f,
                "Degrees the trophy is turned about the vertical. -90 faces the wearer's left.");

            // Not synced by intent - see the plugin. A diagnostic flag is personal, and a
            // host turning on someone else's logging is not a thing anybody asked for.
            Verbose = cfg.Bind("Hausi", "Verbose", false,
                "Write every kill's decision to BepInEx/LogOutput.log: who struck the last blow, what "
                + "they wore, and what it changed. One line per kill.");
        }
    }
}
