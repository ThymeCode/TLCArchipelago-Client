using System.Collections.Generic;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Translates in-game location keys into AP location IDs (from locations.py in the APWorld).
    /// Used by the AddItem/UnlockAbility intercept patches: a key found here is reported as a
    /// check and its vanilla reward is suppressed; a key not found here plays out as vanilla.
    /// </summary>
    public static class LocationMap
    {
        // Location key (from LocationTracker.BuildKey) -> AP location ID.
        // IDs must match locations.py in the APWorld exactly.
        private static readonly Dictionary<string, long> map = new Dictionary<string, long>
        {
            // { "TRG_Interactor_PotionLoot@(-83.59, -26.44, 0.00)", 1 },   // old Chakram swap location, kept for reference
            {"TRG_Interactor_Conversation_Varham_PostFight@(139.62, 16.21, 0.00)", 1},   // Tutorial Vahram Potion
            {"TRG_KIN_THO_CIN_Palace_Ceremony@(25.50, 0.00, 0.00)", 2},                   // Thomyris Scarf Gift
            {"TRG_IASpawn@(106.34, 4.49, 0.00)", 3},                                      // Piercing Attack Cutscene
            {"TRG_HYR_AUT_CIN_Reward_Bow@(17.50, 5.00, 0.00)", 4},                        // Hyrkanian Bow Reward
        };

        /// <summary>
        /// Looks up the AP location ID for a key built by LocationTracker.BuildKey.
        /// Returns false for unmapped locations. Keys must match exactly, including the two decimal places.
        /// </summary>
        public static bool TryGetId(string key, out long id) => map.TryGetValue(key, out id);
    }
}