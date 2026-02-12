using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class AgentBehavior : Agent
{
    private DungeonController dungeon;
    private Rigidbody rb;

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

    [Header("Movement")]
    [SerializeField]
    private float speed = 2.0f;

    [Header("Reward system")]
    [SerializeField]
    private RewardSystem rewardSystem;

    [HideInInspector]
    public Personality personality;

    // Time of the last hit inflicted to a dragon
    private DateTime latestHit;

    // Stats
    [HideInInspector]
    public AgentEpisodeStats stats;

    // Last position the agent was in, used to compute distance traveled
    private Vector3 lastPosition;

    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();
        raysHelper = rays.GetComponent<RaysHelper>();

        stats = new AgentEpisodeStats(this);
    }

    public void Reset()
    {
        hitsInflicted = 0;
        gameObject.SetActive(true);
        hasKey = false;
        key.SetActive(false);
        areDragonsAlive = true;

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
            float reward = -1f * personality.conscientiousness;
            AddReward(reward);

            // [OCEAN, agreeableness] Don't interfere with others
            // Punish an agreeable agent if it obstructs another agent
            reward = -1f * personality.agreeableness;
            AddReward(reward);

            stats.agentCollisions++;
        }
        if (tag == "Key")
        {
            // Destroy the key
            Destroy(collision.gameObject);

            Debug.Log("Knight picked up the key!");

            // Agent acquires the key
            hasKey = true;
            key.SetActive(true);

            // [OCEAN, Conscientiousness] Impatience
            // If low conscientiousness, the agent is rewarded for grabbing the key
            // as soon as possible. If high conscientiousness, it is rewarded for
            // being patient
            float reward =
                rewardSystem.grabKey
                - 0.1f * dungeon.timer.TimeLeft() * personality.conscientiousness;
            AddReward(reward);

            stats.hasKey = true;
            stats.timeToFindKey = dungeon.timer.TimeElapsed();
        }
    }

    private void HitDragon(GameObject dragon)
    {
        Debug.Log("A knight hit the dragon!");
        hitsInflicted++;
        stats.hitsInflicted = hitsInflicted;

        // Swing sword
        swordAnimator.SetTrigger("swing");

        // Inflict damage to the dragon
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();
        int livesLeft = dragonBehavior.TakeAHit(gameObject);

        float reward = rewardSystem.hitDragon;

        // [OCEAN, extraversion] Initiative
        // If extroverted, rewarded progressively more for every hit inflicted.
        // If introverted, rewards diminish
        reward += rewardSystem.hitDragon * personality.extraversion * hitsInflicted * 0.5f;

        // [OCEAN, neuroticism] Panic
        // Punish/reward an agents based on how long it has been since
        // the last hit it has inflicted to a dragon. The more neurotic,
        // the more it is punished for hitting the dragon quickly again
        reward += 0.001f * personality.neuroticism * (float)(DateTime.Now - latestHit).TotalSeconds;
        latestHit = DateTime.Now;

        // [OCEAN, agreeableness] cooperation
        // Agreeable agent should be rewarded for hitting dragons already
        // wounded by others, non agreeable agent should be rewarded
        // for hitting dragons that have not been hit by others yet
        if (dragonBehavior.whoHitMe.Count == 1 && dragonBehavior.whoHitMe.Contains(gameObject))
        {
            // The only agent to have hit the dragon is this one, so reward if not agreeable, punish if agreeable
            reward += rewardSystem.hitDragon * (1 - personality.agreeableness);
        }
        else
        {
            // Multiple agents have already hit the dragon or the dragon was not hit by
            // this agent, so reward if agreeable, punish if not agreeable
            reward += rewardSystem.hitDragon * personality.agreeableness;
        }

        AddReward(reward);

        // If the dragon has been slain
        if (livesLeft == 0)
        {
            stats.dragonsKilled++;
            AddReward(rewardSystem.slayDragon);
        }
    }

    public void HitClosedDoor()
    {
        AddReward(rewardSystem.hitClosedDoor);
    }

    public void Escape()
    {
        Debug.Log($"{gameObject.name} has escaped successfully!");

        // Set rewards
        AddReward(rewardSystem.escape);

        gameObject.SetActive(false);
    }

    public void FailEscape()
    {
        // Set rewards
        AddReward(rewardSystem.failEscape);

        gameObject.SetActive(false);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveRotate = actions.ContinuousActions[0];
        float moveForward = actions.ContinuousActions[1];

        rb.MovePosition(
            transform.position + transform.forward * moveForward * speed * Time.deltaTime
        );

        transform.Rotate(0f, moveRotate * speed, 0f, Space.Self);

        // Small punishment
        AddReward(rewardSystem.punishStep);

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
            float reward =
                0.001f
                * personality.extraversion
                * (distanceFromAgent < 1f ? 1f - distanceFromAgent : 0f);
            AddReward(reward);
        }

        // Punish if dragon is not in view and still alive
        if (!dragonVisible && areDragonsAlive)
        {
            AddReward(rewardSystem.dragonNotInView);
        }

        // [OCEAN, Openness] Exploration
        // Reward forward movement when dragon is not in view
        if (!dragonVisible && moveForward > 0 && areDragonsAlive)
        {
            float reward = Math.Max(0, personality.openness) * 0.001f * moveForward;
            AddReward(reward);
        }

        if (dragonVisible)
        {
            // [OCEAN, Conscientiousness] Impatience
            // If low conscientiousness, rewarded for running towards the dragon
            // directly once it is in view, otherwise punished for it to encourage patience
            float reward =
                -0.002f
                * personality.conscientiousness
                * moveForward
                * (float)Math.Cos(angleWithDragon * Mathf.Deg2Rad);

            // [OCEAN, neuroticism] Scared
            // High neuroticism leads to being scared of the dragons,
            // meaning that the agent will be punished if it looks at them
            reward += 0.002f * personality.neuroticism;

            // [OCEAN, neuroticism] Anxiety
            // If the dragon is visible, then reward the more distance the agent
            // keeps from it the more neurotic it is, punish the more it approaches the dragon
            reward += -0.001f * personality.neuroticism * distanceFromDragon;

            AddReward(reward);
        }

        // [OCEAN, Neuroticism] Hesitance
        // The more hesitant, the more it is rewarded for moving slowly
        if (personality.neuroticism > 0)
            AddReward(-1 * personality.neuroticism * moveForward * 0.002f);
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
        // Calc distance between last position and current one
        // in order to record distance traveled during the episode
        float dist = Vector3.Distance(transform.position, lastPosition);
        stats.distanceTraveled += dist;
        lastPosition = transform.position;

        // Calc mean distance from all agents and time spent
        // in proximity of other agents
        stats.MeanDistanceFromAllAgents(dungeon.agents);

        // Calc mean distance from all dragons
        stats.MeanDistanceFromAllDragons(dungeon.dragons);

        // Calc time spent moving with speed lower than threshold
        stats.UpdateIdleTime(rb.velocity.magnitude);

    }

    void OnDrawGizmos()
    {
        Handles.Label(transform.position, personality.name);
    }
}
