using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic; // For List


public enum DecoThemeType { Mushroom, Rock, Bush, Plant, SpecialItems } // Types of decoration clusters

[System.Serializable]
public class DecoTheme
{
    public string themeName; // For Inspector clarity
    public DecoThemeType themeType;
    public TileBase smallTile;
    public TileBase mediumTile;
    public TileBase bigTile;
    public List<TileBase> specialTiles; // For themes like Pumpkin, Bone, or mixed special items

    [Header("Cluster Settings")]
    [Range(0f, 0.1f)] // Chance to start a cluster at any valid tile
    public float clusterStartChance = 0.01f;
    [Range(0.1f, 1f)] // How many tiles within the cluster get a decoration
    public float densityInCluster = 0.5f;
    public int minClusterSize = 3; // Min number of tiles in a cluster patch
    public int maxClusterSize = 8; // Max
    [Range(0, 10)]
    public int clusterSpreadRadius = 2; // How far a cluster can spread from its seed
}


public class MapGeneratorV2 : MonoBehaviour
{
    // --- Tile Enums ---
    private enum Biome { GreenGrass, DryGrass }
    private enum TileContext { Ground, Platform } // To differentiate rules for ground vs. platform surface

    // --- Map Data ---private int[,] __elevationData; // 0 = base, 1 = plateau (renamed with underscore)
    private int[,] _elevationData; // 0 = base, 1 = plateau (renamed with underscore)
    public int[,] ElevationData => _elevationData; // Public read-only property

    private Biome[,] biomeData; // Also make biomeData accessible if needed by player


    [Header("Map Dimensions")]
    public int mapWidth = 60;
    public int mapHeight = 40;

    [Header("Noise Settings - Elevation")]
    public float elevationNoiseScale = 20f;
    [Range(0f, 1f)]
    public float elevationThreshold = 0.55f; // Values above this become Level 1
    public int elevationOctaves = 3;
    [Range(0f, 1f)]
    public float elevationPersistence = 0.5f;
    public float elevationLacunarity = 2f;
    public Vector2 elevationOffset;

    [Header("Noise Settings - Biome")]
    public float biomeNoiseScale = 25f;
    [Range(0f, 1f)]
    public float biomeThreshold = 0.5f; // Defines boundary between Green and Dry
    public int biomeOctaves = 2;
    [Range(0f, 1f)]
    public float biomePersistence = 0.4f;
    public float biomeLacunarity = 1.8f;
    public Vector2 biomeOffset;

    [Header("General Settings")]
    public int seed;
    public bool useRandomSeed = true;
    [Range(0, 5)]
    public int smoothingIterations = 2; // Smooths out elevation transitions


    [Header("Stair Settings")] 
    [Range(0f, 1f)]   
     public float stairPlacementChance = 0.1f; 

    [Header("Bridge Settings")]
    public int minBridgeLength = 2;
    public int maxBridgeLength = 6;
    [Range(0f, 1f)]
    public float bridgePlacementChance = 0.1f; // 10% de chance par défaut


  

    [Header("Tilemap References")]
    public Tilemap groundLayer;
    public Tilemap wallsLayer;
    public Tilemap stairsLayer;
    public Tilemap shadowsLayer;
    public Tilemap bridgeLayer; 
     public Tilemap decoLayer;

    // --- TILE ASSETS (Assign these meticulously in Inspector!) ---
    [Header("Green Grass Tiles")]
    public TileBase gg_center;
    public TileBase gg_isolated;
    public TileBase gg_top;
    public TileBase gg_bottom;
    public TileBase gg_left;
    public TileBase gg_right;
    public TileBase gg_top_left;
    public TileBase gg_top_right;
    public TileBase gg_bottom_left;
    public TileBase gg_bottom_right;
    public TileBase gg_top_bottom;       // Vertical strip
    public TileBase gg_left_right;       // Horizontal strip
    public TileBase gg_top_right_bottom; // Missing Left (3 sides grass)
    public TileBase gg_top_left_bottom;  // Missing Right (3 sides grass)
    public TileBase gg_top_left_right;   // Missing Bottom (3 sides grass)
    public TileBase gg_left_bottom_right;// Missing Top (3 sides grass)
  
    [Header("Dry Grass Tiles")]
    public TileBase dg_center;
    public TileBase dg_isolated;
    public TileBase dg_top;
    public TileBase dg_bottom;
    public TileBase dg_left;
    public TileBase dg_right;
    public TileBase dg_top_left;
    public TileBase dg_top_right;
    public TileBase dg_bottom_left;
    public TileBase dg_bottom_right;
    public TileBase dg_top_bottom;       // Vertical strip
    public TileBase dg_left_right;       // Horizontal strip
    public TileBase dg_top_right_bottom; // Missing Left
    public TileBase dg_top_left_bottom;  // Missing Right
    public TileBase dg_top_left_right;   // Missing Bottom
    public TileBase dg_left_bottom_right;// Missing Top

    [Header("Elevation Structure")]
    public TileBase cliffWall_S; // For south-facing cliffs (only need this for top-down view)
    public TileBase stair_S_Tile;

    [Header("Shadow Tiles")]
    public TileBase shadow_Edge_S; // Shadow directly below a south edge of platform
    public TileBase shadow_Edge_E; // Shadow to the left of an east edge of platform
    public TileBase shadow_Edge_W; // Shadow to the right of a west edge of platform

    [Header("Bridge Tiles")]
    public TileBase bridge_Start_N; // Placé sur la tuile de niveau 1 au sud du pont
    public TileBase bridge_Start_S; // Placé sur la tuile de niveau 1 au nord du pont
    public TileBase bridge_Platform_V; // Plateforme verticale du pont
    public TileBase bridge_Start_E; // Placé sur la tuile de niveau 1 à l'ouest du pont
    public TileBase bridge_Start_W; // Placé sur la tuile de niveau 1 à l'est du pont
    public TileBase bridge_Platform_H; // Plateforme horizontale du pont


     [Header("Decoration Tiles - Mushrooms")]
    public TileBase mushroom_Small;
    public TileBase mushroom_Medium;
    public TileBase mushroom_Big;

    [Header("Decoration Tiles - Rocks")]
    public TileBase rock_Small;
    public TileBase rock_Medium;
    public TileBase rock_Big;

    [Header("Decoration Tiles - Bushes")]
    public TileBase bush_Small;
    public TileBase bush_Medium;
    public TileBase bush_Big;

    [Header("Decoration Tiles - Plants")]
    public TileBase plant_Small; // Assuming you might add medium/big later
    public TileBase plant_Medium;
    // public TileBase plant_Big; // Uncomment if you have it

    [Header("Decoration Tiles - Special")]
    public TileBase pumpkin_1;
    public TileBase pumpkin_2;
    public TileBase bone_1;
    public TileBase bone_2;
    // Add any other single-tile special decos here

    [Header("Decoration Settings")]
    public List<DecoTheme> decorationThemes = new List<DecoTheme>();

    // --- Neighbor Offsets (for 8-way check) ---
    private readonly Vector2Int[] neighborOffsets = {
        new Vector2Int(0, 1),  // N (Index 0)
        new Vector2Int(1, 1),  // NE (Index 1)
        new Vector2Int(1, 0),  // E (Index 2)
        new Vector2Int(1, -1), // SE (Index 3)
        new Vector2Int(0, -1), // S (Index 4)
        new Vector2Int(-1, -1),// SW (Index 5)
        new Vector2Int(-1, 0), // W (Index 6)
        new Vector2Int(-1, 1)  // NW (Index 7)
    };

    void Start()
    {
        GenerateMap();
    }

    public void GenerateMap()
    {
        ClearMap();
        if (useRandomSeed || seed == 0)
        {
            seed = Random.Range(0, 100000);
        }
        Random.InitState(seed); // Ensure all subsequent Random calls are seeded if any

        Generate_elevationData();
        Smooth_elevationData(); // Apply smoothing to elevation data
        Generate_biomeData();
        PlaceTilesAndFeatures();
        PlaceStairs(); // Place stairs after main structure
        PlaceBridges();
        PlaceDecorations();
        
        // Bake colliders if using CompositeCollider2D on Walls_Layer
        if (wallsLayer.TryGetComponent<CompositeCollider2D>(out var compositeCollider))
        {
            compositeCollider.GenerateGeometry();
        }
    }

    void ClearMap()
    {
        groundLayer.ClearAllTiles();
        wallsLayer.ClearAllTiles();
        stairsLayer.ClearAllTiles();
        shadowsLayer.ClearAllTiles();
        bridgeLayer.ClearAllTiles();
        decoLayer.ClearAllTiles();
    }

    void Generate_elevationData()
    {
        _elevationData = new int[mapWidth, mapHeight];
        System.Random prng = new System.Random(seed); // Use the main seed consistently
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng, elevationOctaves, elevationOffset);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float noiseValue = GenerateNormalizedNoiseValue(x, y, elevationNoiseScale, elevationOctaves, elevationPersistence, elevationLacunarity, octaveOffsets, mapWidth, mapHeight);
                if (noiseValue > elevationThreshold)
                {
                    _elevationData[x, y] = 1; // Level 1 plateau
                }
                else
                {
                    _elevationData[x, y] = 0; // Base level
                }
            }
        }
    }

    void Smooth_elevationData()
    {
        if (smoothingIterations <= 0) return;

        int[,] temp_elevationData = new int[mapWidth, mapHeight];

        for (int iteration = 0; iteration < smoothingIterations; iteration++)
        {
            // Copy current elevation data
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    temp_elevationData[x, y] = _elevationData[x, y];
                }
            }

            // Apply cellular automaton rules for natural looking terrain
            for (int y = 1; y < mapHeight - 1; y++)
            {
                for (int x = 1; x < mapWidth - 1; x++)
                {
                    int highElevationNeighbors = 0;
                    int totalNeighbors = 0;

                    // Check immediate neighbors (4-way connectivity)
                    for (int i = 0; i < 8; i += 2) // Only check cardinal directions (N, E, S, W)
                    {
                        int nx = x + neighborOffsets[i].x;
                        int ny = y + neighborOffsets[i].y;

                        if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                        {
                            totalNeighbors++;
                            if (_elevationData[nx, ny] == 1)
                            {
                                highElevationNeighbors++;
                            }
                        }
                    }

                    // Smooth based on majority rule with threshold
                    if (highElevationNeighbors >= 3) // At least 3 of 4 neighbors are high
                    {
                        temp_elevationData[x, y] = 1;
                    }
                    else if (highElevationNeighbors <= 1) // At most 1 of 4 neighbors is high
                    {
                        temp_elevationData[x, y] = 0;
                    }
                    // Otherwise keep current elevation
                }
            }

            // Update elevation data for next iteration
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    _elevationData[x, y] = temp_elevationData[x, y];
                }
            }
        }
    }

    void Generate_biomeData()
    {
        biomeData = new Biome[mapWidth, mapHeight];
        System.Random prng = new System.Random(seed + 1); // Use a different seed for biomes
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng, biomeOctaves, biomeOffset);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float noiseValue = GenerateNormalizedNoiseValue(x, y, biomeNoiseScale, biomeOctaves, biomePersistence, biomeLacunarity, octaveOffsets, mapWidth, mapHeight);
                if (noiseValue > biomeThreshold)
                {
                    biomeData[x, y] = Biome.DryGrass;
                }
                else
                {
                    biomeData[x, y] = Biome.GreenGrass;
                }
            }
        }
    }

    Vector2[] GenerateOctaveOffsets(System.Random prng, int octaves, Vector2 baseOffset)
    {
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + baseOffset.x;
            float offsetY = prng.Next(-100000, 100000) + baseOffset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }
        return octaveOffsets;
    }

    float GenerateNormalizedNoiseValue(int x, int y, float scale, int octaves, float persistence, float lacunarity, Vector2[] octaveOffsets, int currentMapWidth, int currentMapHeight)
    {
        if (scale <= 0) scale = 0.0001f; // Prevent division by zero

        float amplitude = 1;
        float frequency = 1;
        float noiseHeight = 0;
        float maxPossibleHeight = 0; // Used for normalization

        for (int i = 0; i < octaves; i++)
        {
            // Dividing by width/height makes scale relative to map size, not absolute units
            float sampleX = (x - currentMapWidth / 2f) / scale * frequency + octaveOffsets[i].x;
            float sampleY = (y - currentMapHeight / 2f) / scale * frequency + octaveOffsets[i].y;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlinValue * amplitude;
            maxPossibleHeight += amplitude; // Accumulate max possible amplitude sum

            amplitude *= persistence;
            frequency *= lacunarity;
        }
        // Normalize to 0-1 range
        return maxPossibleHeight > 0 ? noiseHeight / maxPossibleHeight : 0;
    }

    void PlaceTilesAndFeatures()
    {
        // --- STAGE 1: Place Ground Tiles and South-Facing Walls ---
        
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                Vector3Int currentPos = new Vector3Int(x, y, 0);
                int currentLevel = _elevationData[x, y];
                Biome currentBiome = biomeData[x, y];
                TileBase tileToPlace;

                tileToPlace = DetermineTileForCell(x, y, currentBiome, currentLevel == 1 ? TileContext.Platform : TileContext.Ground);
                groundLayer.SetTile(currentPos, tileToPlace);

                if (currentLevel == 1)
                {
                    if (y > 0 && _elevationData[x, y - 1] == 0)
                    {
                        Vector3Int wallPos = new Vector3Int(x, y - 1, 0);
                        if (cliffWall_S != null) wallsLayer.SetTile(wallPos, cliffWall_S);
                    }
                }
            }
        }

        // --- STAGE 2: Place Shadows ---
        for (int y_shadow = 0; y_shadow < mapHeight; y_shadow++) // Renamed loop var to avoid confusion
        {
            for (int x_shadow = 0; x_shadow < mapWidth; x_shadow++) // Renamed loop var
            {
                // We are considering placing a shadow AT (x_shadow, y_shadow)
                Vector3Int shadowPos = new Vector3Int(x_shadow, y_shadow, 0);

                // Only place shadows on base level (level 0) tiles
                if (_elevationData[x_shadow, y_shadow] == 0)
                {
                    // --- Original Shadow Logic (Platform Edges) ---

                    // 1. Shadow cast by a platform edge directly to the NORTH of the current tile
                    // (current tile (x_shadow, y_shadow) is SOUTH of a platform edge)
                    if (y_shadow < mapHeight - 1 && _elevationData[x_shadow, y_shadow + 1] == 1)
                    {
                        if (shadow_Edge_S != null) shadowsLayer.SetTile(shadowPos, shadow_Edge_S);
                    }
                    // 2. Shadow cast by a platform edge directly to the EAST of the current tile
                    // (current tile (x_shadow, y_shadow) is WEST of a platform edge)
                    else if (x_shadow < mapWidth - 1 && _elevationData[x_shadow + 1, y_shadow] == 1)
                    {
                        if (shadow_Edge_E != null) shadowsLayer.SetTile(shadowPos, shadow_Edge_E);
                    }
                    // 3. Shadow cast by a platform edge directly to the WEST of the current tile
                    // (current tile (x_shadow, y_shadow) is EAST of a platform edge)
                    else if (x_shadow > 0 && _elevationData[x_shadow - 1, y_shadow] == 1)
                    {
                        if (shadow_Edge_W != null) shadowsLayer.SetTile(shadowPos, shadow_Edge_W);
                    }

                    // --- New Shadow Logic (Cliff Wall Sides) ---
                    // These conditions check if the *adjacent* tile is a cliff wall base.
                    // A cliff wall at (wallX, wallY) implies:
                    // - _elevationData[wallX, wallY] == 0
                    // - _elevationData[wallX, wallY + 1] == 1 (platform above it)

                    // 4. Shadow cast by a CLIFF WALL to the WEST of the current tile
                    // (current tile (x_shadow, y_shadow) is EAST of a cliff wall)
                    // The wall is at (x_shadow - 1, y_shadow)
                    else if (x_shadow > 0 && // Check bounds for the wall tile
                             _elevationData[x_shadow - 1, y_shadow] == 0 &&         // Wall base is level 0
                             y_shadow < mapHeight - 1 &&                           // Check bounds for platform above wall
                             _elevationData[x_shadow - 1, y_shadow + 1] == 1)       // Platform above wall base
                    {
                        // This shadow is cast from the West (by the wall) onto the current tile.
                        // So, it's like a shadow_Edge_W, visually.
                        if (shadow_Edge_W != null) shadowsLayer.SetTile(shadowPos, shadow_Edge_W);
                    }
                    // 5. Shadow cast by a CLIFF WALL to the EAST of the current tile
                    // (current tile (x_shadow, y_shadow) is WEST of a cliff wall)
                    // The wall is at (x_shadow + 1, y_shadow)
                    else if (x_shadow < mapWidth - 1 && // Check bounds for the wall tile
                             _elevationData[x_shadow + 1, y_shadow] == 0 &&         // Wall base is level 0
                             y_shadow < mapHeight - 1 &&                           // Check bounds for platform above wall
                             _elevationData[x_shadow + 1, y_shadow + 1] == 1)       // Platform above wall base
                    {
                        // This shadow is cast from the East (by the wall) onto the current tile.
                        // So, it's like a shadow_Edge_E, visually.
                        if (shadow_Edge_E != null) shadowsLayer.SetTile(shadowPos, shadow_Edge_E);
                    }
                }
            }
        }
    }
    TileBase DetermineTileForCell(int x, int y, Biome cellBiome, TileContext context)
    {
        bool[] matches = new bool[8]; // Store match status for each direction
        int currentCellElevation = _elevationData[x, y];

        // Check all 8 directions for matches
        for (int i = 0; i < 8; i++)
        {
            int nx = x + neighborOffsets[i].x;
            int ny = y + neighborOffsets[i].y;
            
            // Default to non-match for out of bounds
            matches[i] = false;
            
            if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
            {
                // A cell is a "match" if:
                // 1. It's at the same elevation AND
                // 2. It's the same biome type (green/dry)
                if (_elevationData[nx, ny] >= currentCellElevation && biomeData[nx, ny] == cellBiome)
                {
                    matches[i] = true;
                }
            }
        }

        // Extract match values for convenience
        bool N  = matches[0];
        bool NE = matches[1];
        bool E  = matches[2];
        bool SE = matches[3];
        bool S  = matches[4];
        bool SW = matches[5];
        bool W  = matches[6];
        bool NW = matches[7];

        // Select tile based on connection pattern
        TileBase selectedTile = null;

        // --- Complete tile selection logic ---
        if (N && E && S && W) // All cardinal directions match
        {
            selectedTile = GetTile(cellBiome, "center");
        }
        // 3 Cardinal Matches
        else if (N && E && S && !W) selectedTile = GetTile(cellBiome, "top_right_bottom");
        else if (N && E && !S && W) selectedTile = GetTile(cellBiome, "top_left_right");
        else if (N && !E && S && W) selectedTile = GetTile(cellBiome, "top_left_bottom");
        else if (!N && E && S && W) selectedTile = GetTile(cellBiome, "left_bottom_right");
        // 2 Opposite Cardinal Matches
        else if (N && S && !E && !W) selectedTile = GetTile(cellBiome, "top_bottom");
        else if (E && W && !N && !S) selectedTile = GetTile(cellBiome, "left_right");
        // 2 Adjacent Cardinal Matches
        else if (N && E && !S && !W) selectedTile = GetTile(cellBiome, "top_right");
        else if (E && S && !N && !W) selectedTile = GetTile(cellBiome, "bottom_right");
        else if (S && W && !N && !E) selectedTile = GetTile(cellBiome, "bottom_left");
        else if (W && N && !E && !S) selectedTile = GetTile(cellBiome, "top_left");
        // 1 Cardinal Match
        else if (N && !E && !S && !W) selectedTile = GetTile(cellBiome, "top");
        else if (E && !N && !S && !W) selectedTile = GetTile(cellBiome, "right");
        else if (S && !N && !E && !W) selectedTile = GetTile(cellBiome, "bottom");
        else if (W && !N && !E && !S) selectedTile = GetTile(cellBiome, "left");
        // 0 Cardinal Matches
        else selectedTile = GetTile(cellBiome, "isolated");

        // Fallback if tile is null (shouldn't happen with proper setup)
        if (selectedTile == null)
        {
            Debug.LogWarning($"Could not determine tile for biome {cellBiome} at ({x}, {y}). Using isolated fallback.");
            selectedTile = GetTile(cellBiome, "isolated");
        }

        return selectedTile;
    }

    // Helper function to get the correct tile based on biome and type string
    private TileBase GetTile(Biome biome, string type)
    {
        if (biome == Biome.GreenGrass)
        {
            switch (type)
            {
                case "center": return gg_center;
                case "isolated": return gg_isolated;
                case "top": return gg_top;
                case "bottom": return gg_bottom;
                case "left": return gg_left;
                case "right": return gg_right;
                case "top_left": return gg_top_left;
                case "top_right": return gg_top_right;
                case "bottom_left": return gg_bottom_left;
                case "bottom_right": return gg_bottom_right;
                case "top_bottom": return gg_top_bottom;
                case "left_right": return gg_left_right;
                case "top_right_bottom": return gg_top_right_bottom;
                case "top_left_bottom": return gg_top_left_bottom;
                case "top_left_right": return gg_top_left_right;
                case "left_bottom_right": return gg_left_bottom_right;
                default: 
                    Debug.LogWarning($"Unknown green grass tile type: {type}"); 
                    return gg_isolated;
            }
        }
        else // DryGrass
        {
            switch (type)
            {
                case "center": return dg_center;
                case "isolated": return dg_isolated;
                case "top": return dg_top;
                case "bottom": return dg_bottom;
                case "left": return dg_left;
                case "right": return dg_right;
                case "top_left": return dg_top_left;
                case "top_right": return dg_top_right;
                case "bottom_left": return dg_bottom_left;
                case "bottom_right": return dg_bottom_right;
                case "top_bottom": return dg_top_bottom;
                case "left_right": return dg_left_right;
                case "top_right_bottom": return dg_top_right_bottom;
                case "top_left_bottom": return dg_top_left_bottom;
                case "top_left_right": return dg_top_left_right;
                case "left_bottom_right": return dg_left_bottom_right;
                default: 
                    Debug.LogWarning($"Unknown dry grass tile type: {type}"); 
                    return dg_isolated;
            }
        }
    }

    void PlaceStairs()
        {
            if (stair_S_Tile == null || cliffWall_S == null)
            {
                Debug.LogWarning("Tuile d'escalier (stair_S_Tile) ou de mur de falaise (cliffWall_S) non assignée. Impossible de placer des escaliers.");
                return;
            }

            // Parcourir toutes les positions potentielles de mur sur la carte
            // Les murs (cliffWall_S) sont placés sur la couche 'wallsLayer'
            for (int x = 0; x < mapWidth; x++)
            {
                // Un mur est à la position y, la plateforme est en y+1
                // Donc y pour un mur peut aller de 0 à mapHeight - 2
                for (int y = 0; y < mapHeight -1 ; y++)
                {
                    Vector3Int wallPos = new Vector3Int(x, y, 0);

                    // Vérifier si la tuile actuelle sur la couche des murs est bien un cliffWall_S
                    if (wallsLayer.GetTile(wallPos) == cliffWall_S)
                    {
                        // Appliquer la probabilité de placement d'un escalier
                        if (Random.Range(0f, 1f) < stairPlacementChance)
                        {
                            bool canPlaceStair = true;

                            // Vérifier l'adjacence horizontale (gauche)
                            if (x > 0)
                            {
                                Vector3Int leftNeighborPos = new Vector3Int(x - 1, y, 0);
                                if (stairsLayer.GetTile(leftNeighborPos) == stair_S_Tile)
                                {
                                    canPlaceStair = false;
                                }
                            }

                            // Vérifier l'adjacence horizontale (droite), seulement si on peut encore placer
                            if (canPlaceStair && x < mapWidth - 1)
                            {
                                Vector3Int rightNeighborPos = new Vector3Int(x + 1, y, 0);
                                if (stairsLayer.GetTile(rightNeighborPos) == stair_S_Tile)
                                {
                                    canPlaceStair = false;
                                }
                            }

                            // Si on peut placer l'escalier (pas de chance ratée et pas d'escalier adjacent)
                            if (canPlaceStair)
                            {
                                // Retirer la tuile de mur de la couche wallsLayer
                                wallsLayer.SetTile(wallPos, null);

                                // Retirer également l'ombre qui aurait pu être sur la même case que le mur
                                // (car cliffWall_S et shadow_Edge_S peuvent occuper la même case)
                                shadowsLayer.SetTile(wallPos, null);

                                // Placer la tuile d'escalier sur la couche stairsLayer
                                stairsLayer.SetTile(wallPos, stair_S_Tile);
                            }
                        }
                    }
                }
            }
        }

    void PlaceBridges()
        {
            if (bridgeLayer == null || bridge_Start_N == null || bridge_Start_S == null ||
                bridge_Platform_V == null || bridge_Start_E == null || bridge_Start_W == null ||
                bridge_Platform_H == null)
            {
                Debug.LogWarning("Une ou plusieurs tuiles de pont ou la couche de pont ne sont pas assignées. Impossible de placer des ponts.");
                return;
            }

            // --- PLACEMENT DES PONTS HORIZONTAUX ---
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth - minBridgeLength; x++)
                {
                    if (_elevationData[x, y] == 1)
                    {
                        for (int length = minBridgeLength; length <= maxBridgeLength; length++)
                        {
                            int x_end = x + length + 1;
                            if (x_end < mapWidth)
                            {
                                if (_elevationData[x_end, y] == 1)
                                {
                                    bool gapIsValid = true;
                                    for (int i = 1; i <= length; i++)
                                    {
                                        if (_elevationData[x + i, y] != 0)
                                        {
                                            gapIsValid = false;
                                            break;
                                        }
                                    }

                                    if (gapIsValid)
                                    {
                                        if (Random.Range(0f, 1f) < bridgePlacementChance)
                                        {
                                            // --- NOUVELLE VÉRIFICATION ANTI-CROISEMENT ---
                                            bool canPlaceBridge = true;
                                            // Vérifier la tuile de départ
                                            if (bridgeLayer.GetTile(new Vector3Int(x, y, 0)) != null) canPlaceBridge = false;
                                            // Vérifier la tuile de fin
                                            if (canPlaceBridge && bridgeLayer.GetTile(new Vector3Int(x_end, y, 0)) != null) canPlaceBridge = false;
                                            // Vérifier les plateformes intermédiaires
                                            if (canPlaceBridge) {
                                                for (int i = 1; i <= length; i++)
                                                {
                                                    if (bridgeLayer.GetTile(new Vector3Int(x + i, y, 0)) != null)
                                                    {
                                                        canPlaceBridge = false;
                                                        break;
                                                    }
                                                }
                                            }
                                            // --- FIN DE LA VÉRIFICATION ANTI-CROISEMENT ---

                                            if (canPlaceBridge)
                                            {
                                                bridgeLayer.SetTile(new Vector3Int(x, y, 0), bridge_Start_E);
                                                bridgeLayer.SetTile(new Vector3Int(x_end, y, 0), bridge_Start_W);
                                                for (int i = 1; i <= length; i++)
                                                {
                                                    bridgeLayer.SetTile(new Vector3Int(x + i, y, 0), bridge_Platform_H);
                                                    shadowsLayer.SetTile(new Vector3Int(x + i, y, 0), null);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            // --- PLACEMENT DES PONTS VERTICAUX ---
            for (int x_coord = 0; x_coord < mapWidth; x_coord++) // Renommé 'x' pour éviter la confusion avec le 'x' du pont horizontal
            {
                for (int y = 0; y < mapHeight - minBridgeLength; y++)
                {
                    if (_elevationData[x_coord, y] == 1)
                    {
                        for (int length = minBridgeLength; length <= maxBridgeLength; length++)
                        {
                            int y_end = y + length + 1;
                            if (y_end < mapHeight)
                            {
                                if (_elevationData[x_coord, y_end] == 1)
                                {
                                    bool gapIsValid = true;
                                    for (int i = 1; i <= length; i++)
                                    {
                                        if (_elevationData[x_coord, y + i] != 0)
                                        {
                                            gapIsValid = false;
                                            break;
                                        }
                                    }

                                    if (gapIsValid)
                                    {
                                        if (Random.Range(0f, 1f) < bridgePlacementChance)
                                        {
                                            // --- NOUVELLE VÉRIFICATION ANTI-CROISEMENT ---
                                            bool canPlaceBridge = true;
                                            // Vérifier la tuile de départ
                                            if (bridgeLayer.GetTile(new Vector3Int(x_coord, y, 0)) != null) canPlaceBridge = false;
                                            // Vérifier la tuile de fin
                                            if (canPlaceBridge && bridgeLayer.GetTile(new Vector3Int(x_coord, y_end, 0)) != null) canPlaceBridge = false;
                                            // Vérifier les plateformes intermédiaires
                                            if (canPlaceBridge) {
                                                for (int i = 1; i <= length; i++)
                                                {
                                                    if (bridgeLayer.GetTile(new Vector3Int(x_coord, y + i, 0)) != null)
                                                    {
                                                        canPlaceBridge = false;
                                                        break;
                                                    }
                                                }
                                            }
                                            // --- FIN DE LA VÉRIFICATION ANTI-CROISEMENT ---

                                            if (canPlaceBridge)
                                            {
                                                bridgeLayer.SetTile(new Vector3Int(x_coord, y, 0), bridge_Start_N);
                                                bridgeLayer.SetTile(new Vector3Int(x_coord, y_end, 0), bridge_Start_S);
                                                for (int i = 1; i <= length; i++)
                                                {
                                                    bridgeLayer.SetTile(new Vector3Int(x_coord, y + i, 0), bridge_Platform_V);
                                                    shadowsLayer.SetTile(new Vector3Int(x_coord, y + i, 0), null);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

void PlaceDecorations()
    {
        if (decoLayer == null)
        {
            Debug.LogWarning("Deco Layer not assigned. Skipping decoration placement.");
            return;
        }
        if (decorationThemes.Count == 0)
        {
            Debug.LogWarning("No decoration themes defined. Skipping decoration placement.");
            return;
        }

        bool[,] occupiedByDecoThisPass = new bool[mapWidth, mapHeight]; // To prevent a single cluster from re-decorating its own spots

        foreach (DecoTheme theme in decorationThemes)
        {
            if (theme == null) continue;

            // Resetting occupiedByDecoThisPass for each theme allows different themes to potentially
            // place decorations on the same valid spot if previous themes didn't occupy it permanently on decoLayer.
            // If you want absolutely no deco overlap ever, even between themes, move this array initialization
            // outside the foreach loop and don't reset it.
            for(int i=0; i < mapWidth; i++) for(int j=0; j < mapHeight; j++) occupiedByDecoThisPass[i,j] = false;


            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    // Check if this tile is a ground tile on either level 0 or level 1
                    // and not occupied by walls/stairs/bridges on its own level.
                    int currentTileElevation = _elevationData[x, y];

                    // Only consider ground layer tiles as potential seeds.
                    // Platform tiles (level 1) are groundLayer tiles. Base level (level 0) are also groundLayer tiles.
                    if (groundLayer.GetTile(new Vector3Int(x, y, 0)) == null) continue;


                    if (Random.Range(0f, 1f) < theme.clusterStartChance)
                    {
                        // Potential cluster seed. The cluster will try to grow on currentTileElevation.
                        TryPlaceCluster(x, y, currentTileElevation, theme, occupiedByDecoThisPass);
                    }
                }
            }
        }
    }

    void TryPlaceCluster(int seedX, int seedY, int clusterElevation, DecoTheme theme, bool[,] occupiedByDecoThisPass)
    {
        // Initial check for the seed itself
        if (!IsValidForDecoration(seedX, seedY, clusterElevation, occupiedByDecoThisPass)) return;

        int clusterSize = Random.Range(theme.minClusterSize, theme.maxClusterSize + 1);
        int decorationsPlacedInCluster = 0;
        
        List<Vector2Int> potentialSpots = new List<Vector2Int>();
        HashSet<Vector2Int> visitedSpots = new HashSet<Vector2Int>();

        potentialSpots.Add(new Vector2Int(seedX, seedY));
        visitedSpots.Add(new Vector2Int(seedX, seedY));

        int attempts = 0;
        int maxAttempts = clusterSize * (theme.clusterSpreadRadius * 2 + 1) * (theme.clusterSpreadRadius * 2 + 1) * 2; // Increased attempts a bit

        while (decorationsPlacedInCluster < clusterSize && potentialSpots.Count > 0 && attempts < maxAttempts)
        {
            attempts++;
            Vector2Int currentSpot = potentialSpots[Random.Range(0, potentialSpots.Count)];
            // For a more structured growth (like BFS), you'd take from the start of the list:
            // Vector2Int currentSpot = potentialSpots[0];
            potentialSpots.Remove(currentSpot); // Or potentialSpots.RemoveAt(0) for BFS style

            if (IsValidForDecoration(currentSpot.x, currentSpot.y, clusterElevation, occupiedByDecoThisPass))
            {
                if (Random.Range(0f, 1f) < theme.densityInCluster)
                {
                    TileBase decoTile = GetRandomTileForTheme(theme);
                    if (decoTile != null)
                    {
                        decoLayer.SetTile(new Vector3Int(currentSpot.x, currentSpot.y, 0), decoTile);
                        occupiedByDecoThisPass[currentSpot.x, currentSpot.y] = true;
                        decorationsPlacedInCluster++;
                    }
                }
            }

            // Add valid neighbors to potential spots if within spread radius from original seed
            // and on the same clusterElevation
            for (int i = 0; i < 8; i++) // Check 8 neighbors
            {
                int nx = currentSpot.x + neighborOffsets[i].x;
                int ny = currentSpot.y + neighborOffsets[i].y;
                Vector2Int neighborPos = new Vector2Int(nx, ny);

                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight && // Basic bounds
                    !visitedSpots.Contains(neighborPos) &&                 // Not already processed for this cluster
                    _elevationData[nx,ny] == clusterElevation &&             // *** MUST BE ON THE SAME ELEVATION AS THE CLUSTER ***
                    Mathf.Abs(nx - seedX) <= theme.clusterSpreadRadius &&
                    Mathf.Abs(ny - seedY) <= theme.clusterSpreadRadius)
                {
                    potentialSpots.Add(neighborPos);
                    visitedSpots.Add(neighborPos);
                }
            }
            if (decorationsPlacedInCluster >= clusterSize) break;
        }
    }

    // MODIFIED IsValidForDecoration
    bool IsValidForDecoration(int x, int y, int targetElevation, bool[,] occupiedByDecoThisPass)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;

        // Rule 1: Must be on the target elevation for this cluster
        if (_elevationData[x, y] != targetElevation) return false;

        Vector3Int pos = new Vector3Int(x, y, 0);

        // Rule 2: Must be on an actual ground tile (not empty space within the target elevation)
        // This check is important because _elevationData might be 0 or 1, but the groundLayer might be null
        // if it's, for example, the air under a bridge that is also at level 0.
        if (groundLayer.GetTile(pos) == null) return false;

        // Rule 3: Not on a wall, stair, bridge, or already decorated tile
        // Walls are typically on level 0 but defined by a platform above.
        // Stairs replace walls. Bridges are on their own layer.
        if (wallsLayer.GetTile(pos) != null) return false;
        if (stairsLayer.GetTile(pos) != null) return false;
        if (bridgeLayer != null && bridgeLayer.GetTile(pos) != null) return false;
        if (decoLayer.GetTile(pos) != null) return false; // Check final decoLayer for any existing deco from other themes/passes
        if (occupiedByDecoThisPass[x,y]) return false; // Check if occupied in the current theme's cluster growth

        return true;
    }

    // MODIFIED GetRandomTileForTheme - No changes needed for this specific request, but shown for completeness
    TileBase GetRandomTileForTheme(DecoTheme theme)
    {
        List<TileBase> availableTiles = new List<TileBase>();

        if (theme.themeType == DecoThemeType.SpecialItems)
        {
            if (theme.specialTiles != null && theme.specialTiles.Count > 0)
            {
                availableTiles.AddRange(theme.specialTiles);
            }
        }
        else 
        {
            if (theme.smallTile != null) availableTiles.Add(theme.smallTile);
            if (theme.mediumTile != null) availableTiles.Add(theme.mediumTile);
            if (theme.bigTile != null) availableTiles.Add(theme.bigTile);
        }

        if (availableTiles.Count == 0) return null;

        if (theme.themeType != DecoThemeType.SpecialItems && availableTiles.Count > 0) {
             float roll = Random.Range(0f, 1f);
             if (roll < 0.6f && theme.smallTile != null && availableTiles.Contains(theme.smallTile)) return theme.smallTile; 
             else if (roll < 0.9f && theme.mediumTile != null && availableTiles.Contains(theme.mediumTile)) return theme.mediumTile; 
             else if (theme.bigTile != null && availableTiles.Contains(theme.bigTile)) return theme.bigTile; 
             // Fallback if weighted selection fails (e.g. a specific tile was null but others aren't)
             else return availableTiles[Random.Range(0, availableTiles.Count)]; 
        }
        // For special items or if weighted selection didn't pick (shouldn't happen if tiles are assigned)
        return availableTiles[Random.Range(0, availableTiles.Count)];
    }

public bool IsTileWithinBounds(int x, int y)
{
    // mapWidth et mapHeight doivent être les dimensions de votre grille de données (par exemple, ElevationData)
    return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
}
}