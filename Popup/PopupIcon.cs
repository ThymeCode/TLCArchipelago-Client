using Alkawa.Gameplay;
using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Temporarily replaces the sprite shown on the ritual popup, then puts the original back.
    /// The sprite lives on the live UI_DescriptionPageData (not PlayerRitualInfo.m_icon, which the popup ignores).
    /// </summary>
    public static class PopupIcon
    {
        static UI_DescriptionPageData pagePending;   // page currently holding our sprite
        static Sprite pageOriginal;                  // sprite to restore
        static float restoreAt;                      // unscaled time after which Update() restores it

        /// <summary>Swaps the page's sprite for <paramref name="replacement"/>. Call right after the ritual starts.</summary>
        public static void SwapPage(UI_DescriptionPageData page, Sprite replacement)
        {
            if (page == null || replacement == null) return;
            pageOriginal = page.m_sprite;
            pagePending = page;
            page.m_sprite = replacement;
            restoreAt = Time.unscaledTime + 3f;   // the UI reads the sprite once when it opens, so a short window is enough
        }

        /// <summary>
        /// Restores the original sprite once the window has passed.
        /// Must run every frame (called first in ItemDelivery.Tick), otherwise the page keeps our sprite.
        /// </summary>
        public static void Update()
        {
            if (Time.unscaledTime < restoreAt) return;
            if (pagePending != null) { pagePending.m_sprite = pageOriginal; pagePending = null; pageOriginal = null; }
        }
    }
}