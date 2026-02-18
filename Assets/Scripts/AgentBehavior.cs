using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class AgentBehavior : Agent
{
    private DungeonController dungeon;
    private Rigidbody rb;

    [HideInInspector]
    public bool computeEpisodeStats;

    [Header("Agent components")]
    [SerializeField]
    private GameObject key;

    [SerializeField]
    private Animator swordAnimator;

    [SerializeField]
    private GameObject rays;
    private RaysHelper raysHelper;

    [HideInInspector]
    public bool areDragonsAlive = true;
    private bool hasKey = false;
    private int hitsInflicted;

    // Time of the last hit inflicted to a dragon
    private float latestHitTime;

    // The last dragon that was hit
    private GameObject latestDragonHit;

    [Header("Movement")]
    [SerializeField]
    private float speed = 2.0f;

    [Header("Reward system")]
    [SerializeField]
    private RewardSystem rewardSystem;

    [HideInInspector]
    public Personality personality;

    // Stats
    [HideInInspector]
    public AgentEpisodeStats stats;

    // Last position the agent was in, used to compute distance traveled
    private Vector3 lastPosition;

    // Max radius within which an agent is considered in proximity of another
    private const float MAX_PROXIMITY_RADIUS = 1f;

    // How long a neurotic agents panics for after hitting a dragon
    private const float PANIC_THRESHOLD = 5f;

    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();
        raysHelper = rays.GetComponent<RaysHelper>();
    }

    public void Reset()
    {
        hitsInflicted = 0;
        hasKey = false;
        areDragonsAlive = true;
        latestDragonHit = null;
        key.SetActive(false);

        if (computeEpisodeStats)
            stats = new AgentEpisodeStats(this);
    }

    public void SetDungeonController(DungeonController dungeon)
    {
        this.dungeon = dungeon;
    }

    public void SetPersonality(Personality personality)
    {
        this.personality = personality;

        // [OCEAN, extraversion] Energetic
        // Extroverted agents have more energy, which means they can move faster
        if (personality.extraversion > 0)
            speed += 1f * personality.extraversion;
    }

    public bool HasKey()
    {
        return hasKey;
    }

    void OnCollisionEnter(Collision collision)
    {
        string tag = collision.gameObject.tag;
        if (tag == "Dragon")
        {
            HitDragon(collision.gameObject);
        }
        if (tag == "Walls" || tag == "Cave")
        {
            AddReward(rewardSystem.hitWall);
        }
        if (tag == "Agent")
        {
            // [OCEAN, conscientiousness] Panic
            // A conscientious agent should be punished for hitting
            // other agents, otherwise a low conscientiousness agent
            // should be prone to panic and hit others
            float panicReward = -0.1f * personality.conscientiousness;
            AddReward(panicReward);

            // [OCEAN, agreeableness] politeness
            // Punish an agreeable agent if it obstructs another agent
            float politenessReward = -1f * personality.agreeableness;
            AddReward(politenessReward);

            Debug.Log(
                $"{personality.name}: politeness = {politenessReward}, panic = {panicReward}"
            );

            if (computeEpisodeStats)
                stats.agentCollisions++;
        }
        if (tag == "Key")
        {
            // Destroy the key
            dungeon.RemoveKey();

            // Debug.Log("Knight picked up the key!");

            // Agent acquires the key
            hasKey = true;
            key.SetActive(true);

            // [OCEAN, Conscientiousness] Impatience
            // If low conscientiousness, the agent is rewarded for grabbing the key
            // as soon as possible. If high conscientiousness, it is rewarded for
            // being patient
            float timerFactor = 0.1f * dungeon.timer.TimeLeft();
            float reward = rewardSystem.grabKey - (timerFactor * personality.conscientiousness);

            // Debug.Log($"{personality.name}: key impatience = {reward}");

            AddReward(reward);

            if (computeEpisodeStats)
            {
                stats.hasKey = true;
                stats.timeToFindKey = dungeon.timer.TimeElapsed();
            }
        }
    }

    private void HitDragon(GameObject dragon)
    {
        // Debug.Log("A knight hit the dragon!");
        hitsInflicted++;

        if (computeEpisodeStats)
            stats.hitsInflicted = hitsInflicted;

        // Swing sword
        swordAnimator.SetTrigger("swing");

        // Inflict damage to the dragon
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();
        int livesLeft = dragonBehavior.TakeAHit(gameObject);

        float initiativeReward = 0;
        float commitmentReward = 0;
        float panicReward = 0;
        float coopReward = 0;
        float baseHitReward = rewardSystem.hitDragon;

        // [OCEAN, extraversion] Initiative
        // If extroverted, rewarded progressively more for every hit inflicted.
        // For introverted agents, reward them for hitting dragons when going on a solo mission
        // Full reward when solo, reduced when around other agents, punish when crowded
        if (personality.extraversion > 0)
            initiativeReward = baseHitReward * personality.extraversion * hitsInflicted * 0.5f;
        else if (personality.extraversion < 0)
        {
            int nearbyAgents = dungeon.CountNearbyAgents(gameObject, MAX_PROXIMITY_RADIUS);
            float agentsFactor = 1f - (nearbyAgents * 0.5f);
            initiativeReward = baseHitReward * -personality.extraversion * agentsFactor;
        }

        // [OCEAN, neuroticism] Panic
        // Punish/reward an agents based on how long it has been since
        // the last hit it has inflicted to a dragon. The more neurotic,
        // the more it is punished for hitting the dragon quickly again
        float timeDiff = Time.time - latestHitTime;
        if (timeDiff < PANIC_THRESHOLD)
        {
            // Neurotic agent hit a dragon while panicking, big punishment the less
            // time has passed. Non neurotic agent hit a dragon in a small timeframe after
            // the latest hit, reward it
            panicReward = -personality.neuroticism * Mathf.Exp(-0.5f * timeDiff) * 10f;
        }
        else
        {
            // Neurotic agent has calmed down after latest hit and now hit the dragon again
            // it will receive increasingly bigger reward the more time has passed
            // Using log to avoid increasing reward or punishment too much
            panicReward = personality.neuroticism * Mathf.Log(timeDiff - PANIC_THRESHOLD + 1) * 2f;
        }

        latestHitTime = Time.time;

        // [OCEAN, agreeableness] cooperation
        // Agreeable agent should be rewarded for hitting dragons already
        // wounded by others, non agreeable agent should be rewarded
        // for hitting dragons that have not been hit by others yet
        bool dragonHitByOthers = dragonBehavior.whoHitMe.Any(a => a != gameObject);

        if (!dragonHitByOthers && personality.agreeableness < 0)
        {
            // The agent is the only one to have hit the dragon so far
            // Reward non agreeable
            coopReward = baseHitReward * (1 - personality.agreeableness);
        }
        else
        {
            // Some other agent hit the dragon before
            // Reward agreeable agent, punish non agreeable agent
            coopReward = baseHitReward * personality.agreeableness * 0.5f;
        }

        // [OCEAN, Conscientiousness] Commitment
        // A conscientious agent should not be switching targets while hitting dragons
        // until it has been killed, while a non conscientious agent should be
        // easily distracted and not disciplined and constantly switch
        float factor = baseHitReward * 0.2f; // 20% of base hit reward
        float switchFactor = dragon != latestDragonHit ? -1f : 1f;
        commitmentReward = factor * personality.conscientiousness * switchFactor;
        latestDragonHit = dragon;

        // Debug.Log(
        //     $"{personality.name}: initiative = {initiativeReward}, panic = {panicReward}, cooperation = {coopReward}, commitment = {commitmentReward}"
        // );

        AddReward(initiativeReward + panicReward + coopReward + commitmentReward);

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

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveRotate = actions.ContinuousActions[0];
        float moveForward = actions.ContinuousActions[1];

        // Movement forward
        Vector3 move = transform.forward * moveForward * speed;
        rb.velocity = new Vector3(move.x, rb.velocity.y, move.z);

        // Rotation
        transform.Rotate(0f, moveRotate * speed, 0f, Space.Self);

        var (dragonVisible, distanceFromDragon, angleWithDragon) = raysHelper.CanSeeObjectWithTag(
            "Dragon"
        );
        var (agentVisible, distanceFromAgent, angleWithAgent) = raysHelper.CanSeeObjectWithTag(
            "Agent"
        );

        if (agentVisible)
        {
            // [OCEAN, extraversion] Socialization
            // Reward an extroverted agent for staying in close proximity
            // with another agent, punish an introverted agent for it
            float proximity =
                distanceFromAgent < MAX_PROXIMITY_RADIUS
                    ? MAX_PROXIMITY_RADIUS - distanceFromAgent
                    : 0f;
            float socializationReward = 0.005f * personality.extraversion * proximity;

            // Debug.Log($"{personality.name}: socialization = {socializationReward}");

            AddReward(socializationReward);
        }

        // [OCEAN, Openness] Exploration
        // Reward forward movement when dragon is not in view
        if (!dragonVisible && moveForward > 0 && areDragonsAlive)
        {
            float explorationReward = personality.openness * 0.001f * moveForward;

            // Debug.Log($"{personality.name}: exploration = {explorationReward}");

            AddReward(explorationReward);
        }

        if (dragonVisible)
        {
            float impatienceReward;
            float fearReward;
            float anxietyReward;

            // [OCEAN, Conscientiousness] Impatience
            // If low conscientiousness, rewarded for running towards the dragon
            // directly once it is in view, otherwise punished for it to encourage patience
            float cosine = (float)Math.Cos(angleWithDragon * Mathf.Deg2Rad);
            impatienceReward = -0.02f * personality.conscientiousness * moveForward * cosine;

            // [OCEAN, neuroticism] Anxiety
            // If the dragon is visible, then reward the more distance the agent
            // keeps from it the more neurotic it is, punish the more it approaches the dragon
            float normalizedDistance = distanceFromDragon / dungeon.maxDistance;
            anxietyReward = 0.05f * personality.neuroticism * (normalizedDistance - 0.5f);

            // AddReward(impatienceReward + fearReward + anxietyReward);
            AddReward(impatienceReward + anxietyReward);

            // Debug.Log(
            //     $"{personality.name}: impatience = {impatienceReward}, anxiety = {anxietyReward}"
            // );
        }

        // [OCEAN, Neuroticism] Hesitance
        // The more hesitant, the more it is rewarded for moving slowly
        if (personality.neuroticism > 0)
        {
            float hesitanceReward = personality.neuroticism * moveForward * -0.005f;
            // Debug.Log($"{personality.name}: hesitance = {hesitanceReward}");
            AddReward(hesitanceReward);
        }
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
        sensor.AddObservation(areDragonsAlive ? 1f : 0f);

        // Knows if it has a key
        sensor.AddObservation(hasKey ? 1f : 0f);
    }

    public void FixedUpdate()
    {
        if (!computeEpisodeStats)
            return;

        // Calc distance between last position and current one
        // in order to record distance traveled during the episode
        float dist = Vector3.Distance(transform.position, lastPosition);
        stats.distanceTraveled += dist;
        lastPosition = transform.position;

        // Calc mean distance from all agents and time spent
        // in proximity of other agents
        stats.MeanDistanceAndProximityFromAllAgents(dungeon.agents);

        // Calc mean distance from all dragons
        stats.MeanDistanceFromAllDragons(dungeon.dragons);

        // Calc time spent moving with speed lower than threshold
        stats.UpdateIdleTime(rb.velocity.magnitude);
    }

    void OnDrawGizmos()
    {
        // Write the agent's personality name on top of it
        Handles.Label(transform.position, personality.name);
    }
}
