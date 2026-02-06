using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
public class NetworkUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button connectButton;
    public Button disconnectButton;
    public Button exitButton;
    public GameObject menuPanel;
    public TMP_Text statusText;

    [Header("UI Sections")]
    public GameObject connectSection;
    public GameObject copyCodeSection;

    [Header("Relay UI")]
    public TMP_InputField joinCodeInputField;
    public TMP_Text joinCodeDisplayText;
    public Button copyJoinCodeButton;

    [Header("Dependencies")]
    public NetworkConnectionManager connectionManager;
    private bool wasNetworkActive = false;
    private bool isMenuPanelVisible = true;
    private void Start()
    {
        if (hostButton != null) hostButton.onClick.AddListener(OnHostButtonClicked);
        if (connectButton != null) connectButton.onClick.AddListener(OnConnectButtonClicked);
        if (disconnectButton != null) disconnectButton.onClick.AddListener(() => connectionManager.Disconnect());
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        if (copyJoinCodeButton != null) copyJoinCodeButton.onClick.AddListener(CopyJoinCodeToClipboard);

        if (connectionManager != null)
        {
            connectionManager.OnStatusUpdate += HandleStatusUpdate;
            connectionManager.OnConnectionStateChanged += HandleConnectionStateChanged;
            connectionManager.OnJoinCodeGenerated += HandleJoinCodeGenerated;
        }

        UpdateUI(false, false);
        UpdateCursorState(true);
        
        if (joinCodeDisplayText != null) joinCodeDisplayText.text = "";
        if (copyCodeSection != null) copyCodeSection.SetActive(false);
    }

    private async void OnHostButtonClicked()
    {
        await connectionManager.StartHostAsync();
    }

    private async void OnConnectButtonClicked()
    {
        if (joinCodeInputField == null)
        {
            if (statusText != null) statusText.text = "Input field not found!";
            return;
        }

        string joinCode = joinCodeInputField.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(joinCode))
        {
            if (statusText != null) statusText.text = "Please enter a join code!";
            return;
        }

        if (joinCode.Length != 6)
        {
            if (statusText != null) statusText.text = "Join code must be 6 characters!";
            return;
        }

        await connectionManager.StartClientAsync(joinCode);
    }

    private void OnDestroy()
    {
        if (connectionManager != null)
        {
            connectionManager.OnStatusUpdate -= HandleStatusUpdate;
            connectionManager.OnConnectionStateChanged -= HandleConnectionStateChanged;
            connectionManager.OnJoinCodeGenerated -= HandleJoinCodeGenerated;
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleMenu();
            }
        }
    }

    private void HandleStatusUpdate(string status)
    {
        if (statusText != null) statusText.text = status;
    }

    private void HandleConnectionStateChanged(bool isConnected, bool isHost)
    {
        isMenuPanelVisible = !isConnected;
        UpdateUI(isConnected, isHost);
        UpdateCursorState(isMenuPanelVisible);
    }

    private void HandleJoinCodeGenerated(string joinCode)
    {
        if (joinCodeDisplayText != null)
        {
            joinCodeDisplayText.text = joinCode;
        }
        // CopyCodeSection тепер керується через UpdateUI (показується тільки для хоста)
    }

    private void CopyJoinCodeToClipboard()
    {
        if (joinCodeDisplayText != null && !string.IsNullOrEmpty(joinCodeDisplayText.text))
        {
            string joinCode = joinCodeDisplayText.text;
            GUIUtility.systemCopyBuffer = joinCode;
            if (statusText != null)
            {
                statusText.text = "Join code copied!";
            }
        }
    }

    private void UpdateUI(bool isConnected, bool isHost)
    {
        if (menuPanel != null) menuPanel.SetActive(isMenuPanelVisible);

        if (!isConnected)
        {
            // Показуємо головне меню
            if (hostButton != null) hostButton.gameObject.SetActive(isMenuPanelVisible);
            if (connectSection != null) connectSection.SetActive(isMenuPanelVisible);
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(isMenuPanelVisible);
            if (copyCodeSection != null) copyCodeSection.SetActive(false);
            if (statusText != null) statusText.text = "Disconnected";
        }
        else if (isMenuPanelVisible)
        {
            // Підключено і меню відкрите (ESC)
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (connectSection != null) connectSection.SetActive(false);
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(!isHost);
            if (exitButton != null) exitButton.gameObject.SetActive(true);
            // CopyCodeSection показуємо тільки для хоста
            if (copyCodeSection != null) copyCodeSection.SetActive(isHost);
        }
        else
        {
            // Підключено і меню закрите (в грі)
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (connectSection != null) connectSection.SetActive(false);
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            if (copyCodeSection != null) copyCodeSection.SetActive(false);
        }
    }

    private void ToggleMenu()
    {
        isMenuPanelVisible = !isMenuPanelVisible;
        UpdateUI(NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost, NetworkManager.Singleton.IsHost);
        UpdateCursorState(isMenuPanelVisible);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null) hostButton.interactable = interactable;
        if (connectButton != null) connectButton.interactable = interactable;
    }

    private void UpdateCursorState(bool isVisible)
    {
        Cursor.visible = isVisible;
        Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void ExitGame()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
