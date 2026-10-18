using System.IO;
using System.Text;
using Newtonsoft.Json;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using Alkawa.Engine;

namespace TLCArchipelago_Client
{
    public static class JsonWriter
    {
        /// <summary>Writes the current AP state (seed, slot name, item index) to <paramref name="path"/>.</summary>
        /// <summary>Writes the current AP state, using the index as it is right now.</summary>
        public static void WriteStateToFile(string path)
        {
            WriteStateToFile(path, ArchipelagoClient.ServerData.GetIndex());
        }

        /// <summary>
        /// Writes the AP state (seed, slot name, item index) to <paramref name="path"/>.
        /// Pass an index captured at save time so a delivery during the write delay can't inflate it.
        /// </summary>
        public static void WriteStateToFile(string path, int index)
        {
            var d = ArchipelagoClient.ServerData;
            var seed = d.GetSeed();
            if (seed == null) return;   // not logged in yet: a file without a seed would be rejected on load anyway

            try
            {
                var json = JsonConvert.SerializeObject(new ApSaveState
                {
                    Seed = seed,
                    SlotName = d.GetSlotName(),
                    Index = index
                }, Formatting.Indented);

                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (System.Exception e)
            {
                Plugin.BepinLogger.LogError($"Failed to write state to {path}: {e.Message}");
            }
        }

        public static ApSaveState ReadStateFromFile(string path)
        {
            try
            {
                if(!File.Exists(path)) return null;
                
                return JsonConvert.DeserializeObject<ApSaveState>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch(System.Exception e)
            {
                Plugin.BepinLogger.LogError($"Failed to read state from {path}: {e.Message}");
                return null;
            }
        }
    }
}