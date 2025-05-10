// PathNode.cs
using UnityEngine;

public class PathNode
{
    public Vector2Int gridPosition; // Position sur la grille (x, y)
    public int gCost; // Coût depuis le nœud de départ
    public int hCost; // Heuristique : coût estimé jusqu'au nœud d'arrivée
    public int FCost { get { return gCost + hCost; } } // Coût total

    public PathNode cameFromNode; // Le nœud précédent dans le chemin

    public int elevation; // Élévation de cette tuile (important pour la validité du mouvement)
    public bool isWalkable;

    public PathNode(Vector2Int gridPosition, int elevation, bool isWalkable)
    {
        this.gridPosition = gridPosition;
        this.elevation = elevation;
        this.isWalkable = isWalkable;
    }

    public void CalculateHeuristic(Vector2Int endNodePosition)
    {
        // Heuristique de Manhattan (bonne pour les grilles à 4 directions)
        hCost = Mathf.Abs(gridPosition.x - endNodePosition.x) + Mathf.Abs(gridPosition.y - endNodePosition.y);
    }

    public override bool Equals(object obj)
    {
        return obj is PathNode node && gridPosition.Equals(node.gridPosition);
    }

    public override int GetHashCode()
    {
        return gridPosition.GetHashCode();
    }
}