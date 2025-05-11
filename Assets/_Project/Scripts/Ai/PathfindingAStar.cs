// PathfindingAStar.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps; // <--- AJOUTEZ CETTE LIGNE !
public class PathfindingAStar : MonoBehaviour
{
    public MapGeneratorV2 mapGenerator; // Assignez dans l'Inspecteur
    // Référence au script de mouvement du joueur pour obtenir sa position et son élévation
    // Pourrait aussi être une référence directe au Transform du joueur.
    public PlayerMovement playerMovementScript; // Assignez le joueur ici

    private const int MOVE_STRAIGHT_COST = 10; // Coût pour un mouvement horizontal/vertical
    public int maxSearchDistance = 20;
    public int stairSearchRadius = 15; // Rayon pour chercher un escalier autour du monstre/joueur



    public List<Vector3Int> FindOverallPath(Vector3Int startWorldTile, int startElevation, Vector3Int playerWorldTile, int playerElevation)
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null || mapGenerator.stairsLayer == null)
        {
            Debug.LogError("PathfindingAStar: Critical references (MapGenerator, ElevationData, or StairsLayer) not set!");
            return null;
        }

        if (startElevation == playerElevation)
        {
            return FindDirectPath(startWorldTile, startElevation, playerWorldTile, playerElevation, playerWorldTile);
        }
        else
        {
            // Debug.Log($"Pathfinding: Different elevations. Monster L{startElevation} -> Player L{playerElevation}. Searching for stairs.");
            // Le point de départ de la recherche d'escalier est la position actuelle du monstre.
            // La cible "finale" de l'escalier est de l'amener au niveau du joueur.
            Vector3Int? stairEntryPoint = null; // Le point où le monstre doit aller pour utiliser l'escalier
            Vector3Int? stairExitPoint = null;  // Le point où le monstre sort de l'escalier au niveau cible
            int elevationAfterStair = -1;      // L'élévation du monstre après avoir pris l'escalier

            // 1. Trouver l'escalier le plus pertinent
            if (startElevation == 0 && playerElevation == 1) // Monstre L0 veut monter à L1
            {
                // Chercher un escalier (L0) proche du monstre qui mène à une plateforme L1
                FindClosestStairToTransition(startWorldTile, 0, 1, out stairEntryPoint, out stairExitPoint);
                if (stairEntryPoint.HasValue) elevationAfterStair = 1;
            }
            else if (startElevation == 1 && playerElevation == 0) // Monstre L1 veut descendre à L0
            {
                // Chercher une plateforme d'escalier (L1) proche du monstre qui mène à un escalier L0
                FindClosestStairToTransition(startWorldTile, 1, 0, out stairEntryPoint, out stairExitPoint);
                 if (stairEntryPoint.HasValue) elevationAfterStair = 0;
            }

            if (!stairEntryPoint.HasValue || !stairExitPoint.HasValue)
            {
                // Debug.LogWarning("Pathfinding: No suitable stair found to reach player's elevation.");
                return null; // Aucun escalier approprié trouvé
            }

            // 2. Chemin vers le point d'entrée de l'escalier
            // Le searchFocus pour ce segment est le stairEntryPoint.
            List<Vector3Int> pathToStair = FindDirectPath(startWorldTile, startElevation, stairEntryPoint.Value, startElevation, stairEntryPoint.Value);
            if (pathToStair == null || pathToStair.Count == 0)
            {
                // Debug.LogWarning($"Path to stair entry ({stairEntryPoint.Value} at L{startElevation}) not found.");
                return null;
            }

            // 3. Chemin depuis le point de sortie de l'escalier vers le joueur
            // Le searchFocus pour ce segment est la position du joueur.
            List<Vector3Int> pathFromStair = FindDirectPath(stairExitPoint.Value, elevationAfterStair, playerWorldTile, playerElevation, playerWorldTile);
            if (pathFromStair == null) // pathFromStair peut être vide si le joueur est sur le stairExitPoint
            {
                // Debug.LogWarning($"Path from stair exit ({stairExitPoint.Value} at L{elevationAfterStair}) to player not found.");
                // Si le joueur EST sur le stairExitPoint, le chemin sera juste ce point.
                if(stairExitPoint.Value == playerWorldTile && elevationAfterStair == playerElevation) {
                    pathFromStair = new List<Vector3Int> { stairExitPoint.Value };
                } else {
                    return null;
                }
            }
            
            // 4. Combiner les chemins
            List<Vector3Int> combinedPath = new List<Vector3Int>(pathToStair);
            if (pathFromStair.Count > 0)
            {
                // Si pathFromStair commence par le point où pathToStair se termine (stairExitPoint est conceptuellement lié à stairEntryPoint),
                // on enlève le premier point de pathFromStair s'il est identique au dernier de pathToStair,
                // ou si pathFromStair est juste le point de sortie.
                // La "traversée" de l'escalier est implicite.
                if (combinedPath.Last() == stairEntryPoint.Value) // On est arrivé à l'entrée de l'escalier
                {
                    // Si le point de sortie est différent, on l'ajoute comme un pas "magique"
                    // et on commence pathFromStair à partir de son deuxième élément s'il existe et n'est pas le point de sortie.
                    if (stairExitPoint.Value != pathFromStair.First()) {
                         // Ce cas est peu probable si pathFromStair est correct.
                         // On s'attend à ce que pathFromStair commence par stairExitPoint.Value
                    }

                    // On veut que le chemin continue depuis stairExitPoint.Value
                    // Si pathFromStair commence par stairExitPoint.Value, on peut skipp_er ce premier point de pathFromStair
                    // pour éviter un "pas sur place" si le monstre était déjà sur le stairExitPoint (ce qui est rare ici).
                    // Plus simple: si pathFromStair[0] est stairExitPoint.Value, on ajoute le reste de la liste.
                    if (pathFromStair.First() == stairExitPoint.Value)
                    {
                        combinedPath.AddRange(pathFromStair.Skip(pathFromStair.Count > 1 ? 1: 0)); // Skip le premier si la liste a plus d'un élément
                    } else {
                         // Cas étrange, le chemin depuis l'escalier ne commence pas au point de sortie attendu.
                         // On ajoute quand même, mais il pourrait y avoir un saut.
                        combinedPath.AddRange(pathFromStair);
                    }
                }
                else
                {
                    // Le chemin vers l'escalier ne s'est pas terminé exactement sur le point d'entrée, ou autre souci.
                    // On concatène tel quel, ce qui pourrait causer un saut.
                    combinedPath.AddRange(pathFromStair);
                }
            }
            // Debug.Log($"Pathfinding: Combined path length: {combinedPath.Count}");
            return combinedPath;
        }
    }

    // Cherche l'escalier le plus proche pour effectuer une transition d'élévation.
    // fromTile: position de départ de la recherche (position du monstre)
    // fromElevation: élévation actuelle du monstre
    // toElevation: élévation que le monstre veut atteindre
    // out stairAccessPoint: la tuile que le monstre doit atteindre pour INITIATE la transition
    // out stairDestinationPoint: la tuile où le monstre EMERGE après la transition
    private void FindClosestStairToTransition(Vector3Int fromTile, int fromElevation, int toElevation,
                                           out Vector3Int? stairAccessPoint, out Vector3Int? stairDestinationPoint)
    {
        stairAccessPoint = null;
        stairDestinationPoint = null;
        float minDistanceSq = float.MaxValue;

        // Parcourir une zone limitée autour de 'fromTile' pour trouver des escaliers
        for (int x = fromTile.x - stairSearchRadius; x <= fromTile.x + stairSearchRadius; x++)
        {
            for (int y = fromTile.y - stairSearchRadius; y <= fromTile.y + stairSearchRadius; y++)
            {
                Vector3Int currentTilePos = new Vector3Int(x, y, 0);
                if (!mapGenerator.IsTileWithinBounds(currentTilePos.x,currentTilePos.y)) continue;

                TileBase stairTile = mapGenerator.stairsLayer.GetTile(currentTilePos);
                if (stairTile == null) continue; // Ce n'est pas une tuile d'escalier

                int stairTileDataElevation = mapGenerator.ElevationData[x, y];

                if (fromElevation == 0 && toElevation == 1) // Veut monter L0 -> L1
                {
                    // L'escalier lui-même doit être à L0
                    if (stairTileDataElevation == 0)
                    {
                        // Vérifier si cet escalier mène bien à une plateforme L1 (logique spécifique à vos escaliers)
                        // Ex: pour un escalier "Sud" (monte vers le Nord)
                        Vector3Int platformAbove = new Vector3Int(x, y + 1, 0);
                        if (mapGenerator.IsTileWithinBounds(platformAbove.x,platformAbove.y) && mapGenerator.ElevationData[platformAbove.x, platformAbove.y] == 1 &&
                            (mapGenerator.groundLayer.GetTile(platformAbove) != null || mapGenerator.bridgeLayer.GetTile(platformAbove) != null))
                        {
                            float distSq = (currentTilePos - fromTile).sqrMagnitude;
                            if (distSq < minDistanceSq)
                            {
                                minDistanceSq = distSq;
                                stairAccessPoint = currentTilePos;      // Le monstre va à la tuile d'escalier L0
                                stairDestinationPoint = platformAbove;  // Il sortira sur la plateforme L1
                            }
                        }
                    }
                }
                else if (fromElevation == 1 && toElevation == 0) // Veut descendre L1 -> L0
                {
                    // Le monstre est à L1. Il cherche une tuile de "plateforme" (currentTilePos) qui est L1
                    // et qui est directement connectée à une tuile d'escalier en dessous (L0).
                    if (stairTileDataElevation == 1) // La tuile que nous scannons (currentTilePos) doit être la plateforme L1
                    {
                         // Vérifier si la tuile EN DESSOUS (Sud pour un escalier Sud) est une tuile d'escalier L0
                        Vector3Int stairBelow = new Vector3Int(x, y - 1, 0); // Pour un escalier Sud, on descend vers le Sud
                        if (mapGenerator.IsTileWithinBounds(stairBelow.x,stairBelow.y) &&
                            mapGenerator.stairsLayer.GetTile(stairBelow) != null &&
                            mapGenerator.ElevationData[stairBelow.x, stairBelow.y] == 0)
                        {
                            float distSq = (currentTilePos - fromTile).sqrMagnitude;
                            if (distSq < minDistanceSq)
                            {
                                minDistanceSq = distSq;
                                stairAccessPoint = currentTilePos;      // Le monstre va à la plateforme L1
                                stairDestinationPoint = stairBelow;     // Il sortira sur la tuile d'escalier L0
                            }
                        }
                    }
                }
            }
        }
    }

    // ... (FindDirectPath, CreateNodeFromWorldTile, IsMonsterMoveValid, GetNeighbourOffsets, RetracePath) ...
    // IsMonsterMoveValid doit être mis à jour pour gérer correctement la traversée d'escalier
    // identifiée par FindClosestStairToTransition.

    // Dans IsMonsterMoveValid, la logique d'escalier doit être plus précise :
    private bool IsMonsterMoveValid(Vector2Int currentMonsterGridPos, int currentMonsterActualElevation, Vector2Int targetGridPos, out int targetMonsterActualElevation)
    {
        targetMonsterActualElevation = currentMonsterActualElevation; // Valeur par défaut
        if (!mapGenerator.IsTileWithinBounds(targetGridPos.x, targetGridPos.y)) return false;

        Vector3Int currentWorldTile = new Vector3Int(currentMonsterGridPos.x, currentMonsterGridPos.y, 0);
        Vector3Int targetWorldTile = new Vector3Int(targetGridPos.x, targetGridPos.y, 0);
        int targetTileDataElevation = mapGenerator.ElevationData[targetGridPos.x, targetGridPos.y];

        if (mapGenerator.wallsLayer.GetTile(targetWorldTile) != null) return false;
        
        TileBase groundOnTarget = mapGenerator.groundLayer.GetTile(targetWorldTile);
        TileBase bridgeOnTarget = mapGenerator.bridgeLayer.GetTile(targetWorldTile);
        TileBase stairsOnTarget = mapGenerator.stairsLayer.GetTile(targetWorldTile);
        TileBase stairsOnCurrent = mapGenerator.stairsLayer.GetTile(currentWorldTile);


        if (groundOnTarget == null && bridgeOnTarget == null && stairsOnTarget == null) return false;

        // 1. Transition d'escalier en MONTANT (L0 -> L1)
        if (currentMonsterActualElevation == 0 && stairsOnTarget != null && targetTileDataElevation == 0)
        {
            Vector3Int platformAbove = new Vector3Int(targetGridPos.x, targetGridPos.y + 1, 0); // Pour escalier Sud
            if (mapGenerator.IsTileWithinBounds(platformAbove.x,platformAbove.y) && mapGenerator.ElevationData[platformAbove.x, platformAbove.y] == 1 &&
                (mapGenerator.groundLayer.GetTile(platformAbove) != null || mapGenerator.bridgeLayer.GetTile(platformAbove) != null))
            {
                targetMonsterActualElevation = 1; // Le monstre va être à L1
                return true;
            }
        }

        // 2. Transition d'escalier en DESCENDANT (L1 -> L0)
        // Le monstre est sur une plateforme L1 (current) et la cible est une tuile d'escalier L0 (target)
        if (currentMonsterActualElevation == 1 && stairsOnTarget != null && targetTileDataElevation == 0)
        {
            // Vérifier si la tuile actuelle (plateforme L1) est bien la "sortie haute" de l'escalier cible
            // Ex: pour escalier Sud, current est (target.x, target.y + 1)
            if (currentMonsterGridPos.x == targetGridPos.x && currentMonsterGridPos.y == targetGridPos.y + 1)
            {
                targetMonsterActualElevation = 0; // Le monstre va être à L0
                return true;
            }
        }

        // 3. Mouvement sur Pont
        if (bridgeOnTarget != null)
        {
            if (currentMonsterActualElevation == 1 && targetTileDataElevation == 1) { targetMonsterActualElevation = 1; return true; } // Sur le pont
            else if (currentMonsterActualElevation == 0 && targetTileDataElevation == 1) // Sous un pont
            {
                 if (groundOnTarget != null && mapGenerator.ElevationData[targetGridPos.x, targetGridPos.y] == 0) // Sol L0 sous le pont
                 { targetMonsterActualElevation = 0; return true; } // On suppose que ElevationData pour un pont est 1. Si sol en dessous, ElevationData de la tuile (x,y) sera 0.
                 // Correction : si targetTileDataElevation est 1 (c'est un pont), mais le sol en dessous est L0.
                 // La tile (x,y) a une ElevationData de 1 à cause du pont.
                 // Il faut vérifier la tuile de sol à (x,y) et son ElevationData propre.
                 // Le plus simple: si currentMonsterActualElevation == 0 et bridgeOnTarget != null (signifie qu'il y a un pont au-dessus),
                 // alors le mouvement n'est valide que s'il y a du sol à L0.
                 if (mapGenerator.groundLayer.GetTile(targetWorldTile) != null && mapGenerator.ElevationData[targetWorldTile.x, targetWorldTile.y] == 0) {
                     targetMonsterActualElevation = 0;
                     return true;
                 }
                 return false;
            }
             // Si currentMonsterActualElevation est 1 et targetTileDataElevation est 0 (et ce n'est pas un escalier), c'est une falaise.
            else if (currentMonsterActualElevation == 1 && targetTileDataElevation == 0 && stairsOnTarget == null) return false;
        }

        // 4. Mouvement sur Sol Normal (pas un escalier en transition, pas un pont)
        if (targetTileDataElevation == currentMonsterActualElevation)
        {
            // S'assurer qu'on ne descend pas d'un pont vers du sol si on était L1 sur un pont
            if (mapGenerator.bridgeLayer.GetTile(currentWorldTile) != null && currentMonsterActualElevation == 1 && targetTileDataElevation == 1) {
                // On est sur un pont et on reste à L1 (potentiellement vers une autre tuile de pont ou une plateforme L1)
            } else if (mapGenerator.bridgeLayer.GetTile(currentWorldTile) != null && currentMonsterActualElevation == 1 && targetTileDataElevation == 0) {
                 return false; // Tombe d'un pont
            }
            targetMonsterActualElevation = currentMonsterActualElevation;
            return true;
        }
        
        return false; // Changement d'élévation non géré (falaise) ou autre cas non couvert
    }

    public List<Vector3Int> FindPath(Vector3Int startWorldTile, int startElevation, Vector3Int endWorldTile, int endElevation)
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null)
        {
            Debug.LogError("PathfindingAStar: MapGeneratorV2 or ElevationData not set!");
            return null;
        }

        PathNode startNode = CreateNodeFromWorldTile(startWorldTile, startElevation);
        PathNode endNode = CreateNodeFromWorldTile(endWorldTile, endElevation);

        if (startNode == null || !startNode.isWalkable) { /*Debug.LogWarning("Start node is not walkable or invalid.");*/ return null; }
        if (endNode == null || !endNode.isWalkable) { /* Debug.LogWarning("End node is not walkable or invalid.");*/ return null; }


        List<PathNode> openList = new List<PathNode>();
        HashSet<PathNode> closedList = new HashSet<PathNode>(); // HashSet pour des recherches rapides

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            PathNode currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            if (currentNode.gridPosition == endNode.gridPosition) // Chemin trouvé
            {
                return RetracePath(startNode, currentNode);
            }

            // Vérifier les voisins (haut, bas, gauche, droite)
            foreach (Vector2Int offset in GetNeighbourOffsets())
            {
                Vector2Int neighbourGridPos = currentNode.gridPosition + offset;
                Vector3Int neighbourWorldTile = new Vector3Int(neighbourGridPos.x, neighbourGridPos.y, 0);

                // On doit déterminer l'élévation du voisin.
                // Pour un pathfinding simple, on pourrait supposer que le monstre essaie de rester à la même élévation
                // ou de ne changer que via des "rampes" ou "escaliers" logiques s'ils sont définis.
                // Pour l'instant, on vérifie si le mouvement est logiquement possible pour un PNJ.
                // On utilise l'élévation du currentNode comme "élévation actuelle" du monstre pour ce pas.
                if (!IsMonsterMoveValid(currentNode.gridPosition, currentNode.elevation, neighbourGridPos, out int neighbourElevation))
                {
                    continue; // Mouvement non valide vers ce voisin
                }
                
                PathNode neighbourNode = new PathNode(neighbourGridPos, neighbourElevation, true); // On suppose walkable si IsMonsterMoveValid passe

                if (closedList.Contains(neighbourNode)) // Déjà évalué
                {
                    continue;
                }
                
                neighbourNode.CalculateHeuristic(endNode.gridPosition); // Calculer H seulement maintenant
                int tentativeGCost = currentNode.gCost + MOVE_STRAIGHT_COST; // + coût de déplacement vers ce voisin

                bool inOpenList = openList.Any(node => node.gridPosition == neighbourNode.gridPosition);

                if (tentativeGCost < neighbourNode.gCost || !inOpenList)
                {
                    neighbourNode.gCost = tentativeGCost;
                    // neighbourNode.hCost a déjà été calculé
                    neighbourNode.cameFromNode = currentNode;

                    if (!inOpenList)
                    {
                        openList.Add(neighbourNode);
                    } else {
                        // Mettre à jour le nœud dans openList s'il y est déjà avec un meilleur chemin
                        // C'est géré implicitement si on recalcule gCost et cameFromNode
                        // et que la prochaine sélection du nœud le moins cher le reprend.
                        // Pour une implémentation plus stricte de A*, on pourrait mettre à jour l'objet existant.
                    }
                }
            }
        }
        // Aucun chemin trouvé
        return null;
    }

    private PathNode CreateNodeFromWorldTile(Vector3Int worldTile, int elevation)
    {
        if (!mapGenerator.IsTileWithinBounds(worldTile.x, worldTile.y)) return null;
        
        // Pour la validité du nœud, on pourrait utiliser une logique similaire à CanLogicallyMoveToTile du joueur,
        // mais adaptée aux capacités du monstre (par exemple, un monstre ne peut pas utiliser d'escaliers de la même manière).
        // Pour l'instant, on vérifie juste les murs.
        bool isWalkable = mapGenerator.wallsLayer.GetTile(worldTile) == null;
        // On pourrait aussi vérifier si la tuile a du sol, etc.
        // bool hasGround = mapGenerator.groundLayer.GetTile(worldTile) != null;
        // isWalkable = isWalkable && hasGround;


        // L'élévation du nœud est l'élévation réelle de la tuile, mais la validité du mouvement
        // dépendra de l'élévation ACTUELLE du monstre et de la tuile cible.
        int tileDataElevation = mapGenerator.ElevationData[worldTile.x, worldTile.y];

        // Un nœud est "walkable" pour le pathfinding si la tuile elle-même n'est pas un obstacle intrinsèque (mur).
        // La logique de transition d'élévation sera gérée par IsMonsterMoveValid.
        return new PathNode(new Vector2Int(worldTile.x, worldTile.y), tileDataElevation, isWalkable);
    }

    public List<Vector3Int> FindDirectPath(Vector3Int startWorldTile, int startElevation, Vector3Int endWorldTile, int endElevation, Vector3Int searchFocusTile)
{
    PathNode startNode = CreateNodeFromWorldTile(startWorldTile, startElevation);
    PathNode endNode = CreateNodeFromWorldTile(endWorldTile, endElevation); // L'élévation ici est celle où on s'attend à trouver la cible

    if (startNode == null || !startNode.isWalkable) { /* Debug.Log($"Start node {startWorldTile} invalid or not walkable"); */ return null; }
    // Pour endNode, il est important que la tuile elle-même soit valide (pas hors map),
    // mais sa "walkability" est moins critique si c'est juste une cible.
    // Cependant, si la cible doit être atteinte "sur" une tuile walkable, alors la vérification est pertinente.
    if (endNode == null) { /* Debug.Log($"End node {endWorldTile} invalid (out of bounds)"); */ return null; }
    // On pourrait vouloir vérifier si endNode.isWalkable SEULEMENT si ce n'est pas la position exacte du joueur
    // (qui pourrait être sur une tuile non walkable par le monstre mais quand même une cible)
    // Pour l'instant, on va supposer que la tuile cible elle-même doit être "basiquement" valide.


    List<PathNode> openList = new List<PathNode>();
    HashSet<PathNode> closedList = new HashSet<PathNode>();
    openList.Add(startNode);

    int iterations = 0;
    int maxIterations = maxSearchDistance * maxSearchDistance * 4; // Un peu plus généreux

    while (openList.Count > 0)
    {
        iterations++;
        if (iterations > maxIterations) { /* Debug.LogWarning("Pathfinding A* (FindDirectPath) reached max iterations.");*/ return null; }

        PathNode currentNode = openList[0];
        for (int i = 1; i < openList.Count; i++)
        {
            if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].hCost < currentNode.hCost))
            {
                currentNode = openList[i];
            }
        }

        openList.Remove(currentNode);
        closedList.Add(currentNode);

        if (currentNode.gridPosition == endNode.gridPosition && currentNode.elevation == endElevation) // Vérifier aussi l'élévation pour la cible finale
        {
            return RetracePath(startNode, currentNode);
        }

        foreach (Vector2Int offset in GetNeighbourOffsets())
        {
            Vector2Int neighbourGridPos = currentNode.gridPosition + offset;

            int neighbourDistX = Mathf.Abs(neighbourGridPos.x - searchFocusTile.x);
            int neighbourDistY = Mathf.Abs(neighbourGridPos.y - searchFocusTile.y);
            if (neighbourDistX > maxSearchDistance || neighbourDistY > maxSearchDistance)
            {
                continue;
            }

            if (!IsMonsterMoveValid(currentNode.gridPosition, currentNode.elevation, neighbourGridPos, out int neighbourNodeActualElevation))
            {
                continue;
            }
            
            PathNode neighbourNode = new PathNode(neighbourGridPos, neighbourNodeActualElevation, true);

            if (closedList.Contains(neighbourNode)) continue;
            
            neighbourNode.CalculateHeuristic(endNode.gridPosition); // HCost est basé sur la position de la grille de fin
            int tentativeGCost = currentNode.gCost + MOVE_STRAIGHT_COST;

            bool inOpenList = false;
            PathNode existingNodeInOpenList = null;
            for(int i=0; i < openList.Count; i++) // Utiliser une boucle for pour pouvoir modifier si nécessaire
            {
                if(openList[i].gridPosition == neighbourNode.gridPosition && openList[i].elevation == neighbourNode.elevation)
                {
                    inOpenList = true;
                    existingNodeInOpenList = openList[i];
                    break;
                }
            }

            if (!inOpenList)
            {
                neighbourNode.gCost = tentativeGCost;
                neighbourNode.cameFromNode = currentNode;
                openList.Add(neighbourNode);
            }
            else if (tentativeGCost < existingNodeInOpenList.gCost)
            {
                existingNodeInOpenList.gCost = tentativeGCost;
                existingNodeInOpenList.cameFromNode = currentNode;
                // Pas besoin de retirer et réinsérer pour mettre à jour la priorité dans une List simple,
                // la prochaine recherche du min la trouvera. Pour une PriorityQueue, ce serait différent.
            }
        }
    }
    return null;
}

    private List<Vector2Int> GetNeighbourOffsets()
    {
        return new List<Vector2Int>
        {
            new Vector2Int(0, 1),  // Haut
            new Vector2Int(0, -1), // Bas
            new Vector2Int(1, 0),  // Droite
            new Vector2Int(-1, 0) // Gauche
        };
    }

    private List<Vector3Int> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<PathNode> path = new List<PathNode>();
        PathNode currentNode = endNode;

        while (currentNode != startNode && currentNode != null)
        {
            path.Add(currentNode);
            currentNode = currentNode.cameFromNode;
        }
        if (currentNode == startNode) path.Add(startNode); // Ajouter le nœud de départ

        path.Reverse(); // Le chemin est de la fin au début, donc on l'inverse

        // Convertir en List<Vector3Int> pour une utilisation plus facile par le monstre
        List<Vector3Int> worldPath = new List<Vector3Int>();
        foreach(PathNode node in path)
        {
            worldPath.Add(new Vector3Int(node.gridPosition.x, node.gridPosition.y, 0));
        }
        // On pourrait vouloir retourner List<PathNode> si on a besoin de l'info d'élévation pour chaque pas.
        // Pour l'instant, on retourne juste les positions.
        return worldPath;
    }
}