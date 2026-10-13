using System;
using System.Collections.Generic;
using Alkawa.Engine;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;
using Alkawa.Gameplay.Player.Components; 
using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;
using UnityEngine;

namespace TLCArchipelago_Client
{
    public struct PendingItem
    {
        public long ItemId;
        public string ItemName;
        public string Sender;
        public int Index;

        public PendingItem(long itemId, string itemName, string sender, int index)
        {
            ItemId = itemId;
            ItemName = itemName;
            Sender = sender;
            Index = index;
        }
    }

    public static class PopupIcon
    {
        static PlayerRitualInfo pending;
        static Sprite original;
        static float restoreAt;
        static UI_DescriptionPageData pagePending;
        static Sprite pageOriginal;
        public static void Swap(PlayerRitualInfo info, Sprite replacement)
        {
            Plugin.BepinLogger.LogInfo($"PopupIcon.Swap: info={(info == null ? "null" : "ok")}, sprite={(replacement == null ? "null" : replacement.name)}");
            if (info == null || replacement == null) return;
            original = info.m_icon;
            pending = info;
            info.m_icon = replacement;
            Plugin.BepinLogger.LogInfo($"PopupIcon.Swap: icon now = {info.m_icon?.name ?? "null"} (was {original?.name ?? "null"})");
            restoreAt = Time.unscaledTime + 3f;   // adjust after testing
        }

        // call from ItemDelivery.Tick() (every frame, before the throttle)
        public static void Update()
        {
            if (Time.unscaledTime < restoreAt) return;
            if (pending != null) { pending.m_icon = original; pending = null; original = null; }
            if (pagePending != null) { pagePending.m_sprite = pageOriginal; pagePending = null; pageOriginal = null; }

        }


        public static void SwapPage(UI_DescriptionPageData page, Sprite replacement)
        {
            if (page == null || replacement == null) return;
            pageOriginal = page.m_sprite;
            pagePending = page;
            page.m_sprite = replacement;
            restoreAt = Time.unscaledTime + 3f;
        }
    }

    public static class ItemDelivery
    {
        private static readonly Queue<PendingItem> queue = new Queue<PendingItem>();
        private static readonly object sync = new object();

        private static int nextIndexToEnqueue = 0;
        private static volatile int pendingCount;
        private static float nextCheck;
        private static bool delivering;

        public static bool IsGranting;
        private static PlayerComponent currentPlayer;
        public static void SetPlayer(PlayerComponent p) { currentPlayer = p; }
        public static void ClearPlayer(PlayerComponent p) { if (currentPlayer == p) currentPlayer = null; }
        private static PlayerComponent GetPlayer() => currentPlayer;

        // Socket thread: plain data only, no game calls.
        public static void Enqueue(PendingItem item)
        {
            lock (sync)
            {
                if (item.Index < Math.Max(nextIndexToEnqueue, ArchipelagoClient.ServerData.Index)) return;
                nextIndexToEnqueue = item.Index + 1;
                queue.Enqueue(item);
                pendingCount = queue.Count;
            }
        }

        public static void Tick()
        {
            PopupIcon.Update();
            if (pendingCount == 0) return;
            float now = UnityEngine.Time.unscaledTime;
            if (now < nextCheck) return;
            nextCheck = now + 0.25f;
            TryDeliver();
        }

        // Main thread: called from Tick and from the state-change postfixes.
        public static void TryDeliver()
        {
            if (pendingCount == 0 || delivering) return;
            delivering = true;
            try
            {
                lock (sync)
                {
                    while (queue.Count > 0 && IsSafe())
                    {
                        var item = queue.Peek();
                        try
                        {
                            if (!Grant(item)) break;
                        }
                        catch (Exception e)
                        {
                            Plugin.BepinLogger.LogError(e);
                            break;
                        }
                        queue.Dequeue();
                        pendingCount = queue.Count;
                        ArchipelagoClient.ServerData.Index = item.Index + 1;
                    }
                }
            }
            finally
            {
                delivering = false;
            }
        }

        private static bool IsSafe()
        {
            return GameFlow.HasActiveState
                && GameFlow.ActiveGameFlowState == EGameFlowStateType.InGame;
        }

        // TODO: map AP item ID -> EItemType / EPlayerUnlockableAbility and call your grant logic.
        // Return false if the player component isn't ready, so the item stays queued.
        // For now this logs and succeeds so you can test the queue and timing.
        private static bool Grant(PendingItem item)
        {
            var player = GetPlayer();
            if (player == null || player.IsInjectingProgressionData) return false;

            if(!ItemMap.TryGet(item.ItemId, out var info))
            {
                Plugin.BepinLogger.LogWarning($"Unknown item ID {item.ItemId} ({item.ItemName}) from {item.Sender}, index {item.Index}");
                return true;
            }
            IsGranting = true;
            try{
                if (info.Item.HasValue)
                {
                    player.PlayerInventory.AddItem(info.Item.Value, info.Amount, EItemAcquisitionMode.Loot);
                }
                else if (info.Ability.HasValue)
                {
                    player.PlayerAbilities.UnlockAbility(info.Ability.Value, true, true, 0f);
                }
                Plugin.BepinLogger.LogInfo($"DELIVER: {item.ItemName} (id {item.ItemId}) from {item.Sender}, index {item.Index}");
                return true;
            }
            finally
            {
                IsGranting = false;
            }
        }
        // in ItemDelivery
        public static void ReportCheck(long locationId)
        {
            Plugin.ArchipelagoClient.ReportCheck(locationId);
        }
    }

    
}