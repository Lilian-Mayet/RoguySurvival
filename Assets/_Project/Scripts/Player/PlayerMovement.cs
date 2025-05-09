using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float collisionSkinWidth = 0.02f;


    [Header("References")]
    public MapGeneratorV2 mapGenerator;
    public SpriteRenderer playerSpriteRenderer;

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector3Int currentLogicalTile;
    private int currentPlayerElevation = 0;

    [Header("Spawning")]
    public Vector2Int initialSpawnTileGuess = new Vector2Int(100, 100);
    public int maxSpawnSearchRadius = 20;

    public LayerMask wallCollisionLayerMask;
    private Collider2D playerCollider;

    [Header("Animation Settings")]
    public Animator animator; // Faites glisser le composant Animator de votre joueur ici dans l'Inspecteur

    private Vector2 lastMovementDirection; // Pour se souvenir de la dernière direction


    public int orderInLayerLevel0 = 5;
    public int orderInLayerLevel1 = 7;

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
            currentPlayerElevation = IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y) ? mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y] : 0;
        }
        transform.position = mapGenerator.groundLayer.GetCellCenterWorld(currentLogicalTile);
        // Make sure currentLogicalTile is updated immediately after setting position
        currentLogicalTile = mapGenerator.groundLayer.WorldToCell(transform.position);
        Debug.Log($"Player spawned at tile: {currentLogicalTile}, Elevation: {currentPlayerElevation}");




        if (animator == null)
        {
            animator = GetComponent<Animator>(); // Essayer de le récupérer si non assigné
        }
        if (animator == null)
        {
            Debug.LogError("Animator component not found on the player!");
            enabled = false; // Désactiver le script si l'animator est manquant
            return;
        }
        lastMovementDirection = new Vector2(0, -1); // Par défaut, regarde vers le bas
        UpdateAnimatorParameters(Vector2.zero); // Initialiser l'animator

    }

    void Update()
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null) return;
        HandleInput();
        UpdateAnimatorParameters(movementInput); // Mettre à jour l'animator avant FixedUpdate
    }

    void FixedUpdate()
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null || animator == null) return;
        ApplyContinuousMovement();
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

    void ApplyContinuousMovement()
    {
        if (movementInput == Vector2.zero && rb.linearVelocity == Vector2.zero)
        {
            if (rb.bodyType == RigidbodyType2D.Kinematic) rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector3Int previousLogicalTile = currentLogicalTile; // Store before any movement or evaluation
        int previousPlayerElevation = currentPlayerElevation;

        Vector2 moveDelta = movementInput * moveSpeed * Time.fixedDeltaTime;
        Vector2 intendedTargetPosition = currentPosition + moveDelta;

        // 1. Physical Collision (BoxCast for walls)
        Vector2 boxCastSize = playerCollider.bounds.size * 0.95f;
        RaycastHit2D physicalHit = Physics2D.BoxCast(
            currentPosition, boxCastSize, 0f,
            movementInput.normalized, moveDelta.magnitude,
            wallCollisionLayerMask
        );

        Vector2 actualTargetPosition = intendedTargetPosition;
        if (physicalHit.collider != null)
        {
            float distanceToHit = Mathf.Max(0, physicalHit.distance - collisionSkinWidth);
            actualTargetPosition = currentPosition + movementInput.normalized * distanceToHit;
        }

        // 2. Logical Validation and Elevation Changes
        Vector3Int targetLogicalTileIfMoved = mapGenerator.groundLayer.WorldToCell(actualTargetPosition);
        int newElevationForThisMove = currentPlayerElevation;
        bool logicalMovementAllowed = true;

        // --- Start Elevation Logic ---
        // A. Check for STAIR TRANSITIONS (up/down) if potentially moving to a new tile
        if (targetLogicalTileIfMoved != previousLogicalTile)
        {
            if (TryHandleStairElevationTransition(previousLogicalTile, targetLogicalTileIfMoved, previousPlayerElevation, out int elevationAfterStairTransition))
            {
                newElevationForThisMove = elevationAfterStairTransition;
                // This implies movement is allowed onto the stair for transition
                // Debug.Log($"STAIR TRANSITION: From {previousLogicalTile} (Elev {previousPlayerElevation}) to {targetLogicalTileIfMoved}. New Elev For Move: {newElevationForThisMove}");
            }
            // B. If not a stair transition, or even if it was, check general logical movement rules
            //    IsGeneralMovementLogicallyAllowed will use the (potentially updated) newElevationForThisMove
            else if (!IsGeneralMovementLogicallyAllowed(previousLogicalTile, targetLogicalTileIfMoved, newElevationForThisMove, out newElevationForThisMove))
            {
                logicalMovementAllowed = false;
                // Debug.Log($"General movement BLOCKED from {previousLogicalTile} to {targetLogicalTileIfMoved} with starting elev {previousPlayerElevation}. Proposed elev after check {newElevationForThisMove}");
            }
        }
        else // Staying within the same logical tile
        {
            if (!IsGeneralMovementLogicallyAllowed(previousLogicalTile, targetLogicalTileIfMoved, newElevationForThisMove, out newElevationForThisMove))
            {
                logicalMovementAllowed = false;
                 // Debug.Log($"Movement within {previousLogicalTile} BLOCKED. Starting elev {previousPlayerElevation}. Proposed elev after check {newElevationForThisMove}");
            }
        }
        // --- End Elevation Logic ---


        // 3. Apply Movement or Slide
        if (logicalMovementAllowed)
        {
            rb.MovePosition(actualTargetPosition);
            currentPlayerElevation = newElevationForThisMove;
        }
        else
        {
            Vector2 slidePosition = TrySlide(currentPosition, movementInput.normalized, moveDelta.magnitude, previousPlayerElevation, previousLogicalTile);
            Vector3Int slideTargetLogicalTile = mapGenerator.groundLayer.WorldToCell(slidePosition);
            int slideNewPotentialElevation = previousPlayerElevation;
            bool slideAllowed = false;

            if ((slidePosition - currentPosition).sqrMagnitude > 0.0001f) // If slide actually moved us
            {
                if (slideTargetLogicalTile != previousLogicalTile)
                {
                    if (TryHandleStairElevationTransition(previousLogicalTile, slideTargetLogicalTile, previousPlayerElevation, out int elevationAfterSlideStair))
                    {
                        slideNewPotentialElevation = elevationAfterSlideStair;
                        slideAllowed = true;
                    }
                    else if (IsGeneralMovementLogicallyAllowed(previousLogicalTile, slideTargetLogicalTile, previousPlayerElevation, out slideNewPotentialElevation)) // Pass previousPlayerElevation here
                    {
                        slideAllowed = true;
                    }
                }
                else // Slide kept us on the same tile
                {
                    if (IsGeneralMovementLogicallyAllowed(previousLogicalTile, slideTargetLogicalTile, previousPlayerElevation, out slideNewPotentialElevation)) // Pass previousPlayerElevation
                    {
                        slideAllowed = true;
                    }
                }
            }

            if (slideAllowed)
            {
                rb.MovePosition(slidePosition);
                currentPlayerElevation = slideNewPotentialElevation;
            }
            else
            {
                if (physicalHit.collider != null) {
                    rb.MovePosition(actualTargetPosition); // Move up to physical wall
                } else {
                    rb.MovePosition(currentPosition); // No movement
                }
                // currentPlayerElevation remains previousPlayerElevation
            }
        }
        currentLogicalTile = mapGenerator.groundLayer.WorldToCell(rb.position);


        UpdateSpriteOrderInLayer();
    }



    void UpdateAnimatorParameters(Vector2 currentMoveInput)
    {
    
        if (animator == null) return;

        // Si le joueur bouge
    
        if (currentMoveInput.sqrMagnitude > 0.01f)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentMoveInput.x);
            animator.SetFloat("MoveY", currentMoveInput.y);

            // Mémoriser la dernière direction de mouvement pour l'idle
            // Normaliser pour que LastMoveX/Y soient proches de -1, 0, ou 1
            lastMovementDirection = currentMoveInput.normalized;
        }
        else // Le joueur est immobile
        {
            animator.SetBool("IsMoving", false);
            // Optionnel: Mettre MoveX et MoveY à 0 si vous n'utilisez pas LastMoveX/Y pour l'idle
            // animator.SetFloat("MoveX", 0);
            // animator.SetFloat("MoveY", 0);
        }

        // Mettre à jour les paramètres pour la direction d'idle
        // Ces valeurs seront utilisées par les transitions vers les états Idle
        // lorsque IsMoving devient false.
        animator.SetFloat("LastMoveX", lastMovementDirection.x);
        animator.SetFloat("LastMoveY", lastMovementDirection.y);
    }


    void UpdateSpriteOrderInLayer()
{
    if (playerSpriteRenderer == null) return;

    if (currentPlayerElevation == 0)
    {
        playerSpriteRenderer.sortingOrder = orderInLayerLevel0;
    }
    else if (currentPlayerElevation == 1)
    {
        playerSpriteRenderer.sortingOrder = orderInLayerLevel1;
    }
    // Vous pouvez ajouter d'autres else if pour plus de niveaux d'élévation si nécessaire
}

    // Handles elevation changes when moving ONTO a stair tile that initiates a climb/descent.
    bool TryHandleStairElevationTransition(Vector3Int fromTile, Vector3Int toTile, int currentActualPlayerElev, out int newPlayerElevationOnStair)
    {
        newPlayerElevationOnStair = currentActualPlayerElev;

        TileBase stairsOnToTile = mapGenerator.stairsLayer.GetTile(toTile);
        if (stairsOnToTile == null) return false;

        int toTileActualElevationData = mapGenerator.ElevationData[toTile.x, toTile.y];

        // CLIMBING UP: Player at L0, moving from L0 ground ('fromTile') onto an L0 stair tile ('toTile')
        if (currentActualPlayerElev == 0 && toTileActualElevationData == 0)
        {
            Vector3Int platformAboveStairPos = new Vector3Int(toTile.x, toTile.y + 1, 0); // For stair_S
            if (IsTileWithinBounds(platformAboveStairPos.x, platformAboveStairPos.y) &&
                mapGenerator.ElevationData[platformAboveStairPos.x, platformAboveStairPos.y] == 1)
            {
                newPlayerElevationOnStair = 1;
                return true;
            }
        }
        // DESCENDING: Player at L1, moving from L1 platform ('fromTile') onto an L0 stair tile ('toTile')
        else if (currentActualPlayerElev == 1 && toTileActualElevationData == 0)
        {
            // fromTile must be the L1 platform connected to the stair 'toTile'
            if (fromTile.x == toTile.x && fromTile.y == toTile.y + 1 && // For stair_S
                IsTileWithinBounds(fromTile.x, fromTile.y) && mapGenerator.ElevationData[fromTile.x, fromTile.y] == 1)
            {
                newPlayerElevationOnStair = 0;
                return true;
            }
        }
        return false;
    }

    // Checks general movement rules.
    // 'currentTilePlayerIsOn' is the tile the player is starting from for this check.
    // 'targetTilePlayerMovesTo' is the tile they intend to enter.
    // 'elevationPlayerWouldHave' is the player's elevation *if this move were to occur* (could be affected by TryHandleStairElevationTransition).
    // 'finalPlayerElevationIfAllowed' is the elevation player will have if this function returns true.
    bool IsGeneralMovementLogicallyAllowed(Vector3Int currentTilePlayerIsOn, Vector3Int targetTilePlayerMovesTo, int elevationPlayerWouldHave, out int finalPlayerElevationIfAllowed)
    {
        finalPlayerElevationIfAllowed = elevationPlayerWouldHave;

        if (!IsTileWithinBounds(targetTilePlayerMovesTo.x, targetTilePlayerMovesTo.y)) return false;

        TileBase groundOnTarget = mapGenerator.groundLayer.GetTile(targetTilePlayerMovesTo);
        TileBase stairsOnTarget = mapGenerator.stairsLayer.GetTile(targetTilePlayerMovesTo);
        TileBase bridgeOnTarget = (mapGenerator.bridgeLayer != null) ? mapGenerator.bridgeLayer.GetTile(targetTilePlayerMovesTo) : null;
        TileBase wallOnTarget = mapGenerator.wallsLayer.GetTile(targetTilePlayerMovesTo);

        int targetTileDataElev = mapGenerator.ElevationData[targetTilePlayerMovesTo.x, targetTilePlayerMovesTo.y];

        // --- CORRECTION FOR BRIDGES OVER WALLS ---
        // 1. BRIDGE LOGIC (Highest Priority if player is at L1 or would be at L1)
        if (bridgeOnTarget != null)
        {
            if (elevationPlayerWouldHave == 1)
            {
                finalPlayerElevationIfAllowed = 1; // Stay/move onto bridge at L1
                // Debug.Log($"Bridge: Allowed on bridge {targetTilePlayerMovesTo} at Elev 1.");
                return true; // Allowed on bridge at L1, overrides walls below.
            }
            else // Player is L0 (or would be L0) and target has a bridge (meaning player is under it)
            {
                if (wallOnTarget == mapGenerator.cliffWall_S) { /*Debug.Log($"Bridge: Blocked by wall under bridge at {targetTilePlayerMovesTo}.");*/ return false; }
                if (groundOnTarget != null && targetTileDataElev == 0)
                {
                    finalPlayerElevationIfAllowed = 0; // On ground under bridge
                    // Debug.Log($"Bridge: Allowed on ground under bridge {targetTilePlayerMovesTo} at Elev 0.");
                    return true;
                }
                // Debug.Log($"Bridge: Blocked under bridge at {targetTilePlayerMovesTo} (no ground or wrong elev data).");
                return false; // No ground or non-L0 ground under bridge
            }
        }

        // --- CORRECTION FOR STAIRS (Staying on, or stepping off) ---
        // This logic assumes TryHandleStairElevationTransition already handled the *initial* step onto a stair that changes elevation.
        // Now we handle moving along a stair, or stepping OFF a stair.
        TileBase stairsOnCurrentTile = mapGenerator.stairsLayer.GetTile(currentTilePlayerIsOn);

        if (stairsOnTarget != null) // Moving TO another stair tile (or staying on the same one if target==current)
        {
            // Player is L1 (conceptually on upper part of stair), target stair tile itself is L0 data
            if (elevationPlayerWouldHave == 1 && targetTileDataElev == 0)
            {
                finalPlayerElevationIfAllowed = 1; return true;
            }
            // Player is L0 (conceptually on lower part of stair), target stair tile itself is L0 data
            else if (elevationPlayerWouldHave == 0 && targetTileDataElev == 0)
            {
                finalPlayerElevationIfAllowed = 0; return true;
            }
            // Player is L1, target stair is L1 data (e.g. decorative stair on plateau)
             else if (elevationPlayerWouldHave == 1 && targetTileDataElev == 1)
            {
                finalPlayerElevationIfAllowed = 1; return true;
            }
            // Debug.Log($"Stairs: Blocked on stair {targetTilePlayerMovesTo}. PlayerElevWouldBe: {elevationPlayerWouldHave}, TargetDataElev: {targetTileDataElev}");
            return false; // Other stair configurations might be invalid
        }
        // Moving OFF a stair
        else if (stairsOnCurrentTile != null) // Player is currently on a stair tile, target is NOT a stair
        {
            // Stepping off the "top" of a stair (player L1 on an L0 data stair) onto L0 ground
            if (elevationPlayerWouldHave == 1 && mapGenerator.ElevationData[currentTilePlayerIsOn.x, currentTilePlayerIsOn.y] == 0 &&
                groundOnTarget != null && targetTileDataElev == 0 && wallOnTarget == null)
            {
                finalPlayerElevationIfAllowed = 0; // Land on L0 ground
                // Debug.Log($"Stairs: Stepping OFF L1 stair {currentTilePlayerIsOn} to L0 ground {targetTilePlayerMovesTo}.");
                return true;
            }
            // Stepping off the "bottom" of a stair (player L0 on an L0 data stair) onto L1 platform (unusual without target being a stair itself, but handle for completeness)
            else if (elevationPlayerWouldHave == 0 && mapGenerator.ElevationData[currentTilePlayerIsOn.x, currentTilePlayerIsOn.y] == 0 &&
                     groundOnTarget != null && targetTileDataElev == 1 && wallOnTarget == null) // Assuming platform is groundLayer
            {
                finalPlayerElevationIfAllowed = 1; // Land on L1 platform
                 // Debug.Log($"Stairs: Stepping OFF L0 stair {currentTilePlayerIsOn} to L1 platform {targetTilePlayerMovesTo}.");
                return true;
            }
            // Stepping off a stair onto same-level ground/platform
            else if (targetTileDataElev == elevationPlayerWouldHave && groundOnTarget != null && wallOnTarget == null)
            {
                // finalPlayerElevationIfAllowed is already elevationPlayerWouldHave
                // Debug.Log($"Stairs: Stepping OFF stair {currentTilePlayerIsOn} to same level ({elevationPlayerWouldHave}) ground {targetTilePlayerMovesTo}.");
                return true;
            }
            // Debug.Log($"Stairs: Blocked stepping OFF stair {currentTilePlayerIsOn} to {targetTilePlayerMovesTo}. PlayerElevWouldBe: {elevationPlayerWouldHave}, TargetDataElev: {targetTileDataElev}");
            return false; // Block if stepping off stair to invalid location
        }


        // 3. WALLS (If not a bridge scenario that already returned true)
        if (wallOnTarget == mapGenerator.cliffWall_S)
        {
            // Debug.Log($"Walls: Blocked by wall {targetTilePlayerMovesTo}.");
            return false;
        }

        // 4. GROUND / CLIFFS (No bridge, no stair handled above, no wall on target tile)
        if (targetTileDataElev == elevationPlayerWouldHave)
        {
            if (groundOnTarget != null)
            {
                // finalPlayerElevationIfAllowed is already elevationPlayerWouldHave
                return true; // Valid ground at same elevation
            }
            // Debug.Log($"Ground/Cliff: Blocked - No ground tile at {targetTilePlayerMovesTo} (elev {elevationPlayerWouldHave}).");
            return false; // Empty space even if elevation matches
        }
        else // Elevation mismatch = cliff (and not handled by stair logic)
        {
            // Debug.Log($"Ground/Cliff: Blocked - Cliff at {targetTilePlayerMovesTo}. PlayerElevWouldBe {elevationPlayerWouldHave}, TargetDataElev {targetTileDataElev}.");
            return false;
        }
    }

    Vector2 TrySlide(Vector2 currentPosition, Vector2 normalizedInput, float moveDistance, int playerElevationBeforeSlide, Vector3Int playerTileBeforeSlide)
    {
        Vector2 bestSlidePosition = currentPosition;

        // Try X-only movement
        if (Mathf.Abs(normalizedInput.x) > 0.01f)
        {
            Vector2 xOnlyDirection = new Vector2(normalizedInput.x, 0).normalized;
            Vector2 xPotentialTargetPos = currentPosition + xOnlyDirection * moveDistance;
            RaycastHit2D xPhysicalHit = Physics2D.BoxCast(currentPosition, playerCollider.bounds.size * 0.95f, 0f, xOnlyDirection, moveDistance, wallCollisionLayerMask);
            if (xPhysicalHit.collider != null) {
                xPotentialTargetPos = currentPosition + xOnlyDirection * Mathf.Max(0, xPhysicalHit.distance - collisionSkinWidth);
            }

            Vector3Int xTargetTile = mapGenerator.groundLayer.WorldToCell(xPotentialTargetPos);
            int xNewElev = playerElevationBeforeSlide; // Start with original elevation for this slide check
            bool xSlideAllowed = false;

            if (xTargetTile != playerTileBeforeSlide) {
                if (TryHandleStairElevationTransition(playerTileBeforeSlide, xTargetTile, playerElevationBeforeSlide, out xNewElev) ||
                    IsGeneralMovementLogicallyAllowed(playerTileBeforeSlide, xTargetTile, playerElevationBeforeSlide, out xNewElev)) { // Pass original elevation
                    xSlideAllowed = true;
                }
            } else {
                 if (IsGeneralMovementLogicallyAllowed(playerTileBeforeSlide, xTargetTile, playerElevationBeforeSlide, out xNewElev)) { // Pass original elevation
                    xSlideAllowed = true;
                 }
            }
            if (xSlideAllowed) return xPotentialTargetPos; // Prioritize X slide
        }

        // Try Y-only movement
        if (Mathf.Abs(normalizedInput.y) > 0.01f)
        {
            Vector2 yOnlyDirection = new Vector2(0, normalizedInput.y).normalized;
            Vector2 yPotentialTargetPos = currentPosition + yOnlyDirection * moveDistance;
            RaycastHit2D yPhysicalHit = Physics2D.BoxCast(currentPosition, playerCollider.bounds.size * 0.95f, 0f, yOnlyDirection, moveDistance, wallCollisionLayerMask);
            if (yPhysicalHit.collider != null) {
                yPotentialTargetPos = currentPosition + yOnlyDirection * Mathf.Max(0, yPhysicalHit.distance - collisionSkinWidth);
            }

            Vector3Int yTargetTile = mapGenerator.groundLayer.WorldToCell(yPotentialTargetPos);
            int yNewElev = playerElevationBeforeSlide;
            bool ySlideAllowed = false;

            if(yTargetTile != playerTileBeforeSlide){
                if (TryHandleStairElevationTransition(playerTileBeforeSlide, yTargetTile, playerElevationBeforeSlide, out yNewElev) ||
                    IsGeneralMovementLogicallyAllowed(playerTileBeforeSlide, yTargetTile, playerElevationBeforeSlide, out yNewElev)) {
                    ySlideAllowed = true;
                }
            } else {
                if(IsGeneralMovementLogicallyAllowed(playerTileBeforeSlide, yTargetTile, playerElevationBeforeSlide, out yNewElev)) {
                    ySlideAllowed = true;
                }
            }
            if (ySlideAllowed) return yPotentialTargetPos;
        }
        return bestSlidePosition; // No slide was possible
    }

    bool IsTileWithinBounds(int x, int y) { return x >= 0 && x < mapGenerator.mapWidth && y >= 0 && y < mapGenerator.mapHeight; }
    bool FindValidSpawnPosition(out Vector3Int foundPosition, out int foundElevation)
    {
        foundPosition = Vector3Int.zero; foundElevation = 0;
        if (mapGenerator.ElevationData == null) { Debug.LogError("ElevationData null."); return false; }
        Vector3Int testPos = new Vector3Int(initialSpawnTileGuess.x, initialSpawnTileGuess.y, 0);
        if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
        for (int r = 1; r <= maxSpawnSearchRadius; r++) {
            for (int i = -r; i <= r; i++) {
                testPos = new Vector3Int(initialSpawnTileGuess.x + i, initialSpawnTileGuess.y + r, 0);
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
                testPos = new Vector3Int(initialSpawnTileGuess.x + i, initialSpawnTileGuess.y - r, 0);
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
            }
            for (int i = -r + 1; i < r; i++) {
                testPos = new Vector3Int(initialSpawnTileGuess.x - r, initialSpawnTileGuess.y + i, 0);
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
                testPos = new Vector3Int(initialSpawnTileGuess.x + r, initialSpawnTileGuess.y + i, 0);
                if (IsSpawnCandidateValid(testPos, out foundElevation)) { foundPosition = testPos; return true; }
            }
        }
        Debug.LogWarning($"No valid spawn within {maxSpawnSearchRadius} of {initialSpawnTileGuess}.");
        return false;
    }
    bool IsSpawnCandidateValid(Vector3Int tilePos, out int elevation)
    {
        elevation = 0;
        if (!IsTileWithinBounds(tilePos.x, tilePos.y)) return false;
        if (mapGenerator.groundLayer.GetTile(tilePos) == null) return false;
        if (mapGenerator.wallsLayer.GetTile(tilePos) != null ||
            mapGenerator.stairsLayer.GetTile(tilePos) != null ||
            (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(tilePos) != null) ||
            (mapGenerator.decoLayer != null && mapGenerator.decoLayer.GetTile(tilePos) != null) ) return false;
        elevation = mapGenerator.ElevationData[tilePos.x, tilePos.y];
        return true;
    }
}