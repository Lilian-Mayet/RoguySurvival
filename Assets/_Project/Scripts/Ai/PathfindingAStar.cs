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
        Debug.Log($"[FindOverallPath] All critical references seem OK. Monster L{startElevation}, Player L{playerElevation}");

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
    private void FindClosestStairToTransition(Vector3Int fromTile, int fromElevation, int toElevation,out Vector3Int? stairAccessPoint, out Vector3Int? stairDestinationPoint){
        stairAccessPoint = null;
        stairDestinationPoint = null;
        float minDistanceSq = float.MaxValue;
        int stairsEvaluated = 0;

        // Debug.Log($"[FindClosestStair] Search: From {fromTile} (L{fromElevation}) to L{toElevation}. Radius: {stairSearchRadius}");

        for (int xOffset = -stairSearchRadius; xOffset <= stairSearchRadius; xOffset++)
        {
            for (int yOffset = -stairSearchRadius; yOffset <= stairSearchRadius; yOffset++)
            {
                Vector3Int potentialStairPos = new Vector3Int(fromTile.x + xOffset, fromTile.y + yOffset, 0);

                if (!mapGenerator.IsTileWithinBounds(potentialStairPos.x,potentialStairPos.y)) continue;

                TileBase tileOnStairLayer = mapGenerator.stairsLayer.GetTile(potentialStairPos);

                if (tileOnStairLayer == mapGenerator.stair_S_Tile) // C'est une tuile d'escalier que nous avons placée
                {
                    stairsEvaluated++;
                    // Selon votre nouvelle information, stairTileDataElevation sera TOUJOURS 1 pour un stair_S_Tile
                    int stairTileDataElevation = mapGenerator.ElevationData[potentialStairPos.x, potentialStairPos.y];



                    // CAS 1: Monstre à L0 veut MONTER à L1
                    // Le monstre doit atteindre la tuile d'escalier (qui est L1 data) depuis une tuile L0 adjacente.
                    if (fromElevation == 0 && toElevation == 1)
                    {
                        // La tuile d'escalier (potentialStairPos) est à L1 (data).
                        // Le monstre doit être sur une tuile L0 adjacente pour y accéder.
                        // Pour un stair_S_Tile, la case d'accès depuis L0 est au SUD de l'escalier.
                        Vector3Int accessFromL0Pos = new Vector3Int(potentialStairPos.x, potentialStairPos.y - 1, 0);

                        if (mapGenerator.IsTileWithinBounds(accessFromL0Pos.x,accessFromL0Pos.y) &&
                            mapGenerator.ElevationData[accessFromL0Pos.x, accessFromL0Pos.y] == 0 && // Case d'accès est L0
                            (mapGenerator.groundLayer.GetTile(accessFromL0Pos) != null)) // Et il y a du sol
                        {
                            // La plateforme de destination à L1 est AU-DESSUS (Nord) de la tuile d'escalier L1.
                            // (Note: si stair_S_Tile est déjà L1, la "plateforme" est la tuile où l'escalier se termine,
                            // qui pourrait être une autre tuile de pont/sol à L1, ou le monstre est déjà "sur" l'escalier à L1).
                            // Si stair_S_Tile est la *dernière* tuile de l'escalier en montant, alors la destination
                            // est la tuile adjacente au niveau 1.
                            // Pour un stair_S (qui monte vers le Nord), la sortie est au Nord de la tuile d'escalier.
                            Vector3Int platformDestinationL1 = new Vector3Int(potentialStairPos.x, potentialStairPos.y + 1, 0);

                            if (mapGenerator.IsTileWithinBounds(platformDestinationL1.x,platformDestinationL1.y) &&
                                mapGenerator.ElevationData[platformDestinationL1.x, platformDestinationL1.y] == 1 &&
                                (mapGenerator.groundLayer.GetTile(platformDestinationL1) != null || (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(platformDestinationL1) != null)))
                            {
                                // Cet escalier est valide pour monter.
                                // Le monstre doit d'abord aller à `accessFromL0Pos` (qui est L0),
                                // puis le pas suivant le mènera sur `potentialStairPos` (la tuile d'escalier, qui est L1 data),
                                // et il émergera conceptuellement à `platformDestinationL1` ou continuera sur d'autres tuiles d'escalier L1.

                                // Le point que le pathfinding A* (pour le segment L0) doit viser est `accessFromL0Pos`.
                                // Mais l'escalier pertinent est `potentialStairPos`.
                                // On va considérer que le `stairAccessPoint` est la tuile d'escalier elle-même,
                                // et `IsMonsterMoveValid` gérera le pas depuis `accessFromL0Pos` vers `potentialStairPos`.
                                float distSq = (potentialStairPos - fromTile).sqrMagnitude; // Distance jusqu'à la tuile d'escalier L1 elle-même
                                if (distSq < minDistanceSq)
                                {
                                    minDistanceSq = distSq;
                                    stairAccessPoint = potentialStairPos;      // Viser la tuile d'escalier L1
                                    stairDestinationPoint = platformDestinationL1;  // Sortir sur la plateforme L1 au nord
                                }
                            }
                        }
                    }
                    // CAS 2: Monstre à L1 veut DESCENDRE à L0
                    // Le monstre est sur une plateforme L1 et cherche une tuile d'escalier (L1 data) qui mène à L0.
                    else if (fromElevation == 1 && toElevation == 0)
                    {
                        // `potentialStairPos` est la tuile d'escalier (L1 data).
                        // Pour un stair_S_Tile (qui monte vers le Nord), on y accède depuis une plateforme L1 au Nord
                        // pour descendre vers une tuile L0 au Sud.
                        Vector3Int platformAccessL1 = new Vector3Int(potentialStairPos.x, potentialStairPos.y + 1, 0); // Plateforme au Nord de l'escalier
                        Vector3Int destinationL0 = new Vector3Int(potentialStairPos.x, potentialStairPos.y - 1, 0);    // Sortie L0 au Sud de l'escalier

                        // Vérifier si la plateforme d'accès est valide (L1) et si la destination L0 est valide.
                        if (mapGenerator.IsTileWithinBounds(platformAccessL1.x,platformAccessL1.y) &&
                            mapGenerator.ElevationData[platformAccessL1.x, platformAccessL1.y] == 1 && // Plateforme d'accès est L1
                            (mapGenerator.groundLayer.GetTile(platformAccessL1) != null || (mapGenerator.bridgeLayer != null && mapGenerator.bridgeLayer.GetTile(platformAccessL1) != null)) &&
                            mapGenerator.IsTileWithinBounds(destinationL0.x,destinationL0.y) &&
                            mapGenerator.ElevationData[destinationL0.x, destinationL0.y] == 0 && // Destination est L0
                            (mapGenerator.groundLayer.GetTile(destinationL0) != null)) // Et il y a du sol
                        {
                            // Le monstre doit d'abord aller à `platformAccessL1`.
                            // Le "pas" suivant le mènera sur `potentialStairPos` (l'escalier L1),
                            // et il émergera à `destinationL0`.

                            // On vise la tuile d'escalier `potentialStairPos` (qui est L1 data),
                            // en s'assurant qu'on vient de `platformAccessL1`.
                            float distSq = (potentialStairPos - fromTile).sqrMagnitude; // Distance jusqu'à la tuile d'escalier L1
                            if (distSq < minDistanceSq)
                            {
                                minDistanceSq = distSq;
                                stairAccessPoint = potentialStairPos;      // Viser la tuile d'escalier L1
                                stairDestinationPoint = destinationL0;    // Sortir sur la tuile L0 au sud
                            }
                        }
                    }
                }
            }
            // ... (logs de debug finaux) ...
        }}
        // Dans IsMonsterMoveValid, la logique d'escalier doit être plus précise :
    private bool IsMonsterMoveValid(Vector2Int currentMonsterGridPos, int currentMonsterActualElevation, Vector2Int targetGridPos, out int targetMonsterActualElevation){
        targetMonsterActualElevation = currentMonsterActualElevation;
        if (!mapGenerator.IsTileWithinBounds(targetGridPos.x, targetGridPos.y)) return false;

        Vector3Int currentWorldTile = new Vector3Int(currentMonsterGridPos.x, currentMonsterGridPos.y, 0);
        Vector3Int targetWorldTile = new Vector3Int(targetGridPos.x, targetGridPos.y, 0);
        int targetTileDataElevation = mapGenerator.ElevationData[targetGridPos.x, targetGridPos.y];

        if (mapGenerator.wallsLayer.GetTile(targetWorldTile) != null) return false;
        
        TileBase groundOnTarget = mapGenerator.groundLayer.GetTile(targetWorldTile);
        TileBase bridgeOnTarget = (mapGenerator.bridgeLayer != null) ? mapGenerator.bridgeLayer.GetTile(targetWorldTile) : null;
        TileBase stairsOnTarget = mapGenerator.stairsLayer.GetTile(targetWorldTile);

        if (groundOnTarget == null && bridgeOnTarget == null && stairsOnTarget == null) return false;

        // 1. Transition d'escalier en MONTANT (L0 -> L1)
        // Monstre est à L0 (currentMonsterActualElevation == 0).
        // Cible est une tuile stair_S_Tile (qui a ElevationData == 1).
        // La tuile actuelle (currentWorldTile) doit être la case L0 juste EN DESSOUS (Sud) de l'escalier stair_S_Tile.
        if (currentMonsterActualElevation == 0 && stairsOnTarget == mapGenerator.stair_S_Tile && targetTileDataElevation == 1)
        {
            // Vérifier que la tuile actuelle est bien la case d'accès L0 pour cet escalier L1
            // Pour stair_S_Tile à (targetGridPos.x, targetGridPos.y), la case d'accès L0 est (targetGridPos.x, targetGridPos.y - 1)
            if (currentMonsterGridPos.x == targetGridPos.x && currentMonsterGridPos.y == targetGridPos.y - 1)
            {
                // Vérifier aussi que la "sortie" de l'escalier en haut est valide
                Vector3Int platformExitL1 = new Vector3Int(targetGridPos.x, targetGridPos.y + 1, 0);
                if (mapGenerator.IsTileWithinBounds(platformExitL1.x,platformExitL1.y) && mapGenerator.ElevationData[platformExitL1.x, platformExitL1.y] == 1)
                {
                    targetMonsterActualElevation = 1; // Le monstre monte et sera à L1 sur la tuile d'escalier
                    return true;
                }
            }
        }

        // 2. Transition d'escalier en DESCENDANT (L1 -> L0)
        // Monstre est à L1 (currentMonsterActualElevation == 1).
        // Cible est une tuile stair_S_Tile (qui a ElevationData == 1).
        // La tuile actuelle (currentWorldTile) doit être la plateforme L1 juste AU-DESSUS (Nord) de l'escalier stair_S_Tile.
        if (currentMonsterActualElevation == 1 && stairsOnTarget == mapGenerator.stair_S_Tile && targetTileDataElevation == 1)
        {
            // Vérifier que la tuile actuelle est bien la plateforme d'accès L1 pour cet escalier
            // Pour stair_S_Tile à (targetGridPos.x, targetGridPos.y), la plateforme d'accès L1 est (targetGridPos.x, targetGridPos.y + 1)
            if (currentMonsterGridPos.x == targetGridPos.x && currentMonsterGridPos.y == targetGridPos.y + 1)
            {
                // Vérifier que la "sortie" de l'escalier en bas (L0) est valide
                Vector3Int groundExitL0 = new Vector3Int(targetGridPos.x, targetGridPos.y - 1, 0);
                if (mapGenerator.IsTileWithinBounds(groundExitL0.x,groundExitL0.y) && mapGenerator.ElevationData[groundExitL0.x, groundExitL0.y] == 0 &&
                    mapGenerator.groundLayer.GetTile(groundExitL0) != null)
                {
                    targetMonsterActualElevation = 0; // Le monstre descend et sera à L0 après avoir "traversé" l'escalier
                                                    // Note: il atterrit sur `groundExitL0`. Le pas actuel le met sur `stairsOnTarget` mais son élévation conceptuelle change.
                    return true;
                }
            }
        }

        // 3. Mouvement sur Pont (les ponts sont L1)
        if (bridgeOnTarget != null && targetTileDataElevation == 1)
        {
            if (currentMonsterActualElevation == 1) { targetMonsterActualElevation = 1; return true; } // L1 sur L1 pont
            // Si L0 essayant d'aller sur un pont L1 => invalide (sauf si c'est un escalier, déjà géré)
            return false;
        }
        // Mouvement sous un pont (monstre L0, la tuile cible a un pont L1 au-dessus, mais on vérifie le sol L0)
        if (currentMonsterActualElevation == 0 && bridgeOnTarget != null && targetTileDataElevation == 1) {
            // Le pont est à L1. Si on est L0, on vérifie s'il y a du sol L0 à la même coordonnée.
            // L'ElevationData de la coordonnée (x,y) sera 1 à cause du pont.
            // On doit vérifier le groundLayer à (x,y) ET que son ElevationData est 0.
            if (mapGenerator.groundLayer.GetTile(targetWorldTile) != null && 
                mapGenerator.ElevationData[targetWorldTile.x, targetWorldTile.y] == 0) // Méthode hypothétique
            {
                targetMonsterActualElevation = 0; return true;
            }
            // S'il n'y a pas de "ElevationDataForGroundOnly", on peut supposer que si un pont est là (ElevationData=1)
            // et que groundLayer a une tuile, cette tuile de sol DOIT être L0 pour qu'on passe dessous.
            // Mais mapGenerator.ElevationData[x,y] sera 1. Il faut une info séparée ou une convention.
            // Pour l'instant, on va dire que si on est L0 et la cible est un pont (donc targetTileDataElevation = 1), on ne peut pas y aller.
            // Le pathfinder aurait dû nous faire passer sur une tuile de sol L0 si elle existe.
            return false; // Ne peut pas marcher sur un pont depuis L0, ni sur le vide sous un pont.
        }


        // 4. Mouvement sur Sol Normal ou sur une tuile d'escalier sans changer de niveau (ex: se déplacer latéralement sur un large escalier)
        if (targetTileDataElevation == currentMonsterActualElevation)
        {
            // Si la cible est une tuile d'escalier (L1 data) et que le monstre est déjà à L1, c'est ok.
            if (stairsOnTarget == mapGenerator.stair_S_Tile && currentMonsterActualElevation == 1) {
                targetMonsterActualElevation = 1;
                return true;
            }
            // Si ce n'est pas un escalier ou si c'est un escalier mais que l'élévation correspond déjà, c'est du sol/plateforme normal.
            if (stairsOnTarget != mapGenerator.stair_S_Tile) {
                targetMonsterActualElevation = currentMonsterActualElevation;
                return true;
            }
        }
        
        return false;
    }  

  public List<Vector3Int> FindPath(Vector3Int startWorldTile, int startElevation, Vector3Int endWorldTile, int endElevation){
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