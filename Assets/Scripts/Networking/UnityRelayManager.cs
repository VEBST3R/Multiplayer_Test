using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public class UnityRelayManager : MonoBehaviour
{
    public event Action<string> OnRelayJoinCodeGenerated;
    public event Action<string> OnRelayError;
    public event Action OnRelayInitialized;

    private const int MAX_CONNECTIONS = 10;
    private bool isInitialized = false;
    private bool isInitializing = false;

    private void Start()
    {
        // Для WebGL краще використовувати Coroutine
        StartCoroutine(InitializeUnityServicesCoroutine());
    }

    private IEnumerator InitializeUnityServicesCoroutine()
    {
        if (isInitializing || isInitialized)
        {
            Debug.Log("[Relay] Already initializing or initialized");
            yield break;
        }

        isInitializing = true;
        Debug.Log("[Relay] Starting Unity Services initialization via Coroutine...");

        // Викликаємо async метод і чекаємо його завершення
        var initTask = InitializeUnityServicesAsync();
        
        // Чекаємо поки Task не завершиться (WebGL-friendly спосіб)
        while (!initTask.IsCompleted)
        {
            yield return null;
        }

        isInitializing = false;

        if (initTask.Exception != null)
        {
            Debug.LogError($"[Relay] Initialization failed: {initTask.Exception.Message}");
        }
    }

    private async Task InitializeUnityServicesAsync()
    {
        try
        {
            Debug.Log("[Relay] [Async] Starting Unity Services initialization...");
            
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[Relay] [Async] Initializing Unity Services...");
                await UnityServices.InitializeAsync();
                Debug.Log("[Relay] [Async] Unity Services initialized");
            }
            else
            {
                Debug.Log($"[Relay] [Async] Unity Services already in state: {UnityServices.State}");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[Relay] [Async] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[Relay] [Async] Signed in successfully");
            }
            else
            {
                Debug.Log("[Relay] [Async] Already signed in");
            }

            isInitialized = true;
            OnRelayInitialized?.Invoke();
            Debug.Log("[Relay] ✓ Unity Services fully initialized and ready");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] ✗ Failed to initialize Unity Services: {e.Message}\n{e.StackTrace}");
            OnRelayError?.Invoke($"Initialization failed: {e.Message}");
        }
    }

    public async Task<string> StartHostWithRelay()
    {
        Debug.Log("[Relay] StartHostWithRelay called");
        
        if (!isInitialized)
        {
            Debug.LogWarning("[Relay] Unity Services not initialized yet, waiting...");
            OnRelayError?.Invoke("Unity Services not initialized yet. Please wait...");
            
            // Чекаємо до 15 секунд на ініціалізацію (більше часу для WebGL)
            int attempts = 0;
            int maxAttempts = 30; // 30 * 500ms = 15 секунд
            
            while (!isInitialized && attempts < maxAttempts)
            {
                await Task.Yield(); // WebGL-friendly спосіб
                await Task.Delay(500);
                attempts++;
                
                if (attempts % 4 == 0) // Кожні 2 секунди
                {
                    Debug.Log($"[Relay] Still waiting for initialization... ({attempts * 0.5f}s)");
                }
            }
            
            if (!isInitialized)
            {
                Debug.LogError("[Relay] Timeout waiting for Unity Services initialization after 15 seconds");
                OnRelayError?.Invoke("Unity Services initialization timeout. Check Project ID in Edit→Project Settings→Services");
                return null;
            }
            
            Debug.Log("[Relay] Initialization complete, proceeding with relay creation");
        }

        try
        {
            Debug.Log("[Relay] Creating relay allocation...");
            
            // Створюємо allocation для всіх платформ однаково
            var allocationTask = RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            
            // Даємо Task запуститись
            await Task.Yield();
            
            Allocation allocation = await allocationTask;
            Debug.Log($"[Relay] ✓ Allocation created: {allocation.AllocationId}");
            
            // Логуємо всі доступні endpoints
            Debug.Log($"[Relay] Available endpoints: {allocation.ServerEndpoints.Count}");
            foreach (var endpoint in allocation.ServerEndpoints)
            {
                Debug.Log($"[Relay] - Endpoint: {endpoint.ConnectionType} on {endpoint.Host}:{endpoint.Port} (Secure: {endpoint.Secure})");
            }

            Debug.Log("[Relay] Getting join code...");
            var joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            await Task.Yield();
            
            string joinCode = await joinCodeTask;
            Debug.Log($"[Relay] ✓ Join code received: {joinCode}");

            Debug.Log("[Relay] Configuring transport...");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // Для WebGL використовуємо RelayServerData (важливо для фіксу помилки ConnectionType mismatch)
            Debug.Log("[Relay] WebGL detected - configuring for WSS via RelayServerData");
            
            // RelayServerData автоматично знайде 'wss' endpoint в allocation
            var relayServerData = new RelayServerData(allocation, "wss");
            
            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;
            
            Debug.Log($"[Relay] ✓ WebSocket transport configured using RelayServerData (WSS)");
#else
            // Для не-WebGL платформ використовуємо UDP/DTLS
            Debug.Log("[Relay] Non-WebGL platform - using default DTLS");
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );
            Debug.Log($"[Relay] ✓ DTLS transport configured");
#endif

            OnRelayJoinCodeGenerated?.Invoke(joinCode);
            Debug.Log($"[Relay] ✓ READY! Join Code: {joinCode}");
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] ✗ Failed to start host with relay: {e.Message}\n{e.StackTrace}");
            OnRelayError?.Invoke($"Failed to create relay: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRelayWithCode(string joinCode)
    {
        Debug.Log($"[Relay] JoinRelayWithCode called with code: {joinCode}");
        
        if (!isInitialized)
        {
            Debug.LogWarning("[Relay] Unity Services not initialized yet, waiting...");
            OnRelayError?.Invoke("Unity Services not initialized yet. Please wait...");
            
            // Чекаємо до 15 секунд на ініціалізацію (більше часу для WebGL)
            int attempts = 0;
            int maxAttempts = 30; // 30 * 500ms = 15 секунд
            
            while (!isInitialized && attempts < maxAttempts)
            {
                await Task.Yield(); // WebGL-friendly спосіб
                await Task.Delay(500);
                attempts++;
                
                if (attempts % 4 == 0) // Кожні 2 секунди
                {
                    Debug.Log($"[Relay] Still waiting for initialization... ({attempts * 0.5f}s)");
                }
            }
            
            if (!isInitialized)
            {
                Debug.LogError("[Relay] Timeout waiting for Unity Services initialization");
                OnRelayError?.Invoke("Unity Services initialization timeout. Check Project ID in Edit→Project Settings→Services");
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Relay] Join code is empty");
            OnRelayError?.Invoke("Join code cannot be empty");
            return false;
        }

        try
        {
            Debug.Log($"[Relay] Joining allocation with code: {joinCode}");
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Debug.Log($"[Relay] ✓ Joined allocation: {allocation.AllocationId}");
            
            Debug.Log("[Relay] Configuring client transport...");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // Для WebGL використовуємо RelayServerData для клієнта
            Debug.Log("[Relay] WebGL detected - configuring Client for WSS via RelayServerData");
            
            var relayServerData = new RelayServerData(allocation, "wss");
            
            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;
            
            Debug.Log($"[Relay] ✓ WebSocket client transport configured using RelayServerData (WSS)");
#else
            // Для не-WebGL платформ використовуємо UDP/DTLS
            Debug.Log("[Relay] Non-WebGL platform - using default DTLS");
            transport.SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );
            Debug.Log($"[Relay] ✓ DTLS client transport configured");
#endif

            Debug.Log("[Relay] ✓ Successfully joined relay");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] ✗ Failed to join relay: {e.Message}\n{e.StackTrace}");
            OnRelayError?.Invoke($"Failed to join: {e.Message}");
            return false;
        }
    }
}
