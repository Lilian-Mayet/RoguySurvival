// MonsterAI.cs
using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Pour les Coroutines
using UnityEngine.Tilemaps; // <--- AJOUTEZ CETTE LIGNE !

public class MonsterAI : MonoBehaviour
{
    [Header("References")]

    public Animator animator;
    public PathfindingAStar pathfinder; // Référence au script de pathfinding
    public Transform playerTransform; // Référence directe au Transform du joueur
    public PlayerMovement playerMovementScript; // Pour obtenir l'élévation du joueur
    public MapGeneratorV2 mapGenerator; // Pour les conversions de tuiles

    [Header("Movement")]
    public float moveSpeed = 2f; // Plus lent que le joueur ?
    private List<Vector3Int> currentPath;
    private int currentPathIndex;
    private bool isMovingOnPath = false;
    private Vector3Int currentLogicalTile;
    private int currentMonsterElevation = 0; // Initialiser correctement au spawn

    [Header("Pathfinding Settings")]
    public float pathRefreshRate = 0.1f; // Recalculer le chemin toutes les X secondes
    private float pathRefreshTimer;
    private bool justCompletedStairTransition = false;
    private const float postStairPathDelay = 0.5f; // Small delay before allowing path recalc after stairs
    private float postStairPathTimer = 0f;

    [Header("Attack Settings")]
    public float attackRange = 1.5f; // En unités de monde, ou en nombre de tuiles
    public float attackCooldown = 2.0f;
    private float attackTimer;
    // Ajoutez des stats de monstre ici (PV, dégâts, etc.)

    private Vector2 lastMovementDirectionForAnim;



    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        // Trouver le PathfindingAStar s'il n'est pas assigné (s'il est sur un objet manager)
        if (pathfinder == null) pathfinder = FindFirstObjectByType<PathfindingAStar>();
        // Trouver le joueur s'il n'est pas assigné
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player"); // Assurez-vous que votre joueur a le tag "Player"
            if (playerObj != null) {
                playerTransform = playerObj.transform;
                playerMovementScript = playerObj.GetComponent<PlayerMovement>();
            }
        }
        if (mapGenerator == null){
            
             mapGenerator = FindFirstObjectByType<MapGeneratorV2>();
        }


        if (animator == null || pathfinder == null || playerTransform == null || playerMovementScript == null || mapGenerator == null)
        {
            Debug.LogError($"MonsterAI on {gameObject.name} is missing critical references. Disabling.", gameObject);
            enabled = false;
            return;
        }

        // Initialiser la position logique et l'élévation du monstre
        currentLogicalTile = mapGenerator.groundLayer.WorldToCell(transform.position);
        if (mapGenerator.IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y))
        {
            currentMonsterElevation = mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y];
        }
        else
        {
             Debug.LogWarning($"Monster {gameObject.name} spawned out of bounds or on invalid tile. Defaulting elevation.", gameObject);
             currentMonsterElevation = 0; // Ou une autre logique de spawn
        }

        lastMovementDirectionForAnim = new Vector2(0, -1); // Idle vers le bas par défaut
        UpdateAnimator(Vector2.zero);
        pathRefreshTimer = pathRefreshRate; // Calculer le chemin immédiatement
        attackTimer = attackCooldown;
    }

    void Update()
{
    if (playerTransform == null) return;

    float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
    attackTimer -= Time.deltaTime;
    pathRefreshTimer -= Time.deltaTime;
    if (justCompletedStairTransition) { // If we are in post-stair delay
        postStairPathTimer -= Time.deltaTime;
        if (postStairPathTimer <= 0) {
            // Debug.Log("[Update] Post-stair delay finished.");
            justCompletedStairTransition = false; // Delay over
        }
    }


    // 1. Attack
    if (distanceToPlayer <= attackRange && attackTimer <= 0)
    {
        AttackPlayer();
        if (isMovingOnPath) StopMovement(); // Stop movement maybe
         // Reset path timer? Maybe not needed if StopMovement clears path.
         pathRefreshTimer = pathRefreshRate;
         justCompletedStairTransition = false; // Attacking resets the state
    }
    // 2. Path Recalculation Logic (only if not attacking)
    else
    {
         // Conditions to check for path request:
         bool isOnStairTile = mapGenerator.stairsLayer.GetTile(currentLogicalTile) == mapGenerator.stair_S_Tile;
         bool timerReady = pathRefreshTimer <= 0;
         bool canRequest = timerReady && !isOnStairTile && !justCompletedStairTransition && isMovingOnPath == false; // Request only if timer ready AND not on stair AND not in post-stair delay AND not already moving

         // Let's refine: recalculate if timer ready, not on stair, not in delay. If already moving, the new path will replace old one.
         canRequest = timerReady && !isOnStairTile && !justCompletedStairTransition;


         if (canRequest)
         {
            // Debug.Log($"[Update] Requesting new path. Timer:{pathRefreshTimer}, OnStair:{isOnStairTile}, PostStairDelay:{justCompletedStairTransition}");
            RequestNewPath();
            pathRefreshTimer = pathRefreshRate; // Reset timer after request
         }
         else if (timerReady) {
             // Timer ready but blocked (on stair or post-transition delay)
             // Reset timer anyway to prevent immediate re-check next frame
              pathRefreshTimer = pathRefreshRate;
            //   Debug.Log($"[Update] Path request timer ready but blocked. OnStair:{isOnStairTile}, PostStairDelay:{justCompletedStairTransition}. Timer reset.");
         }
    }
}

    void FixedUpdate()
    {
        HandleMovementOnPath();
    }

    void RequestNewPath()
            {

                // verifier si il n'est pas sur un escalier (sinon ça bug)
            if (mapGenerator.stairsLayer.GetTile(currentLogicalTile) == mapGenerator.stair_S_Tile)
            {
                Debug.Log($"Monster {gameObject.name} is on a stair ({currentLogicalTile}), skipping path request.");
                return; // Ne pas chercher de nouveau chemin si sur un escalier
            }
            Debug.Log($"Monster {gameObject.name} finding a new path request.");
                if (pathfinder == null || playerTransform == null || playerMovementScript == null ) return;

                Vector3Int playerTile = mapGenerator.groundLayer.WorldToCell(playerTransform.position);
                int playerElevation = playerMovementScript.GetCurrentElevation();

                currentLogicalTile = mapGenerator.groundLayer.WorldToCell(transform.position);
                if(mapGenerator.IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y))
                    currentMonsterElevation = mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y];
                    Debug.Log("Monster currently one level : " + currentMonsterElevation);


                // Appel de la nouvelle méthode principale de pathfinding
                List<Vector3Int> newPath = pathfinder.FindOverallPath(currentLogicalTile, currentMonsterElevation, playerTile, playerElevation);

                if (newPath != null && newPath.Count > 1)
                {
                    currentPath = newPath;
                    currentPathIndex = 1;
                    isMovingOnPath = true;
                }
                else
                {
                    StopMovement();
                }
            }

void HandleMovementOnPath()
{
    if (!isMovingOnPath || currentPath == null || currentPathIndex >= currentPath.Count)
    {
        // If we were supposed to be moving but something is wrong (e.g., path became null), stop.
        if (isMovingOnPath)
        {
            // Debug.Log("[HandleMovement] Stopping movement because path is null or index out of bounds.");
            StopMovement();
        }
        return; // Nothing to do if not moving on a valid path/index
    }

    // --- Target Calculation ---
    Vector3Int targetTileInPath = currentPath[currentPathIndex];
    Vector3 targetWorldPosition = mapGenerator.groundLayer.GetCellCenterWorld(targetTileInPath);

    // --- Movement Execution ---
    Vector3 direction = (targetWorldPosition - transform.position).normalized;
    // Prevent NaN issues if somehow target is exactly the current position (shouldn't happen with < 0.1f check below)
    if (direction == Vector3.zero && Vector3.Distance(transform.position, targetWorldPosition) < 0.1f) {
        // Already at target, skip movement physics/animator update for this frame before the check below handles it.
    } else if (direction != Vector3.zero) {
         Vector3 newPosition = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.fixedDeltaTime);
         GetComponent<Rigidbody2D>().MovePosition(newPosition);
         UpdateAnimator(direction); // Update animation based on movement direction
    } else {
        // Target is same as current, but distance > 0.1f? Weird state, update animator to idle
        UpdateAnimator(Vector2.zero);
    }


    // --- Check if target tile is reached ---
    if (Vector3.Distance(transform.position, targetWorldPosition) < 0.1f)
    {
        // Snap to the exact center of the tile
        transform.position = targetWorldPosition;

        // --- State Update Preparation ---
        Vector3Int previousLogicalTile = currentLogicalTile; // Store where we came FROM
        int previousElevation = currentMonsterElevation;     // Store elevation BEFORE the move

        // Identify the tile we just landed on and the one we came from (specifically checking stair tiles)
        TileBase tileOnCurrentStairsLayer = mapGenerator.stairsLayer.GetTile(targetTileInPath); // Tile we just landed on
        bool justArrivedOnStairSTile = (tileOnCurrentStairsLayer == mapGenerator.stair_S_Tile);

        TileBase tileOnPreviousStairsLayer = mapGenerator.stairsLayer.GetTile(previousLogicalTile); // Tile we came from
        bool cameFromStairSTile = (tileOnPreviousStairsLayer == mapGenerator.stair_S_Tile);

        int newElevation = previousElevation; // Default: assume elevation doesn't change
        bool didMoveOffStair = false; // Flag specifically for the transition OFF a stair

        // --- Elevation Update Logic ---

        // Case 1: Just landed ON the special stair tile
        if (justArrivedOnStairSTile)
        {
            Vector3Int expectedL0Access = new Vector3Int(targetTileInPath.x, targetTileInPath.y - 1, 0); // Tile south of stair (L0 access)
            Vector3Int expectedL1Access = new Vector3Int(targetTileInPath.x, targetTileInPath.y + 1, 0); // Tile north of stair (L1 access/platform)

            if (previousLogicalTile == expectedL0Access && previousElevation == 0)
            {
                // Came from L0 South onto L1 Data stair -> Effectively mid-transition UPWARDS
                newElevation = 1; // Represents being 'on' the L1 part of the transition conceptually
                // Debug.Log($"[HandleMovement] Arrived ON Stair {targetTileInPath} from L0 South ({previousLogicalTile}). Temp Elev = 1");
            }
            else if (previousLogicalTile == expectedL1Access && previousElevation == 1)
            {
                // Came from L1 North onto L1 Data stair -> Effectively mid-transition DOWNWARDS
                newElevation = 0; // Represents being 'on' the L0 part of the transition conceptually
                // Debug.Log($"[HandleMovement] Arrived ON Stair {targetTileInPath} from L1 North ({previousLogicalTile}). Temp Elev = 0");
            }
            else
            {
                // Arrived on stair tile from somewhere unexpected (e.g., side, multi-tile stair?). Fallback.
                // Since stair_S_Tile itself has L1 data, use that.
                newElevation = 1;
                // Debug.LogWarning($"[HandleMovement] Arrived ON Stair {targetTileInPath} from unexpected tile {previousLogicalTile} (Elev {previousElevation}). Assuming New Elevation = 1 (Stair Data)");
            }
             // Clear the post-stair delay flag if we somehow moved back onto a stair
             justCompletedStairTransition = false;
             postStairPathTimer = 0f;
        }
        // Case 2: Just moved OFF the special stair tile
        else if (cameFromStairSTile)
        {
             didMoveOffStair = true; // Mark that this step completed the stair traversal

             Vector3Int expectedL0Exit = new Vector3Int(previousLogicalTile.x, previousLogicalTile.y - 1, 0); // Tile south of the stair we left
             Vector3Int expectedL1Exit = new Vector3Int(previousLogicalTile.x, previousLogicalTile.y + 1, 0); // Tile north of the stair we left

            if (targetTileInPath == expectedL0Exit) {
                // Moved South off the stair onto expected L0 ground
                newElevation = 0;
                // Debug.Log($"[HandleMovement] Moved OFF Stair {previousLogicalTile} to L0 South ({targetTileInPath}). Final Elevation = 0");
            }
            else if (targetTileInPath == expectedL1Exit) {
                 // Moved North off the stair onto expected L1 platform/bridge
                newElevation = 1;
                // Debug.Log($"[HandleMovement] Moved OFF Stair {previousLogicalTile} to L1 North ({targetTileInPath}). Final Elevation = 1");
            } else {
                // Moved off the stair tile to an unexpected adjacent tile. Fallback to target tile's data.
                if (mapGenerator.IsTileWithinBounds(targetTileInPath.x, targetTileInPath.y)) {
                     bool targetIsBridge = mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(targetTileInPath) != null;
                     newElevation = targetIsBridge ? 1 : mapGenerator.ElevationData[targetTileInPath.x, targetTileInPath.y];
                     // Debug.LogWarning($"[HandleMovement] Moved OFF Stair {previousLogicalTile} to unexpected tile {targetTileInPath}. Using target Data Elev (Bridge={targetIsBridge}): {newElevation}");
                 } else {
                     newElevation = 0; // Default if somehow moved out of bounds
                    // Debug.LogError($"[HandleMovement] Moved OFF Stair {previousLogicalTile} to OUT OF BOUNDS tile {targetTileInPath}!");
                 }
            }
        }
        // Case 3: Normal movement (not involving stepping onto or off the stair_S_Tile in this specific step)
        else
        {
            if (mapGenerator.IsTileWithinBounds(targetTileInPath.x, targetTileInPath.y))
            {
                bool targetIsBridge = mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(targetTileInPath) != null;
                if (targetIsBridge) {
                    newElevation = 1; // Landed on a bridge tile -> Elevation is 1
                } else {
                    // Landed on ground/platform (non-bridge, non-stair_S) -> Use ElevationData
                    newElevation = mapGenerator.ElevationData[targetTileInPath.x, targetTileInPath.y];
                }
                // Debug.Log($"[HandleMovement] Normal move to {targetTileInPath}. DataElev={mapGenerator.ElevationData[targetTileInPath.x, targetTileInPath.y]}, Bridge={targetIsBridge}. Final Elevation = {newElevation}");
            }
            else
            {
                newElevation = 0; // Default if somehow moved out of bounds
                // Debug.LogError($"[HandleMovement] Moved to OUT OF BOUNDS tile {targetTileInPath} during normal movement!");
            }
             // Note: The justCompletedStairTransition flag is handled by the timer in Update(),
             // no need to clear it during normal movement here.
        }

        // --- Update Monster State ---
        currentLogicalTile = targetTileInPath;        // Update the monster's current tile position
        currentMonsterElevation = newElevation;       // Update the monster's elevation

        // Debug.Log($"[HandleMovement] Step Complete. Current Tile: {currentLogicalTile}, Current Elevation: {currentMonsterElevation}");

        // --- Handle Post-Stair Transition Delay ---
        if (didMoveOffStair)
        {
             // Just completed the step moving OFF the stair.
             // Set the flag and start the delay timer to prevent immediate path recalculation.
             // Debug.Log("[HandleMovement] ** Just moved OFF stair. Setting transition flag and starting delay timer. **");
             justCompletedStairTransition = true;
             postStairPathTimer = postStairPathDelay; // Start the delay timer
        }

        // --- Advance Path Index ---
        currentPathIndex++;

        // --- Check Path Completion ---
        if (currentPathIndex >= currentPath.Count)
        {
            // Debug.Log("[HandleMovement] Path Complete.");
            StopMovement(); // Stop movement and clear path/flags
        }
    }
    // Else: Target tile not reached yet, continue moving towards it in the next FixedUpdate frame.
}    void StopMovement()
    {
        isMovingOnPath = false;
        currentPath = null;
        currentPathIndex = 0;
        UpdateAnimator(Vector2.zero); // Animation d'idle

        // --- ADD THIS ---
        justCompletedStairTransition = false; // Clear transition state on stop
        postStairPathTimer = 0f;
        // --- END ADD ---

        // Debug.Log($"Monster {gameObject.name}: Movement stopped.");
    }

    void AttackPlayer()
    {
        Debug.Log($"Monster {gameObject.name} attacks player!");
        // Ici, vous mettriez la logique d'attaque (animation, dégâts au joueur, etc.)
        // Par exemple: playerTransform.GetComponent<PlayerStats>().TakeDamage(10);
        attackTimer = attackCooldown; // Réinitialiser le cooldown
        // Orienter le monstre vers le joueur pendant l'attaque
        Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
        UpdateAnimator(directionToPlayer); // Pour l'orientation, puis passer à l'anim d'attaque
    }


    void UpdateAnimator(Vector2 moveDirection)
    {
        if (animator == null) return;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", moveDirection.x);
            animator.SetFloat("MoveY", moveDirection.y);
            lastMovementDirectionForAnim = moveDirection.normalized;
        }
        else
        {
            animator.SetBool("IsMoving", false);
            // Utiliser lastMovementDirectionForAnim pour l'idle si vous avez des idles directionnels
            animator.SetFloat("MoveX", lastMovementDirectionForAnim.x); // Pour que l'idle soit orienté
            animator.SetFloat("MoveY", lastMovementDirectionForAnim.y); // Pour que l'idle soit orienté
        }
    }

  
}