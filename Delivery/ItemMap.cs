using System.Collections.Generic;
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// What to give the player for one AP item. Set exactly one of Item or Ability:
    /// Item goes through PlayerInventory.AddItem, Ability through PlayerAbilities.UnlockAbility.
    /// </summary>
    public struct GrantInfo
    {
        public EItemType? Item;
        public EPlayerUnlockableAbility? Ability;
        public int Amount;   // only used for Item; ignored for abilities
    }

    /// <summary>
    /// Translates AP item IDs (from items.py in the APWorld) into game grants.
    /// Used by ItemDelivery.Grant. An ID missing from this table is logged and dropped.
    /// </summary>
    public static class ItemMap
    {
        // AP item ID -> what to give. IDs must match items.py.
        private static readonly Dictionary<long, GrantInfo> map = new Dictionary<long, GrantInfo>
        {
            { 1, new GrantInfo { Item = EItemType.HealingPotion, Amount = 1 } },
            { 2, new GrantInfo { Item = EItemType.KeyItem_ThomyrisScarf, Amount = 1 } },                 // Lore Scarf
            { 3, new GrantInfo { Item = EItemType.QuestItem_CannonBall, Amount = 1 } }, // Cannonball
            { 4, new GrantInfo { Ability = EPlayerUnlockableAbility.AirDash, Amount = 1 } }, // Dash Ability
            { 5, new GrantInfo { Item = EItemType.Crystal, Amount = 1 } },   // filler; not in the current item pool
        };

        /// <summary>Looks up the grant for an AP item ID. Returns false if the ID isn't mapped.</summary>
        public static bool TryGet(long id, out GrantInfo info) => map.TryGetValue(id, out info);
    }
}