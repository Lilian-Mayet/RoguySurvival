// PlayerStats.cs
using UnityEngine;
using UnityEngine.UI; // For UI elements like Image

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

    void Start()
    {
        currentHealth = maxHealth;
        UpdateWeaponUI(); // Initial UI update (will be empty if no weapon)
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
        // Here you would update any health UI
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        Debug.Log($"Player healed {amount}. Current HP: {currentHealth}/{maxHealth}");
        // Here you would update any health UI
    }

    void Die()
    {
        Debug.Log("Player has died!");
        // Add game over logic, respawn, etc.
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

        if (Input.GetKeyDown(KeyCode.Space)) // Example attack
        {
            EquipWeapon("hammer_021_silver_smacker"); // Make sure this ID exists in your JSON
        }

    }
}