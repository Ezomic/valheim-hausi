#if DEBUG
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ezomic.Shared;
using HarmonyLib;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// `hausi` in the console: what you wear and what it does, what a biome's set is, and what the
    /// last kill decided. It exists for a player asking why a kill gave nothing and for a Devkit
    /// scenario, which can run this and assert on what it printed.
    ///
    /// <b>isCheat: false, and that is honest.</b> Every verb but three only reads. `wear` makes the call
    /// the inventory's right click makes, `stats reset` zeroes a counter this mod keeps for itself, and
    /// `reload` reads your own player back from what was written a moment before.
    ///
    /// <b>The output is written to be asserted on.</b> Facts are name=value tokens with no spaces
    /// inside them, because Devkit's `printed` step matches a substring.
    /// </summary>
    internal static class DevConsole
    {
        /// <summary>
        /// Process-wide, not per world. Terminal's command table is a private static that nothing
        /// clears, so a second registration would leave a duplicate behind.
        /// </summary>
        private static bool _registered;

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        [HarmonyPostfix]
        private static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("hausi",
                "[wear <prefab> | fill <prefab> | place <prefab> | window | sets | last | look | stats [reset | within <low> <high>] | floor <prefab> "
                + "[metres] | biomes <prefab...> | reload] - what you wear, what each biome's set is, what the "
                + "last kill decided",
                OnCommand, isCheat: false);
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            Terminal term = args.Context;
            if (term == null) return;

            Player player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null)
            {
                term.AddString("hausi: no world loaded");
                return;
            }

            switch (args.Length > 1 ? args[1].ToLowerInvariant() : "")
            {
                case "wear": Wear(term, player, args); break;
                case "fill": Fill(term, player, args); break;
                case "place": Place(term, player, args); break;
                case "window": term.AddString("hausi: " + InfusionTable.WindowState()); break;
                case "sets": Sets(term); break;
                case "last": Last(term); break;
                case "look": Look(term); break;
                case "stats": Stats(term, args); break;
                case "floor": Floor(term, player, args); break;
                case "biomes": BiomesOf(term, args); break;
                case "reload": Reload(term, player); break;
                default: Summary(term, player); break;
            }
        }

        private static void Summary(Terminal term, Player player)
        {
            ItemDrop.ItemData worn = Slot.Worn(player);
            string name = worn != null && worn.m_dropPrefab != null ? worn.m_dropPrefab.name : "none";

            ZNetView nview = player.GetComponent<ZNetView>();
            int published = nview != null && nview.IsValid() ? nview.GetZDO().GetInt(Slot.WornKey) : 0;

            int marks = player.GetInventory().GetAllItems().Count(i => i.m_equipped && Slot.Wearable(i));

            term.AddString("hausi: worn=" + name + " stack=" + (worn != null ? worn.m_stack : 0)
                + " marked=" + (worn != null && worn.m_equipped ? "yes" : "no") + " marks=" + marks
                + " published=" + (published != 0 ? "yes" : "no")
                + " biome=" + (worn != null ? Biomes.OfWorn(worn.m_dropPrefab) : "none")
                + " ready=" + (Biomes.Ready ? "yes" : "no")
                + " enabled=" + (HausiConfig.Enabled.Value ? "yes" : "no"));
        }

        private static void Wear(Terminal term, Player player, Terminal.ConsoleEventArgs args)
        {
            ItemDrop.ItemData item = args.Length > 2
                ? player.GetInventory().GetItem(args[2], -1, isPrefabName: true)
                : null;

            if (item == null)
            {
                term.AddString("hausi: not carrying that");
                return;
            }

            player.UseItem(player.GetInventory(), item, fromInventoryGui: true);
            term.AddString("hausi: used " + args[2]);
        }

        private static void Fill(Terminal term, Player player, Terminal.ConsoleEventArgs args)
        {
            ItemDrop.ItemData item = args.Length > 2
                ? player.GetInventory().GetItem(args[2], -1, isPrefabName: true)
                : null;

            if (item == null)
            {
                term.AddString("hausi: not carrying that");
                return;
            }

            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(player.transform.position, 20f, pieces);
            Piece table = pieces
                .Where(p => Utils.GetPrefabName(p.gameObject) == InfusionTable.PrefabName)
                .OrderBy(p => Vector3.Distance(p.transform.position, player.transform.position))
                .FirstOrDefault();

            if (table == null)
            {
                term.AddString("hausi: no " + InfusionTable.PrefabName + " within 20 m");
                return;
            }

            bool took = table.GetComponentsInChildren<Interactable>().Any(i => i.UseItem(player, item));
            term.AddString("hausi: fill took=" + (took ? "yes" : "no"));
        }

        private static void Place(Terminal term, Player player, Terminal.ConsoleEventArgs args)
        {
            ItemDrop.ItemData item = args.Length > 2
                ? player.GetInventory().GetItem(args[2], -1, isPrefabName: true)
                : null;

            if (item == null)
            {
                term.AddString("hausi: not carrying that");
                return;
            }

            player.UseItem(player.GetInventory(), item, fromInventoryGui: true);
            term.AddString("hausi: right-clicked " + args[2]);
        }

        private static void Sets(Terminal term)
        {
            foreach (Biomes.BiomeSet set in Biomes.Sets.OrderBy(s => s.Biome))
                term.AddString("hausi: set." + set.Biome + " boss=" + (set.BossTrophy != null ? set.BossTrophy.gameObject.name : "none")
                    + " count=" + set.Trophies.Count + " trophies="
                    + string.Join(",", set.Trophies.Select(t => t.gameObject.name).OrderBy(n => n).ToArray()));

            var placed = new HashSet<string>(Biomes.Sets.SelectMany(s => s.Trophies).Select(t => t.gameObject.name));
            var unplaced = ObjectDB.instance.m_items
                .Where(p => p != null && Biomes.IsTrophy(p.GetComponent<ItemDrop>()) && !placed.Contains(p.name))
                .Select(p => p.name).OrderBy(n => n).ToArray();

            term.AddString("hausi: nobiome=" + string.Join(",", unplaced));

            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Character boss;
                if (prefab == null || !prefab.TryGetComponent(out boss) || !boss.m_boss) continue;

                term.AddString("hausi: boss." + prefab.name + " key=" + boss.m_defeatSetGlobalKey + " drops="
                    + (prefab.TryGetComponent(out CharacterDrop drops)
                        ? string.Join(",", drops.m_drops.Where(d => d.m_prefab != null).Select(d => d.m_prefab.name).ToArray())
                        : "none"));
            }
        }

        private static void Last(Terminal term)
        {
            DropBoost.Decision last = DropBoost.Last;
            if (last == null)
            {
                term.AddString("hausi: last=none");
                return;
            }

            term.AddString("hausi: last=" + last.Creature + " creaturebiome=" + last.CreatureBiome
                + " worn=" + last.Worn + " wornbiome=" + last.WornBiome
                + " chance=" + last.Chance.ToString("0.##", CultureInfo.InvariantCulture)
                + " blocked=" + (last.Blocked.Count > 0 ? string.Join(",", last.Blocked.ToArray()) : "none"));

            foreach (KeyValuePair<string, int> pair in last.Base)
                term.AddString("hausi: drop." + pair.Key + " base=" + pair.Value + " extra=" + last.Extra[pair.Key]
                    + " total=" + (pair.Value + last.Extra[pair.Key]));
        }

        private static void Look(Terminal term)
        {
            foreach (Player player in Player.GetAllPlayers())
            {
                VisEquipment vis;
                if (player.TryGetComponent(out vis))
                    term.AddString("hausi: look." + (player == Player.m_localPlayer ? "self" : "other") + "=" + Worn.Described(vis));
            }
        }

        private static void Stats(Terminal term, Terminal.ConsoleEventArgs args)
        {
            if (args.Length > 2 && args[2] == "reset")
            {
                DropBoost.Units = 0;
                DropBoost.Extras = 0;
                DropBoost.Relics = 0;
            }

            float ratio = DropBoost.Units > 0 ? (float)DropBoost.Extras / DropBoost.Units : 0f;

            string within = "";
            float low, high;
            if (args.Length > 4 && args[2] == "within" && float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out low)
                && float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out high))
                within = " within=" + (ratio >= low && ratio <= high ? "yes" : "no");

            term.AddString("hausi: units=" + DropBoost.Units + " extras=" + DropBoost.Extras
                + " ratio=" + ratio.ToString("0.000", CultureInfo.InvariantCulture) + within
                + " relics=" + DropBoost.Relics);
        }

        /// <summary>
        /// How many of an item lie near you, printed so a scenario can keep the number and compare
        /// it with another, and the pack beside it, because a drop that lands within reach is picked
        /// up by itself and is then not on the floor to count. Devkit's `dropped` wants the count written
        /// in the file, and cannot tell "none yet" from "none": a boss's ragdoll spills its loot seconds
        /// after it dies, so `any=yes` on an item it always drops is how a file knows the loot is down.
        /// </summary>
        private static void Floor(Terminal term, Player player, Terminal.ConsoleEventArgs args)
        {
            string name = args.Length > 2 ? args[2] : "";
            float metres = 20f;
            if (args.Length > 3 && !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out metres)) metres = 20f;

            int count = 0;
            foreach (ItemDrop drop in Object.FindObjectsOfType<ItemDrop>())
            {
                if (drop.m_itemData == null || drop.m_itemData.m_dropPrefab == null) continue;
                if (drop.m_itemData.m_dropPrefab.name != name) continue;
                if (Vector3.Distance(drop.transform.position, player.transform.position) <= metres)
                    count += drop.m_itemData.m_stack;
            }

            int pack = player.GetInventory().GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == name).Sum(i => i.m_stack);

            term.AddString("hausi: floor." + name + "=" + count + " pack=" + pack + " total=" + (count + pack)
                + " any=" + (count + pack > 0 ? "yes" : "no"));
        }

        /// <summary>
        /// Your pack written out and read straight back in the order Player.Load does it, which is the
        /// half of a relog this mod depends on: m_equipped surviving the save format, and
        /// EquipInventoryItems putting it back in the slot. It exists because a scenario cannot survive
        /// a restart, and a Devkit quit leaves no inventory on disk, with or without this mod.
        ///
        /// The pack and not the whole player. Player.Load is not safe to run twice: it adds to
        /// m_knownStations with Dictionary.Add, so on any character who knows a workbench it throws
        /// halfway and leaves the player half loaded. That was this verb's first version.
        /// </summary>
        private static void Reload(Terminal term, Player player)
        {
            var package = new ZPackage();
            player.GetInventory().Save(package);
            package.SetPos(0);
            player.UnequipAllItems();
            player.GetInventory().Load(package);
            AccessTools.Method(typeof(Player), "EquipInventoryItems").Invoke(player, null);
            term.AddString("hausi: reloaded");
        }

        private static void BiomesOf(Terminal term, Terminal.ConsoleEventArgs args)
        {
            for (int i = 2; i < args.Length; i++)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(args[i]);
                term.AddString("hausi: biome." + args[i] + "=" + Biomes.OfWorn(prefab));
            }
        }
    }
}
#endif
