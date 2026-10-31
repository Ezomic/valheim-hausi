using HarmonyLib;

namespace Hausi
{
    /// <summary>
    /// The slot itself: one trophy or one sash, worn like gear.
    ///
    /// <b>There is no slot to add.</b> Vanilla has no gear slots either. Equipping sets
    /// item.m_equipped, which InventoryGrid draws as the checkmark and Inventory saves, and "the
    /// slot" is a field on Humanoid holding that one item. So this is the same arrangement with
    /// the field kept here: no grid work, no UI, and the checkmark, the saving and the re-equip on
    /// load (Player.EquipInventoryItems, which only asks m_equipped) all come for free.
    ///
    /// <b>Trophies stay ItemType.Trophy.</b> Retyping them to Trinket would have got the slot for
    /// nothing, and it would make every trophy fight the trinket for one place and vanish from
    /// anything keyed on Trophy: item stands, the compendium, Yoke's sorting.
    ///
    /// <b>Vanilla's EquipItem and UnequipItem still run</b>, because their guards are the rules a
    /// wearable should keep (not mid-swing, not swimming, must be in the pack) and their tails are
    /// the sound. EquipItem falls through every type branch for a trophy, equips nothing and
    /// returns true, so the postfix below is where it lands in the slot. UnequipItem only needs
    /// IsItemEquiped to say yes to get past its first line, and it clears m_equipped by itself.
    ///
    /// <b>One wearer, the local player.</b> Every other Player object on this machine stands for
    /// somebody whose inventory lives on their own machine. What they wear reaches here through
    /// their ZDO (see Publish), which is the only thing the rest of the mod reads.
    /// </summary>
    internal static class Slot
    {
        /// <summary>The worn item's prefab hash, on the wearer's own ZDO. Zero is nothing worn.</summary>
        internal static readonly int WornKey = "hausi_worn".GetStableHashCode();

        /// <summary>Its quality, which is how an upgraded sash differs from a plain one.</summary>
        internal static readonly int QualityKey = "hausi_worn_quality".GetStableHashCode();

        private const float StrayCheckSeconds = 1f;

        private static Player _wearer;
        private static ItemDrop.ItemData _worn;
        private static float _nextStrayCheck;

        internal static ItemDrop.ItemData Worn(Player player)
        {
            return player != null && player == _wearer ? _worn : null;
        }

        internal static bool Wearable(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;

            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy || Sashes.Is(item);
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.IsEquipable))]
        [HarmonyPostfix]
        private static void Equipable(ItemDrop.ItemData __instance, ref bool __result)
        {
            if (!__result && HausiConfig.Enabled.Value && Wearable(__instance)) __result = true;
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsItemEquiped))]
        [HarmonyPostfix]
        private static void Equiped(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result && item != null && item == _worn && __instance == _wearer) __result = true;
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        [HarmonyPostfix]
        private static void Equipped(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result || !Wearable(item)) return;

            Player player = __instance as Player;
            if (player == null) return;

            // Reached from a save with the mod switched off: EquipInventoryItems clears the mark
            // on a false, and a mark with nothing behind it would keep the trophy out of the
            // grave on death, because MoveInventoryToGrave leaves m_equipped items in the pack.
            if (!HausiConfig.Enabled.Value)
            {
                item.m_equipped = false;
                __result = false;
                return;
            }

            if (_wearer == player && _worn != null) player.UnequipItem(_worn, false);

            _wearer = player;
            _worn = item;
            item.m_equipped = true;
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        [HarmonyPostfix]
        private static void Unequipped(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (item != null && item == _worn && __instance == _wearer) _worn = null;
        }

        // Both of these list the slots by hand, so a slot they do not know about stays on. It would
        // stay on through death, too: MoveInventoryToGrave leaves anything m_equipped in the pack.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipAllItems))]
        [HarmonyPostfix]
        private static void AllOff(Humanoid __instance)
        {
            if (__instance == _wearer && _worn != null) __instance.UnequipItem(_worn, false);
        }

        // The list vanilla takes off for a player who keeps their armour through death. The trinket
        // is on it, so the slot beside the trinket is too.
        [HarmonyPatch(typeof(Player), nameof(Player.UnequipDeathDropItems))]
        [HarmonyPostfix]
        private static void DeathDropOff(Player __instance)
        {
            if (__instance == _wearer && _worn != null) __instance.UnequipItem(_worn, false);
        }

        /// <summary>
        /// Once a frame, from the plugin. The checks that have no event worth patching, and the one
        /// write other machines read.
        /// </summary>
        internal static void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;

            Settle(player);
            ClearStrays(player);
            Publish(player);
        }

        /// <summary>
        /// A worn stack can leave without anybody unequipping it. An offering or a craft takes
        /// trophies by name through Inventory.RemoveItem, so the last of a worn stack simply stops
        /// being in the pack, and a slot still holding it would keep boosting off a ghost.
        /// </summary>
        private static void Settle(Player player)
        {
            if (_wearer != player || _worn == null) return;

            if (!HausiConfig.Enabled.Value)
            {
                player.UnequipItem(_worn, false);
                return;
            }

            if (player.GetInventory().ContainsItem(_worn)) return;

            _worn.m_equipped = false;
            _worn = null;
        }

        /// <summary>
        /// Trophies stack and no vanilla wearable does, and that shows here. ItemData.Clone copies
        /// m_equipped, so splitting a worn stack in the inventory window leaves a second checkmark
        /// on the half that came off. It would also come back to life on the next load, when
        /// EquipInventoryItems equips each marked item in turn.
        /// </summary>
        private static void ClearStrays(Player player)
        {
            if (UnityEngine.Time.unscaledTime < _nextStrayCheck) return;
            _nextStrayCheck = UnityEngine.Time.unscaledTime + StrayCheckSeconds;

            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                if (item.m_equipped && item != Worn(player) && Wearable(item)) item.m_equipped = false;
        }

        /// <summary>
        /// Writes what is worn to this player's own ZDO, if it changed. Everything another machine
        /// knows about this slot comes from here: the model it draws, and the bonus the owner of a
        /// dying creature grants, since that owner may well not be this machine.
        /// </summary>
        private static void Publish(Player player)
        {
            ZNetView nview = player.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            ItemDrop.ItemData worn = Worn(player);
            int hash = worn != null && worn.m_dropPrefab != null ? worn.m_dropPrefab.name.GetStableHashCode() : 0;
            int quality = hash == 0 ? 0 : worn.m_quality;

            ZDO zdo = nview.GetZDO();
            if (zdo.GetInt(WornKey) != hash) zdo.Set(WornKey, hash);
            if (zdo.GetInt(QualityKey) != quality) zdo.Set(QualityKey, quality);
        }
    }
}
