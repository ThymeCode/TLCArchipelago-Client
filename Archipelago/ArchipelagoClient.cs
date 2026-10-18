using System;
using System.Linq;
using System.Threading;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using ArchipelagoBepin6Template.Unity.IL2Cpp.Utils;
using TLCArchipelago_Client;
using System.Threading.Tasks;
using System.Collections;

using Archipelago.MultiClient.Net.Models;   // for ItemInfo
using System.Collections.Generic;

namespace ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;

public class ArchipelagoClient
{
    public const string APVersion = "0.6.8";
    private const string Game = "PoPTLC";

    public static bool Authenticated;
    private bool attemptingConnection;

    public static ArchipelagoData ServerData = new();
    private DeathLinkHandler DeathLinkHandler;
    private ArchipelagoSession session;
    // Stops a second reconnect loop starting while one is already running.
    private bool alreadyRunning = false;
    
    // Set by Disconnect(), cleared only in Connect(). The socket handlers check it so a
    // deliberate disconnect doesn't trigger the reconnect loop.
    private bool intentionalDisconnect = false;
    // Lets CancelReconnect() abort the delay between reconnect attempts.
    private CancellationTokenSource reconnectCTS;

    // Guards ServerData.CheckedLocations. ReportCheck runs on the main thread (game patches)
    // while HandleConnectResult runs on a ThreadPool thread, so both sides take this lock.
    private readonly object checkLock = new object();


    /// <summary>
    /// call to connect to an Archipelago session. Connection info should already be set up on ServerData
    /// </summary>
    /// <returns></returns>
    public void Connect()
    {
        if (Authenticated || attemptingConnection) return;
        attemptingConnection = true;
        intentionalDisconnect = false;
        try
        {
            session = ArchipelagoSessionFactory.CreateSession(ServerData.Uri);
            SetupSession();
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError(e);
        }

        TryConnect();
    }

    /// <summary>
    /// add handlers for Archipelago events
    /// </summary>
    private void SetupSession()
    {
        session.MessageLog.OnMessageReceived += message => ArchipelagoConsole.LogMessage(message.ToString());
        session.Items.ItemReceived += OnItemReceived;
        session.Socket.ErrorReceived += OnSessionErrorReceived;
        session.Socket.SocketClosed += OnSessionSocketClosed;
    }

    /// <summary>
    /// attempt to connect to the server with our connection info
    /// </summary>
    private void TryConnect()
    {
        try
        {
            // it's safe to thread this function call but unity notoriously hates threading so do not use excessively
            ThreadPool.QueueUserWorkItem(
                _ => HandleConnectResult(
                    session.TryConnectAndLogin(
                        Game,
                        ServerData.SlotName,
                        ItemsHandlingFlags.AllItems, // all items, including ones from our own world: every reward goes through ItemDelivery
                        new Version(APVersion),
                        password: ServerData.Password,
                        requestSlotData: true // ServerData.NeedSlotData
                    )));
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError(e);
            HandleConnectResult(new LoginFailure(e.ToString()));
            attemptingConnection = false;
        }
    }

    /// <summary>
    /// handle the connection result and do things
    /// </summary>
    /// <param name="result"></param>
    private void HandleConnectResult(LoginResult result)
    {
        string outText;
        if (result.Successful)
        {
            var success = (LoginSuccessful)result;

            ServerData.SetupSession(success.SlotData, session.RoomState.Seed);
            PlacementCache.Load(success.SlotData);
            Authenticated = true;

            DeathLinkHandler = new(session.CreateDeathLinkService(), ServerData.SlotName);
            lock (checkLock)
            {
                foreach (var id in session.Locations.AllLocationsChecked)
                    if (!ServerData.CheckedLocations.Contains(id))
                        ServerData.CheckedLocations.Add(id);
            }
            // Re-send every stored check on each connect, so checks made while offline aren't lost.
            session.Locations.CompleteLocationChecksAsync(SnapshotChecks());
            outText = $"Successfully connected to {ServerData.Uri} as {ServerData.SlotName}!";
            SendMessage("Hello from the lost crown!");

        }

        else
        {
            var failure = (LoginFailure)result;
            outText = $"Failed to connect to {ServerData.Uri} as {ServerData.SlotName}.";
            outText = failure.Errors.Aggregate(outText, (current, error) => current + $"\n    {error}");

            Plugin.BepinLogger.LogError(outText);

            Authenticated = false;
            // Disconnect() sets intentionalDisconnect, so a failed login (bad slot, wrong password,
            // or server not running yet) does NOT start the reconnect loop.
            Disconnect();

        }

        ArchipelagoConsole.LogMessage(outText);
        attemptingConnection = false;
    }

    /// <summary>
    /// something went wrong, or we need to properly disconnect from the server. cleanup and re null our session
    /// </summary>
    public void Disconnect()
    {
        intentionalDisconnect = true;
        Plugin.BepinLogger.LogDebug("disconnecting from server...");
        session?.Socket.DisconnectAsync();
        CleanupSessionState();
    }
    private void CleanupSessionState()
    {
        session = null;
        Authenticated = false;
        
    }

    public void SendMessage(string message)
    {
        session.Socket.SendPacketAsync(new SayPacket { Text = message });
    }

    /// <summary>
    /// we received an item so reward it here
    /// </summary>
    /// <param name="helper">item helper which we can grab our item from</param>
    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        // [socket thread] Must not touch game objects: items are converted to plain data and queued,
        // and ItemDelivery grants them later on the main thread when the game is in a safe state.
        // Drain the whole batch, in case the library raises this event once per packet rather than once per item.
        var batch = new List<ItemInfo>();
        while (helper.Any()) batch.Add(helper.DequeueItem());
        Plugin.BepinLogger.LogInfo($"OnItemReceived: all={helper.AllItemsReceived.Count}");
        // Index of the first drained item in the server's full item list. ItemDelivery uses it
        // to skip items already delivered in an earlier session (the server resends from 0 on login).
        int firstIndex = helper.AllItemsReceived.Count - batch.Count;
        for (int i = 0; i < batch.Count; i++)
        {
            var it = batch[i];
            ItemDelivery.Enqueue(new PendingItem(it.ItemId, it.ItemName, it.Player.Name, firstIndex + i, it.LocationId));
        }
    }
    /// <summary>
    /// Records a location check and sends it to the server. Called by the AddItem/UnlockAbility
    /// intercept patches when a mapped location is picked up. The check is stored first, then sent,
    /// so if we're offline it goes out on the next connect (see HandleConnectResult).
    /// </summary>
    public void ReportCheck(long locationId)
    {
        lock (checkLock)
        {
            if (!ServerData.CheckedLocations.Contains(locationId))
                ServerData.CheckedLocations.Add(locationId);
        }

        // A failed send must never throw back into game code (this runs inside a Harmony prefix).
        try
        {
            var s = session;                      // local copy: another thread may null it
            if (Authenticated && s != null)
                s.Locations.CompleteLocationChecksAsync(locationId);
        }
        catch (Exception e) { Plugin.BepinLogger.LogError(e); }
    }

    // Copy of the checked locations taken under the lock, safe to use from any thread.
    private long[] SnapshotChecks()
    {
        lock (checkLock) { return ServerData.CheckedLocations.ToArray(); }
    }

    /// <summary>
    /// something went wrong with our socket connection
    /// </summary>
    /// <param name="e">thrown exception from our socket</param>
    /// <param name="message">message received from the server</param>
    private void OnSessionErrorReceived(Exception e, string message)
    {
        Plugin.BepinLogger.LogError(e);
        ArchipelagoConsole.LogMessage(message);

        if (intentionalDisconnect)
        {
            return;
        }
        CleanupSessionState();
        AttemptReconnectWithBackoff();
    }

    /// <summary>
    /// something went wrong closing our connection. disconnect and clean up
    /// </summary>
    /// <param name="reason"></param>
    private void OnSessionSocketClosed(string reason)
    {
        Plugin.BepinLogger.LogError($"Connection to Archipelago lost: {reason}");
        if (intentionalDisconnect)
        {
            return;
        }
        CleanupSessionState();
        AttemptReconnectWithBackoff();
    }

    // Retries Connect() with growing delays (10s, 20s, 30s, then 60s) until authenticated.
    // Both socket handlers above call this: an abrupt server kill raises ErrorReceived, not SocketClosed.
    private async void AttemptReconnectWithBackoff()
    {
        if(alreadyRunning) return;
        alreadyRunning = true;
        reconnectCTS = new CancellationTokenSource();
        var delays = new[] {10000, 20000, 30000, 60000};//ms
        int delayIndex = 0;

        try
        {
            while (!Authenticated)
            {
                await Task.Delay(delays[Math.Min(delayIndex, delays.Length-1)], reconnectCTS.Token);
                delayIndex++;
                Plugin.BepinLogger.LogInfo($"Attempting to reconnect to Archipelago server {ServerData.Uri} as {ServerData.SlotName}...");
                Connect();
                
            }            
        }
        catch (TaskCanceledException)
        {
            Plugin.BepinLogger.LogInfo("Reconnection attempt cancelled.");
        }
        finally
        {
            alreadyRunning = false;
            reconnectCTS.Dispose();
            reconnectCTS = null;
        }
    }

    // Stops the reconnect loop (meant for a future UI button).
    public void CancelReconnect()
    {
        reconnectCTS?.Cancel();
    }

    public void RequestResync()
    {
        var s = session;
        if (Authenticated && s != null)
        {
            try
            {
                s.Socket.SendPacketAsync(new SyncPacket());
            }
            catch(System.Exception e)
            {
                Plugin.BepinLogger.LogError("Resync request failed: " + e);
            }
            
        }


    }
}