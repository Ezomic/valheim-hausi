using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using Ezomic.Shared;
using HarmonyLib;

namespace Hausi
{
    /// <summary>
    /// Hausi. Wear a trophy like gear and the creatures of its biome give more, at the price of
    /// never dropping that trophy while you do.
    ///
    /// Trophies are the one thing every player carries a chest of and nothing asks for. This gives
    /// them a job without a new resource or a new menu, and the block is what keeps it honest: the
    /// bonus is paid for in the thing that makes up the sash, so a player has to choose between
    /// farming a biome and finishing its set.
    ///
    /// Not client-side, and Requirement.Everyone below is load-bearing. A creature's owner decides
    /// its drops by reading the killer's ZDO, and sashes are prefabs.
    ///
    /// There is deliberately no BepInProcess attribute. A dedicated server runs
    /// valheim_server.exe, and Core's gate only refuses on the server side of RPC_PeerInfo -
    /// so a mod that must be enforced has to be allowed to load there.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. A hard dependency that is absent does not degrade - the plugin never
    // loads at all - and every mod here has to be installable on its own, because a stranger
    // should not need two installs to get one mod. Soft still buys the load-order guarantee
    // when Core is present, which is all that registering with the gate needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class HausiPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.hausi";
        public const string PluginName = "Hausi";
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        /// <summary>
        /// Whether Core answered at load. Worth keeping even when nothing reads it yet: the
        /// difference between gated and ungated is invisible to a player otherwise, and this
        /// is what a warning on spawn would be driven by.
        /// </summary>
        internal static bool CorePresent;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Config first. Registering absorbs every entry the mod has bound, so anything
            // bound after this line is carried only because Core re-absorbs at manifest
            // time - and depending on the order of two lines in an Awake is not a thing
            // worth relying on.
            HausiConfig.Bind(Config);

            TryRegisterWithCore();

            Prefabs.Log = Log;
            Biomes.Wire();
            Sashes.Keep();

            // A proof of the shared table, not a feature: the relic is registered by Sashes a few
            // frames in, and InfusionTable retries a name that does not resolve yet.
            InfusionTable.AddRecipe("TrophyBoar", "DeerHide", "HausiRelic_Meadows", InfusionLiquid.Any);

            // PatchAll over named types, never the whole assembly. A bare PatchAll() walks
            // every type in the DLL, so a half-written patch class in another file goes live
            // the moment it compiles.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Slot));
            _harmony.PatchAll(typeof(DropBoost));
            _harmony.PatchAll(typeof(Worn));
            _harmony.PatchAll(typeof(Altar));
#if DEBUG
            _harmony.PatchAll(typeof(DevConsole));
#endif

            // The startup line every mod in the suite writes. It is how a log answers "which
            // build of what is actually loaded" without anyone guessing.
            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        private void Update()
        {
            Prefabs.Tick();
            InfusionTable.Tick();
            Biomes.Tick();
            Sashes.Tick();
            Slot.Tick();
        }

        private static readonly HashSet<string> Said = new HashSet<string>();

        /// <summary>A warning a frame loop would otherwise repeat sixty times a second.</summary>
        internal static void LogOnce(string line)
        {
            if (Said.Add(line)) Log.LogWarning(line);
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Name here exactly what standing alone costs, because it is usually not the mod.
        /// For most of these it is the *enforcement*: without Core nothing refuses a client
        /// that lacks the plugin, so the rule becomes an agreement between players rather
        /// than a property of the server. That is a real loss and a legitimate choice, and
        /// it is the server owner's to make - which is why this logs rather than refusing
        /// to run.
        /// </summary>
        private void TryRegisterWithCore()
        {
            CorePresent = Chainloader.PluginInfos.ContainsKey(CoreGuid);

            if (!CorePresent)
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a
        /// method needs when it first compiles that method, so a Suite call sitting directly
        /// in Awake would drag Ezomic.Core in before the check above could prevent it - and
        /// the missing-assembly exception would land during plugin load, which is the exact
        /// failure this arrangement exists to avoid. Isolating it means the type is only
        /// ever resolved on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // Requirement.Everyone or Requirement.HostOnly, and the choice is not a matter of
            // taste. Everyone for anything that registers a prefab or changes item data,
            // whether it looks networked or not: a client that cannot resolve a prefab hash
            // does not fail loudly, ZNetScene discards the ZDO as junk and the thing a player
            // built is simply gone. HostOnly only when a client without the mod is genuinely
            // unaffected.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config, Requirement.Everyone);

            // Registering already absorbs the whole config file, so this is a formality now.
            // It is still worth writing: naming an entry here is saying out loud that the
            // host decides it.
            Suite.Sync(HausiConfig.Enabled);

            // Where a trophy hangs desyncs nothing and is nobody's rule, and neither is a log.
            Suite.Local(HausiConfig.WornJoint, HausiConfig.WornSize, HausiConfig.WornRight,
                HausiConfig.WornUp, HausiConfig.WornForward, HausiConfig.WornTurn, HausiConfig.Verbose);
        }

        private void OnDestroy()
        {
            // UnpatchSelf, never UnpatchAll(). The argumentless one unpatches every mod in
            // the process, not just this one.
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}
