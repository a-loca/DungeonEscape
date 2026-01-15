using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DungeonController : MonoBehaviour
{
    public GameObject cave;
    public GameObject floor;
    public GameObject dragonPrefab;

    // Start is called before the first frame update
    void Start()
    {
        SpawnDragon();
    }

    private void SpawnDragon()
    {
        // Floor's transform and size
        Vector3 ftp = floor.transform.position;
        Vector3 size = floor.GetComponent<Collider>().bounds.size;

        // On the door's side of the floor
        float z = -1 * size.z / 2 + 0.2f;

        // Random position on the door's side of the floor
        float xRange = size.x / 2 - 0.1f;
        float randX = Random.Range(-xRange, xRange);

        Vector3 dragonPos = new Vector3(ftp.x + randX, ftp.y, ftp.z + z);

        // Instantiate the dragon
        GameObject dragon = Instantiate(dragonPrefab, dragonPos, Quaternion.identity, transform);

        // Turn the dragon towards the cave
        dragon.transform.LookAt(cave.transform);

        // Instruct the dragon to walk towards the cave
        dragon.GetComponent<NavMeshAgent>().SetDestination(cave.transform.position);
    }

    // Update is called once per frame
    void Update() { }
}
