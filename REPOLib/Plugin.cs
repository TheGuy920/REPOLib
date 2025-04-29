﻿using BepInEx;
using HarmonyLib;
using REPOLib.Patches;

namespace REPOLib;

/// <summary>
/// The Plugin class of REPOLib.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    /// <summary>
    /// The REPOLib plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;

        REPOLib.Logger.Initialize(BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID));
        REPOLib.Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} has awoken!");

        _harmony.PatchAll(typeof(RunManagerPatch));
        _harmony.PatchAll(typeof(EnemyDirectorPatch));
        _harmony.PatchAll(typeof(StatsManagerPatch));
        _harmony.PatchAll(typeof(SemiFuncPatch));
        _harmony.PatchAll(typeof(AudioManagerPatch));
        _harmony.PatchAll(typeof(SteamManagerPatch));
        _harmony.PatchAll(typeof(EnemyGnomeDirectorPatch));
        _harmony.PatchAll(typeof(EnemyBangDirectorPatch));
        _harmony.PatchAll(typeof(PlayerControllerPatch));

        ConfigManager.Initialize(Config);
        BundleLoader.LoadAllBundles(Paths.PluginPath, ".repobundle");
    }
}
