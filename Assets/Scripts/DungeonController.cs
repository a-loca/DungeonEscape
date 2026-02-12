using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.AI;

public class DungeonController : MonoBehaviour
{
    private int episodeCounter = 0;

    [Header("Environment Objects")]
    [SerializeField]
    private GameObject cave;

    [SerializeField]
    private GameObject floor;

    [SerializeField]
    private GameObject door;
    private DoorController doorController;

    [SerializeField]
    private GameObject columns;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject dragonPrefab;

    [HideInInspector]
    public List<GameObject> dragons;

    [SerializeField]
    private GameObject agentPrefab;

    [HideInInspector]
    public List<GameObject> agents;

    [SerializeField]
    private GameObject keyPrefab;

    [HideInInspector]
    public GameObject key;

    [HideInInspector]
    public bool keyGrabbedByAgent = false;

    [Header("Episode Settings")]
    [SerializeField]
    public int numberOfAgents = 3;
    public int numberOfDragons = 2;

    [HideInInspector]
    public int remainingDragons;

    [Header("Timer")]
    public float timeToEscape = 30f;

    [HideInInspector]
    public Timer timer;

    [Header("Reward System")]
    [SerializeField]
    private GroupRewardSystem groupRewardSystem;

    private SimpleMultiAgentGroup agentGroup;

    // Need an object of both personality and behavior name
    [System.Serializable]
    public struct PersonalitySettings
    {
        public Personality personality;
        public string behaviorName;
    }

    public PersonalitySettings[] personalitySettings;

    private GlobalEpisodeStats globalEpisodeStats;

    void Start()
    {
        agents = new List<GameObject>();
        dragons = new List<GameObject>();

        agentGroup = new SimpleMultiAgentGroup();

        SpawnDragons();
        SpawnAgents();

        timer = GetComponent<Timer>();
        timer.onTimerEndEvent += () => FailEpisode(FailureReason.Timer);

        doorController = door.GetComponent<DoorController>();
        doorController.OnAgentEscape += WinGroupEpisode;

        ResetEnvironment();
    }

    public void ResetEnvironment()
    {
        // Save global episode stats and create a new object for the next episode
        if (episodeCounter > 0)
            EpisodeCSVLogger.LogGlobalEpisode(globalEpisodeStats.ToCSVString(episodeCounter));

        // Reset the stats
        globalEpisodeStats = new GlobalEpisodeStats(this);

        // Stop running timer from previous episode
        timer.StopTimer();

        // Destroy key if it has not been picked up
        if (key != null)
            Destroy(key);

        keyGrabbedByAgent = false;

        // Reset number of dragons in the environment
        remainingDragons = numberOfDragons;

        // Reposition and register agents
        foreach (var agent in agents)
        {
            AgentBehavior agentBehavior = agent.GetComponent<AgentBehavior>();

            // Log stats of the episode that just ended
            if (episodeCounter > 0)
                EpisodeCSVLogger.LogAgentEpisode(agentBehavior.stats.ToCSVString(episodeCounter));

            agentBehavior.Reset();

            // Reposition the agent
            agent.transform.position = GetRandomPosition();

            // Reset rotation to look in a random direction
            agent.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

            // Need to re-register the agent in the group
            // after having disabled them
            agentGroup.RegisterAgent(agentBehavior);
        }

        // Respawn cave
        Vector3[] dragonPositions = SpawnCaveAndDragons();

        // Respawn door
        SpawnDoor();

        // Heal and reposition dragon (need to warp the mesh agent)
        for (int i = 0; i < dragons.Count; i++)
        {
            dragons[i].GetComponent<DragonBehavior>().Resuscitate(dragonPositions[i]);
        }

        // Lock door
        doorController.LockDoor();

        // Increment episode counter
        episodeCounter++;
        Debug.Log("Starting episode " + episodeCounter);
    }

    public void FailEpisode(FailureReason reason)
    {
        Debug.Log("Agents failed to escape.");

        // Record the loss and the reason for stats
        globalEpisodeStats.failReason = reason;
        globalEpisodeStats.win = false;

        // The group also gets a punishment
        agentGroup.AddGroupReward(groupRewardSystem.dragonEscape);

        ChangeLightsColor("red");

        foreach (var agent in agents)
        {
            // Each agent fails its goal
            agent.GetComponent<AgentBehavior>().FailEscape();
        }

        // End group episode
        agentGroup.EndGroupEpisode();
        ResetEnvironment();
    }

    private void WinGroupEpisode(GameObject agent)
    {
        Debug.Log("Door was unlocked, agents escaped!");

        // Record the win in the stats
        globalEpisodeStats.win = true;

        agent.GetComponent<AgentBehavior>().Escape();

        ChangeLightsColor("green");

        // Add a reward for everyone
        agentGroup.AddGroupReward(groupRewardSystem.allAgentsEscape);

        // End the episode
        agentGroup.EndGroupEpisode();

        // Prepare for next episode
        ResetEnvironment();
    }

    private void SpawnAgents()
    {
        for (int i = 0; i < numberOfAgents; i++)
        {
            GameObject agent = Instantiate(agentPrefab, transform);

            AgentBehavior agentBehavior = agent.GetComponent<AgentBehavior>();
            agentBehavior.SetDungeonController(this);

            // Set the OCEAN personality parameters of the agent
            agentBehavior.SetPersonality(personalitySettings[i].personality);

            // Set the correct behavior name in order to train the correct model
            agent.GetComponent<BehaviorParameters>().BehaviorName = personalitySettings[
                i
            ].behaviorName;

            // Add to the collection of agents spawned by the environment
            agents.Add(agent);

            // Register the agent to the multiagent group
            agentGroup.RegisterAgent(agentBehavior);
        }
    }

    private void SpawnDragons()
    {
        for (int i = 0; i < numberOfDragons; i++)
        {
            GameObject dragon = Instantiate(dragonPrefab, transform);

            DragonBehavior db = dragon.GetComponent<DragonBehavior>();

            db.SetCave(cave);

            db.onDragonEscapeEvent += () => FailEpisode(FailureReason.DragonEscaped);
            db.onDragonSlainEvent += DragonWasKilled;

            dragons.Add(dragon);
        }
    }

    public void DragonWasKilled()
    {
        remainingDragons--;

        if (remainingDragons == 0)
        {
            Debug.Log("All dragons have been slain!");

            // Start the timer to escape
            timer.StartTimer(timeToEscape);

            // All agents need to know that the dragons are dead
            foreach (var agent in agents)
            {
                agent.GetComponent<AgentBehavior>().areDragonsAlive = false;
            }

            // Spawn the key in the environment
            SpawnKey();

            // Also add a global reward
            agentGroup.AddGroupReward(groupRewardSystem.killDragon);
        }
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

    public void RemoveKey()
    {
        Destroy(key);
        keyGrabbedByAgent = true;
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
        LayerMask blockers = LayerMask.GetMask("Obstacle", "Door", "Dragon", "Agent", "Key");

        while (!foundPosition)
        {
            float randomX = Random.Range(floorBounds.min.x + margin, floorBounds.max.x - margin);
            float randomZ = Random.Range(floorBounds.min.z + margin, floorBounds.max.z - margin);

            position = new Vector3(randomX, y, randomZ);

            // If the new position is not overlapping with a column/wall/cave/others
            if (!Physics.CheckSphere(position, safeRadius, blockers))
                foundPosition = true;
        }

        return position;
    }

    private Vector3[] SpawnCaveAndDragons()
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
        Vector3[] dragonPositions = new Vector3[numberOfDragons];
        int foundPositions = 0;
        float safeRadius = 0.8f;
        LayerMask spawnBlockers = LayerMask.GetMask("Obstacle", "Agent", "Dragon", "Door");

        while (foundPositions < numberOfDragons)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);

            Vector3 dragonPos = new Vector3(x, y, z);

            // Keep sampling positions until you find one
            // that is not overlapping a column. Also, avoid spawning
            // overlapping agent
            if (!Physics.CheckSphere(dragonPos, safeRadius, spawnBlockers))
            {
                dragonPositions[foundPositions] = dragonPos;
                foundPositions++;
            }
        }

        return dragonPositions;
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

    void FixedUpdate()
    {
        globalEpisodeStats.UpdateTimedStats();
    }
}
