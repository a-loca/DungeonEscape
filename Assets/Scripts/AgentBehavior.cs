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
    private bool hasKey = false;

    [SerializeField]
    private float speed = 2.0f;

    [SerializeField]
    private GameObject key;

    [SerializeField]
    private Animator swordAnimator;

    [SerializeField]
    private RewardSystem rewardSystem;
    private bool dragonAlive = true;

    [SerializeField]
    private GameObject rays;
    private RaysHelper raysHelper;

    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();
        raysHelper = rays.GetComponent<RaysHelper>();
    }

    public override void OnEpisodeBegin()
    {
        hasKey = false;
        key.SetActive(false);
        dragonAlive = true;

        // Repositions agent and dragon
        dungeon.ResetEnvironment();
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
            Debug.Log("Knight hit a wall");
            AddReward(rewardSystem.hitWall);
            dungeon.ChangeLightsColor("red");
            EndEpisode();
        }
    }

    public void SetDungeonController(DungeonController dungeon)
    {
        this.dungeon = dungeon;
    }

    public bool HasKey()
    {
        return hasKey;
    }

    private void HitDragon(GameObject dragon)
    {
        Debug.Log("A knight hit the dragon!");
        AddReward(rewardSystem.hitDragon);

        // Inflict damage to the dragon
        int livesLeft = dragon.GetComponent<DragonBehavior>().TakeAHit();

        // Swing sword
        swordAnimator.SetTrigger("swing");

        // If the dragon has been slain, the agent
        // will retrieve the key
        if (livesLeft == 0)
        {
            // Acquire the key
            key.SetActive(true);
            hasKey = true;
            dragonAlive = false;
            AddReward(rewardSystem.slayDragon);

            // TOGGLE: step 3
            // EndEpisode();
            // dungeon.ChangeLightsColor("green");
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

        dungeon.ChangeLightsColor("green");

        EndEpisode();
    }

    public void FailEscape()
    {
        // Set rewards
        AddReward(rewardSystem.failEscape);

        EndEpisode();
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

        // Punish if dragon is not in view and still alive
        if (!raysHelper.CanSeeDragon() && dragonAlive)
        {
            AddReward(rewardSystem.dragonNotInView);
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
        // Knows if dragon is dead or alive
        sensor.AddObservation(dragonAlive ? 1f : 0f);
    }

    void OnDrawGizmos()
    {
        // Draw sphere around the agent
        // Gizmos.DrawSphere(transform.position, 0.7f);
    }
}
