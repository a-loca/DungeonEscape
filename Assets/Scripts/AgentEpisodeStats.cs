using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentEpisodeStats
{
    private IAgentState agent;

    // Combat metrics and episode outcomes

    // How many dragons the agent has killed during the episode
    public int dragonsKilled = 0;

    // Time between key dropped in the arena and agent picking it up
    // If -1, the agent never picked up the key
    public float timeToFindKey = -1f;

    // Last position the agent was in, used to compute distance traveled
    private Vector3 lastPosition;

    // Distance traveled during the episode
    public float distanceTraveled = 0f;

    // Mean speed during the episode
    public float meanSpeed = 0f;
    private int numberOfMeanSpeedCalcs = 0;

    // How many times the agent hit another agent
    public int agentCollisions = 0;

    // Mean distance from all agents during the episode
    private float runningAverageOfDistancesFromAgents = 0f;
    private int numberOfMeanDistanceFromAgentsCalcs = 0;

    // Mean distance from all dragons during the episode
    private float runningAverageOfDistancesFromDragons = 0f;
    private int numberOfMeanDistanceFromDragonsCalcs = 0;

    // Idle time during the episode
    public float idleTime = 0f;

    // Time spent in the vicinity of other agents during the episode
    public float timeInVicinityOfAgents = 0f;

    private const float PROXIMITY_THRESHOLD = 1f;
    private const float IDLE_VELOCITY_THRESHOLD = 0.1f;

    public AgentEpisodeStats(IAgentState agent)
    {
        this.agent = agent;
    }

    public void MeanDistanceAndProximityFromAllAgents(List<AgentBehavior> agents)
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

    public void MeanDistanceFromAllDragons(List<DragonBehavior> dragons)
    {
        (float meanDistance, _) = MeanDistanceFromGameObjects(dragons);

        runningAverageOfDistancesFromDragons =
            (
                numberOfMeanDistanceFromDragonsCalcs * runningAverageOfDistancesFromDragons
                + meanDistance
            ) / (numberOfMeanDistanceFromDragonsCalcs + 1);

        numberOfMeanDistanceFromDragonsCalcs++;
    }

    private (float, bool) MeanDistanceFromGameObjects<T>(List<T> gameObjects)
        where T : Component
    {
        float sum = 0f;

        bool isInVicinity = false;

        foreach (var obj in gameObjects)
        {
            if (obj == agent)
            {
                continue;
            }

            float distance = Vector3.Distance(agent.Position, obj.transform.position);
            sum += distance;

            if (distance < PROXIMITY_THRESHOLD)
            {
                isInVicinity = true;
            }
        }

        return (sum / (gameObjects.Count - 1), isInVicinity);
    }

    public void UpdateIdleTime(float velocity)
    {
        if (velocity < IDLE_VELOCITY_THRESHOLD)
        {
            idleTime += Time.fixedDeltaTime;
        }
    }

    public void UpdateMeanSpeed(float velocity)
    {
        // Running average of the speed
        meanSpeed = (numberOfMeanSpeedCalcs * meanSpeed + velocity) / (numberOfMeanSpeedCalcs + 1);

        numberOfMeanDistanceFromDragonsCalcs++;
    }

    public void UpdateTraveledDistance()
    {
        float dist = Vector3.Distance(agent.Position, lastPosition);
        distanceTraveled += dist;
        lastPosition = agent.Position;
    }

    public string ToCSVString(int episodeCounter)
    {
        // Make the CSV string
        string[] values = new string[]
        {
            episodeCounter.ToString(),
            agent.Personality.name,
            agent.HitsInflicted.ToString(),
            dragonsKilled.ToString(),
            agent.HasKey ? "1" : "0",
            timeToFindKey.ToString("0.000"),
            agentCollisions.ToString(),
            distanceTraveled.ToString("0.000"),
            meanSpeed.ToString("0.000"),
            runningAverageOfDistancesFromAgents.ToString("0.000"),
            runningAverageOfDistancesFromDragons.ToString("0.000"),
            idleTime.ToString("0.000"),
            timeInVicinityOfAgents.ToString("0.000"),
        };

        return string.Join(";", values);
    }
}
