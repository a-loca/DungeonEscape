using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    private bool isLocked = true;
    public event Action<GameObject> OnAgentEscape;

    private void UnlockDoor()
    {
        isLocked = false;
    }

    public void LockDoor()
    {
        isLocked = true;
    }

    public bool IsDoorLocked()
    {
        return isLocked;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if agent is trying to escape
        if (collision.gameObject.tag == "Agent")
        {
            AgentBehavior agent = collision.gameObject.GetComponent<AgentBehavior>();

            // If the agent has the key, then the door will be
            // unlocked for everyone
            if (isLocked && agent.HasKey())
            {
                UnlockDoor();
            }

            // Only if the door has already been unlocked
            // the agent can escape the dungeon
            if (!isLocked)
            {
                if (OnAgentEscape != null)
                {
                    // Agent escape
                    OnAgentEscape(collision.gameObject);
                }

                return;
            }

            agent.HitClosedDoor();

            //Debug.Log("You can't escape without a key!");
        }
    }
}
