using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;
using Alkawa.Engine;
using System.Collections.Generic;
using UnityEngine;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Utils;
using System.Threading.Tasks;
using Alkawa.Gameplay.Player.Components;  
using System;
using System.IO;

namespace TLCArchipelago_Client
{
    
    [HarmonyPatch(typeof(LoadSaveContainer), nameof(LoadSaveContainer.Write))]
    public class SaveStateUpdate_Patch
    {
        static void Postfix(LoadSaveContainer __instance)
        {
            try
            {
                if (__instance._saveType != ESaveType.SaveGame) return;
                int saveNumber = SaveLocation.GetSlotNumber(__instance._slotType);
                if (saveNumber == 0) return;
                int index = ArchipelagoClient.ServerData.GetIndex(); 
                // Plain values only; the container itself must not be touched after the hook returns.
                Task.Delay(500).ContinueWith(_ => WriteState(saveNumber, index));
            }
            catch (System.Exception e) { Plugin.BepinLogger.LogError(e); }
        }

        static void WriteState(int saveNumber, int index)
        {
            try
            {
                string saveFolder = SaveLocation.GetSaveFolder(saveNumber);
                if (saveFolder == null) return;
                string marker = Path.Combine(saveFolder, "lastSavedFolder.txt");
                if (!File.Exists(marker)) return;
                string fullSaveFolder = Path.Combine(saveFolder, "Save_" + File.ReadAllText(marker).Trim());
                if (!Directory.Exists(fullSaveFolder)) return;
                JsonWriter.WriteStateToFile(Path.Combine(fullSaveFolder, "ArchipelagoSaveState.json"), index);
            }
            catch (System.Exception e) { Plugin.BepinLogger.LogError(e); }
        }

    }



    [HarmonyPatch(typeof(LoadSaveContainer), nameof(LoadSaveContainer.ReadWithHeaderAndModule))]
    public class LoadStateUpdate_Patch
    {
        public static ApSaveState PendingLoad;
        public static string fullSaveFolder = "";

        public static void ApplyLoadedState(ApSaveState state)
        {
            var d = ArchipelagoClient.ServerData;
            bool valid = state != null
                      && state.Seed == d.GetSeed()
                      && state.SlotName == d.GetSlotName();
            int index = valid ? state.Index : 0;
            d.Index = index;
            Plugin.BepinLogger.LogInfo($"State is null: {state == null}, valid={valid}, Slot: {state?.SlotName}, Index: {state?.Index}, applied={index}, Path: {fullSaveFolder}");
            ItemDelivery.ResetForLoad(index);
            Plugin.ArchipelagoClient.RequestResync();
        }
        static void Postfix(LoadSaveContainer __instance)
        {
            Plugin.BepinLogger.LogInfo($"Loaded a save, current state: {GameFlow.ActiveGameFlowState}");
            try
            {
                if (__instance._saveType != ESaveType.SaveGame) return;
                int saveNumber = SaveLocation.GetSlotNumber(__instance._slotType);
                if (saveNumber == 0)
                {
                  return;
                } 
                string saveFolder = SaveLocation.GetSaveFolder(saveNumber);
                if (saveFolder == null)
                {
                    ApplyLoadedState(null);
                    return;
                }
                string marker = Path.Combine(saveFolder, "lastSavedFolder.txt");
                if (!File.Exists(marker))
                {
                    ApplyLoadedState(null);
                    return;
                }
                fullSaveFolder = Path.Combine(saveFolder, "Save_" + File.ReadAllText(marker).Trim());
                if (!Directory.Exists(fullSaveFolder))
                {
                    ApplyLoadedState(null);
                    return;
                }
                
                var state = JsonWriter.ReadStateFromFile(Path.Combine(fullSaveFolder, "ArchipelagoSaveState.json"));
                
                if (ArchipelagoClient.ServerData.GetSeed() == null)
                {
                    PendingLoad = state ?? new ApSaveState();
                }
                else
                {
                    ApplyLoadedState(state);
                }
                
            }
            catch (System.Exception e) { Plugin.BepinLogger.LogError(e);}

        }
    }
}

