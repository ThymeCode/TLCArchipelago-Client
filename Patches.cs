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

 /*   

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
    }*/

    [HarmonyPatch(typeof(PlayerInventorySubComponent), "AddItem", new System.Type[] { typeof(EItemType), typeof(int), typeof(EItemAcquisitionMode) })]
    public class AddItem_Intercept_Patch
    {
        static bool Prefix(PlayerInventorySubComponent __instance, EItemType _itemType, int _amount, EItemAcquisitionMode _acquisitionMode, ref int __result)
        {
            if (ItemDelivery.IsGranting) return true;

            var key = LocationTracker.PendingLocationKey;
            if (key == null) return true;
            LocationTracker.PendingLocationKey = null;

            if (!LocationMap.TryGetId(key, out long locationId)) return true;

            ItemDelivery.ReportCheck(locationId);
            if (RitualPolicy.NeedsFullRitual(_itemType))
            {

                var ritual = __instance.m_playerComponent.PlayerRitual;
                var info = ritual.GetRitualInfo(EPlayerRitual.PotionReveal);
                //PopupIcon.Swap(info, ApSprite.Get());  
                PopupText.Arm("Archipelago Check", "Check Sent", "Your item was sent into the multiworld.");                
                ritual.InternalTryStartRitual(EPlayerRitual.PotionReveal, _itemType, 0f, false);
                var ri = ritual.StateInfo.m_currentRitualInformation;
                var page = ri?.m_descriptionPageData?.TryCast<UI_DescriptionPageData>();
                PopupIcon.SwapPage(page, ApSprite.Get());
               // __instance.m_playerComponent.PlayerRitual.InternalTryStartRitual(EPlayerRitual.PotionReveal, _itemType, 0f, false);
                //var info = __instance.m_playerComponent.PlayerRitual.GetRitualInfo(EPlayerRitual.PotionReveal);
                //Plugin.BepinLogger.LogInfo($"PotionReveal icon = {info?.m_icon?.name ?? "null"}");

            // PotionReveal is the only ID observed so far
            //    __instance.m_playerComponent.PlayerRitual.InternalTryStartRitual(EPlayerRitual.PotionReveal, _itemType, 0f, false);
            }
            __result = _amount;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerAbilitiesSubComponent), "UnlockAbility")]
    public class UnlockAbility_Intercept_Patch
    {
        static bool Prefix()
        {
            if(ItemDelivery.IsGranting) return true;

            var key = LocationTracker.PendingLocationKey;
            if (key == null) return true;
            LocationTracker.PendingLocationKey = null;

            if(!LocationMap.TryGetId(key, out long locationId)) return true;

            ItemDelivery.ReportCheck(locationId);
            return false;
        }
    }

    [HarmonyPatch(typeof(GameFlow), "get_ActiveGameFlowState")]
    public class ActiveGameFlowState_Patch
    {
        static void Postfix(EGameFlowStateType __result)
        {
            if (__result == EGameFlowStateType.InGame) ItemDelivery.Tick();
        }
    }

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

    [HarmonyPatch(typeof(PlayerComponent), nameof(PlayerComponent.OnAnimatorMoveProxy))]
    public class PlayerTick_Patch
    {
        static void Postfix(PlayerComponent __instance)
        {
            if (__instance.IsPlayerDoppleganger) return;
            ItemDelivery.SetPlayer(__instance);
            RitualPolicy.Init(__instance.PlayerRitualInfoConfig);
            ItemDelivery.Tick();
        }

    }

    [HarmonyPatch(typeof(PlayerComponent), nameof(PlayerComponent.OnAlkawaDestroy))]
    public class PlayerDestroy_Patch
    {
        static void Postfix(PlayerComponent __instance) => ItemDelivery.ClearPlayer(__instance);
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