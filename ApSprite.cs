using System.IO;
using System.Reflection;
using UnityEngine;

namespace TLCArchipelago_Client
{
    public static class ApSprite
    {
        static Sprite cached;

        public static Sprite Get()
        {
            if (cached != null) return cached;   // Unity null check: also catches destroyed objects
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string res = null;
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

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    Plugin.BepinLogger.LogError("LoadImage failed");
                    return null;
                }
                tex.hideFlags = HideFlags.HideAndDontSave;   // keep it from being unloaded with scenes

                cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                cached.name = "AP_Icon";
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