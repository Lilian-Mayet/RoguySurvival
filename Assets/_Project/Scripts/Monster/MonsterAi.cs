// MonsterAI.cs
using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Pour les Coroutines

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
    public float pathRefreshRate = 1.0f; // Recalculer le chemin toutes les X secondes
    private float pathRefreshTimer;

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
        if (mapGenerator == null) mapGenerator = FindFirstObjectByType<MapGeneratorV2>();


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
        if (playerTransform == null) return; // Joueur mort ou non trouvé

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        attackTimer -= Time.deltaTime;

        // 1. Attaquer si à portée et cooldown écoulé
        if (distanceToPlayer <= attackRange && attackTimer <= 0)
        {
            AttackPlayer();
            // Arrêter le mouvement pendant l'attaque (optionnel)
            if (isMovingOnPath) StopMovement();
        }
        // 2. Se déplacer si pas en train d'attaquer (ou si l'attaque n'interrompt pas le mouvement)
        else if (!isMovingOnPath || (isMovingOnPath && currentPath == null)) // Si pas en mouvement ou si le chemin est nul
        {
            pathRefreshTimer -= Time.deltaTime;
            if (pathRefreshTimer <= 0)
            {
                RequestNewPath();
                pathRefreshTimer = pathRefreshRate;
            }
        }

        // Gestion du mouvement le long du chemin existant (se fait dans FixedUpdate pour la physique)
    }

    void FixedUpdate()
    {
        HandleMovementOnPath();
    }

    void RequestNewPath()
    {
        if (pathfinder == null || playerTransform == null || playerMovementScript == null) return;

        Vector3Int playerTile = mapGenerator.groundLayer.WorldToCell(playerTransform.position);
        int playerElevation = playerMovementScript.GetCurrentElevation(); // Vous devrez ajouter cette méthode à PlayerMovement

        // Mettre à jour la tuile et l'élévation actuelles du monstre avant de chercher un chemin
        currentLogicalTile = mapGenerator.groundLayer.WorldToCell(transform.position);
        if(mapGenerator.IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y))
            currentMonsterElevation = mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y];


        List<Vector3Int> newPath = pathfinder.FindPath(currentLogicalTile, currentMonsterElevation, playerTile, playerElevation);

        if (newPath != null && newPath.Count > 1) // > 1 car le premier nœud est la position actuelle
        {
            currentPath = newPath;
            currentPathIndex = 1; // Commencer à se déplacer vers le premier nœud *après* le départ
            isMovingOnPath = true;
            // Debug.Log($"Monster {gameObject.name}: New path found with {currentPath.Count} nodes.");
        }
        else
        {
            // Debug.LogWarning($"Monster {gameObject.name}: No path found or path too short.");
            StopMovement(); // Arrêter si aucun chemin n'est trouvé
        }
    }

    void HandleMovementOnPath()
    {
        if (!isMovingOnPath || currentPath == null || currentPathIndex >= currentPath.Count)
        {
            if (isMovingOnPath) StopMovement(); // Chemin terminé ou invalide
            return;
        }

        Vector3Int targetTileInPath = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = mapGenerator.groundLayer.GetCellCenterWorld(targetTileInPath);

        // Déterminer l'élévation de la prochaine tuile du chemin (pour le mouvement)
        // La logique de PathfindingAStar::IsMonsterMoveValid devrait déjà s'être assurée que c'est un pas valide.
        // On peut recalculer l'élévation de la cible pour être sûr.
        int nextStepElevation = currentMonsterElevation; // Par défaut
        if(mapGenerator.IsTileWithinBounds(targetTileInPath.x, targetTileInPath.y)){
             // La logique IsMonsterMoveValid du pathfinder devrait avoir validé cette transition.
             // Pour simplifier ici, on prend l'elevation data de la tuile cible.
             // Une logique plus robuste vérifierait la transition d'élévation.
            nextStepElevation = mapGenerator.ElevationData[targetTileInPath.x, targetTileInPath.y];

            // Cas spécial: si la tuile cible est un pont et que le monstre est à L0,
            // mais que le chemin l'a fait passer sous le pont, son élévation reste L0.
            // Si le chemin l'a fait passer SUR le pont, son élévation devient L1.
            // La 'targetElevation' de IsMonsterMoveValid dans le pathfinder devrait avoir géré cela.
            // On pourrait stocker l'élévation prévue pour chaque nœud du chemin si nécessaire.
            // Pour l'instant, on fait une supposition simple.
            if (mapGenerator.bridgeLayer.GetTile(targetTileInPath) != null) {
                if (currentMonsterElevation == 1) nextStepElevation = 1; // Sur le pont
                else nextStepElevation = 0; // Sous le pont
            }
        }


        Vector3 direction = (targetWorldPosition - transform.position).normalized;
        Vector3 newPosition = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.fixedDeltaTime);
        GetComponent<Rigidbody2D>().MovePosition(newPosition); // Si vous utilisez un Rigidbody2D Kinematic

        UpdateAnimator(direction);

        // Vérifier si on est arrivé à la tuile cible du chemin
        if (Vector3.Distance(transform.position, targetWorldPosition) < 0.1f)
        {
            transform.position = targetWorldPosition; // Snap à la tuile
            currentLogicalTile = targetTileInPath;
            currentMonsterElevation = nextStepElevation; // Mettre à jour l'élévation
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                StopMovement(); // Fin du chemin
            }
        }
    }

    void StopMovement()
    {
        isMovingOnPath = false;
        currentPath = null;
        currentPathIndex = 0;
        UpdateAnimator(Vector2.zero); // Animation d'idle
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