using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class DungeonController : MonoBehaviour
{
    [SerializeField]
    private GameObject cave;

    [SerializeField]
    private GameObject floor;

    [SerializeField]
    private GameObject door;

    [SerializeField]
    private GameObject dragonPrefab;

    [SerializeField]
    private GameObject agentPrefab;

    [SerializeField]
    private GameObject columns;
    private GameObject agent;
    private GameObject dragon;

    public Timer timer;

    void Start()
    {
        SpawnDragon();
        SpawnAgent();

        timer = gameObject.AddComponent<Timer>();
        timer.onTimerEndEvent += FailEpisode;
    }

    public void ResetEnvironment()
    {
        // Reposition agent
        agent.transform.position = GetNewAgentPosition();

        // Reposition dragon
        dragon.transform.position = GetNewDragonPosition();

        // Heal dragon
        dragon.GetComponent<DragonBehavior>().Resuscitate();

        // Lock door
        door.GetComponent<DoorController>().LockDoor();

        // Stop running timer from previous episode
        timer.StopTimer();
    }

    private void SpawnAgent()
    {
        GameObject agent = Instantiate(agentPrefab, transform);

        agent.GetComponent<AgentBehavior>().SetDungeonController(this);

        this.agent = agent;
    }

    private void SpawnDragon()
    {
        // Instantiate the dragon
        GameObject dragon = Instantiate(dragonPrefab, transform);

        this.dragon = dragon;

        DragonBehavior db = dragon.GetComponent<DragonBehavior>();

        db.SetCave(cave);

        db.onDragonEscapeEvent += FailEpisode;
        db.onDragonSlainEvent += () => timer.StartTimer(60f);
    }

    private Vector3 GetNewAgentPosition()
    {
        Vector3 ftp = floor.transform.position;
        return new Vector3(ftp.x, ftp.y + 0.5f, ftp.z);
    }

    private Vector3 GetNewDragonPosition()
    {
        // Floor's transform and size
        Vector3 ftp = floor.transform.position;
        Vector3 size = floor.GetComponent<Collider>().bounds.size;

        // On the door's side of the floor
        float z = -1 * size.z / 2 + 0.2f;

        // Random position on the door's side of the floor
        float xRange = size.x / 2 - 0.1f;
        float randX = Random.Range(-xRange, xRange);

        return new Vector3(ftp.x + randX, ftp.y, ftp.z + z);
    }

    public void ChangeLightsColor(string color)
    {
        Color newColor;
        switch (color)
        {
            case "green":
                ColorUtility.TryParseHtmlString("#3AC186", out newColor);
                break;
            case "red":
                ColorUtility.TryParseHtmlString("#C13A61", out newColor);
                break;
            default:
                ColorUtility.TryParseHtmlString("#943AC1", out newColor);
                break;
        }

        // Change color of each column light
        foreach (Transform column in columns.transform)
        {
            // Get light game object
            Light light = column.transform.GetChild(1).GetComponent<Light>();

            light.color = newColor;
        }
    }

    private void FailEpisode()
    {
        Debug.Log("The dragon ran away! Quest failed.");

        ChangeLightsColor("red");

        agent.GetComponent<AgentBehavior>().FailEscape();
    }
}
