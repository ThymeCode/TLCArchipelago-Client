using System.Collections.Generic;
using Alkawa.Gameplay;
using HarmonyLib;
using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Replaces the text on the "check sent" ritual popup. Arm() reads the localization IDs from the
    /// live description page, so it works for any item; the getText patches below swap in our strings
    /// for those IDs until the window expires.
    /// </summary>
    public static class PopupText
    {
        static readonly Dictionary<int, string> overrides = new Dictionary<int, string>();
        static float expires;

        /// <summary>
        /// Call AFTER the ritual has started (the page doesn't exist before that).
        /// Pass "" for detail to hide the tutorial hint line.
        /// </summary>
        public static void Arm(UI_DescriptionPageData page, string title, string header, string description, string detail)
        {
            overrides.Clear();
            if (page == null) return;
            Add(page.m_titleText, title);
            Add(page.m_headerText, header);
            Add(page.m_descriptionText, description);
            Add(page.m_detailText, detail);
            expires = Time.unscaledTime + 15f;
        }

        // ID 0 means "no localization entry" (the game falls back to debug text), and many unrelated
        // strings share it, so we never override it.
        static void Add(UISmartLocId id, string text)
        {
            if (id != null && id.m_locId != 0) overrides[id.m_locId] = text;
        }

        public static bool TryGet(int locId, out string text)
        {
            text = null;
            if (overrides.Count == 0 || Time.unscaledTime > expires) return false;
            return overrides.TryGetValue(locId, out text);
        }
    }

    [HarmonyPatch(typeof(UISmartLocId), nameof(UISmartLocId.getText), new System.Type[0])]
    public class GetText_Override_Patch
    {
        static void Postfix(UISmartLocId __instance, ref string __result)
        {
            if (PopupText.TryGet(__instance.m_locId, out var t)) __result = t;
        }
    }
    
    [HarmonyPatch(typeof(UISmartLocId), nameof(UISmartLocId.getText),
        new[] { typeof(bool) }, new[] { ArgumentType.Out })]
    public class GetTextLocalized_Override_Patch
    {
        static void Postfix(UISmartLocId __instance, ref string __result)
        {
            if (PopupText.TryGet(__instance.m_locId, out var t)) __result = t;
        }
    }
}