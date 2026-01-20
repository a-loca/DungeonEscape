using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AgentBehavior : Agent
{
    private DungeonController dungeon;
    public float speed = 2.0f;
    private Rigidbody rb;
    public GameObject key;
    private bool hasKey = false;
    public Animator swordAnimator;

    public override void Initialize()
    {
        // Get rigid body component to allow movement
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        hasKey = false;
        dungeon.ResetEnvironment();
    }

    public void SetDungeonController(DungeonController dungeon)
    {
        this.dungeon = dungeon;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveRotate = actions.ContinuousActions[0];
        float moveForward = actions.ContinuousActions[1];

        rb.MovePosition(
            transform.position - transform.forward * moveForward * speed * Time.deltaTime
        );
        transform.Rotate(0f, moveRotate * speed, 0f, Space.Self);
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
        // Agent knows its location
        sensor.AddObservation(transform.localPosition);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Dragon")
        {
            HitDragon(collision.gameObject);
        }
    }

    public bool HasKey()
    {
        return hasKey;
    }

    private void HitDragon(GameObject dragon)
    {
        Debug.Log("A knight hit the dragon!");
        AddReward(2f);

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
        }
    }

    public void Escape()
    {
        Debug.Log($"{gameObject.name} has escaped successfully!");
        // Set rewards
        AddReward(10f);

        EndEpisode();
    }

    public void FailEscape()
    {
        // Set rewards
        AddReward(-10f);

        EndEpisode();
    }
}
