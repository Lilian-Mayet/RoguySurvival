using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic; // For List

public class MapGenerator : MonoBehaviour
{
    // --- Tile Enums ---
    private enum Biome { GreenGrass, DryGrass }
    private enum TileContext { Ground, Platform } // To differentiate rules for ground vs. platform surface

    // --- Map Data ---
    private int[,] elevationData; // 0 = base, 1 = plateau
    private Biome[,] biomeData;

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

    [Header("Tilemap References")]
    public Tilemap groundLayer;
    public Tilemap wallsLayer;
    public Tilemap stairsLayer;
    public Tilemap shadowsLayer;

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
    public TileBase cliffWall_S; // For south-facing cliffs

    public TileBase stair_S_Tile;

    public TileBase shadow_Edge_S; // Shadow directly below a south edge of platform
public TileBase shadow_Edge_E; // Shadow to the left of an east edge of platform
public TileBase shadow_Edge_W; // Shadow to the right of a west edge of platform


    // --- Neighbor Offsets (for 8-way check) ---
    private readonly Vector2Int[] neighborOffsets = {
        new Vector2Int(0, 1),  // N (Index 0)
        new Vector2Int(1, 1),  // NE (Index 1)
        new Vector2Int(1, 0),  // E (Index 2)
        new Vector2Int(1, -1), // SE (Index 3)
        new Vector2Int(0, -1), // S (Index 4)
        new Vector2Int(-1, -1),// SW (Index 5)
        new Vector2Int(-1, 0), // W (Index 6)
        new Vector2Int(-1, 1) // NW (Index 7)
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

        GenerateElevationData();
        GenerateBiomeData();
        PlaceTilesAndFeatures();
        PlaceStairs(); // Optional, call after main structure

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
    }

    void GenerateElevationData()
    {
        elevationData = new int[mapWidth, mapHeight];
        System.Random prng = new System.Random(seed); // Use the main seed consistently
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng, elevationOctaves, elevationOffset);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float noiseValue = GenerateNormalizedNoiseValue(x, y, elevationNoiseScale, elevationOctaves, elevationPersistence, elevationLacunarity, octaveOffsets, mapWidth, mapHeight);
                if (noiseValue > elevationThreshold)
                {
                    elevationData[x, y] = 1; // Level 1 plateau
                }
                else
                {
                    elevationData[x, y] = 0; // Base level
                }
            }
        }
    }

    void GenerateBiomeData()
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
            // Using currentMapWidth/Height from parameters for flexibility
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
            int currentLevel = elevationData[x, y];
            Biome currentBiome = biomeData[x, y];
            TileBase tileToPlace;

            if (currentLevel == 1) // On a plateau
            {
                tileToPlace = DetermineTileForCell(x, y, currentBiome, TileContext.Platform);
                groundLayer.SetTile(currentPos, tileToPlace);

                // South-facing cliff wall (placed on level 0, below the platform edge at (x,y))
                if (y > 0 && elevationData[x, y - 1] == 0)
                {
                    Vector3Int wallPos = new Vector3Int(x, y - 1, 0);
                    wallsLayer.SetTile(wallPos, cliffWall_S);
                    // South edge shadow will be handled in Stage 2
                }
            }
            else // On base ground (level 0)
            {
                tileToPlace = DetermineTileForCell(x, y, currentBiome, TileContext.Ground);
                groundLayer.SetTile(currentPos, tileToPlace);
            }
        }
    }

    // --- STAGE 2: Place Shadows for Platform Edges ---
    for (int y = 0; y < mapHeight; y++)
    {
        for (int x = 0; x < mapWidth; x++)
        {
            if (elevationData[x, y] == 1) // If this is a platform tile
            {
                // Check South Edge (Shadow below)
                if (y > 0 && elevationData[x, y - 1] == 0 && shadow_Edge_S != null)
                {
                    // Shadow is placed on the ground tile (x, y-1)
                    shadowsLayer.SetTile(new Vector3Int(x, y - 1, 0), shadow_Edge_S);
                }

                // Check East Edge (Shadow to its Left)
                if (x > 0 && elevationData[x - 1, y] == 0 && shadow_Edge_E != null)
                {
                    // Shadow is placed on the ground tile (x-1, y)
                    shadowsLayer.SetTile(new Vector3Int(x - 1, y, 0), shadow_Edge_E);
                }

                // Check West Edge (Shadow to its Right)
                if (x < mapWidth - 1 && elevationData[x + 1, y] == 0 && shadow_Edge_W != null)
                {
                    // Shadow is placed on the ground tile (x+1, y)
                    shadowsLayer.SetTile(new Vector3Int(x + 1, y, 0), shadow_Edge_W);
                }

                // Note: We are NOT placing shadows for North edges of platforms,
                // as per typical top-down perspective where light comes from top/top-left.
                // You could add logic for shadow_Edge_N if desired, placing it at (x, y+1).

                // Optional: Corner Shadows
                // This requires checking two adjacent edges. Example for South-East corner shadow:
                // bool isSECorner = (y > 0 && elevationData[x, y - 1] == 0) && (x < mapWidth - 1 && elevationData[x + 1, y] == 0);
                // if (isSECorner && shadow_Corner_SE != null) {
                //    shadowsLayer.SetTile(new Vector3Int(x + 1, y - 1, 0), shadow_Corner_SE); // Shadow at SE diagonal
                // }
                // Similar logic for SW, NE, NW corners if you have those shadow tiles.
            }
        }
    }
}
    TileBase DetermineTileForCell(int x, int y, Biome cellBiome, TileContext context)
    {
        int bitmask = 0;
        int currentCellElevation = elevationData[x, y];

        for (int i = 0; i < 8; i++) // N, NE, E, SE, S, SW, W, NW
        {
            int nx = x + neighborOffsets[i].x;
            int ny = y + neighborOffsets[i].y;
            bool neighborMatchesCriteria = false;

            if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight) // In bounds
            {
                int neighborElevation = elevationData[nx, ny];
                Biome neighborBiome = biomeData[nx, ny];

                if (neighborElevation == currentCellElevation)
                {
                    // If on the same elevation, "match" means it's the same biome
                    neighborMatchesCriteria = (neighborBiome == cellBiome);
                }
                else
                {
                    // If different elevation, it's always a non-match for biome tiling purposes.
                    // This creates the "edge" for platforms.
                    neighborMatchesCriteria = false;
                }
            }
            else // Out of bounds is a non-match
            {
                neighborMatchesCriteria = false;
            }

            if (neighborMatchesCriteria)
            {
                bitmask |= (1 << i);
            }
        }

        // Cardinal and Diagonal Match Flags (true if neighbor matches criteria)
        bool N  = (bitmask & (1 << 0)) != 0;
        bool NE = (bitmask & (1 << 1)) != 0;
        bool E  = (bitmask & (1 << 2)) != 0;
        bool SE = (bitmask & (1 << 3)) != 0;
        bool S  = (bitmask & (1 << 4)) != 0;
        bool SW = (bitmask & (1 << 5)) != 0;
        bool W  = (bitmask & (1 << 6)) != 0;
        bool NW = (bitmask & (1 << 7)) != 0;

        TileBase selectedTile = null;

        // --- Comprehensive Tile Selection Logic ---
        // This logic attempts to match the 47-tile "blob" pattern.
        // Order: From most surrounded (center) to least (isolated).

        if (N && E && S && W) // All 4 cardinal directions match
        {
            // With no inner corners, if all cardinals match, it's always center,
            // *unless* you have a specific rule for when diagonals don't match but all cardinals do.
            // For simplicity now, if all cardinals match, it's center.
            selectedTile = GetTile(cellBiome, "center");
        }
        // 3 Cardinal Matches (T-Junctions)
        else if (N && E && S && !W && NE && SE) selectedTile = GetTile(cellBiome, "top_right_bottom"); // Missing W
        else if (N && E && !S && W && NE && NW) selectedTile = GetTile(cellBiome, "top_left_right");   // Missing S
        else if (N && !E && S && W && NW && SW) selectedTile = GetTile(cellBiome, "top_left_bottom");  // Missing E
        else if (!N && E && S && W && SE && SW) selectedTile = GetTile(cellBiome, "left_bottom_right"); // Missing N
        // 2 Opposite Cardinal Matches (Corridors/Strips)
        else if (N && S && !E && !W) selectedTile = GetTile(cellBiome, "top_bottom");
        else if (E && W && !N && !S) selectedTile = GetTile(cellBiome, "left_right");
        // 2 Adjacent Cardinal Matches (Outer Corners) - Diagonals are crucial here
        else if (N && E && !S && !W && NE) selectedTile = GetTile(cellBiome, "top_right");
        else if (E && S && !N && !W && SE) selectedTile = GetTile(cellBiome, "bottom_right");
        else if (S && W && !N && !E && SW) selectedTile = GetTile(cellBiome, "bottom_left");
        else if (W && N && !E && !S && NW) selectedTile = GetTile(cellBiome, "top_left");
        // 1 Cardinal Match (Edges)
        else if (N && !E && !S && !W) selectedTile = GetTile(cellBiome, "top");
        else if (E && !N && !S && !W) selectedTile = GetTile(cellBiome, "right");
        else if (S && !N && !E && !W) selectedTile = GetTile(cellBiome, "bottom");
        else if (W && !N && !E && !S) selectedTile = GetTile(cellBiome, "left");
        // 0 Cardinal Matches
        else selectedTile = GetTile(cellBiome, "isolated");


        if (selectedTile == null) {
             // This fallback handles cases where the above logic might miss a combination,
             // or if not all specific tiles (like all inner corners) are assigned.
             // It tries to find the best fit from simpler tiles.
            if (N && E && S && W) selectedTile = GetTile(cellBiome, "center");
            else if (N && S) selectedTile = GetTile(cellBiome, "top_bottom");
            else if (E && W) selectedTile = GetTile(cellBiome, "left_right");
            else if (N && E) selectedTile = GetTile(cellBiome, "top_right");
            else if (E && S) selectedTile = GetTile(cellBiome, "bottom_right");
            else if (S && W) selectedTile = GetTile(cellBiome, "bottom_left");
            else if (W && N) selectedTile = GetTile(cellBiome, "top_left");
            else if (N) selectedTile = GetTile(cellBiome, "top");
            else if (E) selectedTile = GetTile(cellBiome, "right");
            else if (S) selectedTile = GetTile(cellBiome, "bottom");
            else if (W) selectedTile = GetTile(cellBiome, "left");
            else selectedTile = GetTile(cellBiome, "isolated");

            if (selectedTile == null) { // Absolute fallback
                Debug.LogError($"CRITICAL: Could not determine tile for biome {cellBiome} (isolated fallback failed). Assign gg_isolated and dg_isolated.");
                selectedTile = (cellBiome == Biome.GreenGrass) ? gg_isolated : dg_isolated; // Should always have isolated
            }
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

                default: Debug.LogWarning($"Unknown green grass tile type: {type}"); return gg_isolated;
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

                default: Debug.LogWarning($"Unknown dry grass tile type: {type}"); return dg_isolated;
            }
        }
    }

    void PlaceStairs()
    {
        if (stair_S_Tile == null) return; // No stair tile assigned

        List<Vector3Int> potentialStairLocations = new List<Vector3Int>();

        // Find south-facing cliff edges suitable for stairs
        for (int x = 1; x < mapWidth - 1; x++) // Avoid map edges for simplicity
        {
            for (int y = 1; y < mapHeight -1; y++) // y is platform edge, y-1 is the wall/stair base
            {
                // Check if (x,y) is on platform and (x,y-1) is ground level
                if (elevationData[x, y] == 1 && elevationData[x, y - 1] == 0)
                {
                    // Check if there's actually a cliff wall placed at (x, y-1)
                    if (wallsLayer.GetTile(new Vector3Int(x, y - 1, 0)) == cliffWall_S)
                    {
                        // Further checks: ensure space for landing isn't an awkward corner.
                        // For now, any south-facing wall is a candidate.
                        potentialStairLocations.Add(new Vector3Int(x, y - 1, 0)); // Store wall location
                    }
                }
            }
        }

        if (potentialStairLocations.Count > 0)
        {
            // Place one stair at a random valid location for now
            Vector3Int stairBasePos = potentialStairLocations[Random.Range(0, potentialStairLocations.Count)];
            
            wallsLayer.SetTile(stairBasePos, null); // Clear the wall tile
            shadowsLayer.SetTile(stairBasePos, null); // Clear the shadow tile at wall base

            // Place the stair tile. It visually spans from y-1 (base) to y (top).
            // It's placed on the 'Stairs_Layer' at the same Z-level as other ground/wall tiles.
            stairsLayer.SetTile(stairBasePos, stair_S_Tile);

            // Optional: Change the ground tile at the top of the stairs (x, stairBasePos.y + 1)
            // to a specific "stair landing" tile if you have one.
            // groundLayer.SetTile(new Vector3Int(stairBasePos.x, stairBasePos.y + 1, 0), your_stair_landing_tile);
        }
    }
}