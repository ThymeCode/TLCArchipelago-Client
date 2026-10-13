using Alkawa.Gameplay;
using HarmonyLib;
using UnityEngine;

namespace TLCArchipelago_Client
{
    public static class PopupText
    {
        const int NameId = 4903, HeaderId = 4456, DescId = 4904, HintId = 4905;

        static string name, header, desc;
        static float expires;

        public static void Arm(string itemName, string headerText, string description)
        {
            name = itemName; header = headerText; desc = description;
            expires = Time.unscaledTime + 15f;   // Possibly unneccessary.
        }

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