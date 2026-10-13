using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;

namespace TLCArchipelago_Client
{
    public static class MethodLogger
    {
        private static readonly HashSet<string> HotPathSkipNames = new HashSet<string>
        {
            "Update", "FixedUpdate", "LateUpdate", "OnAnimatorMove", "OnAnimatorIK"
        };

        // Methods known/suspected to run during early init or in a fragile context —
        // hooking these has caused hard crashes (no exception, just silence). Skip them
        // outright rather than trying to log them.
        private static readonly HashSet<string> RiskySkipNames = new HashSet<string>
        {
            "RestoreDefaultInventory", "Awake", "Start", "OnEnable", "OnDisable",
            "Init", "Initialize", "OnDestroy"
        };

        private static readonly HashSet<string> _alreadyLogged = new HashSet<string>();

        // keywordFilter: only methods whose name contains one of these substrings
        // (case-insensitive) get patched. Pass null/empty to patch everything
        // (not recommended — that's what caused the crash).
        public static void LogAllMethodsInType(Harmony harmony, Type type, ManualLogSource log, string[] keywordFilter = null)
        {
            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);

            int patched = 0;
            foreach (var method in methods)
            {
                if (method.IsAbstract) continue;
                if (method.IsGenericMethod || method.IsGenericMethodDefinition) continue;
                if (method.IsConstructor) continue;
                if (HotPathSkipNames.Contains(method.Name)) continue;
                if (RiskySkipNames.Contains(method.Name)) continue;

                if (keywordFilter != null && keywordFilter.Length > 0)
                {
                    bool matches = false;
                    foreach (var keyword in keywordFilter)
                    {
                        if (method.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (!matches) continue;
                }

                try
                {
                    var postfix = new HarmonyMethod(typeof(MethodLogger).GetMethod(nameof(GenericPostfix)));
                    harmony.Patch(method, postfix: postfix);
                    patched++;
                }
                catch (Exception e)
                {
                    log.LogWarning($"Could not patch {type.Name}.{method.Name}: {e.Message}");
                }
            }

            log.LogInfo($"MethodLogger: patched {patched} methods on {type.FullName}");
        }

        // Postfix runs AFTER the real method completes — safer than Prefix for
        // observation-only logging, since it doesn't interfere with the method's
        // own execution or fire before the game's own setup has happened.
        public static void GenericPostfix(MethodBase __originalMethod, object[] __args)
        {
            var key = $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}";

            if (!_alreadyLogged.Add(key))
                return;

            var argsStr = __args != null && __args.Length > 0
                ? string.Join(", ", Array.ConvertAll(__args, a => a?.ToString() ?? "null"))
                : "(no args)";

            Plugin.BepinLogger.LogInfo($"FIRST CALL (completed): {key}({argsStr})");
        }
    }
}
