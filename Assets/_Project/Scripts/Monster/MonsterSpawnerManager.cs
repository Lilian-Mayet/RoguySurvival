// MonsterSpawnerManager.cs
using UnityEngine;
using System.Collections.Generic; // Required for Lists

public class MonsterSpawnerManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    public List<GameObject> monsterPrefabs; // List of monster prefabs to choose from
    public float spawnInterval = 20f;      // Time between spawn attempts
    public int spawnRadius = 15;           // Max tile distance from player to spawn
    public int maxSpawnAttemptsPerInterval = 20; // How many times to try finding a valid spot

    [Header("References")]
    public Transform playerTransform;      // Reference to the player's transform
    public MapGeneratorV2 mapGenerator;    // Reference to the map generator
    // PlayerMovement is needed to get the player's current elevation
    private PlayerMovement playerMovementScript;

    private float spawnTimer;

    void Start()
    {
        // --- Validate Prefabs ---
        if (monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            Debug.LogError("MonsterSpawnerManager: No monster prefabs assigned! Spawning will not work.", this);
            enabled = false; // Disable the spawner
            return;
        }
        // Remove any null entries from the list to prevent errors
        monsterPrefabs.RemoveAll(item => item == null);
        if (monsterPrefabs.Count == 0)
        {
            Debug.LogError("MonsterSpawnerManager: All assigned monster prefabs were null! Spawning will not work.", this);
            enabled = false;
            return;
        }

        // --- Get Player References ---
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerMovementScript = playerObj.GetComponent<PlayerMovement>();
            }
        } else {
            playerMovementScript = playerTransform.GetComponent<PlayerMovement>();
        }

        if (playerTransform == null || playerMovementScript == null)
        {
            Debug.LogError("MonsterSpawnerManager: Player Transform or PlayerMovement script not found! Tag your player with 'Player' or assign manually.", this);
            enabled = false;
            return;
        }

        // --- Get Map Generator Reference ---
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGeneratorV2>();
            if (mapGenerator == null)
            {
                Debug.LogError("MonsterSpawnerManager: MapGeneratorV2 not found in the scene!", this);
                enabled = false;
                return;
            }
        }

        spawnTimer = spawnInterval; // Initialize timer to spawn after the first interval
        Debug.Log("MonsterSpawnerManager initialized. Will start spawning monsters.");
    }

    void Update()
    {
        if (playerTransform == null || mapGenerator == null) return; // Essential references missing

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnMonsterNearPlayer();
            spawnTimer = spawnInterval; // Reset timer
        }
    }

    void SpawnMonsterNearPlayer()
    {
        if (monsterPrefabs.Count == 0) return; // No prefabs to spawn

        // 1. Get player's current tile and elevation
        Vector3Int playerTile = mapGenerator.groundLayer.WorldToCell(playerTransform.position);
        int playerElevation = playerMovementScript.GetCurrentElevation();

        Vector3Int spawnTile = Vector3Int.zero;
        bool foundValidSpawn = false;
        GameObject selectedMonsterPrefab = null;

        // 2. Try to find a valid spawn location
        for (int attempt = 0; attempt < maxSpawnAttemptsPerInterval; attempt++)
        {
            // Choose a random offset within the spawnRadius
            int offsetX = Random.Range(-spawnRadius, spawnRadius + 1);
            int offsetY = Random.Range(-spawnRadius, spawnRadius + 1);

            // Ensure not spawning directly on player tile (if radius is > 0)
            if (offsetX == 0 && offsetY == 0 && spawnRadius > 0) continue;

            Vector3Int potentialSpawnTile = new Vector3Int(playerTile.x + offsetX, playerTile.y + offsetY, 0);

            // 3. Check if the potential tile is valid for spawning (using player's current elevation)
            if (IsSpawnLocationValid(potentialSpawnTile, playerElevation))
            {
                spawnTile = potentialSpawnTile;
                foundValidSpawn = true;
                break;
            }
        }

        // 4. If a valid location is found, spawn a monster
        if (foundValidSpawn)
        {
            // Select a random monster prefab from the list
            selectedMonsterPrefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];

            Vector3 spawnWorldPosition = mapGenerator.groundLayer.GetCellCenterWorld(spawnTile);

            // Instantiate the selected monster
            GameObject newMonster = Instantiate(selectedMonsterPrefab, spawnWorldPosition, Quaternion.identity);
            Debug.Log($"MonsterSpawnerManager: Spawned '{selectedMonsterPrefab.name}' at tile {spawnTile} (World: {spawnWorldPosition}) near player.");

            // Optional: You might want to set the monster's initial elevation explicitly if its Start() method doesn't handle it robustly.
            // MonsterAI monsterAI = newMonster.GetComponent<MonsterAI>();
            // if (monsterAI != null) {
            //    monsterAI.InitializeElevation(mapGenerator.ElevationData[spawnTile.x, spawnTile.y]);
            // }
        }
        else
        {
            Debug.LogWarning($"MonsterSpawnerManager: Could not find a valid spawn location for any monster after {maxSpawnAttemptsPerInterval} attempts near player tile {playerTile} at elevation {playerElevation}.");
        }
    }

    bool IsSpawnLocationValid(Vector3Int tileCoordinates, int targetElevation)
    {
        // Check 1: Within map bounds
        if (!mapGenerator.IsTileWithinBounds(tileCoordinates.x, tileCoordinates.y))
        {
            // Debug.Log($"Spawn invalid: {tileCoordinates} out of bounds.");
            return false;
        }

        // Check 2: Not a wall
        if (mapGenerator.wallsLayer.GetTile(tileCoordinates) != null)
        {
            // Debug.Log($"Spawn invalid: {tileCoordinates} is a wall.");
            return false;
        }

        // Check 3: Must have a walkable surface (ground or bridge)
        bool hasGround = mapGenerator.groundLayer.GetTile(tileCoordinates) != null;
        bool hasBridge = (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(tileCoordinates) != null);
        if (!hasGround && !hasBridge)
        {
            // Debug.Log($"Spawn invalid: {tileCoordinates} has no ground or bridge.");
            return false;
        }

        // Check 4: Elevation must match targetElevation (typically player's elevation)
        int tileDataElevation = mapGenerator.ElevationData[tileCoordinates.x, tileCoordinates.y];

        if (hasBridge) // If there's a bridge, it's L1 data, and defines the surface.
        {
            if (targetElevation != 1)
            {
                // Trying to spawn at L0 but there's a bridge L1. Invalid.
                // Debug.Log($"Spawn invalid: {tileCoordinates} is bridge (L1), target elev is {targetElevation}.");
                return false;
            }
            // If targetElevation IS 1 and hasBridge IS true, this is valid.
        }
        else // No bridge, so rely on ground tile's ElevationData
        {
            if (tileDataElevation != targetElevation)
            {
                // Debug.Log($"Spawn invalid: {tileCoordinates} data elev {tileDataElevation}, target elev is {targetElevation}.");
                return false;
            }
        }

        // Check 5 (Optional but good): Check for existing colliders (player, other monsters)
        // This is a simple check, for more robust checks you might need a grid of occupied cells.
        Collider2D[] colliders = Physics2D.OverlapCircleAll(mapGenerator.groundLayer.GetCellCenterWorld(tileCoordinates), 0.4f); // Small radius
        foreach (Collider2D col in colliders)
        {
            if (col.CompareTag("Player") || col.CompareTag("Monster")) // Ensure your player and monsters have these tags
            {
                // Debug.Log($"Spawn invalid: {tileCoordinates} is already occupied by {col.tag}.");
                return false;
            }
        }

        // Debug.Log($"Spawn VALID: {tileCoordinates} for target elevation {targetElevation}");
        return true;
    }
}