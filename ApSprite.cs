using System.IO;
using System.Reflection;
using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Loads the embedded ap_icon.png and returns it as a cached sprite.
    /// The sprite is created with HideAndDontSave so it won't be unloaded with scenes.
    /// </summary>
    public static class ApSprite
    {
        static Sprite cached;

        /// <summary>
        /// Returns the Archipelago logo sprite, building it from the embedded PNG on first call.
        /// Returns null (and logs an error) if the resource is missing or fails to load.
        /// Must be called on the main thread, since it creates Unity objects.
        /// </summary>
        public static Sprite Get()
        {
            if (cached != null) return cached;   // Unity null check: also catches destroyed objects
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string res = null;
                // Match on the suffix: the full resource name is prefixed with the project's default namespace.
                foreach (var n in asm.GetManifestResourceNames())
                    if (n.EndsWith("ap_icon.png")) { res = n; break; }
                if (res == null) { Plugin.BepinLogger.LogError("ap_icon.png resource not found"); return null; }

                byte[] bytes;
                using (var s = asm.GetManifestResourceStream(res))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);   // 2x2 is a placeholder; LoadImage resizes it to the PNG's real size
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    Plugin.BepinLogger.LogError("LoadImage failed");
                    return null;
                }
                tex.hideFlags = HideFlags.HideAndDontSave;   // keep it from being unloaded with scenes

                cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);   // centred pivot, 100 pixels per unit
                cached.name = "AP_Icon";   // named so it's recognisable in logs
                cached.hideFlags = HideFlags.HideAndDontSave;
                return cached;
            }
            catch (System.Exception e)
            {
                Plugin.BepinLogger.LogError(e);
                return null;
            }
        }
    }
}