using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class AgentBehavior : Agent, IAgentState
{
    // ========================================================================
    // Inspector fields
    // ========================================================================
    [Header("Agent components")]
    [SerializeField]
    private GameObject key;

    [SerializeField]
    private Animator swordAnimator;

    [SerializeField]
    private GameObject rays;

    [Header("Movement")]
    [SerializeField]
    private float speed = 2.0f;

    [Header("Reward system")]
    public RewardSystem rewardSystem;

    // ========================================================================
    // Private fields
    // ========================================================================
    private AgentRewardCalculator rewardCalculator;
    private Rigidbody rb;

    // ========================================================================
    // State information
    // =========================================================================
    public int Id { get; set; }
    public bool AreDragonsAlive { get; set; }
    public bool HasKey { get; set; }
    public int HitsInflicted { get; set; }
    public RaysHelper RaysHelper { get; set; }
    public DungeonController Dungeon { get; set; }
    public Personality Personality { get; set; }
    public Vector3 Position => transform.position;
    public RewardSystem RewardSystem => rewardSystem;

    // ========================================================================
    // Stats
    // ========================================================================
    [HideInInspector]
    public bool computeEpisodeStats;

    [HideInInspector]
    public AgentEpisodeStats stats;

    // ========================================================================
    // Initialization and reset
    // ========================================================================
    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();

        RaysHelper = rays.GetComponent<RaysHelper>();

        rewardCalculator = new AgentRewardCalculator(this);
    }

    public void Reset()
    {
        // Reset episode state variables
        HitsInflicted = 0;
        HasKey = false;
        AreDragonsAlive = true;

        // Some rewards rely on cumulative episode stats
        rewardCalculator.ResetCounters();

        // Hide held key
        key.SetActive(false);

        if (computeEpisodeStats)
            stats = new AgentEpisodeStats(this);
    }

    // ========================================================================
    // Collision handling
    // ========================================================================
    void OnCollisionEnter(Collision collision)
    {
        string tag = collision.gameObject.tag;

        switch (tag)
        {
            case "Dragon":
                HitDragon(collision.gameObject);
                break;

            case "Walls":
            case "Cave":
                AddReward(rewardCalculator.GetObstacleHitReward());
                break;

            case "Agent":
                AddReward(rewardCalculator.GetAgentHitReward());

                if (computeEpisodeStats)
                    stats.agentCollisions++;

                break;

            case "Key":
                // Destroy the key
                Dungeon.DestroyKey();

                // Debug.Log("Knight picked up the key!");

                // Agent acquires the key
                HasKey = true;
                key.SetActive(true);

                rewardCalculator.SetPreviousDistanceFromExit(
                    Dungeon.NormalizedDistanceFromExit(transform.position)
                );

                float reward = rewardCalculator.GetKeyGrabReward();
                AddReward(reward);

                if (computeEpisodeStats)
                {
                    stats.timeToFindKey = Dungeon.timer.TimeElapsed();
                }
                break;
        }
    }

    private void HitDragon(GameObject dragon)
    {
        // Debug.Log("A knight hit the dragon!");
        HitsInflicted++;

        // Swing sword
        swordAnimator.SetTrigger("swing");

        // Inflict damage to the dragon
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();
        int livesLeft = dragonBehavior.TakeAHit(Id);

        // Reward based on personality for having hit a dragon
        float reward = rewardCalculator.GetDragonHitReward(dragon, livesLeft);
        AddReward(reward);

        // If the dragon has been slain
        if (livesLeft == 0)
        {
            if (computeEpisodeStats)
                stats.dragonsKilled++;
        }
    }

    public void HitClosedDoor()
    {
        AddReward(rewardSystem.hitClosedDoor);
    }

    // ========================================================================
    // Agent overrides
    // ========================================================================

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveRotate = actions.ContinuousActions[0];
        float moveForward = actions.ContinuousActions[1];

        // Movement forward
        Vector3 move = transform.forward * moveForward * speed;
        rb.velocity = new Vector3(move.x, rb.velocity.y, move.z);

        // Rotation
        // transform.Rotate(0f, moveRotate * speed, 0f, Space.Self);
        Quaternion deltaRotation = Quaternion.Euler(
            0f,
            moveRotate * 180f * Time.fixedDeltaTime,
            0f
        );
        rb.MoveRotation(rb.rotation * deltaRotation);

        float reward = rewardCalculator.GetStepReward(moveForward);
        AddReward(reward);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual movement in behavior type heuristic
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Knows if dragons are dead or alive
        sensor.AddObservation(AreDragonsAlive ? 1f : 0f);

        // Knows if it has a key
        sensor.AddObservation(HasKey ? 1f : 0f);
    }

    // ========================================================================
    // Stats collection
    // ========================================================================

    public void FixedUpdate()
    {
        if (!computeEpisodeStats)
            return;

        // Calc distance between last position and current one
        // in order to record distance traveled during the episode
        stats.UpdateTraveledDistance();

        // Calc mean distance from all agents and time spent
        // in proximity of other agents
        stats.MeanDistanceAndProximityFromAllAgents(Dungeon.agents);

        // Calc mean speed during the episode
        stats.UpdateMeanSpeed(rb.velocity.magnitude);

        // Calc mean distance from all dragons
        stats.MeanDistanceFromAllDragons(Dungeon.dragons);

        // Calc time spent moving with speed lower than threshold
        stats.UpdateIdleTime(rb.velocity.magnitude);
    }

    // ========================================================================
    // Others
    // ========================================================================
    void OnDrawGizmos()
    {
        // Write the agent's personality name on top of it
        Handles.Label(transform.position, Personality.name);
    }
}
