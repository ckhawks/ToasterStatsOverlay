using System;
using System.Reflection;
using HarmonyLib;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ToasterStatsOverlay;

public class StatsOverlay : MonoBehaviour
{
    // static readonly MethodInfo _extractPacketLossMethod = typeof(UnityTransport)
    //     .GetMethod("ExtractPacketLoss",
    //         BindingFlags.Instance | BindingFlags.NonPublic);
    
    // Field rootVisualElement defined on type UIComponent`1[T] is not a field on the target object which is of type UIHUD.
    static readonly FieldInfo _rootVisualElementField = typeof(UIComponent<UIHUD>)
        .GetField("rootVisualElement", 
            BindingFlags.Instance | BindingFlags.NonPublic);    
    
    static readonly FieldInfo _playerButtonField = typeof(UIMainMenu)
        .GetField("playerButton", 
            BindingFlags.Instance | BindingFlags.NonPublic);   
    
    static readonly FieldInfo _chatContainerField = typeof(UIComponent<UIChat>)
        .GetField("container", 
            BindingFlags.Instance | BindingFlags.NonPublic);   
    
    // static readonly FieldInfo _packetLossCacheField = typeof(UnityTransport)
    //     .GetField("m_PacketLossCache", 
    //         BindingFlags.Instance | BindingFlags.NonPublic);  
    //
    // static readonly FieldInfo _packetLossCacheField = typeof(UnityTransport)
    //     .GetInterface("m_PacketLossCache", 
    //         BindingFlags.Instance | BindingFlags.NonPublic);   
    
    public float updateInterval = 0.5f;    // FPS refresh
    public float pingInterval   = 0.5f;      // how often to ping

    public int pingWidth = 100;

    // FPS
    float fpsTimeLeft;
    int   fpsFrames;
    public VisualElement statsContainer;
    public Label fpsLabel;
    public Label pingLabel;
    // public Label lossLabel;

    // public static float packetLossValue;
    
    public void Setup()
    {
        UIHUD uiHud = UIHUD.Instance;
        UIMainMenu uiMainMenu = UIMainMenu.Instance;
        
        if (uiHud == null)
        {
            Plugin.LogError($"uiHud is null");
            return;
        }
        
        if (_rootVisualElementField == null)
        {
            Plugin.LogError($"_rootVisualElementField is null");
            return;
        }
        
        Button playerButton = (Button) _playerButtonField.GetValue(uiMainMenu);
            
        // mainMenu = uiMainMenuInstance;
        // uiMainMenu = ; // might need to add one more .parent to this
        VisualElement rootVisualElement = playerButton.parent.parent;
        
        // VisualElement rootVisualElement = (VisualElement) _rootVisualElementField.GetValue(uiHud);
        if (rootVisualElement == null)
        {
            Plugin.LogError($"rootVisualElement is null");
            return;
        }

        statsContainer = MakeContainer(rootVisualElement, 16, 10);
        fpsLabel = MakeLabel(statsContainer, "ToasterStatsFPSLabel", 60);
        pingLabel = MakeLabel(statsContainer, "ToasterStatsPingLabel", pingWidth);
        // lossLabel = MakeLabel(statsContainer, "ToasterStatsLossLabel", 90);
        fpsTimeLeft = updateInterval;
        
        UIChat chat = UIChat.Instance;
        VisualElement container = (VisualElement) _chatContainerField.GetValue(chat);
        Plugin.Log($"Container starting height: {container.style.top}");
        container.style.top = 18;
    }

    // [HarmonyPatch(typeof(UIHUD), nameof(UIHUD.Initialize))]
    // public class UihudInitialize
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(UIHUD __instance, VisualElement rootVisualElement)
    //     {
    //         Plugin.Log($"UIHUD.Initialize");
    //         Plugin.statsOverlay = UIHUD.Instance.gameObject.AddComponent<StatsOverlay>();
    //         Plugin.statsOverlay.Setup(rootVisualElement);
    //     }
    // }

    static VisualElement MakeContainer(VisualElement root, int x, int y)
    {
        VisualElement container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexGrow = 1;
        container.style.position = Position.Absolute;
        container.style.left = x;
        container.style.top = y;
        root.Add(container);
        return container;
    }
    
    static Label MakeLabel(VisualElement parent, string name, int width)
    {
        var l = new Label("…") { name = name };
        l.style.color    = Color.white;
        l.style.width = width;
        l.style.fontSize = 11;
        parent.Add(l);
        return l;
    }
    
    void Update()
    {
        if (fpsLabel == null)
        {
            return;
        }

        if (pingLabel == null)
            return;
        

        // --- FPS ---
        fpsTimeLeft -= Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsTimeLeft <= 0f)
        {
            var fps = fpsFrames / updateInterval;
            // fpsLabel.text = $"{fps:0} FPS";
            fpsLabel.text = $"{fps:0} FPS";
            fpsFrames    = 0;
            fpsTimeLeft += updateInterval;
            
            Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
            if (localPlayer != null)
            {
                pingLabel.text = $"{GetRtt():0} ms";
                // lossLabel.text = $"{packetLossValue:0}% loss";
                pingLabel.style.width = pingWidth;
            }
            else
            {
                pingLabel.text = $"Not connected";
                // pingLabel.style.width = 0;
                // lossLabel.text = $"Not connected";
            }
        }
        
        // --- Ping & Loss ---
        // we update the UI every frame so it's responsive
        // statsLabel.text = $"Ping: {lastPingRttMs:0} ms";
        // TODO upgrade this to use the NetworkManager to get more realtime ping
        // outputText += $" | Ping: {lastPingRttMs:0} ms";

        // avoid div by zero
        // float lossPct = pingsSent > 0
        //     ? (100f * (pingsSent - pingsReceived) / pingsSent)
        //     : 0f;
        // lossLabel.text = $"Loss: {lossPct:0.0}%";
        // outputText += $" | Loss: {lossPct:0.0}%";

    }
    
    /// <summary>
    /// Gets the latest round-trip time (in ms) that the client is measuring toward the server.
    /// </summary>
    public static float GetRtt()
    {
        return NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton
            .NetworkConfig.NetworkTransport.ServerClientId);
        var nm   = NetworkManager.Singleton;
        var utp  = nm.NetworkConfig.NetworkTransport;
        if (utp == null)
        {
            Debug.LogWarning("Transport is not UnityTransport – no RTT available");
            return 0;
        }

        // ensure we have metrics enabled on the UTP component
        // utp.EnableMetrics = true;

        // LocalClientId is the client’s own ID – for a client measuring to the server use that.
        // On a dedicated host you might want to loop over ConnectedClientsList instead.
        var clientId = nm.LocalClientId;

        // UTP API has two flavors: a “raw” getter or a struct‐based getter:
        // 1) Raw:
        var rttRaw = utp.GetCurrentRtt(clientId);
        // 2) Struct:
        // var stats = utp.GetNetworkMetrics(clientId);
        // var rttMs  = stats.RoundTripTime;

        
        
        return rttRaw;
    }

    public void OnDestroy()
    {
        statsContainer.parent.Remove(statsContainer);
        UIChat chat = UIChat.Instance;
        VisualElement container = (VisualElement) _chatContainerField.GetValue(chat);
        container.style.top = 10;
    }

    /// <summary>
    /// Gets the estimated packet-loss fraction [0…1].
    /// </summary>
    // public static float GetPacketLoss()
    // {
    //     var nm  = NetworkManager.Singleton;
    //     var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
    //     if (utp == null)
    //     {
    //         Debug.LogWarning("Transport is not UnityTransport – no loss data");
    //         return 0;
    //     }
    //
    //     UnityTransport.PacketLossCache packetLossCache =
    //         (UnityTransport.PacketLossCache)_packetLossCacheField.GetValue(utp);
    //     
    //     // utp.EnableMetrics = true;
    //     var clientId = nm.LocalClientId;
    //     // utp.ExtractPacketLoss(NetworkManager.Singleton.);
    //     //
    //     // MetricsManager
    //
    //     // var lossRaw = (float) _extractPacketLossMethod.Invoke(utp, new object[] { clientId });
    //     
    //     // 1) Raw:
    //     // var lossRaw = utp.ExtractPacketLoss(clientId);
    //     // 2) Struct:
    //     // var stats = utp.GetNetworkMetrics(clientId);
    //     // var lossRaw = stats.PacketLoss;
    //
    //     return lossRaw;
    // }

    // [HarmonyPatch(typeof(Unity.Netcode.NetworkMetrics), nameof(Unity.Netcode.NetworkMetrics.UpdatePacketLoss))]
    // public static class UpdatePacketLossPatch
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(NetworkMetrics __instance, float packetLoss)
    //     {
    //         packetLoss = packetLoss;
    //     }
    // }
    
    // [HarmonyPatch]               // no args here
    // public static class UpdatePacketLossPatch
    // {
    //     // 1) Tell Harmony which method to patch
    //     static MethodBase TargetMethod()
    //     {
    //         // look up the internal Unity.Netcode.NetworkMetrics type
    //         var nmType = AccessTools.TypeByName("Unity.Netcode.NetworkMetrics");
    //         if (nmType == null)
    //             throw new Exception("Could not find Unity.Netcode.NetworkMetrics");
    //
    //         // find the internal UpdatePacketLoss method (instance, non-public)
    //         return AccessTools.Method(
    //             nmType, 
    //             "UpdatePacketLoss", 
    //             new[] { typeof(float) }    // your overload selector
    //         );
    //     }
    //
    //     // 2) The postfix can only use types you *can* name.
    //     //    We’ll capture the updated packetLoss value and maybe log it.
    //     static void Postfix(object __instance, float packetLoss)
    //     {
    //         // __instance is your NetworkMetrics instance (as object)
    //         // packetLoss is the value passed into UpdatePacketLoss
    //         // UnityEngine.Debug.Log($"[NetStats] packetLoss→ {packetLoss:0.00}");
    //         packetLossValue = packetLoss;
    //     }
    // }
}