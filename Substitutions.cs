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

// Likely to be deleted, kept for now as a reference.
namespace TLCArchipelago_Client
{    public static class Substitutions
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
}