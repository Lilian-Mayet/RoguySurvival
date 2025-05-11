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
    public float pathRefreshRate = 0.1f; // Recalculer le chemin toutes les X secondes
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
        else if (true) // Si pas en mouvement ou si le chemin est nul (!isMovingOnPath || (isMovingOnPath && currentPath == null))
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
                int playerElevation = playerMovementScript.GetCurrentElevation();

                currentLogicalTile = mapGenerator.groundLayer.WorldToCell(transform.position);
                if(mapGenerator.IsTileWithinBounds(currentLogicalTile.x, currentLogicalTile.y))
                    currentMonsterElevation = mapGenerator.ElevationData[currentLogicalTile.x, currentLogicalTile.y];


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
                if (isMovingOnPath) StopMovement();
                return;
            }

            Vector3Int targetTileInPath = currentPath[currentPathIndex];
            Vector3 targetWorldPosition = mapGenerator.groundLayer.GetCellCenterWorld(targetTileInPath);
            
            // Déterminer l'élévation où le monstre sera après ce pas.
            // La logique de FindOverallPath et IsMonsterMoveValid devrait déjà avoir planifié cela.
            // On peut essayer de la déduire pour mettre à jour currentMonsterElevation correctement.
            int nextStepElevation = currentMonsterElevation; // Supposition initiale
            
            // Si on est sur une tuile qui est une entrée/sortie d'escalier connue,
            // et que la tuile suivante du chemin est de l'autre côté de cet escalier,
            // alors l'élévation change.
            // Cette logique est complexe à déduire ici sans avoir les PathNodes complets.
            // Pour l'instant, on va se fier à l'ElevationData de la tuile cible,
            // en espérant que le pathfinding a correctement géré les transitions d'escalier.
            // Une solution plus robuste serait que FindOverallPath retourne des PathNodes avec l'élévation prévue.
            if (mapGenerator.IsTileWithinBounds(targetTileInPath.x, targetTileInPath.y))
            {
                int targetDataElevation = mapGenerator.ElevationData[targetTileInPath.x, targetTileInPath.y];
                bool targetIsStair = mapGenerator.stairsLayer.GetTile(targetTileInPath) != null;
                bool targetIsBridge = mapGenerator.bridgeLayer.GetTile(targetTileInPath) != null;

                // Logique simplifiée pour la mise à jour de l'élévation du monstre pendant le mouvement
                if (targetIsStair) { // Si la cible est une tuile d'escalier
                    // On est monté si on était L0 et l'escalier L0 mène à L1
                    // Ou on est descendu si on était L1 sur plateforme et l'escalier L0 est la cible
                    // Cela a dû être validé par IsMonsterMoveValid.
                    // On prend l'ElevationData comme indicateur brut, mais ce n'est pas parfait.
                    // Exemple: si currentMonsterElevation est 0 et on va sur un escalier (data 0) qui monte à L1,
                    // alors nextStepElevation devrait devenir 1.
                    // Pour une logique plus simple ici, on va se fier à IsMonsterMoveValid du pathfinder
                    // qui aurait dû déterminer la targetMonsterActualElevation
                    // Pour l'instant, on va prendre l'elevation data de la tuile, et ajuster pour les ponts.
                    nextStepElevation = targetDataElevation;
                    if (targetIsBridge && targetDataElevation == 1) nextStepElevation = 1; // Sur un pont, c'est L1
                    else if (targetIsBridge && targetDataElevation == 0) nextStepElevation = 0; // Devrait pas arriver, mais pour être sûr sous un pont L0
                    // La logique d'escalier dans IsMonsterMoveValid du pathfinder est la clé pour le *calcul* du chemin.
                    // Ici, on essaie de *refléter* ce changement.
                } else if (targetIsBridge) {
                    nextStepElevation = 1; // Si on est sur un pont, on est à L1
                } else {
                    nextStepElevation = targetDataElevation; // Sol normal
                }
            }


            Vector3 direction = (targetWorldPosition - transform.position).normalized;
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.fixedDeltaTime);
            GetComponent<Rigidbody2D>().MovePosition(newPosition);
            UpdateAnimator(direction);

            if (Vector3.Distance(transform.position, targetWorldPosition) < 0.1f)
            {
                transform.position = targetWorldPosition;
                currentLogicalTile = targetTileInPath;
                currentMonsterElevation = nextStepElevation; // MISE A JOUR IMPORTANTE
                currentPathIndex++;
                if (currentPathIndex >= currentPath.Count)
                {
                    StopMovement();
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