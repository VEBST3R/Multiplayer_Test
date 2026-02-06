using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkConnectionManager : MonoBehaviour
{
    public event Action<string> OnStatusUpdate;
    public event Action<bool, bool> OnConnectionStateChanged;
    public event Action<string> OnJoinCodeGenerated;

    [Header("Connection Mode")]
    [Tooltip("Use Unity Relay for online multiplayer (WebGL compatible)")]
    public bool useRelay = true;

    [Header("Dependencies")]
    public UnityRelayManager relayManager;

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;

        if (useRelay && relayManager != null)
        {
            relayManager.OnRelayJoinCodeGenerated += HandleJoinCodeGenerated;
            relayManager.OnRelayError += HandleRelayError;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;

        if (relayManager != null)
        {
            relayManager.OnRelayJoinCodeGenerated -= HandleJoinCodeGenerated;
            relayManager.OnRelayError -= HandleRelayError;
        }
    }

    // New async methods for relay
    public async Task StartHostAsync()
    {
        if (useRelay && relayManager != null)
        {
            Debug.Log("[NetworkManager] Starting host with relay...");
            OnStatusUpdate?.Invoke("Creating relay host...");
            
            string joinCode = await relayManager.StartHostWithRelay();
            
            if (!string.IsNullOrEmpty(joinCode))
            {
                Debug.Log($"[NetworkManager] Relay host ready, starting NetworkManager host...");
                OnStatusUpdate?.Invoke("Starting host...");
                NetworkManager.Singleton.StartHost();
                Debug.Log("[NetworkManager] Host started successfully!");
            }
            else
            {
                Debug.LogError("[NetworkManager] Failed to get join code from relay");
                OnStatusUpdate?.Invoke("Failed to create relay host. Check console for details.");
                OnConnectionStateChanged?.Invoke(false, false);
            }
        }
        else
        {
            Debug.Log("[NetworkManager] Starting host without relay (local mode)");
            StartHost();
        }
    }

    public async Task StartClientAsync(string joinCode = "")
    {
        if (useRelay && relayManager != null)
        {
            Debug.Log($"[NetworkManager] Starting client with relay (code: {joinCode})...");
            OnStatusUpdate?.Invoke("Joining relay...");
            
            bool success = await relayManager.JoinRelayWithCode(joinCode);
            
            if (success)
            {
                Debug.Log("[NetworkManager] Relay join successful, starting NetworkManager client...");
                OnStatusUpdate?.Invoke("Connecting to host...");
                NetworkManager.Singleton.StartClient();
            }
            else
            {
                Debug.LogError("[NetworkManager] Failed to join relay");
                OnStatusUpdate?.Invoke("Failed to join relay. Check console for details.");
                OnConnectionStateChanged?.Invoke(false, false);
            }
        }
        else
        {
            Debug.Log("[NetworkManager] Starting client without relay (local mode)");
            StartClient();
        }
    }
    public void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (IsPortInUse(transport.ConnectionData.Port))
        {
            OnStatusUpdate?.Invoke("Port is already in use.");
            return;
        }
        OnStatusUpdate?.Invoke("Starting Host...");
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (!IsPortInUse(transport.ConnectionData.Port))
        {
            OnStatusUpdate?.Invoke("Host not found. Port is not in use.");
            OnConnectionStateChanged?.Invoke(false, false);
            return;
        }

        OnStatusUpdate?.Invoke("Connecting...");
        NetworkManager.Singleton.StartClient();
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            bool isHost = NetworkManager.Singleton.IsHost;
            OnStatusUpdate?.Invoke(isHost ? "Host started." : "Connected to host.");
            OnConnectionStateChanged?.Invoke(true, isHost);
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            OnStatusUpdate?.Invoke("Disconnected.");
            OnConnectionStateChanged?.Invoke(false, false);
        }
    }

    private void HandleTransportFailure()
    {
        OnStatusUpdate?.Invoke("Failed to connect. Host not found or address is incorrect.");
        OnConnectionStateChanged?.Invoke(false, false);
    }

    private void HandleJoinCodeGenerated(string joinCode)
    {
        OnJoinCodeGenerated?.Invoke(joinCode);
    }

    private void HandleRelayError(string errorMessage)
    {
        OnStatusUpdate?.Invoke(errorMessage);
        OnConnectionStateChanged?.Invoke(false, false);
    }

    private bool IsPortInUse(ushort port)
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
        var udpConnInfoArray = ipGlobalProperties.GetActiveUdpListeners();
        if (tcpConnInfoArray.Any(l => l.Port == port)) return true;
        if (udpConnInfoArray.Any(l => l.Port == port)) return true;
        return false;
    }
}
