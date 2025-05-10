// PathfindingAStar.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps; 
public class PathfindingAStar : MonoBehaviour
{
    public MapGeneratorV2 mapGenerator; // Assignez dans l'Inspecteur
    // Référence au script de mouvement du joueur pour obtenir sa position et son élévation
    // Pourrait aussi être une référence directe au Transform du joueur.
    public PlayerMovement playerMovementScript; // Assignez le joueur ici

    private const int MOVE_STRAIGHT_COST = 10; // Coût pour un mouvement horizontal/vertical
    // private const int MOVE_DIAGONAL_COST = 14; // Si vous autorisez les diagonales

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

    // Logique de mouvement simplifiée pour le monstre.
    // Adaptez ceci aux capacités de votre monstre (peut-il utiliser des escaliers, des ponts ?)
    // currentMonsterGridPos: la tuile où le monstre est actuellement
    // currentMonsterElevation: l'élévation où se trouve actuellement le monstre
    // targetGridPos: la tuile voisine où il essaie d'aller
    // out targetElevation: l'élévation où le monstre serait s'il allait sur targetGridPos
    private bool IsMonsterMoveValid(Vector2Int currentMonsterGridPos, int currentMonsterElevation, Vector2Int targetGridPos, out int targetElevation)
    {
        targetElevation = currentMonsterElevation; // Par défaut, reste à la même élévation

        if (!mapGenerator.IsTileWithinBounds(targetGridPos.x, targetGridPos.y)) return false;

        Vector3Int targetWorldTile = new Vector3Int(targetGridPos.x, targetGridPos.y, 0);
        int targetTileDataElevation = mapGenerator.ElevationData[targetGridPos.x, targetGridPos.y];

        // 1. Murs physiques
        if (mapGenerator.wallsLayer.GetTile(targetWorldTile) != null) return false;

        // 2. Pas de sol (vide)
        if (mapGenerator.groundLayer.GetTile(targetWorldTile) == null && mapGenerator.bridgeLayer.GetTile(targetWorldTile) == null) return false;


        // 3. Logique d'élévation simple pour le monstre (pas d'escaliers pour l'instant, peut marcher sur ponts si à L1)
        TileBase bridgeOnTarget = mapGenerator.bridgeLayer.GetTile(targetWorldTile);

        if (bridgeOnTarget != null)
        {
            if (currentMonsterElevation == 1) // Monstre à L1 peut marcher sur le pont
            {
                targetElevation = 1;
                return true;
            }
            else // Monstre à L0, pont au-dessus, vérifier sol en dessous
            {
                if (mapGenerator.groundLayer.GetTile(targetWorldTile) != null && targetTileDataElevation == 0)
                {
                    targetElevation = 0;
                    return true;
                }
                return false; // Pas de sol sous le pont pour L0
            }
        }

        // 4. Mouvement sur sol normal / falaises
        if (targetTileDataElevation == currentMonsterElevation) // Même niveau
        {
            // targetElevation est déjà currentMonsterElevation
            return true;
        }
        else // Différence d'élévation = falaise (le monstre ne peut pas monter/descendre pour l'instant)
        {
            return false;
        }
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