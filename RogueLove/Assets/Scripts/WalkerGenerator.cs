using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using JetBrains.Annotations;
using NUnit.Framework.Internal;
using Pathfinding;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.Tilemaps;

public class WalkerGenerator : MonoBehaviour
{
    // TILE TYPES
    public enum Grid {
        FLOOR, EMPTY, OBSTACLES, DECOR
    }
    
    // 2D Array
    public Grid[,] gridHandler;

    public List<int> tileListX = new();

    public List<int> tileListY = new();

    // List of all active Walkers
    private List<WalkerObject> walkers;

    [Header("TILEMAP OBJECTS")]

    // TILEMAP REFERENCES
    public AreaTiles tiles;
    
    public Tilemap tilemap;

    public Tilemap oTilemap;

    [Space(10)]
    [Header("MAP SETTINGS")]

    [Range(7, 60)]
    // Map maximum width <-->
    public int mapWidth;

    [Range(7, 60)]
    // Map maximum height ^v
    public int mapHeight;

    [SerializeField]
    [Range(1, 10)]
    // Maximum amount of active walkers
    private int maxWalkers;

    [SerializeField]
    // Chance for a single tile to not generate an obstacle
    private int emptyChance;

    // Current tile count
    public int tileCount = default;

    // Current obstacle tile count
    public int oTileCount = default;

    [SerializeField]
    [Range(0.1f, 0.98f)]
    // Compares the amount of floor tiles to the percentage of the total grid covered (in floor tiles)
    // NEVER SET THIS TO 1 OR ELSE UNITY WILL CRASH :skull:
    private float fillPercentage;

    [SerializeField]
    // Generation method, pause time between each successful movement
    private float waitTime;

    // A* pathfinder object
    private AstarPath astarPath;

    // SAVE FILE PATH
    private string pathMap;

    // Boolean to see if save file exists
    private bool loadFromSave;

    [SerializeField]
    private int lvl;
    [SerializeField]
    private int stg;

    [SerializeField]
    private float spawnRadiusX;
    
    [SerializeField]
    private float spawnRadiusY;

    [Space(10)]
    [Header("ENTITIES")]

    // Finds player
    [SerializeField]
    private GameObject player;
    private PlayerController playerCont;

    // List of all common enemies in this level
    public GameObject[] commonEnemies;

    // List of all rare enemies in this level
    public GameObject[] rareEnemies;

    // List of all stationary enemies in this level
    public GameObject[] stationEnemies;

    // List of all minibosses in this level
    public GameObject[] minibosses;

    // List of all bosses in this level
    public GameObject[] bosses;

    [Space(10)]
    [Header("LEVEL INFO")]

    private static int deadEnemies;
    public static void SetDeadEnemy() {
        deadEnemies++;
    }
    public static int GetDeadEnemies() {
        return deadEnemies;
    }

    private static int enemyTotal;
    public static int GetEnemyTotal() {
        return enemyTotal;
    }

    [SerializeField]
    private bool bossLevel = false;

    // Initializes grid to be generated (size)
    void Start() {
        TransitionManager.SetLoadingBar(true);
        enemyTotal = 0;
        deadEnemies = 0;
        GameStateManager.SetLevelClear(false);

        if (tilemap == null) {
            tilemap = GameObject.Find("Tilemap").GetComponent<Tilemap>();
        }
        if (oTilemap == null) {
            oTilemap = GameObject.Find("Obstacles").GetComponent<Tilemap>();
        }
        if (tiles == null) {
            tiles = GameObject.FindGameObjectWithTag("AreaTiles").GetComponent<AreaTiles>();
        }
        if (player == null) {
            player = GameObject.FindGameObjectWithTag("Player");
            playerCont = player.GetComponent<PlayerController>();
        }

        pathMap = Application.persistentDataPath + "/map.chris";
        enemyTotal = 0;

        if (File.Exists(pathMap) && GameStateManager.SavePressed() == true) {
            loadFromSave = true;
            GameStateManager.SetSave(false);
        } else {
            loadFromSave = false;
            GameStateManager.SetSave(false);
        }

        if (GameStateManager.GetLevel() == 0) {
            GameStateManager.SetLevel(1);
        }
        if (GameStateManager.GetStage() == 0) {
            GameStateManager.SetStage(1);
        }

        lvl = GameStateManager.GetLevel();
        stg = GameStateManager.GetStage();

        InitializeGrid();
        Debug.Log("Attempted to initialize grid");

        if (loadFromSave == true) {
            LoadMap();
            Debug.Log("Loaded Map");
        }
    }

    void Update() {
        if (GetEnemyTotal() != 0) {
            if (GetEnemyTotal() == GetDeadEnemies() && GameStateManager.GetLevelClear() == false) {
                GameStateManager.SetLevelClear(true);
                SpawnDoorway();
            }
        }
    }
    void InitializeGrid() {

        // Initializes grid size from variables
        gridHandler = new Grid[mapWidth, mapHeight];
        tileCount = 0;

        // Loops through grid size and sets every tile to EMPTY tile
        for (int x = 0; x < gridHandler.GetLength(0); x++) {
            for (int y = 0; y < gridHandler.GetLength(1); y++) {
                gridHandler[x,y] = Grid.EMPTY;
            }
        }

        // New instance of the WalkerObject list
        walkers = new List<WalkerObject>();

        // Creates reference of the exact centerpiece of the tilemap
        Vector3Int tileCenter = new(0, 0, 0);

        // Creates a new walker, and sets initial values
        WalkerObject currWalker = new WalkerObject(new Vector2(tileCenter.x, tileCenter.y), GetDirection(), 0.5f);
        // Sets current grid location to floor
        gridHandler[tileCenter.x, tileCenter.y] = Grid.FLOOR;
        tilemap.SetTile(tileCenter, tiles.floor);
        //tilemapObstacles.SetTile(tileCenter, floor);
        // Adds current walker to Walker list
        walkers.Add(currWalker);

        // Increases total tile count
        tileCount++;

        if (loadFromSave != true) {
            // Handles walker rules
            StartCoroutine(CreateFloors());
        }
    }

    // Returns a random single vector direction
    Vector2 GetDirection() {
        int choice = Mathf.FloorToInt(UnityEngine.Random.value * 3.99f);

        switch (choice) {
            case 0:
                return Vector2.down;
            case 1:
                return Vector2.left;
            case 2:
                return Vector2.up;
            case 3:
                return Vector2.right;
            default:
                return Vector2.zero;
        }
    }

    IEnumerator CreateFloors() {
        // Compares tile count as a float to the total size of the grid, and will continue looping as long as it is
        // less than the fillPercentage value set earlier
        while ((float)tileCount / (float)gridHandler.Length < fillPercentage) {
            
            // Yield return value for coroutine
            bool hasCreatedFloor = false;
            
            // Loops through every walker in the list, and creates a reference to its current position to
            // check if it is a FLOOR piece
            foreach (WalkerObject currWalker in walkers) {

                Vector3Int currPos = new Vector3Int((int)currWalker.position.x, (int)currWalker.position.y, 0);
                Vector3Int currPos2 = new Vector3Int((int)currWalker.position.x - 1, (int)currWalker.position.y, 0);
                Vector3Int currPos3 = new Vector3Int((int)currWalker.position.x, (int)currWalker.position.y - 1, 0);
                Vector3Int currPos4 = new Vector3Int((int)currWalker.position.x - 1, (int)currWalker.position.y - 1, 0);

                // If ^ is not, then set a new FLOOR tile in that position and increment the total tile count
                if (gridHandler[currPos.x, currPos.y] != Grid.FLOOR) {
                    tilemap.SetTile(currPos, tiles.floor);
                    tileListX.Add(currPos.x);
                    tileListY.Add(currPos.y);
                    tilemap.SetTile(currPos2, tiles.floor);
                    tileListX.Add(currPos2.x);
                    tileListY.Add(currPos2.y);
                    tilemap.SetTile(currPos3, tiles.floor);
                    tileListX.Add(currPos3.x);
                    tileListY.Add(currPos3.y);
                    tilemap.SetTile(currPos4, tiles.floor);
                    tileListX.Add(currPos4.x);
                    tileListY.Add(currPos4.y);
                    tileCount++;
                    tileCount++;
                    tileCount++;
                    gridHandler[currPos.x, currPos.y] = Grid.FLOOR;
                    gridHandler[currPos.x - 1, currPos.y] = Grid.FLOOR;
                    gridHandler[currPos.x, currPos.y - 1] = Grid.FLOOR;
                    gridHandler[currPos.x - 1, currPos.y - 1] = Grid.FLOOR;

                    hasCreatedFloor = true;
                }
            }

            // Walker methods
            ChanceToRemove();
            ChanceToRedirect();
            ChanceToCreate();
            UpdatePosition();

            if (hasCreatedFloor) {
                yield return new WaitForSeconds(waitTime);
            }
        }

        if (!bossLevel) {
            StartCoroutine(CreateObstacles());
        }
        StartCoroutine(CreateBreakables());
        
        tilemap.SetTile(new Vector3Int(0, 0, 0), tiles.empty);
        tileCount--;
        gridHandler[0, 0] = Grid.EMPTY;
        StartCoroutine(CreateDecor());
    }

    // Loops through walker list, randomly compares a value to the WalkersChance, and if it's > 1 then 
    // deactivates walker if true
    void ChanceToRemove() {
        int updatedCount = walkers.Count;
        for (int i = 0; i < updatedCount; i++) {
            if (UnityEngine.Random.value < walkers[i].chanceToChange && walkers.Count > 1) {
                walkers.RemoveAt(i);
                break;
            }
        }
    }

    // ^^ same thing but updates direction instead of deletes
    void ChanceToRedirect() {
        for (int i = 0; i < walkers.Count; i++) {
            if (UnityEngine.Random.value < walkers[i].chanceToChange) {
                WalkerObject currWalker = walkers[i];
                currWalker.direction = GetDirection();
                walkers[i] = currWalker;
            }
        }
    }

    // ^^ same thing but creates a new walker (duplicates at position) as long as it is under max walkers
    void ChanceToCreate() {
        int updatedCount = walkers.Count;
        for (int i = 0; i < updatedCount; i++) {
            if (UnityEngine.Random.value < walkers[i].chanceToChange && walkers.Count < maxWalkers) {
                Vector2 newDirection = GetDirection();
                Vector2 newPosition = walkers[i].position;

                WalkerObject newWalker = new WalkerObject(newPosition, newDirection, 0.5f);
                walkers.Add(newWalker);
            }
        }
    }

    // Update actual position of the walkers, within the bounds of the grid size
    void UpdatePosition() {
        for (int i = 0; i < walkers.Count; i++) {
            WalkerObject foundWalker = walkers[i];
            foundWalker.position += foundWalker.direction;
            foundWalker.position.x = Mathf.Clamp(foundWalker.position.x, 1, gridHandler.GetLength(0) - 2);
            foundWalker.position.y = Mathf.Clamp(foundWalker.position.y, 1, gridHandler.GetLength(1) - 2);
            walkers[i] = foundWalker;
        }
    }

    // CREATES OBSTACLES
    IEnumerator CreateObstacles() {

        for (int x = 0; x < gridHandler.GetLength(0) - 1; x++) {
            for (int y = 0; y < gridHandler.GetLength(1) - 1; y++) {
                int rand = UnityEngine.Random.Range(0, emptyChance);

                // Checks each x and y value of the grid to see if they are floors
                if (gridHandler[x, y] == Grid.FLOOR) {
                    bool hasCreatedObstacle = false;

                    // SINGLE FENCE CHECK
                    if (gridHandler[x, y] == Grid.FLOOR && rand == 0) {
                        oTilemap.SetTile(new Vector3Int(x, y, 0), tiles.obstacles);
                        gridHandler[x, y] = Grid.OBSTACLES;
                        hasCreatedObstacle = true;
                        oTileCount++;
                    }

                    if (hasCreatedObstacle) {
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
        }
    }

    IEnumerator CreateBreakables() {
        // For all different breakables in the level
        for (int b = 0; b < tiles.breakables.Length; b++) {

            // Generate random amount of common enemies in level (e.g. 4 Wisplings, 5 Slimes, 1 Joseph)
            int breakRange = UnityEngine.Random.Range(6, 10);

            for (int br = 0; br < breakRange; br++) {

                int rand = GetRandomTile();

                // Check all X tiles
                for (int x = 0; x < tileListX.Count; x++) {

                    // SINGLE FENCE CHECK
                    if ((tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand], 0)) == tiles.ground)
                    && (oTilemap.GetTile(new Vector3Int(tileListX[rand], tileListY[rand], 0)) != tiles.obstacles)) {

                        Instantiate(tiles.breakables[b], new Vector3(tileListX[rand] * 0.16f, tileListY[rand] * 0.16f, 0), Quaternion.identity);
                        break;

                    } else {
                        rand = GetRandomTile();
                    }
                }
            }
        }
        yield return null;
    }

    IEnumerator CreateDecor() {

        for (int x = 0; x < gridHandler.GetLength(0) - 1; x++) {
            for (int y = 0; y < gridHandler.GetLength(1) - 1; y++) {

                // Checks each x and y value of the grid to see if they are floors
                if (gridHandler[x, y] == Grid.EMPTY) {
                    bool hasCreatedDecor = false;

                    // DECOR CHECK
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles.decor);
                    gridHandler[x, y] = Grid.DECOR;
                    hasCreatedDecor = true;
                    tileCount++;

                    if (hasCreatedDecor) {
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
        }
        //Debug.Log(tileListX.Count);
        //Debug.Log(tileListY.Count);
        //Debug.Log(gridHandler.Length);
        SpawnRandomPlayer();
        SpawnRandomEnemies();
        PathScan();
        TransitionManager.EndLeaf(true);
        SaveMap();
        playerCont.SavePlayer();
    }

    // SPAWN PLAYER
    public void SpawnRandomPlayer() {

        // Generates random number to pick Player spawnpoint
        int randP = GetRandomTile();

        // For as many floor tiles as there are in the tilemap:
        for (int i = 0; i < tileListX.Count; i++) {

            // If suitable floor tiles have been found (Ground tiles and no obstacles on those tiles)
            if ((tilemap.GetSprite(new Vector3Int(tileListX[i], tileListY[randP])) == tiles.ground)
            && (oTilemap.GetTile(new Vector3Int(tileListX[i], tileListY[randP])) != tiles.obstacles)
            && (gridHandler[tileListX[i], tileListY[i]] == Grid.FLOOR)) {

                // Spawns Player
                //player.SetActive(true);
                player.transform.position = new Vector2((tileListX[i] * 0.16f) + 0.08f, (tileListY[randP] * 0.16f) + 0.08f);
                break;

            } else {
                // Generates random number to pick Player spawnpoint
                randP = UnityEngine.Random.Range(0, tileListX.Count);
            }
        }
    }

    // Spawns doorway to next level after level cleared
    private void SpawnDoorway() {

        for (int i = gridHandler.GetLength(0); i <= gridHandler.GetLength(0) && i >= 0; i--) {
            //Debug.Log(i);

            //int rand = UnityEngine.Random.Range(0, tileListY.Count);

            // If suitable floor tiles have been found (Ground tiles and no obstacles on those tiles)
            if ((tilemap.GetSprite(new Vector3Int(i, (int)Mathf.Round(mapHeight/2))) == tiles.wallRight)
            && (oTilemap.GetTile(new Vector3Int(i, (int)Mathf.Round(mapHeight/2))) != tiles.obstacles)
            && (gridHandler[i, (int)Mathf.Round(mapHeight/2)] == Grid.FLOOR)) {

                // Spawns doorway
                if (tiles.doorwayObject.TryGetComponent<Doorway>(out var door)) {
                    door.Create(tiles.doorwayObject, new Vector3((i * 0.16f) + 0.08f, ((int)Mathf.Round(mapHeight/2) * 0.16f) + 0.08f), Quaternion.identity, player);
                    break;
                } else {
                    Debug.LogError("Could not find Doorway component of door while spawning!");
                    break;
                }

            } 
            /*
            else {
                // Generates random number to pick doorway spawnpoint
                rand = GetRandomTile();
            } */
        }
    }

    // SPAWN ENEMIES
    void SpawnRandomEnemies() {

        // TODO: If level number is the last level in area, then disregard everything except Boss spawning
        if (!IsArrayEmpty(bosses)) {
            bossLevel = true;
            Debug.Log(bosses);
        }

        if (!IsArrayEmpty(commonEnemies) && !bossLevel) {
            // For every common enemy in the level (e.g. Wispling, Slime, Joseph)
            for (int c = 0; c < commonEnemies.Length; c++) {

                // Generate random amount of common enemies in level (e.g. 4 Wisplings, 5 Slimes, 1 Joseph)
                int commonRange = UnityEngine.Random.Range(5, 8);

                // For the amount of every different type of common enemy (e.g. for 4 Wisplings, for 5 Slimes, for 1 Joseph)
                for (int s = 0; s < commonRange; s++) {
                    
                    // Generates random number to pick Enemy spawnpoint
                    int rand = GetRandomTile();

                    // For as many floor tiles as there are in the tilemap:
                    for (int i = 0; i < tileListX.Count; i++) {

                        // If suitable floor tiles have been found (Ground tiles and no obstacles on those tiles)
                        if ((tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand], 0)) == tiles.ground)
                        && (oTilemap.GetTile(new Vector3Int(tileListX[rand], tileListY[rand], 0)) != tiles.obstacles)) {

                            if (tileListX[rand] <= player.transform.position.x + spawnRadiusX 
                            && tileListX[rand] >= player.transform.position.x - spawnRadiusX) {
                                rand = GetRandomTile();
                            } else if (tileListY[rand] <= player.transform.position.y + spawnRadiusY 
                            && tileListY[rand] >= player.transform.position.y - spawnRadiusY) {
                                rand = GetRandomTile();
                            } else {
                                // Spawns Enemy
                                if (commonEnemies[c].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(commonEnemies[c], new Vector2(tileListX[rand] * 0.16f, tileListY[rand] * 0.16f), Quaternion.identity, this);   
                                    enemyTotal++;
                                    break;
                                }
                            }
                        } else {
                            
                            // Generates random number to pick Enemy spawnpoint
                            rand = GetRandomTile();
                        }
                    }
                }
            }
        }

        if (!IsArrayEmpty(rareEnemies) && !bossLevel) {
            // For every rare enemy in the level (e.g. Deforestation Guy, Nancy)
            for (int r = 0; r < rareEnemies.Length; r++) {
                
                // Generate random amount of rare enemies in level (e.g. 3 Deforestation Guy, 2 Nancy)
                int rareRange = UnityEngine.Random.Range(3, 5);
                
                // For the amount of every different type of rare enemy (e.g. for 3 Deforestation Guy, for 2 Nancy)
                for (int s = 0; s < rareRange; s++) {
                    
                    // Generates random number to pick Enemy spawnpoint
                    int rand = GetRandomTile();

                    // For as many floor tiles as there are in the tilemap:
                    for (int i = 0; i < tileListX.Count; i++) {

                        // If suitable floor tiles have been found (Ground tiles and no obstacles on those tiles)
                        if ((tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand], 0)) == tiles.ground)
                        && (oTilemap.GetTile(new Vector3Int(tileListX[rand], tileListY[rand], 0)) != tiles.obstacles)) {

                            if (tileListX[rand] <= player.transform.position.x + spawnRadiusX 
                            && tileListX[rand] >= player.transform.position.x - spawnRadiusX) {
                                rand = GetRandomTile();
                            } else if (tileListY[rand] <= player.transform.position.y + spawnRadiusY 
                            && tileListY[rand] >= player.transform.position.y - spawnRadiusY) {
                                rand = GetRandomTile();
                            } else {

                                if (rareEnemies[r].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(rareEnemies[r], new Vector2(tileListX[rand] * 0.16f, tileListY[rand] * 0.16f), Quaternion.identity, this);   
                                    enemyTotal++;
                                    break;
                                }
                            }

                        } else {
                            
                            // Generates random number to pick Enemy spawnpoint
                            rand = GetRandomTile();
                        }
                    }
                }
            }
        }

        if (!IsArrayEmpty(stationEnemies) && !bossLevel) {
            // For every common enemy in the level (e.g. Wispling, Slime, Joseph)
            for (int st = 0; st < stationEnemies.Length; st++) {

                // Generate random amount of common enemies in level (e.g. 4 Wisplings, 5 Slimes, 1 Joseph)
                int stationRange = UnityEngine.Random.Range(2, 4);

                // For the amount of every different type of common enemy (e.g. for 4 Wisplings, for 5 Slimes, for 1 Joseph)
                for (int s = 0; s < stationRange; s++) {
                    
                    // Generates random number to pick Enemy spawnpoint
                    int rand = GetRandomTile();

                    // For as many floor tiles as there are in the tilemap:
                    for (int i = 0; i < tileListX.Count; i++) {

                        // If suitable floor tiles have been found (Floor tiles and no ground tiles and no obstacles on those tiles)
                        if ((tilemap.GetTile(new Vector3Int(tileListX[i], tileListY[rand], 0)) == tiles.floor)
                        && (tilemap.GetSprite(new Vector3Int(tileListX[i], tileListY[rand], 0)) != tiles.ground)
                        && (oTilemap.GetTile(new Vector3Int(tileListX[i], tileListY[rand], 0)) != tiles.obstacles)) {

                            Quaternion rot = Quaternion.Euler(0, 0, 0);

                            // If tile found is one of four different wall tiles then instaniate with correct rotation
                            // UP WALL
                            if (tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand] + 1, 0)) == tiles.wallUp) {
                                rot = Quaternion.Euler(0, 0, -90);

                                // Spawns Enemy
                                if (stationEnemies[st].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(stationEnemies[st], new Vector2(tileListX[rand] * 0.16f + 0.08f, tileListY[rand] * 0.16f + 0.27f), rot, this);   
                                    enemyTotal++;
                                    break;
                                }
                            } 
                            // DOWN WALL
                            else if (tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand] - 1, 0)) == tiles.wallDown) {
                                rot = Quaternion.Euler(0, 0, 90);

                                // Spawns Enemy
                                if (stationEnemies[st].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(stationEnemies[st], new Vector2(tileListX[rand] * 0.16f + 0.08f, tileListY[rand] * 0.16f - 0.12f), rot, this);   
                                    enemyTotal++;
                                    break;
                                }
                            } 
                            // LEFT WALL
                            else if (tilemap.GetSprite(new Vector3Int(tileListX[rand] - 1, tileListY[rand], 0)) == tiles.wallLeft) {
                                rot = Quaternion.Euler(0, 0, 0);

                                // Spawns Enemy
                                if (stationEnemies[st].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(stationEnemies[st], new Vector2(tileListX[rand] * 0.16f - 0.12f, tileListY[rand] * 0.16f + 0.08f), rot, this);   
                                    enemyTotal++;
                                    break;
                                }
                            } 
                            // RIGHT WALL
                            else if (tilemap.GetSprite(new Vector3Int(tileListX[rand] + 1, tileListY[rand] + 1, 0)) == tiles.wallRight) {
                                rot = Quaternion.Euler(0, 0, 180);

                                // Spawns Enemy
                                if (stationEnemies[st].TryGetComponent<Enemy>(out var enemy)) {
                                    enemy.Create(stationEnemies[st], new Vector2(tileListX[rand] * 0.16f + 0.27f, tileListY[rand] * 0.16f + 0.24f), rot, this);   
                                    enemyTotal++;
                                    break;
                                }
                            }

                        } else {
                            
                            // Generates random number to pick Enemy spawnpoint
                            rand = GetRandomTile();
                        }
                    }
                }
            }
        }

        if (!IsArrayEmpty(minibosses) && !bossLevel) {

            // For every miniboss in the level (e.g. Scout, Chris)
            for (int m = 0; m < minibosses.Length; m++) {
                
                // Generate random amount of minibosses in level (e.g. 1 Scout, 1 Chris)
                int minibossesRange = 1;
                
                // For the amount of every different type of miniboss (e.g. for 1 Scout, for 1 Chris)
                for (int s = 0; s < minibossesRange; s++) {
                    
                    // Generates random number to pick Enemy spawnpoint
                    int rand = GetRandomTile();

                    // For as many floor tiles as there are in the tilemap:
                    for (int i = 0; i < tileListX.Count; i++) {

                        // If suitable floor tiles have been found (Ground tiles and no obstacles on those tiles)
                        if ((tilemap.GetSprite(new Vector3Int(tileListX[rand], tileListY[rand], 0)) == tiles.ground)
                        && (oTilemap.GetTile(new Vector3Int(tileListX[rand], tileListY[rand], 0)) != tiles.obstacles)) {

                            // Spawns Enemy
                            if (minibosses[m].TryGetComponent<Enemy>(out var enemy)) {
                                enemy.Create(minibosses[m], new Vector2(tileListX[rand] * 0.16f, tileListY[rand] * 0.16f), Quaternion.identity, this);   
                                enemyTotal++;
                                break;
                            }

                        } else {
                            
                            // Generates random number to pick Enemy spawnpoint
                            rand = GetRandomTile();
                        }
                    }
                }
            }
        }
        
    }

    private bool IsArrayEmpty(GameObject[] array) {
        return Array.TrueForAll(array, x => x == null);
    }

    public int GetRandomTile() {
        int rand = UnityEngine.Random.Range(0, tileListY.Count);
        return rand;
    }

    // GENERATE PATHFINDING MAP
    private void PathScan() {
        astarPath = FindFirstObjectByType<AstarPath>();

        AstarData aData = AstarPath.active.data;
        GridGraph gg = aData.gridGraph;

        // Gets the distance from 0,0 on the x axis (half of the total tilemap size)
        float radiusX = (0.16f * (mapWidth - 1f)) / 2f;
        // Gets the distance from 0,0 on the y axis (half of the total tilemap size)
        float radiusY = (0.16f * (mapHeight - 1f)) / 2f;

        gg.SetDimensions((mapWidth - 1) * 4, (mapHeight - 1) * 4, 0.04f);
        gg.center = new Vector3(radiusX, radiusY, 0);

        AstarPath.active.Scan();
        //Debug.Log("Scanned!");
    }

    public void SaveMap () {
        SaveSystem.SaveMap(this);
    }

    public void LoadMap () {

        // Load save data
        MapData data = SaveSystem.LoadMap();

        GameStateManager.SetLevel(data.levelNum);
        GameStateManager.SetStage(data.stageNum);

        // Clear tile locations
        tileListX.Clear();
        tileListY.Clear();
        tileCount = 0;

        int listNum = 0;

        // For every tile slot in our grid
        for (int x = 0; x < gridHandler.GetLength(0); x++) {

            for (int y = 0; y < gridHandler.GetLength(1); y++) {

                // Add tile x and y locations
                tileListX.Add(x);
                tileListY.Add(y);

                // If save data indicates 1, then set current tile position / type to a FLOOR tile
                if (data.tileTypes[listNum] == 1) {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles.floor);
                    gridHandler[x, y] = WalkerGenerator.Grid.FLOOR;
                    tileCount++;
                }
                // If save data indicates 2, then set current tile position / type to an EMPTY tile
                else if (data.tileTypes[listNum] == 2) {

                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles.empty);
                    gridHandler[x, y] = WalkerGenerator.Grid.EMPTY;
                    tileCount++;
                }
                // If save data indicates 3, then set current tile position / type to a FLOOR tile
                else if (data.tileTypes[listNum] == 3) {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles.empty);
                    gridHandler[x, y] = WalkerGenerator.Grid.EMPTY;
                    tileCount++;
                }

                // If save data indicates 1 in obstacle tile lists, then set current tile position / type to an OBSTACLE tile
                if (data.oTileTypes[listNum] == 1) {
                    oTilemap.SetTile(new Vector3Int(x, y, 0), tiles.obstacles);
                    gridHandler[x, y] = WalkerGenerator.Grid.OBSTACLES;
                    oTileCount++;
                }

                // Increment tile number
                listNum++;
                tileCount++;
            }
        }

        // Set grid origin to empty
        tilemap.SetTile(new Vector3Int(0, 0, 0), tiles.empty);
        tileCount--;
        gridHandler[0, 0] = Grid.EMPTY;

        // Create decoration around loaded map
        StartCoroutine(CreateDecor());
    }

}
