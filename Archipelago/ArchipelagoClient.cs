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



namespace ArchipelagoBepin6Template.Unity.IL2Cpp.Archipelago;

public class ArchipelagoClient
{
    public const string APVersion = "0.5.0";
    private const string Game = "APQuest";

    public static bool Authenticated;
    private bool attemptingConnection;

    public static ArchipelagoData ServerData = new();
    private DeathLinkHandler DeathLinkHandler;
    private ArchipelagoSession session;
    private bool alreadyRunning = false;
    
    private bool intentionalDisconnect = false;
    private CancellationTokenSource reconnectCTS;

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
                        ItemsHandlingFlags.NoItems, // TODO make sure to change this line
                        new Version(APVersion),
                        password: ServerData.Password,
                        requestSlotData: false // ServerData.NeedSlotData
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
            Authenticated = true;

            DeathLinkHandler = new(session.CreateDeathLinkService(), ServerData.SlotName);
            session.Locations.CompleteLocationChecksAsync(ServerData.CheckedLocations.ToArray());
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
        var receivedItem = helper.DequeueItem();

        if (helper.Index <= ServerData.Index) return;

        ServerData.Index++;

        // TODO reward the item here
        // if items can be received while in an invalid state for actually handling them, they can be placed in a local
        // queue/collection to be handled later
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

    public void CancelReconnect()
    {
        reconnectCTS?.Cancel();
    }
}