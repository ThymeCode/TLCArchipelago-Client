using System;
using System.Collections.Generic;
using Alkawa.Engine;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using Alkawa.Gameplay.Player.Components; 
using Alkawa.Gameplay;
using UnityEngine;
using System.ComponentModel.DataAnnotations;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Owns the queue of items received from the server and grants them to the player.
    /// Enqueue runs on the socket thread and must not touch game objects.
    /// Tick/TryDeliver/Grant run on the main thread, and only when the game is in a safe state.
    /// </summary>
    public static class ItemDelivery
    {
        private static readonly Queue<PendingItem> queue = new Queue<PendingItem>();
        private static readonly object sync = new object();   // guards queue and nextIndexToEnqueue (socket thread vs main thread)

        private static int nextIndexToEnqueue = 0;   // dedupes repeats when the server resends items on reconnect
        private static volatile int pendingCount;    // lets Tick() check for work without taking the lock
        private static float nextCheck;              // throttles delivery attempts
        private static bool delivering;              // re-entrancy guard: a grant can trigger game callbacks that call TryDeliver

        /// <summary>
        /// True while Grant() is calling into the game. The AddItem/UnlockAbility intercept patches
        /// check this so they don't swallow our own deliveries as location pickups.
        /// </summary>
        public static bool IsGranting;
        public static float timestamp;

        private static PlayerComponent currentPlayer;   // captured by the player tick patch, cleared on destroy
        public static void SetPlayer(PlayerComponent p) { currentPlayer = p; }
        public static void ClearPlayer(PlayerComponent p) { if (currentPlayer == p) currentPlayer = null; }
        public static PlayerComponent GetPlayer() => currentPlayer;

        // Socket thread: plain data only, no game calls.
        public static void Enqueue(PendingItem item)
        {
            Plugin.BepinLogger.LogInfo($"Started Enqueue for {item.ItemName} at {item.Index}");
            lock (sync)
            {
                // Skip anything already delivered in an earlier session (ServerData.Index) or already queued.
            
                if (item.Index < Math.Max(nextIndexToEnqueue, ArchipelagoClient.ServerData.Index))
                {
                    Plugin.BepinLogger.LogInfo($"Already logged {item.ItemName} with index {item.Index}, Server index: {ArchipelagoClient.ServerData.GetIndex()}, nextIndexToEnqueue: {nextIndexToEnqueue}");
                    return;
                } 
                nextIndexToEnqueue = item.Index + 1;
                queue.Enqueue(item);
                pendingCount = queue.Count;
            }
        }

        /// <summary>
        /// Called every frame from the player tick patch (main thread).
        /// Restores the popup icon, then attempts delivery at most every 0.25s.
        /// </summary>
        public static void Tick()
        {
            PopupIcon.Update();   // must run every frame, before the early returns below
            PendingCheck.Update(currentPlayer);
            if (pendingCount == 0) return;
            float now = UnityEngine.Time.unscaledTime;   // unscaled so delivery still ticks while paused
            if (now < nextCheck) return;
            nextCheck = now + 0.25f;
            TryDeliver();
        }

        // Main thread: called from Tick and from the state-change postfixes.
        public static void TryDeliver()
        {
            if (pendingCount == 0 || delivering || Time.unscaledTime - timestamp < 1) return;
            delivering = true;
            try
            {
                lock (sync)
                {
                    // Re-check the game state before every item: a grant (e.g. an ability ritual)
                    // can change it, and the next item should wait until we're back in gameplay.
                    while (queue.Count > 0 && (IsSafe() || PendingCheck.IsPendingFor(queue.Peek().LocationId)))
                    {
                        var item = queue.Peek();
                        try
                        {
                            if (!Grant(item)) break;   // not ready yet: leave it queued and retry next tick
                        }
                        catch (Exception e)
                        {
                            Plugin.BepinLogger.LogError(e);
                            break;
                        }
                        queue.Dequeue();
                        pendingCount = queue.Count;
                        // Advance the index only after a successful grant, so a queued-but-undelivered item isn't lost on exit.
                        ArchipelagoClient.ServerData.Index = item.Index + 1;
                    }
                }
            }
            finally
            {
                delivering = false;
            }
        }

        // Only deliver during normal gameplay, not menus, cutscenes, loading or respawn.
        private static bool IsSafe()
        {
            return GameFlow.HasActiveState
                && GameFlow.ActiveGameFlowState == EGameFlowStateType.InGame;
        }

        // Returns true if the item was handled (granted or deliberately dropped), false to retry later.
        private static bool Grant(PendingItem item)
        {
            var player = GetPlayer();
            if (player == null || player.IsInjectingProgressionData) return false;   // player missing or a save is loading
            bool resolvesPending = PendingCheck.IsPendingFor(item.LocationId);
            if(!ItemMap.TryGet(item.ItemId, out var info))
            {
                // Dropped, not retried: an unmapped item would otherwise block the queue forever.
                Plugin.BepinLogger.LogWarning($"Unknown item ID {item.ItemId} ({item.ItemName}) from {item.Sender}, index {item.Index}");
                return true;
            }
            IsGranting = true;   // see the IsGranting field; always cleared in the finally below
            try{
                if (info.Item.HasValue)
                {
                    Plugin.BepinLogger.LogInfo("Trying to unlock item");
                    player.PlayerInventory.AddItem(info.Item.Value, info.Amount, EItemAcquisitionMode.Loot);
                }
                else if (info.Ability.HasValue)
                {
                    // Second bool shows the unlock ritual; the float is the ritual start delay.
                    Plugin.BepinLogger.LogInfo("Trying to unlock ability");
                    player.PlayerAbilities.UnlockAbility(info.Ability.Value, true, true, 0f);
                }
                Plugin.BepinLogger.LogInfo($"DELIVER: {item.ItemName} (id {item.ItemId}) from {item.Sender}, index {item.Index}");
                if (resolvesPending) PendingCheck.OnEchoGranted();
                return true;
            }
            finally
            {
                IsGranting = false;
            }
        }

        public static void ResetForLoad(int index)
        {
            Plugin.BepinLogger.LogInfo("Starting state reset");
            timestamp = Time.unscaledTime;
            lock (sync)
            {
                queue.Clear();
                pendingCount = 0;
                nextIndexToEnqueue = index;
            }
                PendingCheck.ClearPending();

        }

        // Thin wrapper so patches don't need to reference the client directly.
        public static void ReportCheck(long locationId)
        {
            Plugin.ArchipelagoClient.ReportCheck(locationId);
        }
    }
}