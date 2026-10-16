using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;
using System.Collections.Generic;
using UnityEngine;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Utils;
using System.Threading.Tasks;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// BepInEx entry point. Sets up the AP client, registers every Harmony patch in the assembly,
    /// and connects to the server. All other behaviour lives in the patch classes and ItemDelivery.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGUID = "com.ThymeCodes.poplostcrown.TLCArchipelago_Client";
        public const string PluginName = "The Lost Crown Archipelago Client";
        public const string PluginVersion = "0.5.3";

        public const string ModDisplayInfo = $"{PluginName} v{PluginVersion}";
        public const string APDisplayInfo = $"Archipelago v{ArchipelagoClient.APVersion}";
        internal static ManualLogSource BepinLogger;   // shared logger used by every class in the plugin
        public static ArchipelagoClient ArchipelagoClient;

        public override void Load()
        {
            BepinLogger = base.Log;   // set first: the patches and client log through it
            ArchipelagoClient = new ArchipelagoClient();
            // Hardcoded for testing. Replace with a config file or in-game UI later.
            ArchipelagoClient.ServerData.Uri = "localhost:38281";
            ArchipelagoClient.ServerData.SlotName = "Player1";   // must match the name in the player YAML
            ArchipelagoClient.ServerData.Password = "";
            ArchipelagoConsole.Awake();

            var harmony = new Harmony("com.ThymeCodes.poplostcrown.TLCArchipelago_Client");
            harmony.PatchAll();   // finds every [HarmonyPatch] class in the assembly
            BepinLogger.LogInfo($"{ModDisplayInfo} loaded!");
            // Starts the connection on a background thread. A failed login does not retry (see HandleConnectResult),
            // so start the server before launching the game.
            ArchipelagoClient.Connect();
            
            // For testing connection
            // Task.Delay(10000).ContinueWith(_ => Plugin.ArchipelagoClient.Disconnect());   // intentional disconnect: should NOT reconnect
            // Task.Delay(20000).ContinueWith(_ => ArchipelagoClient.CancelReconnect());      // cancel an in-progress reconnect loop
        }
    }
}