using Alkawa.Gameplay;
using HarmonyLib;
using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Replaces the text on the "check sent" ritual popup. Arm() sets the replacement strings,
    /// and the getText patches below swap them in for the potion popup's localization IDs
    /// until the window expires.
    /// </summary>
    public static class PopupText
    {
        // Localization IDs the potion popup resolves (found by logging UISmartLocId.getText during a vanilla pickup).
        // These are specific to the potion ritual; other items' popups use different IDs.
        const int NameId = 4903, HeaderId = 4456, DescId = 4904, HintId = 4905;

        static string name, header, desc;
        static float expires;

        /// <summary>
        /// Sets the replacement text and opens a 15 second window in which it is applied.
        /// Call just before starting the ritual.
        /// </summary>
        public static void Arm(string itemName, string headerText, string description)
        {
            name = itemName; header = headerText; desc = description;
            // Possibly unneccessary. The window stops the override leaking onto later text that reuses these IDs
            // (4456 is the generic "New Item Acquired" header), so it's a safety net rather than a requirement.
            expires = Time.unscaledTime + 15f;
        }

        /// <summary>
        /// Called by the getText patches for every localized string the game resolves.
        /// Returns true and the replacement if this ID is one we override and the window is open.
        /// Runs very often, so it must stay cheap and must not throw.
        /// </summary>
        public static bool TryGet(int locId, out string text)
        {
            text = null;
            if (Time.unscaledTime > expires) return false;
            switch (locId)
            {
                case NameId:   text = name;   break;
                case HeaderId: text = header; break;
                case DescId:   text = desc;   break;
                case HintId:   text = "";     break;   // hide "drink a Potion" hint
            }
            return text != null;
        }
    }

    // Postfix on the parameterless getText overload: swaps the result when PopupText says so.
    // getText has two overloads and the game may call either, so both are patched.
    [HarmonyPatch(typeof(UISmartLocId), nameof(UISmartLocId.getText), new System.Type[0])]
    public class GetText_Override_Patch
    {
        static void Postfix(UISmartLocId __instance, ref string __result)
        {
            if (PopupText.TryGet(__instance.m_locId, out var t)) __result = t;
        }
    }

    // Same, for getText(out bool _isLocalized). The out parameter is declared via ArgumentType.Out
    // because attribute arguments can't use typeof(bool).MakeByRefType().
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