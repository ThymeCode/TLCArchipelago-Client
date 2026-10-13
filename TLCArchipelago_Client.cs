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
            ArchipelagoClient.ServerData.SlotName = "Player1";
            ArchipelagoClient.ServerData.Password = "";
            ArchipelagoConsole.Awake();

            var harmony = new Harmony("com.ThymeCodes.poplostcrown.TLCArchipelago_Client");
            harmony.PatchAll();
            BepinLogger.LogInfo($"{ModDisplayInfo} loaded!");
            ArchipelagoClient.Connect();
            // For testing connection
            // Task.Delay(10000).ContinueWith(_ => Plugin.ArchipelagoClient.Disconnect());
            Task.Delay(20000).ContinueWith(_ => ArchipelagoClient.CancelReconnect());
        }
    }




    public static class Substitutions
    {
        public static readonly Dictionary<string, EItemType> ItemOverrides = new Dictionary<string, EItemType>
        {
        };

        // Case 1 (REPLACE): this location's natural UnlockAbility call gets
        // rewritten to grant a different ability instead.
        public static readonly Dictionary<string, EPlayerUnlockableAbility> AbilityOverrides = new Dictionary<string, EPlayerUnlockableAbility>
        {
            { "TRG_IASpawn@(106.34, 4.49, 0.00)", EPlayerUnlockableAbility.DoubleJump },
            
        };

        // Case 2 (SWAP): this location's item grant is replaced with an ability
        // grant instead. AddItem_Substitution_Patch skips the original item grant
        // and calls UnlockAbility itself.
        public static readonly Dictionary<string, EPlayerUnlockableAbility> EarlyAbilityGrants = new Dictionary<string, EPlayerUnlockableAbility>
        {
            { "TRG_Interactor_Conversation_Varham_PostFight@(139.62, 16.21, 0.00)", EPlayerUnlockableAbility.Bow },
            { "TRG_Interactor_PotionLoot@(-83.59, -26.44, 0.00)", EPlayerUnlockableAbility.Chakram },
        };
        
    }

    public static class LocationTracker
    {
        public static string PendingLocationKey;

        public static string BuildKey(GameObject owner)
        {
            if (owner == null) return null;
            var pos = owner.transform.position;
            return $"{owner.name}@({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";
        }
    }

    [HarmonyPatch(typeof(InteractiveElementLogic_CollectibleItem), "TriggerLogic_Internal")]
    public class TriggerLogic_Patch
    {
        static void Postfix(InteractiveElementLogic_CollectibleItem __instance, ETriggerLogicType _triggerType)
        {
            var key = LocationTracker.BuildKey(__instance.m_Owner);
            LocationTracker.PendingLocationKey = key;
            Plugin.BepinLogger.LogInfo($"COLLECTIBLE TRIGGERED: location='{key}'");
        }
    }

    [HarmonyPatch(typeof(InteractiveElementLogic_CutsceneBase), "TriggerLogic_Internal")]
    public class CutsceneTriggerLogic_Patch
    {
        static void Postfix(InteractiveElementLogic_CutsceneBase __instance, ETriggerLogicType _triggerType)
        {
            var key = LocationTracker.BuildKey(__instance.m_Owner);
            LocationTracker.PendingLocationKey = key;
            Plugin.BepinLogger.LogInfo($"CUTSCENE TRIGGERED: location='{key}'");
        }
    }

    [HarmonyPatch(typeof(PlayerInventorySubComponent), "AddItem",
        new System.Type[] { typeof(EItemType), typeof(int), typeof(EItemAcquisitionMode) })]
    public class AddItem_Substitution_Patch
    {
        static bool Prefix(PlayerInventorySubComponent __instance, ref EItemType _itemType, ref int _amount, EItemAcquisitionMode _acquisitionMode, ref int __result)
        {
            var key = LocationTracker.PendingLocationKey;

            // Genuine swap case: this location's item grant is replaced with an
            // ability grant instead — the player gets ONE reward, not both.
            if (key != null && Substitutions.EarlyAbilityGrants.TryGetValue(key, out var earlyAbility))
            {
                Plugin.BepinLogger.LogInfo($"SWAPPING ITEM FOR ABILITY: {earlyAbility} (location={key}) — original item grant skipped");
                __instance.m_playerComponent.PlayerAbilities.UnlockAbility(earlyAbility, true, true, 0f);
                LocationTracker.PendingLocationKey = null;
                __result = 0; // no item was actually added
                return false; // skip the original AddItem entirely
            }

            if (key != null && Substitutions.ItemOverrides.TryGetValue(key, out var replacement))
            {
                Plugin.BepinLogger.LogInfo($"SUBSTITUTING ITEM: {_itemType} -> {replacement} (location={key})");
                _itemType = replacement;
            }

            LocationTracker.PendingLocationKey = null;
            return true; // let the (possibly substituted) item grant proceed normally
        }
    }

    // Case 1 (REPLACE): rewrites which ability actually gets unlocked at a location
    // that naturally calls UnlockAbility already.
    [HarmonyPatch(typeof(PlayerAbilitiesSubComponent), "UnlockAbility")]
    public class UnlockAbility_Substitution_Patch
    {
        static void Prefix(ref EPlayerUnlockableAbility _ability)
        {
            var key = LocationTracker.PendingLocationKey;
            if (key != null && Substitutions.AbilityOverrides.TryGetValue(key, out var replacement))
            {
                Plugin.BepinLogger.LogInfo($"SUBSTITUTING ABILITY: {_ability} -> {replacement} (location={key})");
                _ability = replacement;
            }
            LocationTracker.PendingLocationKey = null;
        }
    }
//    public static class ShopSubstitutions
//{
//    // Keyed by shop location, then original item -> replacement item.
//    public static readonly Dictionary<string, Dictionary<EItemType, EItemType>> Overrides =
//        new Dictionary<string, Dictionary<EItemType, EItemType>>
//    {
//        {
//            "NPC_ShopKeeper_Cartographer Old_Lady Variant@(161.72, 37.50, 2.00)",
//            new Dictionary<EItemType, EItemType>
//            {
//                { EItemType.SeedOfKnowledge, EItemType.StoneOfKnowledge_Prayer },
//            }
//        },
//    };
//}
/*
//[HarmonyPatch(typeof(InteractiveElementLogic_ShopKeeper), "OpenShopMenu")]
public class OpenShopMenu_Substitution_Patch
{
    static void Prefix(InteractiveElementLogic_ShopKeeper __instance)
    {
        var key = LocationTracker.BuildKey(__instance.m_Owner);
        if (key == null || !ShopSubstitutions.Overrides.TryGetValue(key, out var itemMap))
            return;

        SwapList(__instance.m_availableItems, itemMap);
        SwapList(__instance.m_availableUpgrades, itemMap);

        static void SwapList(Il2CppSystem.Collections.Generic.List<ShopItemTrade> list, Dictionary<EItemType, EItemType> itemMap)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var trade = list[i];
                var current = trade?.m_item?.m_itemId;
                if (current.HasValue && itemMap.TryGetValue(current.Value, out var replacement))
                {
                    Plugin.BepinLogger.LogInfo($"SUBSTITUTING SHOP ITEM: {current.Value} -> {replacement}");
                    trade.m_item.m_itemId = replacement;
                }
            }
        }
    }
}*/
}