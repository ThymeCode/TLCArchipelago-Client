using System.IO;
using BepInEx.Configuration;
using Alkawa.Engine;

namespace TLCArchipelago_Client
{
    public static class SaveLocation
    {
        static ConfigEntry<string> saveLocationConfig;
        public static void Init(ConfigFile config)
        {
            saveLocationConfig = config.Bind("Saves", "SaveFolder","", "Full path to the game's Saves folder (the one containing SaveSlot1, SaveSlot2, SaveSlot3, etc).");
        }
        public static string Root
        {
            get 
            {
                var path = saveLocationConfig?.Value?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(path))
                {
                    Plugin.BepinLogger.LogWarning("Save folder path is not set in config. Save features disabled.");
                    return null;
                }
                if (!Directory.Exists(path))
                {
                    Plugin.BepinLogger.LogWarning($"Save folder path '{path}' does not exist. Save features disabled.");
                    return null;
                }
                return path;
            }
        }
        /// <summary>Returns 1-3 for a Game slot, or 0 for anything else (e.g. Profile).</summary>
        public static int GetSlotNumber(ESaveSlotType slot)
        {
            var s = slot.ToString();
            return s.StartsWith("Game") && int.TryParse(s.Substring(4), out var n) ? n : 0;
        }
        public static string GetSaveFolder(int slotNumber)
        {
            var root = Root;
            return root == null ? null : Path.Combine(root, "SaveSlot" + slotNumber);
        }
        
    }
}