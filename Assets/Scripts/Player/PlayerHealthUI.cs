using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text healthText;
    
    [Header("Text Settings")]
    [Tooltip("Font size for health text")]
    public float fontSize = 48f;
    
    [Header("Display Format")]
    [Tooltip("Show as 'HP: 100/100' or just '100'")]
    public bool showMaxHealth = true;
    public string healthPrefix = "HP: ";
    
    private PlayerHealth playerHealth;

    private void Start()
    {
        // Ховаємо текст поки не знайдемо гравця
        if (healthText != null)
        {
            healthText.fontSize = fontSize;
            healthText.gameObject.SetActive(false);
        }
        
        // Знаходимо локального гравця
        FindLocalPlayer();
    }

    private void FindLocalPlayer()
    {
        // Чекаємо поки з'явиться локальний гравець
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                SetupPlayer(localPlayer.GetComponent<PlayerHealth>());
                return;
            }
        }
        
        // Якщо не знайшли, спробуємо знайти пізніше
        Invoke(nameof(FindLocalPlayer), 0.5f);
    }

    private void SetupPlayer(PlayerHealth health)
    {
        if (health == null) return;
        
        // Відписуємось від старого гравця
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
        }
        
        playerHealth = health;
        playerHealth.OnHealthChanged += UpdateHealthDisplay;
        
        // Вмикаємо текст здоров'я
        if (healthText != null)
        {
            healthText.gameObject.SetActive(true);
        }
        
        // Оновлюємо дисплей відразу
        UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.maxHealth);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
        }
    }

    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (healthText == null) return;
        
        if (showMaxHealth)
        {
            healthText.text = $"{healthPrefix}{currentHealth}/{maxHealth}";
        }
        else
        {
            healthText.text = $"{healthPrefix}{currentHealth}";
        }
        
        // Змінюємо колір в залежності від здоров'я
        float healthPercent = (float)currentHealth / maxHealth;
        if (healthPercent > 0.6f)
        {
            healthText.color = Color.green;
        }
        else if (healthPercent > 0.3f)
        {
            healthText.color = Color.yellow;
        }
        else
        {
            healthText.color = Color.red;
        }
    }

    // Можна викликати для зміни розміру в runtime
    public void SetFontSize(float newSize)
    {
        fontSize = newSize;
        if (healthText != null)
        {
            healthText.fontSize = fontSize;
        }
    }
}
