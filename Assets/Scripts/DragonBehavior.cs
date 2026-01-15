using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DragonBehavior : MonoBehaviour
{

    // Start is called before the first frame update
    void Start() { }

    void OnCollisionEnter(Collision collision)
    {
        // When the dragon reaches its destination
        // the episode should end
        if (collision.gameObject.name == "Cave")
        {
            gameObject.SetActive(false);
        }
    }
}
