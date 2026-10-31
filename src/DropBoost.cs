using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// What a worn trophy actually does: more of a matching creature's drops, and none of its
    /// trophy, for the player who landed the last blow and nobody else.
    ///
    /// <b>GenerateDropList, as Vandi's metal does it.</b> A creature whose Ragdoll has m_dropItems
    /// has its list made by Ragdoll.Setup during Character.OnDeath, and dropped seconds later when
    /// no blow is in sight. CharacterDrop.OnDeath makes the list for every other creature. Both
    /// call this method on the owner while Character.m_lastHit is still the killing blow, and both
    /// before OnDeath's last line lets go of the creature's ZDO. So nothing needs handing from an
    /// OnDeath prefix to a postfix, which is the trap the suite's notes warn about: a postfix on
    /// OnDeath has no ZDO left, and this is not one.
    ///
    /// <b>The owner of the creature decides, and the owner need not be the killer.</b> A creature
    /// belongs to whichever machine stood nearest first, so two players fighting one boar means
    /// one machine runs this for a blow the other struck. That is why the killer's worn item is
    /// read off the killer's ZDO and never off Slot on this machine, and it is the whole of the
    /// isolation rule: another player's kill reads another player's ZDO. Reading the local slot
    /// here would look right in singleplayer and hand out the wrong player's bonus on a server.
    ///
    /// <b>Only a player has the key.</b> The last blow of a tame, a trap or a fire finds nothing
    /// on the attacker's ZDO, or no attacker at all, so the kill is left exactly as the game made
    /// it. A staff's skeleton is a tame like any other; the credit is the blow, not the summoner.
    /// </summary>
    internal static class DropBoost
    {
        /// <summary>What the last kill on this machine decided, for the console and the scenarios.</summary>
        internal sealed class Decision
        {
            internal string Creature, CreatureBiome, Worn, WornBiome;
            internal long Killer;
            internal float Chance;
            internal readonly Dictionary<string, int> Base = new Dictionary<string, int>();
            internal readonly Dictionary<string, int> Extra = new Dictionary<string, int>();
            internal readonly List<string> Blocked = new List<string>();
        }

        internal static Decision Last;

        /// <summary>Units rolled and extras won since the last reset, for checking a chance.</summary>
        internal static int Units, Extras;

        private static AccessTools.FieldRef<Character, HitData> _lastHit;
        private static bool _bound, _warned;

        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        [HarmonyPostfix]
        private static void Boost(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            try
            {
                if (HausiConfig.Enabled.Value && __result != null && __instance != null) Apply(__instance, __result);
            }
            catch (Exception error)
            {
                // A drop table is the wrong place to break a kill. The list is the game's by now
                // or half boosted, and either is better than a creature that drops nothing.
                if (_warned) return;
                _warned = true;
                HausiPlugin.Log.LogWarning("Could not work out a kill's drops, so kills go unboosted "
                    + "where this fails. Said once per session: " + error);
            }
        }

        private static void Apply(CharacterDrop drops, List<KeyValuePair<GameObject, int>> list)
        {
            Character character = drops.GetComponent<Character>();

            // Anything else asking what a creature would drop, while it is alive, gets the plain
            // answer. Only a real death spends a roll.
            if (character == null || !character.IsDead()) return;

            HitData hit = LastHit(character);
            if (hit == null || hit.m_attacker.IsNone()) return;

            ZDO killer = ZDOMan.instance.GetZDO(hit.m_attacker);
            if (killer == null) return;

            int hash = killer.GetInt(Slot.WornKey);
            if (hash == 0) return;

            GameObject worn = ObjectDB.instance.GetItemPrefab(hash);
            if (worn == null) return;

            var decision = new Decision
            {
                Creature = Utils.GetPrefabName(character.gameObject),
                CreatureBiome = Biomes.OfCreature(character),
                Worn = worn.name,
                WornBiome = Biomes.OfWorn(worn),
                Killer = killer.GetLong(ZDOVars.s_playerID),
            };

            Last = decision;

            // Biome "none" matches nothing, even another "none". A location creature and the
            // trophy of a location creature have no biome in common: they have no biome.
            if (decision.WornBiome == Ezomic.Shared.BiomeIndex.None || decision.WornBiome != decision.CreatureBiome)
            {
                Say(decision);
                return;
            }

            decision.Chance = Sashes.Chance(worn.name, killer.GetInt(Slot.QualityKey));

            for (int i = list.Count - 1; i >= 0; i--)
            {
                ItemDrop item;
                if (list[i].Key == null || !list[i].Key.TryGetComponent(out item)) continue;

                string name = list[i].Key.name;
                int count = list[i].Value;

                if (Biomes.IsTrophy(item))
                {
                    decision.Blocked.Add(name);
                    list.RemoveAt(i);
                    continue;
                }

                int extra = Roll(count, decision.Chance);
                Add(decision.Base, name, count);
                Add(decision.Extra, name, extra);

                list[i] = new KeyValuePair<GameObject, int>(list[i].Key, count + extra);
            }

            Say(decision);
        }

        /// <summary>
        /// One roll per unit, so a Boar dropping 2 meat rolls twice. Rolling once for the stack
        /// would make the drop lumpier without changing what it averages, and the rule a player
        /// was told is per unit.
        /// </summary>
        private static int Roll(int units, float chance)
        {
            int won = 0;

            for (int i = 0; i < units; i++)
                if (UnityEngine.Random.value < chance) won++;

            Units += units;
            Extras += won;

            return won;
        }

        private static void Add(Dictionary<string, int> into, string name, int count)
        {
            int had;
            into.TryGetValue(name, out had);
            into[name] = had + count;
        }

        private static HitData LastHit(Character character)
        {
            if (!_bound)
            {
                _bound = true;

                // Bound here and not in a static field: a renamed field in a static initializer
                // poisons every patch in the class. Here it costs the boost and says so.
                try { _lastHit = AccessTools.FieldRefAccess<Character, HitData>("m_lastHit"); }
                catch (Exception error)
                {
                    HausiPlugin.Log.LogWarning("Character.m_lastHit has moved, so nothing can tell who "
                        + "struck the last blow and no kill is boosted: " + error.Message);
                }
            }

            return _lastHit == null ? null : _lastHit(character);
        }

        private static void Say(Decision decision)
        {
            if (!HausiConfig.Verbose.Value) return;

            HausiPlugin.Log.LogInfo(decision.Creature + " (" + decision.CreatureBiome + ") killed by "
                + decision.Killer + " wearing " + decision.Worn + " (" + decision.WornBiome + "), chance "
                + decision.Chance + ", blocked " + decision.Blocked.Count + ".");
        }
    }
}
