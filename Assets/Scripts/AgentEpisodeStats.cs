using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentEpisodeStats
{
    private AgentBehavior agent;

    public AgentEpisodeStats(AgentBehavior agent)
    {
        this.agent = agent;
    }

    // Combat metrics and episode outcomes

    // How many dragons the agent has killed during the episode
    public int dragonsKilled = 0;

    // Whether the agent has picked up the key during the episode
    public bool hasKey = false;

    // How many times the agent hit a dragon
    public int hitsInflicted = 0;

    // Time between key dropped in the arena and agent picking it up
    // If -1, the agent never picked up the key
    public float timeToFindKey = -1f;

    // Distance traveled during the episode
    public float distanceTraveled = 0f;

    // How many times the agent hit another agent
    public int agentCollisions = 0;

    // Mean distance from all agents during the episode
    private float runningAverageOfDistancesFromAgents = 0f;
    private int numberOfMeanDistanceFromAgentsCalcs = 0;
    private const float VICINITY_THRESHOLD = 1f;

    // Mean distance from all dragons during the episode
    private float runningAverageOfDistancesFromDragons = 0f;
    private int numberOfMeanDistanceFromDragonsCalcs = 0;

    // Idle time during the episode
    public float idleTime = 0f;
    private const float IDLE_VELOCITY_THRESHOLD = 0.1f;

    // Time spent in the vicinity of other agents during the episode
    public float timeInVicinityOfAgents = 0f;

    public void MeanDistanceFromAllAgents(List<GameObject> agents)
    {
        (float meanDistance, bool isInVicinity) = MeanDistanceFromGameObjects(agents);

        // Running average: ((n - 1) * m_{n-1} + a_n) / n
        runningAverageOfDistancesFromAgents =
            (
                numberOfMeanDistanceFromAgentsCalcs * runningAverageOfDistancesFromAgents
                + meanDistance
            ) / (numberOfMeanDistanceFromAgentsCalcs + 1);

        numberOfMeanDistanceFromAgentsCalcs++;

        if (isInVicinity)
        {
            timeInVicinityOfAgents += Time.fixedDeltaTime;
        }
    }

    public void MeanDistanceFromAllDragons(List<GameObject> dragons)
    {
        (float meanDistance, _) = MeanDistanceFromGameObjects(dragons);

        runningAverageOfDistancesFromDragons =
            (
                numberOfMeanDistanceFromDragonsCalcs * runningAverageOfDistancesFromDragons
                + meanDistance
            ) / (numberOfMeanDistanceFromDragonsCalcs + 1);

        numberOfMeanDistanceFromDragonsCalcs++;
    }

    private (float, bool) MeanDistanceFromGameObjects(List<GameObject> gameObjects)
    {
        float sum = 0f;

        bool isInVicinity = false;

        foreach (var obj in gameObjects)
        {
            if (obj == agent.gameObject)
            {
                continue;
            }

            float distance = Vector3.Distance(agent.transform.position, obj.transform.position);
            sum += distance;

            if (distance < VICINITY_THRESHOLD)
            {
                isInVicinity = true;
            }
        }

        return (sum / gameObjects.Count, isInVicinity);
    }

    public void UpdateIdleTime(float velocity)
    {
        if (velocity < IDLE_VELOCITY_THRESHOLD)
        {
            idleTime += Time.fixedDeltaTime;
        }
    }

    public string ToCSVString(int episodeCounter)
    {
        // Make the CSV string
        string[] values = new string[]
        {
            episodeCounter.ToString(),
            agent.personality.name,
            hitsInflicted.ToString(),
            dragonsKilled.ToString(),
            hasKey ? "1" : "0",
            timeToFindKey.ToString("0.000"),
            agentCollisions.ToString(),
            distanceTraveled.ToString("0.000"),
            runningAverageOfDistancesFromAgents.ToString("0.000"),
            runningAverageOfDistancesFromDragons.ToString("0.000"),
            idleTime.ToString("0.000"),
            timeInVicinityOfAgents.ToString("0.000"),
        };

        return string.Join(";", values);
    }

    public void Print()
    {
        Debug.Log(
            $"Dragons Killed: {dragonsKilled}, "
                + $"Has Key: {hasKey}, "
                + $"Hits Inflicted: {hitsInflicted}, "
                + $"Time to Find Key: {timeToFindKey}, "
                + $"Distance Traveled: {distanceTraveled}"
        // + $"Average Distance to Closest Agent: {averageDistanceToClosestAgent()}"
        );
    }
}
