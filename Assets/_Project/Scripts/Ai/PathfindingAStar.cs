// PathfindingAStar.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps; // <--- AJOUTEZ CETTE LIGNE !
public class PathfindingAStar : MonoBehaviour
{
    public MapGeneratorV2 mapGenerator;
    public PlayerMovement playerMovementScript;

    private const int MOVE_STRAIGHT_COST = 10;
    public int maxSearchDistanceDirectPath = 20; // Limite pour FindDirectPath
    public int stairSearchRadius = 15;         // Rayon pour scanner les tuiles d'escalier

    // Structure pour stocker un escalier candidat et le chemin pour y accéder
    private class PotentialStairTarget
    {
        public Vector3Int stairTilePos;        // Position de la tuile d'escalier (L1 data)
        public Vector3Int accessPointToReach;  // Tuile que le monstre doit atteindre pour initier la transition
        public Vector3Int exitPointAfter;      // Tuile où le monstre sort après la transition
        public List<Vector3Int> pathToAccessPoint;
        public int pathCostToAccessPoint;

        public PotentialStairTarget(Vector3Int stair, Vector3Int access, Vector3Int exit, List<Vector3Int> path, int cost)
        {
            stairTilePos = stair;
            accessPointToReach = access;
            exitPointAfter = exit;
            pathToAccessPoint = path;
            pathCostToAccessPoint = cost;
        }
    }

    public List<Vector3Int> FindOverallPath(Vector3Int startWorldTile, int startElevation, Vector3Int playerWorldTile, int playerElevation)
    {
        if (mapGenerator == null || mapGenerator.ElevationData == null || mapGenerator.stairsLayer == null || mapGenerator.stair_S_Tile == null)
        {
            Debug.LogError("PathfindingAStar: Critical references not set (MapGenerator, ElevationData, StairsLayer, or Stair_S_Tile)!");
            return null;
        }

        if (startElevation == playerElevation)
        {
            // Debug.Log($"[FindOverallPath] Same elevation (L{startElevation}). Direct path to player {playerWorldTile}.");
            return FindDirectPath(startWorldTile, startElevation, playerWorldTile, playerElevation, playerWorldTile, maxSearchDistanceDirectPath);
        }
        else
        {
            // Debug.Log($"[FindOverallPath] Different elevations. Monster L{startElevation} -> Player L{playerElevation}. Searching for accessible stairs.");
            List<PotentialStairTarget> accessibleStairs = FindAccessibleStairsForTransition(startWorldTile, startElevation, playerElevation);

            if (accessibleStairs == null || accessibleStairs.Count == 0)
            {
                // Debug.LogWarning("[FindOverallPath] No accessible stairs found for elevation transition.");
                return null;
            }

            // Choisir l'escalier avec le chemin d'accès le plus court
            PotentialStairTarget bestStairTarget = accessibleStairs.OrderBy(s => s.pathCostToAccessPoint).FirstOrDefault();

            if (bestStairTarget == null) // Ne devrait pas arriver si la liste n'est pas vide, mais par sécurité
            {
                // Debug.LogWarning("[FindOverallPath] BestStairTarget is null after ordering.");
                return null;
            }

            // Debug.Log($"[FindOverallPath] Best accessible stair chosen: {bestStairTarget.stairTilePos} (Access via {bestStairTarget.accessPointToReach}). Path cost to access: {bestStairTarget.pathCostToAccessPoint}");

            List<Vector3Int> pathToStairAccess = bestStairTarget.pathToAccessPoint;

            // Le dernier pas du pathToStairAccess amène le monstre sur le point d'accès.
            // Le "pas suivant" est la transition via l'escalier.
            // L'élévation après l'escalier sera 'playerElevation' (la cible).
            int elevationAfterStair = playerElevation; // On veut atteindre l'élévation du joueur

            // Path depuis la sortie de l'escalier jusqu'au joueur
            List<Vector3Int> pathFromStairExit = FindDirectPath(bestStairTarget.exitPointAfter, elevationAfterStair, playerWorldTile, playerElevation, playerWorldTile, maxSearchDistanceDirectPath);

            if (pathFromStairExit == null)
            {
                // Debug.LogWarning($"[FindOverallPath] Path from stair exit {bestStairTarget.exitPointAfter} (L{elevationAfterStair}) to player {playerWorldTile} (L{playerElevation}) not found.");
                // Si le joueur est sur le point de sortie, pathFromStairExit pourrait être juste ce point.
                 if(bestStairTarget.exitPointAfter == playerWorldTile && elevationAfterStair == playerElevation) {
                    pathFromStairExit = new List<Vector3Int> { bestStairTarget.exitPointAfter };
                } else {
                    return null; // Impossible d'atteindre le joueur depuis cet escalier
                }
            }
            
            // Combiner les chemins
            List<Vector3Int> combinedPath = new List<Vector3Int>(pathToStairAccess);
            
            // La "traversée" de l'escalier est le passage de bestStairTarget.accessPointToReach
            // à bestStairTarget.exitPointAfter.
            // On ajoute le point de sortie de l'escalier comme prochain pas si ce n'est pas déjà la fin du chemin d'accès
            // et si le chemin depuis la sortie n'est pas vide et ne commence pas déjà par ce point.
            if (combinedPath.Last() == bestStairTarget.accessPointToReach) {
                if (pathFromStairExit.Count > 0) {
                    if (pathFromStairExit.First() == bestStairTarget.exitPointAfter) {
                        combinedPath.AddRange(pathFromStairExit); // pathFromStairExit inclut déjà exitPointAfter
                    } else {
                        // Cas étrange, on ajoute quand même, pourrait créer un "saut"
                        combinedPath.Add(bestStairTarget.exitPointAfter);
                        combinedPath.AddRange(pathFromStairExit);
                    }
                } else { // pathFromStairExit est vide, signifie que le joueur est sur exitPointAfter
                     combinedPath.Add(bestStairTarget.exitPointAfter);
                }
            } else {
                 // Le chemin vers l'escalier ne s'est pas terminé sur le point d'accès attendu.
                 // On ajoute quand même, mais il pourrait y avoir un saut.
                 combinedPath.Add(bestStairTarget.exitPointAfter);
                 combinedPath.AddRange(pathFromStairExit);
            }
            
            // Optionnel: Nettoyer les doublons consécutifs au cas où
            if (combinedPath.Count > 1) {
                for (int i = combinedPath.Count - 1; i > 0; i--) {
                    if (combinedPath[i] == combinedPath[i-1]) {
                        combinedPath.RemoveAt(i);
                    }
                }
            }

            // Debug.Log($"[FindOverallPath] Combined path length: {combinedPath.Count}");
            return combinedPath;
        }
    }


    private List<PotentialStairTarget> FindAccessibleStairsForTransition(Vector3Int fromTileStartSearch, int currentMonsterElevation, int targetPlayerElevation)
    {
        List<PotentialStairTarget> potentialTargets = new List<PotentialStairTarget>();

        // Debug.Log($"[FindAccessibleStairs] Searching from {fromTileStartSearch} (L{currentMonsterElevation}) to L{targetPlayerElevation}. Radius: {stairSearchRadius}");

        for (int xOffset = -stairSearchRadius; xOffset <= stairSearchRadius; xOffset++)
        {
            for (int yOffset = -stairSearchRadius; yOffset <= stairSearchRadius; yOffset++)
            {
                Vector3Int potentialStairTilePos = new Vector3Int(fromTileStartSearch.x + xOffset, fromTileStartSearch.y + yOffset, 0);

                if (!mapGenerator.IsTileWithinBounds(potentialStairTilePos.x, potentialStairTilePos.y)) continue;

                TileBase tileOnStairLayer = mapGenerator.stairsLayer.GetTile(potentialStairTilePos);

                if (tileOnStairLayer == mapGenerator.stair_S_Tile) // C'est un stair_S_Tile
                {
                    

                    Vector3Int accessPointForThisStair = Vector3Int.zero;
                    Vector3Int exitPointFromThisStair = Vector3Int.zero;
                    bool stairIsValidForTransition = false;

                    // Pour un stair_S_Tile à potentialStairTilePos (L1 data):
                    Vector3Int groundSouthOfStair = new Vector3Int(potentialStairTilePos.x, potentialStairTilePos.y - 1, 0);
                    Vector3Int platformNorthOfStair = new Vector3Int(potentialStairTilePos.x, potentialStairTilePos.y + 1, 0);

                    bool groundSouthIsValidL0 = mapGenerator.IsTileWithinBounds(groundSouthOfStair.x, groundSouthOfStair.y) &&
                                             mapGenerator.ElevationData[groundSouthOfStair.x, groundSouthOfStair.y] == 0 &&
                                             mapGenerator.groundLayer.GetTile(groundSouthOfStair) != null;

                    bool platformNorthIsValidL1 = mapGenerator.IsTileWithinBounds(platformNorthOfStair.x, platformNorthOfStair.y) &&
                                                 mapGenerator.ElevationData[platformNorthOfStair.x, platformNorthOfStair.y] == 1 &&
                                                 (mapGenerator.groundLayer.GetTile(platformNorthOfStair) != null || (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(platformNorthOfStair) != null));
                    
                    if (!groundSouthIsValidL0 || !platformNorthIsValidL1) continue; // Escalier mal formé ou bloqué

                    if (currentMonsterElevation == 0 && targetPlayerElevation == 1) // Veut MONTER
                    {
                        // Pour monter, le monstre (L0) doit aller à la tuile d'escalier L1 (potentialStairTilePos)
                        // en passant par la tuile de sol L0 adjacente (groundSouthOfStair).
                        // Le point d'accès direct pour le pathfinding A* (qui se fera à L0) est groundSouthOfStair.
                        accessPointForThisStair = groundSouthOfStair;
                        exitPointFromThisStair = platformNorthOfStair; // Sortira sur la plateforme L1 au nord
                        stairIsValidForTransition = true;
                    }
                    else if (currentMonsterElevation == 1 && targetPlayerElevation == 0) // Veut DESCENDRE
                    {
                        // Pour descendre, le monstre (L1) doit aller à la tuile d'escalier L1 (potentialStairTilePos)
                        // en passant par la tuile de plateforme L1 adjacente (platformNorthOfStair).
                        // Le point d'accès direct pour le pathfinding A* (qui se fera à L1) est platformNorthOfStair.
                        accessPointForThisStair = platformNorthOfStair;
                        exitPointFromThisStair = groundSouthOfStair; // Sortira sur le sol L0 au sud
                        stairIsValidForTransition = true;
                    }

                    if (stairIsValidForTransition)
                    {
                        // Calculer un chemin A* vers le point d'accès de cet escalier (en restant sur l'élévation actuelle du monstre)
                        // Le searchFocus pour ce A* est l'accessPointForThisStair lui-même.
                        // La limite de recherche pour ce A* pourrait être plus petite que maxSearchDistanceDirectPath,
                        // ou on utilise stairSearchRadius comme approximation de la distance max à l'escalier.
                        List<Vector3Int> pathToAccess = FindDirectPath(fromTileStartSearch, currentMonsterElevation, accessPointForThisStair, currentMonsterElevation, accessPointForThisStair, stairSearchRadius + 5); // +5 de marge

                        if (pathToAccess != null && pathToAccess.Count > 0)
                        {
                            int pathCost = (pathToAccess.Count -1) * MOVE_STRAIGHT_COST; // Coût simple basé sur la longueur
                            potentialTargets.Add(new PotentialStairTarget(potentialStairTilePos, accessPointForThisStair, exitPointFromThisStair, pathToAccess, pathCost));
                            // Debug.Log($"[FindAccessibleStairs] Found accessible stair {potentialStairTilePos} via {accessPointForThisStair}. Path cost: {pathCost}");
                        }
                    }
                }
            }
        }
        // Debug.Log($"[FindAccessibleStairs] Found {potentialTargets.Count} accessible stairs.");
        return potentialTargets;
    }


    // FindDirectPath a maintenant un paramètre maxDistance pour limiter sa recherche
    public List<Vector3Int> FindDirectPath(Vector3Int startWorldTile, int startElevation, Vector3Int endWorldTile, int endElevation, Vector3Int searchFocusTile, int maxDistance)
    {
        PathNode startNode = CreateNodeFromWorldTile(startWorldTile, startElevation);
        PathNode endNode = CreateNodeFromWorldTile(endWorldTile, endElevation);

        if (startNode == null || !startNode.isWalkable) return null;
        if (endNode == null) return null;

        List<PathNode> openList = new List<PathNode>();
        HashSet<PathNode> closedList = new HashSet<PathNode>();
        openList.Add(startNode);

        int iterations = 0;
        int maxIterations = maxDistance * maxDistance * 4; // Ajusté avec maxDistance

        while (openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations) { /*Debug.LogWarning($"FindDirectPath A* reached max iterations ({maxIterations}). Search focus: {searchFocusTile}, MaxDist: {maxDistance}");*/ return null; }

            PathNode currentNode = openList[0];
            // ... (logique de sélection du meilleur nœud dans openList - inchangée) ...
            for (int i = 1; i < openList.Count; i++) { if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].hCost < currentNode.hCost)) { currentNode = openList[i]; } }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            if (currentNode.gridPosition == endNode.gridPosition && currentNode.elevation == endElevation)
            {
                return RetracePath(startNode, currentNode);
            }

            // LIMITE DE PORTEE DE RECHERCHE (utilisant maxDistance)
            int distFromStartX = Mathf.Abs(currentNode.gridPosition.x - startNode.gridPosition.x);
            int distFromStartY = Mathf.Abs(currentNode.gridPosition.y - startNode.gridPosition.y);
            // On utilise la distance de Manhattan depuis le point de départ du chemin actuel
            if (distFromStartX + distFromStartY > maxDistance) 
            {
                continue; // Trop loin du point de départ de ce segment de chemin
            }

            foreach (Vector2Int offset in GetNeighbourOffsets())
            {
                Vector2Int neighbourGridPos = currentNode.gridPosition + offset;

                // NOUVELLE LIMITE DE PORTEE : ne pas considérer les voisins qui nous éloigneraient trop de la source de CE CHEMIN.
                // Cela remplace la vérification par rapport à searchFocusTile pour cette fonction spécifique.
                int neighbourDistFromStartX = Mathf.Abs(neighbourGridPos.x - startNode.gridPosition.x);
                int neighbourDistFromStartY = Mathf.Abs(neighbourGridPos.y - startNode.gridPosition.y);
                if (neighbourDistFromStartX + neighbourDistFromStartY > maxDistance)
                {
                    continue; 
                }

                if (!IsMonsterMoveValid(currentNode.gridPosition, currentNode.elevation, neighbourGridPos, out int neighbourNodeActualElevation))
                {
                    continue;
                }
                
                PathNode neighbourNode = new PathNode(neighbourGridPos, neighbourNodeActualElevation, true);
                if (closedList.Contains(neighbourNode)) continue;
                
                neighbourNode.CalculateHeuristic(endNode.gridPosition);
                int tentativeGCost = currentNode.gCost + MOVE_STRAIGHT_COST;

                // ... (logique de mise à jour ou d'ajout à openList - inchangée) ...
                bool inOpenList = false; PathNode existingNodeInOpenList = null;
                for(int i=0; i < openList.Count; i++) { if(openList[i].gridPosition == neighbourNode.gridPosition && openList[i].elevation == neighbourNode.elevation) { inOpenList = true; existingNodeInOpenList = openList[i]; break; } }
                if (!inOpenList) { neighbourNode.gCost = tentativeGCost; neighbourNode.cameFromNode = currentNode; openList.Add(neighbourNode); }
                else if (tentativeGCost < existingNodeInOpenList.gCost) { existingNodeInOpenList.gCost = tentativeGCost; existingNodeInOpenList.cameFromNode = currentNode; }
            }
        }
        return null;
    }
private bool IsMonsterMoveValid(Vector2Int currentMonsterGridPos, int currentMonsterActualElevation, Vector2Int targetGridPos, out int targetMonsterActualElevation)
{
    targetMonsterActualElevation = currentMonsterActualElevation; // Valeur par défaut
    if (!mapGenerator.IsTileWithinBounds(targetGridPos.x, targetGridPos.y)) return false;

    Vector3Int currentWorldTile = new Vector3Int(currentMonsterGridPos.x, currentMonsterGridPos.y, 0);
    Vector3Int targetWorldTile = new Vector3Int(targetGridPos.x, targetGridPos.y, 0);
    // L'ElevationData de la tuile cible est importante pour le sol/ponts,
    // mais pour les escaliers, on se fie plus à la position relative et au type de tuile.
    int targetTileDataElevation = mapGenerator.ElevationData[targetGridPos.x, targetGridPos.y];

    // 0. Vérification des Obstacles Bloquants
    if (mapGenerator.wallsLayer.GetTile(targetWorldTile) != null) return false;
    
    TileBase groundOnTarget = mapGenerator.groundLayer.GetTile(targetWorldTile);
    TileBase bridgeOnTarget = (mapGenerator.bridgeLayer != null) ? mapGenerator.bridgeLayer.GetTile(targetWorldTile) : null;
    TileBase stairsOnTarget = mapGenerator.stairsLayer.GetTile(targetWorldTile);

    // Si la cible n'a aucune surface praticable (sol, pont, ou escalier), c'est invalide.
    if (groundOnTarget == null && bridgeOnTarget == null && stairsOnTarget == null) return false;

    // 1. EST-CE QUE LA CIBLE EST NOTRE TUILE D'ESCALIER SPÉCIFIQUE ?
    if (stairsOnTarget == mapGenerator.stair_S_Tile)
    {
        // Supposition: la tuile stair_S_Tile elle-même a une ElevationData de 1.

        // CAS 1.A: MONTER (L0 -> L1)
        // Le monstre est à L0 (currentMonsterActualElevation == 0).
        // La cible (targetWorldTile) est la tuile stair_S_Tile.
        // La tuile actuelle du monstre (currentMonsterGridPos) doit être la case L0 juste EN DESSOUS (Sud) de cet escalier.
        if (currentMonsterActualElevation == 0)
        {
            // Pour un stair_S_Tile à targetGridPos, la case d'accès L0 est (targetGridPos.x, targetGridPos.y - 1)
            if (currentMonsterGridPos.x == targetGridPos.x && currentMonsterGridPos.y == targetGridPos.y - 1)
            {
                // On vérifie aussi que la "sortie" de l'escalier en haut (plateforme L1) est valide pour s'assurer que c'est un escalier fonctionnel.
                Vector3Int platformExitL1 = new Vector3Int(targetGridPos.x, targetGridPos.y + 1, 0);
                if (mapGenerator.IsTileWithinBounds(platformExitL1.x, platformExitL1.y) &&
                    mapGenerator.ElevationData[platformExitL1.x, platformExitL1.y] == 1 &&
                    (mapGenerator.groundLayer.GetTile(platformExitL1) != null || (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(platformExitL1) != null)))
                {
                    targetMonsterActualElevation = 1; // Le monstre monte et sera à L1 (conceptuellement, après ce pas)
                    // Debug.Log($"[IsMonsterMoveValid] STAIR UP from {currentMonsterGridPos}(L0) to {targetGridPos}(Stair L1 Data). New Elev: L1");
                    return true;
                }
            }
        }
        // CAS 1.B: DESCENDRE (L1 -> L0)
        // Le monstre est à L1 (currentMonsterActualElevation == 1).
        // La cible (targetWorldTile) est la tuile stair_S_Tile.
        // La tuile actuelle du monstre (currentMonsterGridPos) doit être la plateforme L1 juste AU-DESSUS (Nord) de cet escalier.
        else if (currentMonsterActualElevation == 1)
        {
            // Pour un stair_S_Tile à targetGridPos, la plateforme d'accès L1 est (targetGridPos.x, targetGridPos.y + 1)
            if (currentMonsterGridPos.x == targetGridPos.x && currentMonsterGridPos.y == targetGridPos.y + 1)
            {
                // On vérifie aussi que la "sortie" de l'escalier en bas (sol L0) est valide.
                Vector3Int groundExitL0 = new Vector3Int(targetGridPos.x, targetGridPos.y - 1, 0);
                if (mapGenerator.IsTileWithinBounds(groundExitL0.x, groundExitL0.y) &&
                    mapGenerator.ElevationData[groundExitL0.x, groundExitL0.y] == 0 &&
                    mapGenerator.groundLayer.GetTile(groundExitL0) != null)
                {
                    targetMonsterActualElevation = 0; // Le monstre descend et sera à L0 (conceptuellement, après ce pas)
                    // Debug.Log($"[IsMonsterMoveValid] STAIR DOWN from {currentMonsterGridPos}(L1 Platform) to {targetGridPos}(Stair L1 Data). New Elev: L0");
                    return true;
                }
            }
            // CAS 1.C: SE DÉPLACER SUR UN ESCALIER (L1 -> L1)
            // Si le monstre est déjà à L1 et la cible est une tuile d'escalier L1,
            // et que ce n'est pas un pas de descente (déjà géré au-dessus).
            // Cela permet de se déplacer sur des escaliers plus longs ou des paliers.
            else if (targetTileDataElevation == 1) // La tuile d'escalier est L1 data
            {
                 targetMonsterActualElevation = 1; // Reste à L1
                 // Debug.Log($"[IsMonsterMoveValid] MOVING ON STAIR L1 from {currentMonsterGridPos} to {targetGridPos}. Elev remains L1");
                 return true;
            }
        }
    }

    // 2. MOUVEMENT SUR PONT (si la cible n'est PAS un escalier)
    if (bridgeOnTarget != null) // La cible est un pont
    {
        // Les ponts sont à ElevationData == 1
        if (targetTileDataElevation == 1)
        {
            if (currentMonsterActualElevation == 1) // Monstre L1 allant sur un pont L1
            {
                targetMonsterActualElevation = 1;
                // Debug.Log($"[IsMonsterMoveValid] BRIDGE L1 from {currentMonsterGridPos} to {targetGridPos}. Elev remains L1");
                return true;
            }
            // Si monstre L0 et cible pont L1 => invalide (on ne peut pas sauter sur un pont depuis le dessous)
            // Debug.Log($"[IsMonsterMoveValid] BLOCKED: Monster L0 cannot move to Bridge L1 at {targetGridPos}");
            return false;
        }
        // Si un pont a une ElevationData != 1, la carte est mal configurée pour cette logique.
    }
    // Si le monstre est à L0 et la cible a un pont AU-DESSUS (donc targetTileDataElevation serait 1 à cause du pont)
    // mais qu'on veut marcher sur du sol L0 EN DESSOUS.
    if (currentMonsterActualElevation == 0 && bridgeOnTarget != null && targetTileDataElevation == 1) {
        // Ce cas est délicat. Si `targetTileDataElevation` est 1 à cause du pont,
        // mais qu'il y a du sol L0 en dessous, comment le savoir ?
        // On suppose ici que si `bridgeOnTarget` est non nul, la tuile EST le pont.
        // Si on voulait marcher sous le pont, `bridgeOnTarget` serait non nul, MAIS la `targetTileDataElevation`
        // pour le pathfinding devrait être 0 (si on se concentre sur le sol).
        // Simplification : si la tuile scannée est un pont, on la traite comme L1. Le monstre L0 ne peut y aller.
        // Le pathfinding devrait trouver un chemin sur les tuiles de sol L0 s'il y en a.
    }


    // 3. MOUVEMENT SUR SOL NORMAL (la cible n'est ni un escalier en transition, ni un pont)
    if (targetTileDataElevation == currentMonsterActualElevation)
    {
        // Vérifier qu'on ne "tombe" pas d'un pont de manière invalide
        bool currentIsBridge = (mapGenerator.bridgeLayer != null) && mapGenerator.bridgeLayer.GetTile(currentWorldTile) != null;
        if (currentIsBridge && currentMonsterActualElevation == 1 && targetTileDataElevation == 0)
        {
            // On était sur un pont L1, la cible est du sol L0, et ce n'est pas un escalier. C'est une chute.
            // Debug.Log($"[IsMonsterMoveValid] BLOCKED: Falling off bridge from {currentMonsterGridPos} to {targetGridPos}");
            return false;
        }

        // Le mouvement est sur le même niveau, c'est valide.
        targetMonsterActualElevation = currentMonsterActualElevation;
        // Debug.Log($"[IsMonsterMoveValid] NORMAL GROUND/PLATFORM from {currentMonsterGridPos}(L{currentMonsterActualElevation}) to {targetGridPos}(L{targetTileDataElevation}). Elev OK.");
        return true;
    }

    // Debug.Log($"[IsMonsterMoveValid] BLOCKED: Unhandled case or cliff. From {currentMonsterGridPos}(L{currentMonsterActualElevation}) to {targetGridPos}(DataL{targetTileDataElevation}, IsStair:{stairsOnTarget!=null}, IsBridge:{bridgeOnTarget!=null})");
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
    int maxIterations = maxSearchDistanceDirectPath * maxSearchDistanceDirectPath * 4; // Un peu plus généreux

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
            if (neighbourDistX > maxSearchDistanceDirectPath || neighbourDistY > maxSearchDistanceDirectPath)
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


    private PathNode CreateNodeFromWorldTile(Vector3Int worldTile, int monsterElevationOnThisTile) {
             if (!mapGenerator.IsTileWithinBounds(worldTile.x, worldTile.y)) return null; bool isWalkable = mapGenerator.wallsLayer.GetTile(worldTile) == null; return new PathNode(new Vector2Int(worldTile.x, worldTile.y), monsterElevationOnThisTile, isWalkable); }
    private List<Vector2Int> GetNeighbourOffsets() { 
        return new List<Vector2Int>{new Vector2Int(0,1), new Vector2Int(0,-1), new Vector2Int(1,0), new Vector2Int(-1,0)};}
    private List<Vector3Int> RetracePath(PathNode startNode, PathNode endNode) {
         List<PathNode> path = new List<PathNode>(); PathNode c = endNode;
          while(c!=startNode && c!=null){
            path.Add(c); c=c.cameFromNode;
            }
            if(c==startNode) path.Add(startNode);
             path.Reverse();
            return path.Select(node => new Vector3Int(node.gridPosition.x, node.gridPosition.y,0)).ToList();}
}