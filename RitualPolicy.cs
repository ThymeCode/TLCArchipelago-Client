using System.Collections.Generic;
using Alkawa.Gameplay;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Decides which items get the full ritual popup when picked up, using the game's own item lists
    /// (PlayerRitualInfoConfig) instead of a hand-built table. Used by AddItem_Intercept_Patch to
    /// decide whether to start a ritual after suppressing the vanilla add.
    /// </summary>
    public static class RitualPolicy
    {
        static readonly HashSet<EItemType> noPopup = new HashSet<EItemType>();    // NoRitual + NeverShowingNotifAdd
        static readonly HashSet<EItemType> notifOnly = new HashSet<EItemType>();  // ShowNotifAdd minus DefaultRitualAndNotif
        static bool ready;   // false until Init has run; NeedsFullRitual answers false until then

        /// <summary>
        /// Builds the item sets from the game's config. Safe to call every frame: it does nothing
        /// after the first successful run, or while the config is still null.
        /// </summary>
        public static void Init(PlayerRitualInfoConfig cfg)
        {
            if (ready || cfg == null) return;
            Fill(noPopup, cfg.m_itemTypesNoRitual);
            Fill(noPopup, cfg.m_itemTypesNeverShowingNotifAdd);
            Fill(notifOnly, cfg.m_itemTypesShowNotifAdd);
            // Items in DefaultRitualAndNotif get a notification AND a ritual, so they are not "notification only"
            // even though they also appear in ShowNotifAdd.
            var def = new HashSet<EItemType>();
            Fill(def, cfg.m_itemTypesDefaultRitualAndNotif);
            notifOnly.ExceptWith(def);
            ready = true;
        }

        // Copies a game (Il2Cpp) list into a managed set. Null-safe because some config lists can be empty/unset.
        static void Fill(HashSet<EItemType> set, Il2CppSystem.Collections.Generic.List<EItemType> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++) set.Add(list[i]);
        }

        /// <summary>
        /// True if picking up this item normally shows the full popup (so we must start one ourselves
        /// after cancelling AddItem, or the player freezes). False for items with no popup or only a
        /// corner notification (e.g. Crystal, Arrow), and also false before Init has run.
        /// </summary>
        public static bool NeedsFullRitual(EItemType t) => ready && !noPopup.Contains(t) && !notifOnly.Contains(t);
    }
}