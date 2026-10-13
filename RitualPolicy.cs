using System.Collections.Generic;
using Alkawa.Gameplay;

namespace TLCArchipelago_Client
{
    public static class RitualPolicy
    {
        static readonly HashSet<EItemType> noPopup = new HashSet<EItemType>();    // NoRitual + NeverShowingNotifAdd
        static readonly HashSet<EItemType> notifOnly = new HashSet<EItemType>();  // ShowNotifAdd minus DefaultRitualAndNotif
        static bool ready;

        public static void Init(PlayerRitualInfoConfig cfg)
        {
            if (ready || cfg == null) return;
            Fill(noPopup, cfg.m_itemTypesNoRitual);
            Fill(noPopup, cfg.m_itemTypesNeverShowingNotifAdd);
            Fill(notifOnly, cfg.m_itemTypesShowNotifAdd);
            var def = new HashSet<EItemType>();
            Fill(def, cfg.m_itemTypesDefaultRitualAndNotif);
            notifOnly.ExceptWith(def);
            ready = true;
        }

        static void Fill(HashSet<EItemType> set, Il2CppSystem.Collections.Generic.List<EItemType> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++) set.Add(list[i]);
        }

        public static bool NeedsFullRitual(EItemType t) => ready && !noPopup.Contains(t) && !notifOnly.Contains(t);
    }
}