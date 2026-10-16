using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;
using Alkawa.Engine;
using System.Collections.Generic;
using UnityEngine;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Utils;
using System.Threading.Tasks;
using Alkawa.Gameplay.Player.Components;  

namespace TLCArchipelago_Client
{ 
    // All game-facing Harmony patches. PatchAll() in Plugin.Load() picks up every class here.
    // Flow: a pickup trigger sets LocationTracker.PendingLocationKey, then the AddItem/UnlockAbility
    // intercepts consume it, report the check, and suppress the vanilla reward.

    #region Location tracking

    // Records which pickup location just fired. Postfix, so the game's own trigger logic has already run.
    // The key is consumed by the intercept patches below; if no AddItem/UnlockAbility follows, it goes stale.
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

    // Same as above, for conversation/cutscene-driven rewards (e.g. the Varham post-fight potion).
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
    // A few items are triggered by a TimelineLauncher (Namely the bow). Otherwise same as above.
    [HarmonyPatch(typeof(InteractiveElementLogic_TimelineLauncher), "TriggerLogic_Internal")]
    public class TimelineTriggerLogic_Patch
    {
        static void Postfix(InteractiveElementLogic_TimelineLauncher __instance, ETriggerLogicType _triggerType)
        {
            var key = LocationTracker.BuildKey(__instance.m_Owner);
            LocationTracker.PendingLocationKey = key;
            Plugin.BepinLogger.LogInfo($"TIMELINE TRIGGERED: location='{key}'");
        }
    }

    #endregion

    #region Check interception

    // Turns a mapped item pickup into an AP location check and suppresses the vanilla reward.
    // The reward itself is delivered later through ItemDelivery, whatever the server decides it is.
    [HarmonyPatch(typeof(PlayerInventorySubComponent), "AddItem", new System.Type[] { typeof(EItemType), typeof(int), typeof(EItemAcquisitionMode) })]
    public class AddItem_Intercept_Patch
    {
        static bool Prefix(PlayerInventorySubComponent __instance, EItemType _itemType, int _amount, EItemAcquisitionMode _acquisitionMode, ref int __result)
        {
            if (ItemDelivery.IsGranting) return true;   // our own delivery: let it through untouched

            var key = LocationTracker.PendingLocationKey;
            if (key == null) return true;               // not tied to a tracked location (crystals, etc.): vanilla
            LocationTracker.PendingLocationKey = null;  // consume first, so a stale key can't swallow a later unrelated pickup

            if (!LocationMap.TryGetId(key, out long locationId)) return true;   // unmapped location: vanilla

            // Cancelling AddItem also cancels the ritual (popup) it would have started, which leaves the
            // player frozen. So for items that normally get a full popup, make sure some ritual still plays.
            // Decide now, from slot data. Own item: wait for the server's echo, so Grant() gives it and the
            // game builds its own popup. Another game's item: generic popup straight away.
            if (RitualPolicy.NeedsFullRitual(_itemType))
            {
                if (PlacementCache.TryGet(locationId, out var placement) && !placement.Own)
                    PendingCheck.ShowGeneric(__instance.m_playerComponent, _itemType,
                        placement.ItemName, "Item Sent", $"Sent to {placement.Player} ({placement.Game}).");
                else
                    PendingCheck.Begin(locationId, _itemType);   // own item, or no placement data: wait for the echo (3s fallback)
            }

            // Order matters: Begin (above) must come before ReportCheck, so the echo can't arrive before we're waiting for it.
            ItemDelivery.ReportCheck(locationId);

            __result = _amount;   // report "added" to the caller even though nothing was
            return false;         // skip the original AddItem
        }
    }

    // Same idea for ability pickups. Known gap: this cancels the unlock ritual too, and unlike
    // AddItem above it doesn't start a replacement (the SimorghFeather ritual would be the one to use).
    [HarmonyPatch(typeof(PlayerAbilitiesSubComponent), "UnlockAbility")]
    public class UnlockAbility_Intercept_Patch
    {
        static bool Prefix()
        {
            if (ItemDelivery.IsGranting) return true;

            var key = LocationTracker.PendingLocationKey;
            if (key == null) return true;
            LocationTracker.PendingLocationKey = null;

            if (!LocationMap.TryGetId(key, out long locationId)) return true;

            var player = ItemDelivery.GetPlayer();
            if (player != null)
            {
                // HealingPotion is a dummy item for the PotionReveal frame; its text and icon are replaced anyway.
                if (PlacementCache.TryGet(locationId, out var placement) && !placement.Own)
                    PendingCheck.ShowGeneric(player, EItemType.HealingPotion, placement.ItemName, "Item Sent",
                        $"Sent to {placement.Player} ({placement.Game}).");
                else
                    PendingCheck.Begin(locationId, EItemType.HealingPotion);   // before ReportCheck
            }

            ItemDelivery.ReportCheck(locationId);
            return false;
        }
    }



    #endregion

    #region Delivery timing (GameFlow)

    // These four patches only exist to give ItemDelivery extra chances to run. PlayerTick_Patch below
    // already ticks every frame, so they are candidates for removal. Re-test the Dash delivery and
    // "send an item while paused" if you take them out.
    // Note ActiveGameFlowState_Patch is a postfix on a getter that ItemDelivery.IsSafe() also calls.
    [HarmonyPatch(typeof(GameFlow), "get_ActiveGameFlowState")]
    public class ActiveGameFlowState_Patch
    {
        static void Postfix(EGameFlowStateType __result)
        {
            if (__result == EGameFlowStateType.InGame) ItemDelivery.Tick();
        }
    }

    // PopState has two overloads, so each needs its parameter types spelled out or Harmony reports an ambiguous match.
    [HarmonyPatch(typeof(GameFlow), nameof(GameFlow.PushState), new[] { typeof(EGameFlowStateType) })]
    public class PushState_Patch
    {
        static void Postfix() => ItemDelivery.TryDeliver();
    }

    [HarmonyPatch(typeof(GameFlow), nameof(GameFlow.PopState), new[] { typeof(EGameFlowStateType), typeof(bool) })]
    public class PopStateTyped_Patch
    {
        static void Postfix() => ItemDelivery.TryDeliver();
    }

    [HarmonyPatch(typeof(GameFlow), nameof(GameFlow.PopState), new System.Type[0])]
    public class PopState_Patch
    {
        static void Postfix() => ItemDelivery.TryDeliver();
    }

    #endregion

    #region Player capture and tick

    // Main per-frame hook: captures the player for ItemDelivery and drives its Tick.
    [HarmonyPatch(typeof(PlayerComponent), nameof(PlayerComponent.OnAnimatorMoveProxy))]
    public class PlayerTick_Patch
    {
        static void Postfix(PlayerComponent __instance)
        {
            if (__instance.IsPlayerDoppleganger) return;   // a second PlayerComponent can exist; ignore it
            ItemDelivery.SetPlayer(__instance);
            RitualPolicy.Init(__instance.PlayerRitualInfoConfig);   // builds the popup rules once; no-op after that
            ItemDelivery.Tick();
        }

    }

    // Clears the stored player so ItemDelivery never holds a destroyed object.
    [HarmonyPatch(typeof(PlayerComponent), nameof(PlayerComponent.OnAlkawaDestroy))]
    public class PlayerDestroy_Patch
    {
        static void Postfix(PlayerComponent __instance) => ItemDelivery.ClearPlayer(__instance);
    }

    #endregion

    #region Shop substitution (shelved)

    // Shelved: swapping the items a shopkeeper sells. Substituting the catalog in OpenShopMenu crashed
    // the game (mutating the shop UI data), and purchases still need handling (OnBuyItem).
    // The tracker plugin already logs full shop catalogs, and the diagnostics plugin has an
    // OpenShopMenu patch plus OnBuyItem stack-trace patches for investigating this.
    // Left commented out so it doesn't compile or patch.
    //
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

    #endregion

}