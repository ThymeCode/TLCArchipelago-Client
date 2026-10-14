using Alkawa.Gameplay;
using Alkawa.Gameplay.Controller;
using Alkawa.Gameplay.Player.Components;
using UnityEngine;

namespace TLCArchipelago_Client
{
    /// <summary>
    /// Handles a pickup of OUR OWN item. We cancel the vanilla reward, so the player stays frozen until some
    /// ritual plays. Here the server echoes the item back, ItemDelivery.Grant gives it, and the game builds
    /// its own ritual, which releases the player. If that never happens (no echo, or the granted item has no
    /// ritual) we fall back to our generic popup after a timeout. Everything here runs on the main thread.
    /// </summary>
    public static class PendingCheck
    {
        const float TimeoutSeconds = 3f;        // no echo: show the generic popup
        const float VerifyDelaySeconds = 1.5f;  // after the echo is granted, how long to wait before checking a ritual started

        static bool active;
        static long locationId;
        static EItemType vanillaItem;
        static float startedAt;
        static bool loggedState;
        static bool echoGranted;
        static float verifyAt;

        /// <summary>Call BEFORE ReportCheck, so the echo can't arrive before we're waiting for it.</summary>
        public static void Begin(long location, EItemType vanilla)
        {
            if (active) Plugin.BepinLogger.LogWarning($"PendingCheck: replacing unresolved check {locationId}");
            active = true; locationId = location; vanillaItem = vanilla;
            startedAt = Time.unscaledTime; echoGranted = false; loggedState = false;
        }

        /// <summary>True if this echoed item is the reward we're waiting for.</summary>
        public static bool IsPendingFor(long location) => active && !echoGranted && locationId == location;

        /// <summary>Grant() has just given the player the echoed item.</summary>
        public static void OnEchoGranted()
        {
            echoGranted = true;
            verifyAt = Time.unscaledTime + VerifyDelaySeconds;
        }

        /// <summary>Called every frame from ItemDelivery.Tick.</summary>
        public static void Update(PlayerComponent player)
        {
            if (!active || player == null) return;
            float now = Time.unscaledTime;

            if (!loggedState)
            {
                loggedState = true;
                Plugin.BepinLogger.LogInfo($"PendingCheck: waiting for echo, game state = {Alkawa.Engine.GameFlow.ActiveGameFlowState}");
            }

            if (echoGranted)
            {
                if (now < verifyAt) return;
                active = false;
                if (RitualRunning(player)) return;   // the game made its own popup: done
                Plugin.BepinLogger.LogInfo("PendingCheck: granted item started no ritual, showing generic popup");
                ShowGeneric(player, vanillaItem, "Archipelago Check", "Item Found", "You found your own item.");
            }
            else if (now - startedAt > TimeoutSeconds)
            {
                active = false;
                Plugin.BepinLogger.LogInfo("PendingCheck: timed out waiting for echo, showing generic popup");
                ShowGeneric(player, vanillaItem, "Archipelago Check", "Check Sent", "Your item was sent into the multiworld.");
            }
        }

        static bool RitualRunning(PlayerComponent player)
        {
            var r = player.PlayerRitual;
            return r.StateInfo.m_ritualInProgress || r.HasDelayedRituals;
        }

        /// <summary>
        /// Starts the generic ritual (PotionReveal) as a popup frame and replaces its text and icon.
        /// Also used directly by the intercept for other games' items.
        /// </summary>
        public static void ShowGeneric(PlayerComponent player, EItemType item, string title, string header, string desc)
        {
            try
            {
                var ritual = player.PlayerRitual;
                ritual.InternalTryStartRitual(EPlayerRitual.PotionReveal, item, 0f, false);
                var ri = ritual.StateInfo.m_currentRitualInformation;
                var page = ri?.m_descriptionPageData?.TryCast<UI_DescriptionPageData>();
                PopupText.Arm(page, title, header, desc, "");   // "" hides the tutorial hint
                PopupIcon.SwapPage(page, ApSprite.Get());
            }
            catch (System.Exception e) { Plugin.BepinLogger.LogError(e); }
        }
    }
}