using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Links a pickup trigger to the AddItem/UnlockAbility call that follows it.
    /// The trigger patches store a key in PendingLocationKey; the intercept patches read it,
    /// look it up in LocationMap, and clear it.
    /// </summary>
    public static class LocationTracker
    {
        // Key of the most recently triggered pickup, or null. Set by the trigger patches, consumed by the intercepts.
        // Goes stale if a trigger fires but no AddItem/UnlockAbility follows; the intercepts clear it on every use.
        public static string PendingLocationKey;

        /// <summary>
        /// Builds a location key like "TRG_Interactor_PotionLoot@(-83.59, -26.44, 0.00)" from the trigger's owner:
        /// object name plus world position to two decimals. These strings are the keys in LocationMap,
        /// so changing the format would break every entry. Returns null if there is no owner.
        /// </summary>
        public static string BuildKey(GameObject owner)
        {
            if (owner == null) return null;
            var pos = owner.transform.position;
            return $"{owner.name}@({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";
        }
    }
}