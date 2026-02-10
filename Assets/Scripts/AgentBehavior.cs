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

    [SerializeField]
    private Personality personality;

    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();
        raysHelper = rays.GetComponent<RaysHelper>();

        // [OCEAN, extraversion] Energetic
        // Extroverted agents have more energy, which means they can move faster
        if (personality.extraversion > 0)
            speed += 1f * personality.extraversion;
    }

    public void Reset()
    {
        hitsInflicted = 0;
        gameObject.SetActive(true);
        hasKey = false;
        key.SetActive(false);
        areDragonsAlive = true;
    }

    public void SetDungeonController(DungeonController dungeon)
    {
        this.dungeon = dungeon;
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
            dungeon.HitObstacle(gameObject);
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
        }
    }

    private void HitDragon(GameObject dragon)
    {
        Debug.Log("A knight hit the dragon!");
        hitsInflicted++;

        // [OCEAN, extraversion] Initiative
        // If extroverted, rewarded progressively more for every hit inflicted.
        // If introverted, rewards diminish
        float reward =
            rewardSystem.hitDragon
            + rewardSystem.hitDragon * personality.extraversion * hitsInflicted * 0.5f;

        AddReward(reward);



        // Swing sword
        swordAnimator.SetTrigger("swing");

        // Inflict damage to the dragon
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();
        int livesLeft = dragonBehavior.TakeAHit();

        // TODO:
        // [OCEAN, neuroticism] Panic
        // Punish neurotic agent if it hits a dragon multiple times in a row

        // TODO:
        // [OCEAN, agreeableness] cooperation
        // Agreeable agent should be rewarded for hitting dragons already
        // wounded by others, non agreeable agent should be rewarded
        // for hitting dragons that have not been hit by others


        // If the dragon has been slain
        if (livesLeft == 0)
        {
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

        var (dragonVisible, distance, angle) = raysHelper.CanSeeDragon();

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
                * (float)Math.Cos(angle * Mathf.Deg2Rad);

            AddReward(reward);

            // [OCEAN, neuroticism] Scared
            // High neuroticism leads to being scared of the dragons,
            // meaning that the agent will be punished if it looks at them
            AddReward(0.002f * personality.neuroticism);
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
}
