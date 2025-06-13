using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    public List<Room> rooms;
    public Hallway vertical_hallway;
    public Hallway horizontal_hallway;
    public Room start;
    public Room target;

    public int MAX_SIZE = 5;
    public int THRESHOLD = 1000;
    public int BOUND = 10;

    private List<GameObject> generated_objects;
    private int iterations;
    private bool targetPlaced = false;

    public void Generate()
    {
        foreach (var go in generated_objects)
        {
            Destroy(go);
        }
        generated_objects.Clear();

        Vector2Int startPos = new Vector2Int(0, 0);
        generated_objects.Add(start.Place(startPos));

        List<Door> openDoors = new List<Door>(start.GetDoors(startPos));
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(start.GetGridCoordinates(startPos));
        iterations = 0;
        targetPlaced = false;

        bool success = GenerateWithBacktracking(occupied, openDoors, 1);
        if (!success)
        {
            Debug.LogWarning("Dungeon generation failed.");
        }
    }

    bool GenerateWithBacktracking(HashSet<Vector2Int> occupied, List<Door> openDoors, int depth)
    {
        if (iterations++ > THRESHOLD)
        {
            Debug.LogWarning("Iteration threshold hit at depth: " + depth);
            return false;
        }

        if (openDoors.Count == 0)
            return targetPlaced && depth >= MAX_SIZE;

        Shuffle(openDoors);

        foreach (Door door in openDoors.ToArray())
        {
            Door matching = door.GetMatching();
            Door.Direction neededDir = matching.GetDirection();

            // Try placing target near the end
            if (!targetPlaced && depth >= MAX_SIZE - 1 && target.HasDoorOnSide(neededDir))
            {
                Door targetDoor = target.GetDoors().Find(d => d.GetDirection() == neededDir);
                if (targetDoor == null) continue;

                Vector2Int placementCell = matching.GetGridCoordinates() - targetDoor.GetGridCoordinates();
                if (!IsWithinBounds(placementCell)) continue;

                List<Vector2Int> newCells = target.GetGridCoordinates(placementCell);
                if (newCells.Any(c => occupied.Contains(c))) continue;

                generated_objects.Add(target.Place(placementCell));
                PlaceHallway(door);
                targetPlaced = true;
                return true;
            }

            foreach (Room room in rooms.OrderBy(r => Random.Range(0, 100)))
            {
                if (!room.HasDoorOnSide(neededDir)) continue;

                Door roomDoor = room.GetDoors().Find(d => d.GetDirection() == neededDir);
                if (roomDoor == null) continue;

                Vector2Int placementCell = matching.GetGridCoordinates() - roomDoor.GetGridCoordinates();
                if (!IsWithinBounds(placementCell)) continue;

                List<Vector2Int> newCells = room.GetGridCoordinates(placementCell);
                if (newCells.Any(c => occupied.Contains(c))) continue;

                var newOccupied = new HashSet<Vector2Int>(occupied);
                foreach (var c in newCells) newOccupied.Add(c);

                List<Door> newDoors = room.GetDoors(placementCell);
                newDoors.RemoveAll(d => d.GetGridCoordinates() == matching.GetGridCoordinates() &&
                                        d.GetDirection() == neededDir);

                var newOpenDoors = new List<Door>(openDoors);
                newOpenDoors.Remove(door);
                newOpenDoors.AddRange(newDoors);

                // Early pruning to avoid dead ends at max depth
                if (depth + 1 == MAX_SIZE && newOpenDoors.Count > 1)
                    continue;

                if (GenerateWithBacktracking(newOccupied, newOpenDoors, depth + 1))
                {
                    generated_objects.Add(room.Place(placementCell));
                    PlaceHallway(door);
                    return true;
                }
            }
        }

        return false;
    }

    bool IsWithinBounds(Vector2Int pos)
    {
        return pos.x >= -BOUND && pos.x <= BOUND && pos.y >= -BOUND && pos.y <= BOUND;
    }

    void PlaceHallway(Door door)
    {
        if (door.IsVertical())
            generated_objects.Add(vertical_hallway.Place(door));
        else
            generated_objects.Add(horizontal_hallway.Place(door));
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    void Start()
    {
        generated_objects = new List<GameObject>();
        Generate();
    }

    void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
            Generate();
    }
}