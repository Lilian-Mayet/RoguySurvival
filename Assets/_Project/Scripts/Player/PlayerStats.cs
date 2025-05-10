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

    [Header("Monster Spawning")]
    public GameObject monsterPrefab; // Faites glisser votre Prefab de monstre ici dans l'Inspecteur
    public int spawnRadius = 10; // Rayon en nombre de tuiles autour du joueur
    public int maxSpawnAttempts = 20; // Nombre de tentatives pour trouver une tuile valide

    private MapGeneratorV2 mapGenerator;
    private PlayerMovement playerMovement; // Pour la position et l'élévation actuelles du joueur

    void Start()
    {
        currentHealth = maxHealth;
        UpdateWeaponUI(); // Initial UI update (will be empty if no weapon)
        UpdateHealthUI(); // Appel initial pour afficher les PV

        // Récupérer les références nécessaires
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            mapGenerator = playerMovement.mapGenerator; // Obtenir via PlayerMovement si possible
        }
        else
        {
            Debug.LogError("PlayerStats: PlayerMovement script not found on this GameObject!");
            // Alternative: trouver MapGenerator globalement si PlayerMovement n'est pas là
            // mapGenerator = FindObjectOfType<MapGeneratorV2>();
        }

        if (mapGenerator == null)
        {
            Debug.LogError("PlayerStats: MapGeneratorV2 reference is missing!");
        }
        if (monsterPrefab == null)
        {
            Debug.LogWarning("PlayerStats: Monster Prefab is not assigned. Monster spawning will not work.");
        }
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

    
    void SpawnMonsterNearPlayer()
    {
        Vector3Int playerTile = mapGenerator.groundLayer.WorldToCell(transform.position); // Position actuelle du joueur en tuiles
        int playerElevation = playerMovement.GetCurrentElevation(); // Élévation actuelle du joueur

        Vector3Int spawnTile = Vector3Int.zero;
        bool foundValidSpawn = false;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Choisir une direction et une distance aléatoires
            int randomAngleDeg = Random.Range(0, 360);
            float randomRadius = Random.Range(1, spawnRadius + 1); // De 1 à spawnRadius inclus

            // Convertir en offset de tuiles (approximation, peut être affiné)
            // Pour une grille, il est souvent plus simple de choisir un offset x et y directement
            int offsetX = Random.Range(-spawnRadius, spawnRadius + 1);
            int offsetY = Random.Range(-spawnRadius, spawnRadius + 1);

            // S'assurer qu'on n'est pas exactement sur le joueur
            if (offsetX == 0 && offsetY == 0 && spawnRadius > 0) continue;


            Vector3Int potentialSpawnTile = new Vector3Int(playerTile.x + offsetX, playerTile.y + offsetY, 0);

            // Vérifier si la tuile est valide pour le spawn
            if (IsSpawnLocationValid(potentialSpawnTile, playerElevation)) // On essaie de spawner à la même élévation que le joueur
            {
                spawnTile = potentialSpawnTile;
                foundValidSpawn = true;
                break;
            }
        }

        if (foundValidSpawn)
        {
            Vector3 spawnWorldPosition = mapGenerator.groundLayer.GetCellCenterWorld(spawnTile);
            Instantiate(monsterPrefab, spawnWorldPosition, Quaternion.identity);
            Debug.Log($"Monster spawned at tile {spawnTile} (World: {spawnWorldPosition})");
        }
        else
        {
            Debug.LogWarning("Could not find a valid spawn location for the monster after " + maxSpawnAttempts + " attempts.");
        }
    }

    bool IsSpawnLocationValid(Vector3Int tileCoordinates, int targetElevation)
    {
        if (!mapGenerator.IsTileWithinBounds(tileCoordinates.x,tileCoordinates.y))
        {
            return false; // En dehors des limites de la carte
        }

        // Vérifier si la tuile est un mur
        if (mapGenerator.wallsLayer.GetTile(tileCoordinates) != null)
        {
            return false; // C'est un mur
        }

        // Vérifier s'il y a du sol ou un pont
        bool hasSurface = mapGenerator.groundLayer.GetTile(tileCoordinates) != null ||
                          (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(tileCoordinates) != null);
        if (!hasSurface)
        {
            return false; // C'est du vide
        }
        
        // Vérifier l'élévation
        // On veut spawner le monstre sur une tuile dont l'élévation de donnée correspond à targetElevation
        // (typiquement, l'élévation du joueur pour que le monstre soit au même niveau général).
        int tileDataElevation = mapGenerator.ElevationData[tileCoordinates.x, tileCoordinates.y];

        // Cas simple: le monstre doit spawner sur une tuile dont l'ElevationData correspond à l'élévation cible.
        if (tileDataElevation != targetElevation)
        {
            // Exception: si la cible est un pont (L1) et que targetElevation est L1, c'est bon.
            // Ou si la cible est du sol (L0) et que targetElevation est L0.
            bool isBridge = mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(tileCoordinates) != null;
            if (isBridge && targetElevation == 1 && tileDataElevation == 1) { // Pont à L1, le monstre doit spawner à L1
                return true;
            }
            // Si ce n'est pas un pont et que les élévations ne correspondent pas, c'est invalide.
            // Ou si c'est un pont (qui est toujours à ElevationData 1 par convention) mais qu'on veut spawner à L0
            if (!(isBridge && tileDataElevation == 1 && targetElevation == 0)) // Permettre de spawner sous un pont si targetElevation est L0
            {
                 return false;
            }
        }


        // Optionnel: Vérifier si la case est déjà occupée par un autre monstre ou le joueur
        // (nécessiterait une manière de suivre les positions des entités)

        return true; // La tuile est valide pour le spawn
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

        if (Input.GetKeyDown(KeyCode.U)) // Example: Equip Random Weapon
            {
                TakeDamage(2);
            }

        if (Input.GetKeyDown(KeyCode.I)) // Example: Equip Random Weapon
            {
                Heal(2);
            }
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (monsterPrefab != null && mapGenerator != null && playerMovement != null)
            {
                SpawnMonsterNearPlayer();
            }
            else
            {
                Debug.LogWarning("Cannot spawn monster: Prefab, MapGenerator, or PlayerMovement not set.");
            }
        }


}
}