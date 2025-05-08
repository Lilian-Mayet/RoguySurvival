using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f; // Vitesse de déplacement en unités mondiales par seconde
    public float collisionSkinWidth = 0.02f; // Petite marge pour éviter de se coincer avec BoxCast

    [Header("References")]
    public MapGeneratorV2 mapGenerator; // Assignez dans l'Inspecteur

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector3Int currentLogicalTile;  // La tuile sur laquelle le joueur est logiquement (centre)
    private int currentPlayerElevation = 0; // 0 pour base, 1 pour plateau/pont
    private bool isMovingBetweenTiles = false; // Si le joueur est en transition entre les centres de tuiles
    private Vector3 targetWorldPositionForLerp; // Position mondiale cible pour l'interpolation
    private int pendingElevationOnArrival; // Utilisé pour stocker l'élévation cible pendant le lerp

    [Header("Spawning")]
    public Vector2Int initialSpawnTileGuess = new Vector2Int(100, 100);
    public int maxSpawnSearchRadius = 20;

    public LayerMask wallCollisionLayerMask;
    private Collider2D playerCollider;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            Debug.LogError("PlayerMovement requires a Collider2D component on the player.");
            enabled = false;
            return;
        }
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (mapGenerator == null)
        {
            Debug.LogError("MapGeneratorV2 reference not set!");
            enabled = false;
            return;
        }

        if (!FindValidSpawnPosition(out currentLogicalTile, out currentPlayerElevation))
        {
            Debug.LogError("COULD NOT FIND VALID SPAWN POSITION.");
            currentLogicalTile = new Vector3Int(mapGenerator.mapWidth / 2, mapGenerator.mapHeight / 2, 0);
            if (mapGenerator.ElevationData != null && IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y))
                currentPlayerElevation = mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y];
            else
                currentPlayerElevation = 0;
        }
        transform.position = mapGenerator.groundLayer.GetCellCenterWorld(currentLogicalTile);
        targetWorldPositionForLerp = transform.position;
        pendingElevationOnArrival = currentPlayerElevation; // Initialisation
        Debug.Log($"Player spawned at tile: {currentLogicalTile}, Elevation: {currentPlayerElevation}");
    }

    void Update()
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null) return;
        HandleInput();

        if (!isMovingBetweenTiles)
        {
            TryInitiateMove();
        }
    }

    void FixedUpdate()
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null) return;

        if (isMovingBetweenTiles)
        {
            PerformInterpolatedMove();
        }
        else
        {
            ApplyContinuousMovement();
        }
    }

    void HandleInput()
    {
        movementInput = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) movementInput.y += 1;
        if (Input.GetKey(KeyCode.S)) movementInput.y -= 1;
        if (Input.GetKey(KeyCode.A)) movementInput.x -= 1;
        if (Input.GetKey(KeyCode.D)) movementInput.x += 1;

        if (movementInput.sqrMagnitude > 0.1f)
        {
            movementInput.Normalize();
        }
    }

    void TryInitiateMove()
    {
        if (movementInput == Vector2.zero) return;

        Vector3Int nextIntentTile = currentLogicalTile;
        if (Mathf.Abs(movementInput.x) > Mathf.Abs(movementInput.y))
        {
            nextIntentTile.x += (movementInput.x > 0) ? 1 : -1;
        }
        else
        {
            nextIntentTile.y += (movementInput.y > 0) ? 1 : -1;
        }

        if (nextIntentTile != currentLogicalTile)
        {
            if (CanLogicallyMoveToTile(nextIntentTile, out int newElevationForNextTile))
            {
                targetWorldPositionForLerp = mapGenerator.groundLayer.GetCellCenterWorld(nextIntentTile);
                isMovingBetweenTiles = true;
                pendingElevationOnArrival = newElevationForNextTile; // CORRECTION BUG 3: Stocker l'élévation cible
            }
        }
    }

    void PerformInterpolatedMove()
    {
        Vector3 newPos = Vector3.MoveTowards(rb.position, targetWorldPositionForLerp, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        if (Vector3.Distance(rb.position, targetWorldPositionForLerp) < 0.05f)
        {
            rb.MovePosition(targetWorldPositionForLerp);
            isMovingBetweenTiles = false;

            currentLogicalTile = mapGenerator.groundLayer.WorldToCell(targetWorldPositionForLerp);
            currentPlayerElevation = pendingElevationOnArrival; // CORRECTION BUG 3: Appliquer l'élévation stockée
            // Debug.Log($"Arrived at tile: {currentLogicalTile}, New Elevation: {currentPlayerElevation}");
        }
    }

    void ApplyContinuousMovement()
    {
        if (movementInput == Vector2.zero) return;

        Vector2 currentPos = rb.position;
        Vector2 moveDelta = movementInput * moveSpeed * Time.fixedDeltaTime;
        
        // CORRECTION BUG 1: Utilisation de BoxCast pour une meilleure détection de collision
        Vector2 boxCastSize = playerCollider.bounds.size * 0.95f; // Légèrement plus petit pour éviter de se coincer sur des bords parfaits
        
        RaycastHit2D hit = Physics2D.BoxCast(
            currentPos,
            boxCastSize,
            0f,
            movementInput.normalized, // Direction normalisée du mouvement
            moveDelta.magnitude,      // Distance à parcourir ce frame
            wallCollisionLayerMask
        );

        Vector2 targetPos;

        if (hit.collider != null) // Collision physique détectée par BoxCast
        {
            // Se déplacer seulement jusqu'à la collision, moins une petite marge (skin width)
            float distanceToHit = Mathf.Max(0, hit.distance - collisionSkinWidth);
            targetPos = currentPos + movementInput.normalized * distanceToHit;
            // Debug.Log($"Blocked by physical wall via BoxCast. Moving {distanceToHit} of {moveDelta.magnitude}");
        }
        else // Pas de mur physique détecté par BoxCast
        {
            targetPos = currentPos + moveDelta;
        }

        // Vérification logique supplémentaire (falaise, etc.) si on change de tuile
        Vector3Int tileAtPotentialTargetPos = mapGenerator.groundLayer.WorldToCell(targetPos);
        if (tileAtPotentialTargetPos == currentLogicalTile || CanLogicallyMoveToTile(tileAtPotentialTargetPos, out int _))
        {
            rb.MovePosition(targetPos);
        }
        else
        {
            // Bloqué par la logique de grille (falaise, etc.), on ne bouge pas plus loin dans cette direction
            // On pourrait essayer de glisser le long du bord de la tuile actuelle.
            // Pour l'instant, on s'assure de ne pas dépasser les limites logiques si BoxCast n'a rien trouvé
            // (par exemple, si wallCollisionLayerMask n'est pas sur les tuiles de falaise)
            // Essayer de bouger le long de l'axe autorisé si un seul est bloqué logiquement.
            // Ceci est une simplification, un vrai "slide" serait plus complexe.
            if (hit.collider == null) // Seulement si pas déjà bloqué par un mur physique
            {
                // Tenter de ne bouger que sur l'axe X si Y est bloqué logiquement, et vice-versa
                Vector2 testPosX = currentPos + new Vector2(moveDelta.x, 0);
                Vector3Int tileAtTestPosX = mapGenerator.groundLayer.WorldToCell(testPosX);
                if (tileAtTestPosX == currentLogicalTile || CanLogicallyMoveToTile(tileAtTestPosX, out int _))
                {
                    rb.MovePosition(testPosX);
                    return;
                }

                Vector2 testPosY = currentPos + new Vector2(0, moveDelta.y);
                Vector3Int tileAtTestPosY = mapGenerator.groundLayer.WorldToCell(testPosY);
                 if (tileAtTestPosY == currentLogicalTile || CanLogicallyMoveToTile(tileAtTestPosY, out int _))
                {
                    rb.MovePosition(testPosY);
                    return;
                }
            }
        }
    }

    bool CanLogicallyMoveToTile(Vector3Int targetLogicalTile, out int newElevation)
    {
        newElevation = currentPlayerElevation;

        if (!IsTileWithinBounds(targetLogicalTile.x, targetLogicalTile.y)) return false;

        TileBase groundTileAtTarget = mapGenerator.groundLayer.GetTile(targetLogicalTile);
        TileBase stairsTileAtTarget = mapGenerator.stairsLayer.GetTile(targetLogicalTile);
        TileBase bridgeTileAtTarget = (mapGenerator.bridgeLayer != null) ? mapGenerator.bridgeLayer.GetTile(targetLogicalTile) : null;
        TileBase wallTileAtTarget = mapGenerator.wallsLayer.GetTile(targetLogicalTile); // Peut être null

        int targetTileActualElevationData = mapGenerator.ElevationData[targetLogicalTile.x, targetLogicalTile.y];

        // --- Interaction avec les Escaliers ---
        if (stairsTileAtTarget != null)
        {
            if (currentPlayerElevation == 0 && targetTileActualElevationData == 0) // Tente de MONTER
            {
                Vector3Int platformAboveStairPos = new Vector3Int(targetLogicalTile.x, targetLogicalTile.y + 1, 0); // Pour stair_S
                if (IsTileWithinBounds(platformAboveStairPos.x, platformAboveStairPos.y) &&
                    mapGenerator.ElevationData[platformAboveStairPos.x, platformAboveStairPos.y] == 1)
                {
                    newElevation = 1;
                    return true;
                }
            }
            else if (currentPlayerElevation == 1 && targetTileActualElevationData == 0) // Tente de DESCENDRE
            {
                if (currentLogicalTile.x == targetLogicalTile.x && currentLogicalTile.y == targetLogicalTile.y + 1) // Pour stair_S
                {
                    newElevation = 0;
                    return true;
                }
            }
            // Si on est déjà sur l'escalier et qu'on veut juste actualiser l'élévation (cas géré par pendingElevationOnArrival)
            // ou si l'interaction d'escalier n'est pas valide pour un *déplacement*
            // Debug.Log($"Stair interaction invalid or re-eval at {targetLogicalTile}. CurrentElev: {currentPlayerElevation}, TargetElevData: {targetTileActualElevationData}");
            return false; // Bloque si c'est une interaction d'escalier non valide pour un mouvement
        }

        // CORRECTION BUG 2: Vérification des ponts AVANT les murs pour le cas où un pont passe au-dessus d'un mur.
        // --- Interaction avec les Ponts ---
        if (bridgeTileAtTarget != null)
        {
            if (currentPlayerElevation == 1) // Le joueur est au Niveau 1, il peut marcher sur le pont.
            {
                newElevation = 1;
                return true; // Le pont est praticable au Niveau 1, peu importe ce qu'il y a dessous
            }
            else // currentPlayerElevation == 0. Le joueur est en dessous du pont.
            {
                // Le pont lui-même ne bloque pas. Vérifier si un MUR est SOUS le pont.
                if (wallTileAtTarget == mapGenerator.cliffWall_S) // Assumant que cliffWall_S est votre tuile de mur spécifique
                {
                    // Debug.Log($"Blocked by wall under bridge at {targetLogicalTile}");
                    return false; // Mur sous le pont, joueur au sol = bloqué
                }
                // Sinon, vérifier si le SOL SOUS le pont est praticable.
                if (groundTileAtTarget != null && targetTileActualElevationData == 0)
                {
                    newElevation = 0;
                    return true;
                }
                else
                {
                    // Debug.Log($"Blocked: No ground under bridge or mismatch elev at {targetLogicalTile}");
                    return false;
                }
            }
        }

        // --- Vérification des Murs (si pas d'escalier ni de pont géré activement) ---
        if (wallTileAtTarget == mapGenerator.cliffWall_S) // Assumant que cliffWall_S est votre tuile de mur spécifique
        {
            // Debug.Log($"Blocked by wall at {targetLogicalTile}");
            return false;
        }

        // --- Mouvement sur Sol Normal / Gestion des Falaises ---
        if (targetTileActualElevationData == currentPlayerElevation)
        {
            if (groundTileAtTarget != null)
            {
                return true;
            }
            else
            {
                // Debug.Log($"Blocked: Target tile at same elevation {targetLogicalTile} has no ground tile.");
                return false;
            }
        }
        else if (Mathf.Abs(targetTileActualElevationData - currentPlayerElevation) == 1) // Différence d'élévation de 1
        {
            // C'est une falaise (escaliers et ponts déjà gérés)
            // Debug.Log($"Blocked: Cliff interaction from L{currentPlayerElevation} to L{targetTileActualElevationData} at {targetLogicalTile}");
            return false;
        }
        
        // Debug.Log($"Blocked: Default fall-through or unhandled elevation diff at {targetLogicalTile}. CurrentElev: {currentPlayerElevation}, TargetActualElev: {targetTileActualElevationData}");
        return false;
    }

    // --- Fonctions de Spawn (inchangées) ---
    bool FindValidSpawnPosition(out Vector3Int foundPosition, out int foundElevation)
    {
        foundPosition = Vector3Int.zero; foundElevation = 0;
        if (mapGenerator.ElevationData == null) { Debug.LogError("ElevationData null."); return false; }
        Vector3Int testPos = new Vector3Int(initialSpawnTileGuess.x, initialSpawnTileGuess.y, 0);
        if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
        for (int r = 1; r <= maxSpawnSearchRadius; r++) {
            // Spiral search (simplified)
            for (int i = -r; i <= r; i++) {
                testPos = new Vector3Int(initialSpawnTileGuess.x + i, initialSpawnTileGuess.y + r, 0); // Top
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
                testPos = new Vector3Int(initialSpawnTileGuess.x + i, initialSpawnTileGuess.y - r, 0); // Bottom
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
            }
            for (int i = -r + 1; i < r; i++) { // Avoid double-checking corners
                testPos = new Vector3Int(initialSpawnTileGuess.x - r, initialSpawnTileGuess.y + i, 0); // Left
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
                testPos = new Vector3Int(initialSpawnTileGuess.x + r, initialSpawnTileGuess.y + i, 0); // Right
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
            }
        }
        Debug.LogWarning($"No valid spawn within {maxSpawnSearchRadius} of {initialSpawnTileGuess}.");
        return false;
    }
    bool IsTileWithinBounds(int x, int y) { return x >= 0 && x < mapGenerator.mapWidth && y >= 0 && y < mapGenerator.mapHeight; }
    bool IsSpawnCandidateValid(Vector3Int tilePos, out int elevation)
    {
        elevation = 0;
        if (!IsTileWithinBounds(tilePos.x, tilePos.y)) return false;
        if (mapGenerator.groundLayer.GetTile(tilePos) == null) return false; // Must have ground
        // Cannot spawn on walls, stairs, bridges, or deco
        if (mapGenerator.wallsLayer.GetTile(tilePos) != null ||
            mapGenerator.stairsLayer.GetTile(tilePos) != null ||
            (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(tilePos) != null) ||
            (mapGenerator.decoLayer != null && mapGenerator.decoLayer.GetTile(tilePos) != null) ) return false;
        
        elevation = mapGenerator.ElevationData[tilePos.x, tilePos.y];
        return true; // Valid spawn point
    }
}