using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Hausi
{
    /// <summary>
    /// The only file that knows Vandi exists, and it knows it by name.
    ///
    /// Malmr compiles against VandiApi through a bridge class. Hausi cannot yet: the two calls it needs
    /// are not in a published Vandi, and a project reference would make this repo's build depend on a
    /// change in another. So the calls are found at run time, through BepInEx's list of plugins that
    /// really loaded, as Vandi finds Malmr in MalmrLink. A Vandi without them, or no Vandi at all, costs
    /// exactly what the rule says it costs: the altar stays a plain summon, and the slot still works.
    /// Bound lazily and inside a try, so a rename costs the offering and never a patch class.
    /// </summary>
    internal static class VandiLink
    {
        private const string VandiGuid = "ezomic.valheim.vandi";

        private static bool _bound;
        private static MethodInfo _askHarder, _isHarder;

        /// <summary>Whether this machine can ask for a harder summon at all.</summary>
        internal static bool Available
        {
            get
            {
                Bind();
                return _askHarder != null && _isHarder != null;
            }
        }

        private static void Bind()
        {
            if (_bound) return;

            // First asked from a world, long after every plugin has loaded, so an absent key is final.
            if (!Chainloader.PluginInfos.TryGetValue(VandiGuid, out BepInEx.PluginInfo info) || info.Instance == null)
            {
                _bound = true;
                HausiPlugin.Log.LogInfo("Vandi is not installed, so the altar offering is unavailable. The slot still boosts drops.");
                return;
            }

            _bound = true;

            Type api = info.Instance.GetType().Assembly.GetType("Vandi.VandiApi");
            if (api == null) return;

            _askHarder = api.GetMethod("AskHarder", new[] { typeof(string), typeof(int) });
            _isHarder = api.GetMethod("IsHarderSummon", new[] { typeof(Character) });

            if (_askHarder == null || _isHarder == null)
                HausiPlugin.Log.LogWarning("This Vandi has no AskHarder and IsHarderSummon, so the altar offering is "
                                           + "unavailable until a Vandi that has them is installed.");
        }

        internal static void AskHarder(string bossKey, int stars)
        {
            if (!Available) return;

            try { _askHarder.Invoke(null, new object[] { bossKey, stars }); }
            catch (Exception error) { HausiPlugin.LogOnce("Could not ask Vandi for a harder summon: " + error); }
        }

        internal static bool IsHarderSummon(Character boss)
        {
            if (!Available) return false;

            try { return (bool)_isHarder.Invoke(null, new object[] { boss }); }
            catch (Exception error)
            {
                HausiPlugin.LogOnce("Could not ask Vandi whether a boss was a harder summon: " + error);
                return false;
            }
        }
    }
}
