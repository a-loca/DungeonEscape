using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class DungeonController : MonoBehaviour
{
    public GameObject cave;
    public GameObject floor;
    public GameObject dragonPrefab;
    public GameObject agentPrefab;
    private List<AgentBehavior> agents = new List<AgentBehavior>();

    void Start()
    {
        SpawnDragon();
        SpawnAgent();
    }

    private void SpawnAgent()
    {
        Vector3 ftp = floor.transform.position;
        Vector3 pos = new Vector3(ftp.x, ftp.y + 0.5f, ftp.z);
        GameObject agent = Instantiate(agentPrefab, pos, Quaternion.identity, transform);
        agents.Add(agent.GetComponent<AgentBehavior>());
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
        DragonBehavior dragon = Instantiate(dragonPrefab, dragonPos, Quaternion.identity, transform)
            .GetComponent<DragonBehavior>();
        dragon.SetCave(cave);
        dragon.onDragonEscapeEvent += FailEpisode;
    }

    private void FailEpisode()
    {
        Debug.Log("The dragon ran away! Quest failed.");
        // Fail every agent
        foreach (var agent in agents)
        {
            agent.FailEscape();
        }
    }
}
