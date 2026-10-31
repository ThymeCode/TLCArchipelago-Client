using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Alkawa.Gameplay;
using HarmonyLib;
using UnityEngine;

namespace TLCArchipelago_Client
{
    public class ShopText
    {
        static readonly Dictionary<EItemType, int> nameIds = new Dictionary<EItemType, int>
        {
            { EItemType.SeedOfKnowledge, 1535 },                          // Amulet Holder
            { EItemType.CollectorToken, 1671 },                           // Xerxes
            { EItemType.StoneOfKnowledge_Poison, 2468 },                  // Horned Viper
            { EItemType.StoneOfKnowledge_Treasure, 2474 },                // King Jamshid
            { EItemType.StoneOfKnowledge_Safety, 1609 },                  // Hardiness
            { EItemType.GearUpgrade_PotionCount_PackShop1, 1533 },        // Additional Healing Potion
            { EItemType.GearUpgrade_PotionHealingEffect_PackShop2, 4899 } // Potion Efficiency +2
        };

        
        static volatile Dictionary<int, string> overrides = new Dictionary<int, string>();
        /// <summary>
        /// Call AFTER the ritual has started (the page doesn't exist before that).
        /// Pass "" for detail to hide the tutorial hint line.
        /// </summary>
        public static void Arm(InteractiveElementLogic_ShopKeeper shop)
        {
            var shopTable = new Dictionary<int, string>();
            var shopKey = ShopTracker.CurrentShopKey;
            if(shopKey != null)
            {
                AddEntries(shopTable, shop.m_availableItems, shopKey);
                AddEntries(shopTable, shop.m_availableUpgrades, shopKey);
            }
            overrides = shopTable;
            Plugin.BepinLogger.LogInfo($"Shop text armed {shopTable.Count} override(s)");


        }

        static void AddEntries(Dictionary<int, string> table, Il2CppSystem.Collections.Generic.List<ShopItemTrade> list, string shopKey)
        {
            if (list == null) return;
            for (int i=0; i< list.Count; i++)
            {
                var item = list[i]?.m_item?.m_itemId;
                if(item == null) continue;
                if(!nameIds.TryGetValue(item.Value, out int nameId)) continue;
                if(!LocationMap.TryGetId(shopKey + "#" + item.Value, out long locationId)) continue;
                if(!PlacementCache.TryGet(locationId, out var placement) || placement.Own) continue;

                table[nameId] = placement.ItemName;
                table[nameId + 1] = $"Sent to {placement.Player} ({placement.Game})";

            }
        }
        public static void Clear()
        {
            overrides = new Dictionary<int, string>();
        }

        public static bool TryGet(int locId, out string text)
        {  
            var shopTable = overrides;
            if (shopTable.Count == 0){ 
                text = null; return false;
            };
            return shopTable.TryGetValue(locId, out text);
        }
    }
}
