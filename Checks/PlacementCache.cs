using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TLCArchipelago_Client
{
    public class Placement { public bool Own; public string ItemName, Player, Game; }

    /// <summary>
    /// What each of our locations holds, read once from slot data at login. Lets the pickup
    /// intercept know synchronously whether an item is ours or belongs to another game.
    /// </summary>
    public static class PlacementCache
    {
        static volatile Dictionary<long, Placement> map = new Dictionary<long, Placement>();

        // [socket thread] Replaces the whole table, so readers never see a half-filled one.
        public static void Load(IDictionary<string, object> slotData)
        {
            var fresh = new Dictionary<long, Placement>();
            if (slotData != null && slotData.TryGetValue("placements", out var raw) && raw is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (!long.TryParse(prop.Name, out long id)) continue;
                    var v = prop.Value;
                    fresh[id] = new Placement
                    {
                        Own = (bool)v["own"],
                        ItemName = (string)v["item"],
                        Player = (string)v["player"],
                        Game = (string)v["game"],
                    };
                }
            }
            map = fresh;
            Plugin.BepinLogger.LogInfo($"PlacementCache: loaded {fresh.Count} placements");
        }

        public static bool TryGet(long locationId, out Placement p) => map.TryGetValue(locationId, out p);
    }
}