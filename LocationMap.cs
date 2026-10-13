using System.Collections.Generic;

namespace TLCArchipelago_Client
{
    public static class LocationMap
    {
        // Location key (from LocationTracker.BuildKey) -> AP location ID.
        // IDs must match locations.py in the APWorld exactly.
        private static readonly Dictionary<string, long> map = new Dictionary<string, long>
        {
            // { "TRG_Interactor_PotionLoot@(-83.59, -26.44, 0.00)", 1 },
            {"TRG_Interactor_Conversation_Varham_PostFight@(139.62, 16.21, 0.00)", 1},
            {"TRG_KIN_THO_CIN_Palace_Ceremony@(25.50, 0.00, 0.00)", 2},
            {"TRG_IASpawn@(106.34, 4.49, 0.00)", 3},
        };

        public static bool TryGetId(string key, out long id) => map.TryGetValue(key, out id);
    }
}