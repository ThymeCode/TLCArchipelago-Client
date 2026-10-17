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
        static void Postfix(LoadSaveContainer __instance)
        {
            if (__instance._saveType != ESaveType.SaveGame) return;

        }
    }

}

