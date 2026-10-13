using System.Collections.Generic;
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;

namespace TLCArchipelago_Client
{
    public struct GrantInfo
    {
        public EItemType? Item;
        public EPlayerUnlockableAbility? Ability;
        public int Amount;
    }

    public static class ItemMap
    {
        // AP item ID -> what to give. IDs must match items.py.
        private static readonly Dictionary<long, GrantInfo> map = new Dictionary<long, GrantInfo>
        {
            { 1, new GrantInfo { Item = EItemType.HealingPotion, Amount = 1 } },
            { 2, new GrantInfo { Item = EItemType.KeyItem_ThomyrisScarf, Amount = 1 } },                 // Lore Scarf
            { 3, new GrantInfo { Ability = EPlayerUnlockableAbility.FocusPiercingAttack, Amount = 1 } }, // Piercing Attack
            { 4, new GrantInfo { Ability = EPlayerUnlockableAbility.AirDash, Amount = 1 } }, // Dash Ability
            { 5, new GrantInfo { Item = EItemType.Crystal, Amount = 1 } },
        };

        public static bool TryGet(long id, out GrantInfo info) => map.TryGetValue(id, out info);
    }
}