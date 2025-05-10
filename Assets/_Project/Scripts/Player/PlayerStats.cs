// PlayerStats.cs
using UnityEngine;
using UnityEngine.UI; // For UI elements like Image
using TMPro; 

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Weapon")]
    public WeaponStats equippedWeaponStats; // Stores the stats of the currently equipped weapon
    private string equippedWeaponID = ""; // Store just the ID to re-fetch if needed

    [Header("UI References")]
    public Image weaponIconDisplay; // NOUVELLE LIGNE : Assignez votre "WeaponSlot_Icon" ici
    public TextMeshProUGUI healthTextDisplay;

    [Header("Health Bar Colors")]
    public Color healthFullColor = Color.green;
    public Color healthMidColor = Color.yellow; // Peut aussi être orange
    public Color healthLowColor = Color.red;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateWeaponUI(); // Initial UI update (will be empty if no weapon)
        UpdateHealthUI(); // Appel initial pour afficher les PV
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log($"Player took {amount} damage. Current HP: {currentHealth}/{maxHealth}");
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
         UpdateHealthUI(); // Mettre à jour l'affichage des PV
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        Debug.Log($"Player healed {amount}. Current HP: {currentHealth}/{maxHealth}");
        UpdateHealthUI(); // Mettre à jour l'affichage des PV
    }

    void Die()
    {
        Debug.Log("Player has died!");
        // Add game over logic, respawn, etc.
    }
void UpdateHealthUI()
    {
        if (healthTextDisplay == null)
        {
            Debug.LogWarning("PlayerStats: healthTextDisplay UI element is not assigned!");
            return;
        }

        // S'assurer que maxHealth n'est pas zéro pour éviter une division par zéro
        if (maxHealth <= 0) 
        {
            healthTextDisplay.text = $"<color=red>{currentHealth}</color><color=white>/{maxHealth}</color>";
            return;
        }

        float healthPercent = (float)currentHealth / maxHealth;
        healthPercent = Mathf.Clamp01(healthPercent); // S'assurer que le pourcentage est entre 0 et 1

        Color currentHealthColor;

        // Gradient Vert -> Jaune -> Rouge
        // Si healthPercent est entre 1.0 (vert) et 0.5 (jaune)
        if (healthPercent >= 0.5f)
        {
            // Interpoler entre healthFullColor (vert) et healthMidColor (jaune)
            // Le 't' pour Lerp doit aller de 0 (à 0.5 de vie) à 1 (à 1.0 de vie)
            // (healthPercent - 0.5f) va de 0 à 0.5
            // Donc on divise par 0.5 pour normaliser entre 0 et 1
            float t = (healthPercent - 0.5f) / 0.5f;
            currentHealthColor = Color.Lerp(healthMidColor, healthFullColor, t);
        }
        // Si healthPercent est entre 0.5 (jaune) et 0.0 (rouge)
        else
        {
            // Interpoler entre healthLowColor (rouge) et healthMidColor (jaune)
            // Le 't' pour Lerp doit aller de 0 (à 0.0 de vie) à 1 (à 0.5 de vie)
            // healthPercent va de 0 à 0.5
            // Donc on divise par 0.5 pour normaliser entre 0 et 1
            float t = healthPercent / 0.5f;
            currentHealthColor = Color.Lerp(healthLowColor, healthMidColor, t);
        }

        // Convertir la couleur en code hexadécimal pour TextMesh Pro Rich Text
        string currentColorHex = ColorUtility.ToHtmlStringRGB(currentHealthColor);

        // Construire la chaîne avec Rich Text
        healthTextDisplay.text = $"<color=#{currentColorHex}>{currentHealth}</color><color=white>/{maxHealth}</color>";
    }
    public void EquipWeapon(string weaponID)
    {
        if (string.IsNullOrEmpty(weaponID)) // Unequipping
        {
            equippedWeaponStats = null;
            equippedWeaponID = "";
            Debug.Log("Player unequipped weapon.");
        }
        else
        {
            WeaponStats statsToEquip = WeaponDataManager.Instance.GetWeaponStats(weaponID);
            if (statsToEquip != null)
            {
                equippedWeaponStats = statsToEquip;
                equippedWeaponID = weaponID;
                Debug.Log($"Player equipped: {equippedWeaponStats.itemName} of type {equippedWeaponStats.weaponType} ");
            }
            else
            {
                Debug.LogWarning($"PlayerStats: Attempted to equip non-existent weapon with ID: {weaponID}");
                // Optionally, unequip current weapon if the new one is invalid
                // equippedWeaponStats = null;
                // equippedWeaponID = "";
            }
        }
        UpdateWeaponUI();
    }

    public void UpdateWeaponUI()
    {
        if (weaponIconDisplay == null) return;

        if (equippedWeaponStats != null && !string.IsNullOrEmpty(equippedWeaponStats.iconSpriteName))
        {
            Sprite icon = WeaponDataManager.Instance.GetWeaponIcon(equippedWeaponStats.weaponType.ToString(), equippedWeaponStats.iconSpriteName);
            if (icon != null)
            {
                weaponIconDisplay.sprite = icon;
                weaponIconDisplay.enabled = true; // Make sure it's visible
            }
            else
            {
                weaponIconDisplay.sprite = null; // Clear sprite if icon not found
                weaponIconDisplay.enabled = false;
                Debug.LogWarning($"Could not find icon sprite for {equippedWeaponStats.iconSpriteName}");
            }
        }
        else // No weapon equipped or no icon name
        {
            weaponIconDisplay.sprite = null;
            weaponIconDisplay.enabled = false; // Hide if no weapon
        }
    }

    // Example of how the player might attack (you'll expand this)
    public void Attack()
    {
        if (equippedWeaponStats != null)
        {
            Debug.Log($"Player attacks with {equippedWeaponStats.itemName} for {equippedWeaponStats.damage} damage!");
            // Implement attack animation, hit detection, etc.
        }
        else
        {
            Debug.Log("Player attacks with fists (no weapon equipped)!");
            // Implement unarmed attack
        }
    }

    // For testing - equip a weapon on key press
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space)) // Example: Equip Random Weapon
            {
                if (WeaponDataManager.Instance != null && WeaponDataManager.Instance.weaponList.Count > 0)
                {
                    // Choisir un index aléatoire dans la liste des armes
                    int randomIndex = Random.Range(0, WeaponDataManager.Instance.weaponList.Count);
                    
                    // Récupérer l'ID de l'arme à cet index
                    string randomWeaponID = WeaponDataManager.Instance.weaponList[randomIndex].weaponID;
                    
                    EquipWeapon(randomWeaponID);
                    Debug.Log($"Equipped random weapon: {randomWeaponID} ({WeaponDataManager.Instance.GetWeaponStats(randomWeaponID)?.itemName})");
                }
                else
                {
                    Debug.LogWarning("WeaponDataManager not ready or weapon list is empty. Cannot equip random weapon.");
                }
            }

        if (Input.GetKeyDown(KeyCode.D)) // Example: Equip Random Weapon
            {
                TakeDamage(2);
            }

        if (Input.GetKeyDown(KeyCode.H)) // Example: Equip Random Weapon
            {
                Heal(2);
            }
}
}