using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class DungeonController : MonoBehaviour
{
    [Header("Environment Objects")]
    [SerializeField]
    private GameObject columns;

    [SerializeField]
    private GameObject cave;

    [SerializeField]
    private GameObject floor;

    [SerializeField]
    private GameObject door;
    private DoorController doorController;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject dragonPrefab;

    [SerializeField]
    private GameObject agentPrefab;

    [SerializeField]
    private GameObject keyPrefab;

    private GameObject agent;
    private GameObject dragon;
    private GameObject key;

    [Header("Timer")]
    public float timeToEscape = 30f;
    private Timer timer;

    void Start()
    {
        SpawnDragon();
        SpawnAgent();

        doorController = door.GetComponent<DoorController>();

        timer = GetComponent<Timer>();
        timer.onTimerEndEvent += FailEpisode;
    }

    public void ResetEnvironment()
    {
        // Destroy key if it has not been picked up
        if (key != null)
            Destroy(key);

        // Reposition agent
        agent.transform.position = GetRandomPosition();

        // Reset rotation to look in a random direction
        agent.transform.eulerAngles = new Vector3(0, Random.Range(0, 360), 0);

        // Respawn cave
        Vector3 dragonPosition = SpawnCaveAndDragon();

        // Respawn door
        SpawnDoor();

        // Heal and reposition dragon (need to warp the mesh agent)

        // TOGGLE - STEP 1: random position for dragon
        //dragon.GetComponent<DragonBehavior>().Resuscitate(GetRandomPosition());
        // STEP 3
        dragon.GetComponent<DragonBehavior>().Resuscitate(dragonPosition);

        // Lock door
        door.GetComponent<DoorController>().LockDoor();

        // Stop running timer from previous episode
        timer.StopTimer();
    }

    private void SpawnAgent()
    {
        GameObject agent = Instantiate(agentPrefab, transform);

        agent.GetComponent<AgentBehavior>().SetDungeonController(this);

        this.agent = agent;
    }

    private void SpawnDragon()
    {
        // Instantiate the dragon
        GameObject dragon = Instantiate(dragonPrefab, transform);

        this.dragon = dragon;

        DragonBehavior db = dragon.GetComponent<DragonBehavior>();

        db.SetCave(cave);

        db.onDragonEscapeEvent += FailEpisode;
        db.onDragonSlainEvent += DragonWasKilled;
    }

    private void DragonWasKilled()
    {
        // Start timer
        timer.StartTimer(timeToEscape);

        // Spawn key
        SpawnKey();
    }

    public bool IsDoorLocked()
    {
        return doorController.IsDoorLocked();
    }

    private Vector3 GetRandomPosition()
    {
        Bounds floorBounds = floor.GetComponent<Collider>().bounds;

        // Position slightly on top of the floor
        float y = floorBounds.max.y + 0.3f;

        // Generate positions until one is not overlapping columns
        bool foundPosition = false;
        Vector3 position = new Vector3(0, 0, 0);

        // Some margin to avoid spawning on the walls
        float margin = 1f;
        float safeRadius = 0.8f;
        LayerMask blockers = LayerMask.GetMask(
            "Obstacle",
            "Door",
            "Dragon",
            "Agent",
            "Key",
            "Cave"
        );

        while (!foundPosition)
        {
            float randomX = Random.Range(floorBounds.min.x + margin, floorBounds.max.x - margin);
            float randomZ = Random.Range(floorBounds.min.z + margin, floorBounds.max.z - margin);

            position = new Vector3(randomX, y, randomZ);

            Debug.DrawLine(position, position + Vector3.up * 2, Color.red, 5f);

            // If the new position is not overlapping with a column/wall/cave/others
            if (!Physics.CheckSphere(position, safeRadius, blockers))
                foundPosition = true;
        }

        return position;
    }

    private Vector3 SpawnCaveAndDragon()
    {
        float margin = 0.7f;

        Bounds floorBounds = floor.GetComponent<Collider>().bounds;

        float y = floorBounds.max.y + 0.5f;

        // Define the 4 corners
        Vector3[] corners = new Vector3[4];
        corners[0] = new Vector3(floorBounds.min.x + margin, y, floorBounds.min.z + margin);
        corners[1] = new Vector3(floorBounds.min.x + margin, y, floorBounds.max.z - margin);
        corners[2] = new Vector3(floorBounds.max.x - margin, y, floorBounds.min.z + margin);
        corners[3] = new Vector3(floorBounds.max.x - margin, y, floorBounds.max.z - margin);

        // Choose a random corner of the 4 available on the floor
        int caveIndex = Random.Range(0, corners.Length);
        Vector3 cavePosition = corners[caveIndex];

        // Move cave to generated position
        cave.transform.position = cavePosition;

        // Rotate cave towards center of the floor
        cave.transform.LookAt(floorBounds.center);

        // Find bounds of the opposite half of the dungeon
        // with respect to the cave
        float minX,
            maxX,
            minZ,
            maxZ;

        // Check where the cave is
        bool caveOnLeft = cavePosition.x < floorBounds.center.x;
        bool caveOnBottom = cavePosition.z < floorBounds.center.z;

        if (caveOnLeft)
        {
            // If the cave is on the left,
            // then the lower boun of the spawn area of the dragon
            // should be the center, and the top the top right
            minX = floorBounds.center.x;
            maxX = floorBounds.max.x - margin;
        }
        else
        {
            // If the cave is on the right, then the upper bound is the
            // center of the arena, the lower bound is the opposite corner
            minX = floorBounds.min.x + margin;
            maxX = floorBounds.center.x;
        }

        if (caveOnBottom)
        {
            // If the cave is on the bottom side,
            // lower bound is the center, upper bound is the
            // top of the arena
            minZ = floorBounds.center.z;
            maxZ = floorBounds.max.z - margin;
        }
        else
        {
            minZ = floorBounds.min.z + margin;
            maxZ = floorBounds.center.z;
        }

        // Get a position for the dragon within the opposite side of the cave
        // avoiding columns
        Vector3 dragonPos = Vector3.zero;
        bool foundPosition = false;
        float safeRadius = 0.8f;
        LayerMask spawnBlockers = LayerMask.GetMask("Obstacle", "Agent", "Key");

        while (!foundPosition)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);

            dragonPos = new Vector3(x, y, z);

            Debug.DrawLine(dragonPos, dragonPos + Vector3.up * 2, Color.green, 5f);

            // Keep sampling positions until you find one
            // that is not overlapping a column. Also, avoid spawning
            // overlapping agent
            if (!Physics.CheckSphere(dragonPos, safeRadius, spawnBlockers))
            {
                foundPosition = true;
            }
        }

        return dragonPos;
    }

    private void SpawnDoor()
    {
        // Spawn the door in one of the 4 sides of the arena
        float margin = 1f;
        float marginBottom = 0.9f;

        Bounds floorBounds = floor.GetComponent<Collider>().bounds;

        float y = floorBounds.max.y + marginBottom;
        Vector3 center = floorBounds.center;

        // Defining the 4 possible spawn points
        // which are the center point of each wall
        Vector3[] sideCenters = new Vector3[4];

        // Left side
        sideCenters[0] = new Vector3(floorBounds.min.x + margin, y, center.z);

        // Right side
        sideCenters[1] = new Vector3(floorBounds.max.x - margin, y, center.z);

        // Bottom side
        sideCenters[2] = new Vector3(center.x, y, floorBounds.min.z + margin);

        // Top side
        sideCenters[3] = new Vector3(center.x, y, floorBounds.max.z - margin);

        // Extracting a random side
        Vector3 doorPos = sideCenters[Random.Range(0, sideCenters.Length)];
        door.transform.position = doorPos;

        // Rotate door towards center of the floor
        door.transform.LookAt(new Vector3(center.x, center.y + marginBottom, center.z));
    }

    private void SpawnKey()
    {
        // Spawn key in random position
        GameObject key = Instantiate(
            keyPrefab,
            GetRandomPosition(),
            Quaternion.identity,
            transform
        );

        this.key = key;
    }

    public void ChangeLightsColor(string color)
    {
        Color newColor;
        switch (color)
        {
            case "green":
                ColorUtility.TryParseHtmlString("#3AC186", out newColor);
                break;
            case "red":
                ColorUtility.TryParseHtmlString("#C13A61", out newColor);
                break;
            default:
                ColorUtility.TryParseHtmlString("#943AC1", out newColor);
                break;
        }

        // Change color of each column light
        foreach (Transform column in columns.transform)
        {
            // Get light game object
            Light light = column.transform.GetChild(1).GetComponent<Light>();

            light.color = newColor;
        }
    }

    private void FailEpisode()
    {
        Debug.Log("The dragon ran away! Quest failed.");

        ChangeLightsColor("red");

        agent.GetComponent<AgentBehavior>().FailEscape();
    }
}
