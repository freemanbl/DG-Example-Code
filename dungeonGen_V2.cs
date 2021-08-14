using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class dungeonGen_V2 : MonoBehaviour
{
    public static dungeonGen_V2 instance;
    public GameObject headerUI;
    public FPScharacterCtrl characterCtrl;
    public bool levelLoading, infiniteMode = false, drawGrid, drawRoomPlacement;
    public int cellSize, cellPadding;
    public Dungeon dungeonProperties;
    public int floorNum;
    [HideInInspector]
    public int step = 0;
    public NavMeshSurface navSurface;
    private Cell[,] cells;
    private Cell startCell, endCell;
    private bool cellsCreated;

    public dungeonType currentDungeonType; //library of room, arena, wall and enemy types
    [HideInInspector] public dungeonRoom startRoom, endRoom;
    #region storage
    [HideInInspector]public List<dungeonRoom> rooms = new List<dungeonRoom>();
    [HideInInspector]public List<dungeonRoom> activeRooms = new List<dungeonRoom>();
    [HideInInspector]public List<dungeonRoom> arenaPlacements = new List<dungeonRoom>();
    [HideInInspector]public List<dungeonRoom> activeArenas = new List<dungeonRoom>(); //0 is connected to start cell, 1 is connected to end cell
    [HideInInspector]public List<Cell> cellPath = new List<Cell>();
    [HideInInspector]public List<GameObject> activeWalls;
    [HideInInspector]public enemySpawnPoint[] spawnPoints;
    [HideInInspector]public List<enemySpawnPoint> activeSpawnPoints;
    [HideInInspector]public List<GameObject> enemiesAlive;
    #endregion

    public void Awake()
    {
        instance = this;
        getDungeonPrefabs();
    }

    public void getDungeonPrefabs()
    {
        switch (dungeonProperties.Theme)
        {
            case DungeonThemes.test:
                currentDungeonType = GetComponent<dRoomLister>().test;
                break;
            case DungeonThemes.cave:
                currentDungeonType = GetComponent<dRoomLister>().cave;
                break;
        }
    }
    public void Start()
    {
        generateNew();
    }
    public void generateNew()
    {
        step = 0;
        generate(dungeonProperties);
    }
    public void OnValidate()
    {
        getDungeonPrefabs();
    }
    public void generate(Dungeon d)
    {
        switch (step)
        {
            case 0: //cells/grid gen + pick start/end
                clear();
                levelLoading = true;
                Cell[,] n_cells = new Cell[d.sizeX + cellPadding, d.sizeY + cellPadding];
                for (int y = 0; y < d.sizeY + cellPadding; y++)
                {
                    for (int x = 0; x < d.sizeX + cellPadding; x++)
                    {
                        n_cells[x, y] = new Cell();
                        n_cells[x, y].cellPos = new Vector3Int(x * cellSize, 0, y * cellSize);
                    }
                }
                cells = n_cells;
                cellsCreated = true;
                startCell = n_cells[Mathf.RoundToInt(dungeonProperties.sizeX / 2) + Random.Range(-5, 5), cellPadding + 1];
                endCell = n_cells[Mathf.RoundToInt(dungeonProperties.sizeX / 2) + Random.Range(-5, 5), dungeonProperties.sizeY - (cellPadding + 1)];
                step++;
                generate(d);
                break;
            case 1: //place arenas
                for (int i = 0; i < Random.Range(dungeonProperties.minArenas, dungeonProperties.maxArenas + 1); i++)
                {
                    Vector2Int cellIndex = new Vector2Int(Random.Range(cellPadding + 1, dungeonProperties.sizeX - cellPadding * 2), Random.Range(cellPadding + 1, dungeonProperties.sizeY - cellPadding * 2));
                    dungeonRoom arenaRoom = currentDungeonType.arenas[Random.Range(0, currentDungeonType.arenas.Count)];
                    dungeonRoom n_arenaRoom = new dungeonRoom();
                    //copyRoomData(n_arenaRoom, arenaRoom, cellIndex.x, cellIndex.y); //copy room info to protect original
                    n_arenaRoom.tag = arenaRoom.tag; n_arenaRoom.roomSize = arenaRoom.roomSize; n_arenaRoom.roomPrefab = arenaRoom.roomPrefab; n_arenaRoom.entryPoints = new List<Vector3Int>(arenaRoom.entryPoints); n_arenaRoom.type = arenaRoom.type;
                    while (checkArenaPlacement(n_arenaRoom, cellIndex.x, cellIndex.y) == false)
                    {
                        //find new placement
                        cellIndex = new Vector2Int(Random.Range(cellPadding + 1, dungeonProperties.sizeX - (cellPadding * 2)), Random.Range(cellPadding + 1, dungeonProperties.sizeY - (cellPadding * 2)));
                    }
                    Cell originCell = cells[cellIndex.x, cellIndex.y];
                    //overwrite roompos with new pos
                    n_arenaRoom.roomCellCoord = new Vector2Int(cellIndex.x, cellIndex.y);
                    n_arenaRoom.roomPos = new Vector3Int(Mathf.RoundToInt(originCell.cellPos.x), 0, Mathf.RoundToInt(originCell.cellPos.z));
                    occupyRoomPlacement(n_arenaRoom);
                    arenaPlacements.Add(n_arenaRoom);
                }
                step++;
                generate(d);
                break;
            case 2: //fill remaining space with connector 'rooms'
                for (int y = 0; y < d.sizeY + cellPadding; y++)
                {
                    for (int x = 0; x < d.sizeX + cellPadding; x++)
                    {
                        if (cells[x, y].occupied == false && checkIfCellPadding(x, y) == false)
                        {
                            dungeonRoom chosenRoom = new dungeonRoom();
                            dungeonRoom roomResource = chooseRoom(x, y);
                            copyRoomData(chosenRoom, roomResource, x, y);
                            occupyRoomPlacement(chosenRoom);
                            chosenRoom.gizmoColor = new Color(Random.Range(0f, 1), Random.Range(0f, 1), Random.Range(0f, 1), 0.3f);
                            rooms.Add(chosenRoom);
                        }
                    }
                }
                step++;
                generate(d);//eez nuts
                break;
            case 3://path find across dungeon
                //make start and end rooms active
                foreach (dungeonRoom room in rooms)
                {
                    if (room == startCell.room)
                    {
                        startRoom = room;
                        room.active = true;
                        room.gizmoColor = Color.white;
                    }
                    else if (room == endCell.room)
                    {
                        endRoom = room;
                        room.active = true;
                        room.gizmoColor = Color.white;
                    }
                }
                //Find arena closest to start
                makeCellPath(endCell, findClosestArenaEntranceToCell(endCell), true);
                makeCellPath(startCell, findClosestArenaEntranceToCell(startCell), true);
                for (int i = 1; i < arenaPlacements.Count - 2; i++)
                {
                    Cell arenaExitCell = getRandomArenaEntryCell(activeArenas[i]);
                    makeCellPath(arenaExitCell, findClosestArenaEntranceToCell(arenaExitCell), true);
                }
                Cell lastArenaExitCell = getRandomArenaEntryCell(activeArenas[0]);
                Cell pathToExitArenaCell = getRandomArenaEntryCell(activeArenas[activeArenas.Count - 1]);
                makeCellPath(lastArenaExitCell, pathToExitArenaCell, true);

                step++;
                generate(d);
                break;
            case 4:
                foreach (dungeonRoom room in rooms)
                {
                    if (room.active == true)
                    {
                        //Debug.Log("creating " + room.tag);
                        GameObject nRoomObj = Instantiate(room.roomPrefab, room.roomPos, Quaternion.identity) as GameObject;
                        dungeonRoom nRoom = room;
                        nRoom.objInWorld = nRoomObj;
                        activeRooms.Add(nRoom);
                    }
                }
                foreach (dungeonRoom arena in arenaPlacements)
                {
                    if (arena.active == true && arena.roomPrefab != null)
                    {
                        GameObject nRoomObj = Instantiate(arena.roomPrefab, arena.roomPos, Quaternion.identity) as GameObject;
                        dungeonRoom nRoom = arena;
                        nRoom.objInWorld = nRoomObj;
                    }
                }
                step++;
                generate(d);
                break;
            case 5: //place walls
                foreach (dungeonRoom rm in activeRooms)
                {
                    //Debug.Log("checking " + rm.tag);
                    //check right side
                    if (rm.type != roomType.arena) //arenas come with walls
                    {
                        for (int i = 0; i < rm.roomSize.y; i++) //size.y because we're checking up the right side
                        {
                            Vector2 checkPos = new Vector2(rm.roomCellCoord.x + rm.roomSize.x, rm.roomCellCoord.y + i);
                            //Cell checkCell = cells[rm.roomCellCoord.x + Mathf.RoundToInt(rm.roomSize.x), rm.roomCellCoord.y + i];
                            if (checkIfPlaceWallAtCellPos(checkPos, cells[rm.roomCellCoord.x + Mathf.RoundToInt(rm.roomSize.x - 1), rm.roomCellCoord.y + i]) == true)
                            {
                                GameObject nWall = Instantiate(currentDungeonType.walls[Random.Range(0, currentDungeonType.walls.Count - 1)], new Vector3(checkPos.x * cellSize - (cellSize / 2) - .5f, 0, checkPos.y * cellSize), Quaternion.Euler(0, 90, 0)) as GameObject;
                                activeWalls.Add(nWall);
                            }
                        }
                        //check down side
                        for (int i = 0; i < rm.roomSize.x; i++)
                        {
                            Vector2 checkPos = new Vector2(rm.roomCellCoord.x + i, rm.roomCellCoord.y - 1);
                            if (checkIfPlaceWallAtCellPos(checkPos, cells[rm.roomCellCoord.x + i, rm.roomCellCoord.y]) == true) //no room, border of the map
                            {
                                GameObject nWall = Instantiate(currentDungeonType.walls[Random.Range(0, currentDungeonType.walls.Count)], new Vector3(checkPos.x * cellSize, 0, checkPos.y * cellSize + (cellSize / 2) + .5f), Quaternion.Euler(0, 180, 0)) as GameObject;
                                activeWalls.Add(nWall);
                            }
                        }
                        //check left side
                        for (int i = 0; i < rm.roomSize.y; i++)
                        {
                            Vector2 checkPos = new Vector2(rm.roomCellCoord.x - 1, rm.roomCellCoord.y + i);
                            if (checkIfPlaceWallAtCellPos(checkPos, cells[rm.roomCellCoord.x, rm.roomCellCoord.y + i]) == true) //no room, border of the map
                            {
                                GameObject nWall = Instantiate(currentDungeonType.walls[Random.Range(0, currentDungeonType.walls.Count)], new Vector3(checkPos.x * cellSize + (cellSize / 2) + .5f, 0, checkPos.y * cellSize), Quaternion.Euler(0, -90, 0)) as GameObject;
                                activeWalls.Add(nWall);
                            }
                        }
                        //check upper side
                        for (int i = 0; i < rm.roomSize.x; i++)
                        {
                            Vector2 checkPos = new Vector2(rm.roomCellCoord.x + i, rm.roomCellCoord.y + rm.roomSize.y);
                            if (checkIfPlaceWallAtCellPos(checkPos, cells[rm.roomCellCoord.x + i, rm.roomCellCoord.y + Mathf.RoundToInt(rm.roomSize.y - 1)]) == true) //no room, border of the map
                            {
                                GameObject nWall = Instantiate(currentDungeonType.walls[Random.Range(0, currentDungeonType.walls.Count)], new Vector3(checkPos.x * cellSize, 0, checkPos.y * cellSize - (cellSize / 2) - .5f), Quaternion.Euler(0, 0, 0)) as GameObject;
                                activeWalls.Add(nWall);
                            }
                        }
                    }

                }
                changeArenaEntranceObjects();
                navSurface.BuildNavMesh();
                step++;
                generate(d);
                break;
            case 6://enemy and portal placement
                spawnPoints = FindObjectsOfType<enemySpawnPoint>();
                int howManySquads = Mathf.RoundToInt(activeRooms.Count / 4);
                Vector3 startPoint = new Vector3(startRoom.roomPos.x, startRoom.roomPos.y + 1, startRoom.roomPos.z); //player starting position
                for (int i = 0; i < howManySquads; i++)
                {
                    enemySpawnPoint cSP = spawnPoints[Random.Range(0, spawnPoints.Length - 1)]; //chosen spawnpoint
                    while(Vector3.Distance(startPoint, cSP.transform.position) < 20f) //make sure it's not right next to the player's start point to avoid immediate death
                    {
                        cSP = spawnPoints[Random.Range(0, spawnPoints.Length - 1)];
                    }
                    //Debug.Log("cSp is " + cSP);
                    if (i == 0) //if its the first one just skip
                    {
                        activeSpawnPoints.Add(cSP);
                        Debug.Log("added first sp");
                        continue;
                    }
                    //Debug.Log(i + " sp");
                    bool duplicate = false;
                    foreach (enemySpawnPoint esp in activeSpawnPoints)
                    {
                        if (cSP == esp)
                        {
                            //Debug.Log("csp is a duplicate");
                            duplicate = true;
                        }
                    } //if its a duplicate, start the loop to find a new one, itll keep going until it finds an unused one
                    if (duplicate == false)
                    {
                        activeSpawnPoints.Add(cSP);
                    }
                }
                foreach (enemySpawnPoint esp in activeSpawnPoints)
                {
                    placeEnemy(esp);
                }
                placeEndPortal();
                floorNum++;
                //place player
                releasePlayer();
                StartCoroutine(showHeaderUI("CAVES - " + floorNum, 2f));
                levelLoading = false;
                break;
        }
    }
    public void clear()
    {
        if (Application.isPlaying)
        {
            foreach (dungeonRoom obj in activeRooms) { Destroy(obj.objInWorld); }
            foreach (GameObject obj in activeWalls) { Destroy(obj); }
            foreach (GameObject obj in enemiesAlive) { Destroy(obj); }
        }
        else if (Application.isEditor)
        {
            foreach (dungeonRoom obj in activeRooms) { DestroyImmediate(obj.objInWorld); }
            foreach (GameObject obj in activeWalls) { DestroyImmediate(obj); }
            foreach (GameObject obj in enemiesAlive) { DestroyImmediate(obj); }
        }
        foreach(GameObject lootDrop in GameObject.FindGameObjectsWithTag("LootDrop"))
        {
            Destroy(lootDrop);
        }
        cellsCreated = false;
        arenaPlacements.Clear();
        rooms.Clear();
        activeRooms.Clear();
        activeWalls.Clear();
        activeArenas.Clear();
        activeSpawnPoints.Clear();
        enemiesAlive.Clear();
    }
    public void copyRoomData(dungeonRoom newRoom, dungeonRoom original, int wherex, int wherey)
    {
        newRoom.tag = original.tag;
        newRoom.roomPrefab = original.roomPrefab;
        newRoom.roomSize = original.roomSize;
        newRoom.roomPos = original.roomPos;
        newRoom.roomPos = new Vector3Int(wherex * cellSize, 0, wherey * cellSize);
        newRoom.roomCellCoord = new Vector2Int(wherex, wherey);
        newRoom.type = original.type;
        if(newRoom.type == roomType.arena)
        {
            newRoom.entryPoints = original.entryPoints;
        }
    }
    public bool checkArenaPlacement(dungeonRoom arena, int originX, int originY)
    {
        int count = 0;
        for (int y = -2; y < arena.roomSize.y + 2; y++) //start at -1 and end +1 to check the perimeter too
        {
            for (int x = -2; x < arena.roomSize.x + 2; x++) //loop thru cells that this room would cover
            {
                if (cells[originX + x, originY + y].occupied == true || cells[originX + x, originY + y] == startCell || cells[originX + x, originY + y] == endCell) //check these cells in cells array if occupied or start/end
                {
                    count++; //cell is already occupied, will return false
                }

            }
        }
        if (count == 0)
        {
            return true;
        } else
        {
            return false;
        }
    }
    public void occupyRoomPlacement(dungeonRoom room)
    {
        for (int ry = 0; ry < room.roomSize.y; ry++)
        {
            for (int rx = 0; rx < room.roomSize.x; rx++)
            {
                cells[room.roomCellCoord.x + rx, room.roomCellCoord.y + ry].occupied = true; //occupy the cells that this room covers
                cells[room.roomCellCoord.x + rx, room.roomCellCoord.y + ry].room = room;
                if (cells[room.roomCellCoord.x + rx, room.roomCellCoord.y + ry] == startCell)
                {
                    startCell.room = room;
                }
                else if (cells[room.roomCellCoord.x + rx, room.roomCellCoord.y + ry] == endCell)
                {
                    endCell.room = room;
                }
                //Debug.Log("occupied cell " + (x + rx) + ", " + (y + ry)); 
            }
        }
    }
    public dungeonRoom chooseRoom(int cellx, int celly)
    {
        dungeonRoom roomOutput = new dungeonRoom();
        bool roomIsClear = false;
        while (roomIsClear == false)
        {
            roomOutput = currentDungeonType.rooms[Random.Range(0, currentDungeonType.rooms.Count)];
            int count = 0;
            for (int y = 0; y < roomOutput.roomSize.y; y++)
            {
                for (int x = 0; x < roomOutput.roomSize.x; x++) //loop thru cells that this room would cover
                {
                    if (cells[cellx + x, celly + y].occupied == true) //check these cells in cells array
                    {
                        count++; //cell is already occupied, restart loop
                    }
                }
            }
            if (count == 0)
            {
                roomIsClear = true;
            }
        }

        return roomOutput;
    }
    public Cell getEndCell(Cell start, int minDistance)
    {
        Cell n_endCell = cells[Random.Range(cellPadding + 1, dungeonProperties.sizeX - cellPadding - 1), Random.Range(cellPadding + 1, dungeonProperties.sizeY - cellPadding - 1)];
        while (Vector3.Distance(n_endCell.cellPos, start.cellPos) < (minDistance * cellSize))
        {
            //get a new one
            n_endCell = cells[Random.Range(cellPadding, dungeonProperties.sizeX - cellPadding), Random.Range(cellPadding, dungeonProperties.sizeY - cellPadding)];
        }
        return n_endCell;
    }
    private bool checkIfCellPadding(int x, int y)
    {
        if (x >= dungeonProperties.sizeX - cellPadding || x <= cellPadding - 1 || y >= dungeonProperties.sizeY - cellPadding || y <= cellPadding - 1)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public bool checkIfPlaceWallAtCellPos(Vector2 where, Cell from) //origin should be in world space units. e.g. cell[3,3]'s origin is 15,15 with a cellSize of 5
    {
        int fails = 0; // +1 if CANNOT PLACE wall in condition. Place walls where cell is arena so they can only be entered via entrance.
        Cell checkingCell = cells[Mathf.RoundToInt(where.x), Mathf.RoundToInt(where.y)];
        if(checkIfCellPadding(Mathf.RoundToInt(where.x), Mathf.RoundToInt(where.y)) == false)
        {
            if (checkingCell.room.active == true && checkingCell.room.type != roomType.arena) //room is there, but it's not an arena. should be open space.
            {
                fails++;
            }
            if (checkingCell.room.active == true && checkingCell.room.type == roomType.arena && from.arenaEntryPoint == true) //room is there, is an arena, but this is an entry point
            {
                fails++;
            }
        }
        
        if (fails > 0)
        {
            return false;
        }
        return true;
    }
    public void makeCellPath(Cell start, Cell end, bool isMainPath)
    {
        Cell curCell = start;
        List<Cell> cellsOnPath = new List<Cell>();
        while (curCell != end && curCell.occupied == true
            && checkIfCellPadding(Mathf.RoundToInt(curCell.cellPos.x / cellSize), Mathf.RoundToInt(curCell.cellPos.z / cellSize)) == false)
        {
            int dir = getNextCellDir(curCell, end);
            
            curCell.room.active = true;
            cellsOnPath.Add(curCell);
            curCell.room.gizmoColor = Color.white;
            Cell n_Cell = new Cell();
            if (dir == 1) //go up
            {
                //Debug.Log("went up");
                n_Cell = cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize), Mathf.RoundToInt(curCell.cellPos.z / cellSize) + 1];
            }
            else if (dir == 2) //go right
            {
                //Debug.Log("went right");
                n_Cell = cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize) + 1, Mathf.RoundToInt(curCell.cellPos.z / cellSize)];
            }
            else if (dir == 3) //go down
            {
                // Debug.Log("went down");
                n_Cell = cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize), Mathf.RoundToInt(curCell.cellPos.z / cellSize) - 1];
            }
            else if (dir == 4) //go left
            {
                //Debug.Log("went left");
                n_Cell = cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize) - 1, Mathf.RoundToInt(curCell.cellPos.z / cellSize)];
            }
            /*if(n_Cell.room.type != roomType.arena) //this is causing crashes
            {
                curCell = n_Cell;
            }*/
            if(n_Cell.room.type == roomType.arena)
            {
                //Debug.Log("tried to path into arena");
                Cell[] allCellDirs = { cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize), Mathf.RoundToInt(curCell.cellPos.z / cellSize) + 1], cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize) + 1, Mathf.RoundToInt(curCell.cellPos.z / cellSize)], 
                       cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize), Mathf.RoundToInt(curCell.cellPos.z / cellSize) - 1], cells[Mathf.RoundToInt(curCell.cellPos.x / cellSize) - 1, Mathf.RoundToInt(curCell.cellPos.z / cellSize)]};
                List<Cell> goodCells = new List<Cell>();
                foreach (Cell cell in allCellDirs)
                {
                    if(cell.occupied)
                    {
                        if (cell.room.type != roomType.arena)
                        {
                            goodCells.Add(cell);
                        }
                    }
                    
                }
                curCell = goodCells[Random.Range(0, goodCells.Count)];
            } else
            {
                curCell = n_Cell;
            }
            
        }
        //if (isMainPath == true) { cellPath = cellsOnPath; }

    }
    public int getNextCellDir(Cell curCell, Cell end)
    {
        Vector3 dir = (end.cellPos - curCell.cellPos).normalized;
        //Debug.Log(dir);
        int dirToGo = 0; //null
        List<int> possibleDirs = new List<int>();
        if (dir.x == 0) //end is above or below
        {
            //dir = new Vector3(dir.x, dir.y, Mathf.RoundToInt(dir.y));
            //Debug.Log("end is directly above or below");
            if (dir.z > 0)
            {
                dirToGo = 1; //go up
            }
            else if (dir.z < 0)
            {
                dirToGo = 3; //go down
            }
            //dirToGo = dir.y > 0 ? 3 : 1; //end will either be up or down
        }
        else if (dir.x > 0)//end is to the right
        {
            possibleDirs.Add(2);
            if (dir.z > 0)
            {
                possibleDirs.Add(1);
            }
            else if (dir.z < 0)
            {
                possibleDirs.Add(3);
            }
            dirToGo = possibleDirs[Random.Range(0, possibleDirs.Count)];
        }
        else if (dir.x < 0) //end is to the left
        {
            possibleDirs.Add(4);
            if (dir.z > 0)
            {
                possibleDirs.Add(1);
            }
            else if (dir.z < 0)
            {
                possibleDirs.Add(3);
            }
            dirToGo = possibleDirs[Random.Range(0, possibleDirs.Count)];
        }
        
        return dirToGo;
    }
    public Cell findClosestArenaEntranceToCell(Cell cell)
    {
        Cell output = new Cell();
        cell.room.active = true;
        dungeonRoom closestArena = arenaPlacements[0];
        float closestArenaDist = Mathf.Infinity;
        foreach(dungeonRoom arena in arenaPlacements)
        {
            float dist = Vector3.Distance(arena.roomPos, cell.cellPos);
            if (dist < closestArenaDist && arena.active == false)
            {
                closestArena = arena;
                closestArenaDist = dist;
            }
        }
        closestArena.active = true;
        activeRooms.Add(closestArena);
        activeArenas.Add(closestArena);
        //Debug.Log("arena " + arenaPlacements[closestIndex] + " Index " + closestIndex);
        int closestEntrance = int.MaxValue;
        int closeEntryIndex = 2;
        for (int i = 0; i < closestArena.entryPoints.Count; i++)
        {
            Vector3 entryPos = findArenaEntranceCellPos(closestArena, i);
            int thisDist = Mathf.RoundToInt(Vector3.Distance(entryPos, cell.cellPos));
            //Debug.Log(i + " @ pos " + entryPos + " dist " + thisDist);
            //closestArena.entryPoints[i] = new Vector3Int(closestArena.entryPoints[i].x, closestArena.entryPoints[i].y, 0);
            if(thisDist < closestEntrance)
            {
                //print(thisDist);
                closestEntrance = thisDist;
                closeEntryIndex = i;
            }
        }
        closestArena.entryPoints[closeEntryIndex] = new Vector3Int(closestArena.entryPoints[closeEntryIndex].x, closestArena.entryPoints[closeEntryIndex].y, 1); //set entrance to ACTIVE (z = 1)
        //Debug.Log("set arena " + closestArena.tag + " entry " + closeEntryIndex + " to open");
        output = cells[closestArena.roomCellCoord.x + closestArena.entryPoints[closeEntryIndex].x, closestArena.roomCellCoord.y + closestArena.entryPoints[closeEntryIndex].y];
        //Debug.Log(" arena " + closestArena.tag + " entry " + closeEntryIndex);
        output.room.active = true;
        output.arenaEntryPoint = true;
        return output;
    }
    public Vector3 findArenaEntranceCellPos(dungeonRoom arena, int entranceIndex) {
        
        return new Vector3(arena.roomPos.x + (arena.entryPoints[entranceIndex].x * cellSize), 0, arena.roomPos.z + (arena.entryPoints[entranceIndex].y * cellSize));
    }
    public Cell getRandomArenaEntryCell(dungeonRoom arena)
    {
        Cell output = new Cell();
        int i = Random.Range(0, arena.entryPoints.Count - 1); //choose which entrypoint
        //change if open or closed
        arena.entryPoints[i] = new Vector3Int(arena.entryPoints[i].x, arena.entryPoints[i].y, 1);
        //get cell index from world position
        Vector3 v3Pos = findArenaEntranceCellPos(arena, i);
        output = cells[Mathf.RoundToInt(v3Pos.x / cellSize), Mathf.RoundToInt(v3Pos.z / cellSize)];
        output.arenaEntryPoint = true;
        output.room.active = true;
        return output;
    }
    public void placeEnemy(enemySpawnPoint r)
    {
        GameObject cEnemyType = currentDungeonType.enemySpawns[Random.Range(0, currentDungeonType.enemySpawns.Count)];
        GameObject newEnemy = Instantiate(cEnemyType, new Vector3(r.transform.position.x, 1, r.transform.position.z), Quaternion.identity);
        enemiesAlive.Add(newEnemy);
    }
    public void placeEndPortal()
    {
        Vector3 placePos = endRoom.objInWorld.GetComponentInChildren<portalSpawnPoint>().gameObject.transform.position; //jesus fucking christ
        GameObject portal = Instantiate(Resources.Load("prefabs/PortalTest"), placePos, Quaternion.identity) as GameObject;
        enemiesAlive.Add(portal);
        compass c = FindObjectOfType<compass>();
        c.portalPos = null; //make it look for this new portal
    }
    public void changeArenaEntranceObjects()
    {
        for (int i = 0; i < activeArenas.Count; i++)
        {
            dungeonRoom thisArena = activeArenas[i];
            for (int e = 0; e < thisArena.entryPoints.Count; e++)
            {
                if(thisArena.entryPoints[e].z == 1)
                {
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].open = true;
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].openObject.SetActive(true);
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].closedObject.SetActive(false);
                } else
                {
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].open = false;
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].closedObject.SetActive(true);
                    thisArena.objInWorld.GetComponent<base_Arena_Behaviour>().entrances[e].openObject.SetActive(false);
                }
            }
        }
    }

    public IEnumerator showHeaderUI(string text, float time)
    {
        yield return new WaitForSeconds(1);
        headerUI.SetActive(true);
        headerUI.GetComponentInChildren<TextMeshProUGUI>().text = text;
        yield return new WaitForSeconds(time);
        headerUI.SetActive(false);
    }
    public void holdPlayerBetweenLevels()
    {
        //FPScharacterCtrl.canCtrl = false;
        Debug.Log("holdplayer");
        playerManager.instance.player.GetComponent<Rigidbody>().useGravity = false;
    }
    public void releasePlayer()
    {
        LockMouse.inMenu = false;
        FPScharacterCtrl.canCtrl = true;
        Debug.Log("control " + FPScharacterCtrl.canCtrl);
        playerManager.instance.player.GetComponent<Rigidbody>().useGravity = true;
        Vector3 startPoint = new Vector3(startRoom.roomPos.x, startRoom.roomPos.y + 1, startRoom.roomPos.z); //player starting position
        if (Application.isPlaying == true)
        {
            playerManager.instance.player.transform.position = startPoint;
        }
        LockMouse.inMenu = false;
        LockMouse.instance.locked();
    }
    private void OnDrawGizmos()
    {
        //draw cells
        if (cellsCreated == true && drawGrid)
        {
            for (int y = 0; y < dungeonProperties.sizeY - 1; y++)
            {
                for (int x = 0; x < dungeonProperties.sizeX - 1; x++)
                {
                    if (checkIfCellPadding(x, y) == true)
                    {
                        Gizmos.color = Color.gray;
                    }
                    else
                    {
                        Gizmos.color = Color.cyan;
                    }
                    if (cells[x, y] == startCell)
                    {
                        Gizmos.color = Color.yellow;
                    }
                    else if (cells[x, y] == endCell)
                    {
                        Gizmos.color = Color.red;
                    }
                    Gizmos.DrawWireCube(cells[x, y].cellPos, new Vector3(cellSize, 0, cellSize));
                }
            }
        }
        //Arenas
        if(arenaPlacements.Count > 0 && drawRoomPlacement == true)
        {
            foreach(dungeonRoom dr in arenaPlacements)
            {
                if (dr.active == true)
                {
                    Gizmos.color = Color.white;
                }
                else
                {
                    Gizmos.color = Color.black;
                }
                for (int y = 0; y < dr.roomSize.y; y++)
                {
                    for (int x = 0; x < dr.roomSize.x; x++)
                    {
                        Gizmos.DrawCube(new Vector3(dr.roomPos.x + (x * cellSize), 1, dr.roomPos.z + (y * cellSize)), new Vector3(cellSize, cellSize, cellSize));
                    }
                }
            }
        }
        //connecting rooms
        if (rooms.Count > 0 && drawRoomPlacement == true)
        {
            foreach (dungeonRoom dr in rooms)
            {
                if (dr.active == true)
                {
                    Gizmos.color = Color.blue;
                } else
                {
                    Gizmos.color = Color.red;
                }
                    for (int y = 0; y < dr.roomSize.y; y++)
                    {
                        for (int x = 0; x < dr.roomSize.x; x++)
                        {
                            Gizmos.DrawCube(new Vector3(dr.roomPos.x + (x * cellSize), 1, dr.roomPos.z + (y * cellSize)), new Vector3(cellSize, cellSize, cellSize));
                        }
                    }
                
            }
        }
        /*foreach(Cell ac in cells)
        {
            if(ac.arenaEntryPoint == true)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(new Vector3(ac.cellPos.x, ac.cellPos.y + 5, ac.cellPos.z), 1);
            }
            
        }*/
    }
}

//CUSTOM CLASSES TO REFERENCE
public enum DungeonThemes { test, cave, forest}
[System.Serializable]
public class Dungeon
{
    public int Level;
    public DungeonThemes Theme;
    public int sizeX;
    public int sizeY;
    public int minArenas, maxArenas;
    //store list of room prefabs, specific list will be chosen based on theme
    //should room list be a class that contains multiple arrays of rooms based on their dimensions?
}

[System.Serializable]
public class Cell
{
    public bool occupied = false;
    public bool arenaEntryPoint = false;
    public dungeonRoom room;
    public Vector3 cellPos;
}
public enum roomType { standard, arena, bonus}
[System.Serializable]
public class dungeonRoom
{
    public string tag;
    public Vector2 roomSize;
    public roomType type;
    [Header("Arenas Only")]
    public List<Vector3Int> entryPoints; //x,y for cell position within room context. z for direction of door
    [Header("Read only")]
    public Vector3Int roomPos;
    public Vector2Int roomCellCoord;
    public Color gizmoColor; //for testing
    public GameObject roomPrefab;
    public GameObject _RoomPrefab
    {
        get { return roomPrefab; }
        set
        {
            tag = roomPrefab.name;
            roomPrefab = value;
        }
    }
    public GameObject objInWorld;
    public bool active;
    //ALWAYS MAKE ROOM ORIGINATE IN MIDDLE OF BOTTOM LEFT CELL!
}

[System.Serializable]
public class dungeonType
{
    public List<dungeonRoom> rooms;
    public List<dungeonRoom> arenas;
    public List<GameObject> walls;
    public List<GameObject> enemySpawns;
}