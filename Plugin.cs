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
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGUID = "com.ThymeCodes.poplostcrown.TLCArchipelago_Client";
        public const string PluginName = "The Lost Crown Archipelago Client";
        public const string PluginVersion = "0.1.1";

        public const string ModDisplayInfo = $"{PluginName} v{PluginVersion}";
        public const string APDisplayInfo = $"Archipelago v{ArchipelagoClient.APVersion}";
        internal static ManualLogSource BepinLogger;
        public static ArchipelagoClient ArchipelagoClient;

        public override void Load()
        {
            BepinLogger = base.Log;
            ArchipelagoClient = new ArchipelagoClient();
            ArchipelagoClient.ServerData.Uri = "localhost:38281"; // or wherever your test server is
            ArchipelagoClient.ServerData.SlotName = "Player2";
            ArchipelagoClient.ServerData.Password = "";
            ArchipelagoConsole.Awake();

            var harmony = new Harmony("com.ThymeCodes.poplostcrown.TLCArchipelago_Client");
            harmony.PatchAll();
            BepinLogger.LogInfo($"{ModDisplayInfo} loaded!");
            ArchipelagoClient.Connect();
            // For testing connection
            // Task.Delay(10000).ContinueWith(_ => Plugin.ArchipelagoClient.Disconnect());
            //Task.Delay(20000).ContinueWith(_ => ArchipelagoClient.CancelReconnect());
        }
    }
}