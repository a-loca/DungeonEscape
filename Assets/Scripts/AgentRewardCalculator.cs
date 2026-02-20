using System.Linq;
using UnityEngine;

public class AgentRewardCalculator
{
    private IAgentState state;

    // Max radius within which an agent is considered in proximity of another
    private const float MAX_PROXIMITY_RADIUS = 1f;

    // How long a neurotic agents panics for after hitting a dragon
    private const float PANIC_THRESHOLD = 5f;

    // The last dragon that was hit
    private GameObject latestDragonHit;

    // Time of the last hit inflicted to a dragon
    private float latestHitTime;

    // Distance from team centroid at previous step
    private float previousError = 0f;

    // How far the agent was from the door on previous step
    private float previousDistanceFromExit = 0f;

    public AgentRewardCalculator(IAgentState state)
    {
        this.state = state;
    }

    public void ResetCounters()
    {
        latestDragonHit = null;
        previousError = 0f;
        previousDistanceFromExit = 0f;
    }

    public void SetPreviousDistanceFromExit(float value)
    {
        previousDistanceFromExit = value;
    }

    public float GetDragonHitReward(GameObject dragon)
    {
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();

        float initiativeReward = 0;
        float commitmentReward = 0;
        float panicReward = 0;
        float coopReward = 0;
        float baseHitReward = state.RewardSystem.hitDragon;

        // [OCEAN, extraversion] Initiative
        // If extroverted, rewarded progressively more for every hit inflicted.
        // For introverted agents, reward them for hitting dragons when going on a solo mission
        // Full reward when solo, reduced when around other agents, punish when crowded
        if (state.Personality.extraversion > 0)
            initiativeReward =
                baseHitReward * state.Personality.extraversion * state.HitsInflicted * 0.5f;
        else if (state.Personality.extraversion < 0)
        {
            int nearbyAgents = state.Dungeon.CountNearbyAgents(
                state.Id,
                state.Position,
                MAX_PROXIMITY_RADIUS
            );
            float agentsFactor = 1f - (nearbyAgents * 0.5f);
            initiativeReward = baseHitReward * -state.Personality.extraversion * agentsFactor;
        }

        // [OCEAN, neuroticism] Panic
        // Punish/reward an agents based on how long it has been since
        // the last hit it has inflicted to a dragon. The more neurotic,
        // the more it is punished for hitting the dragon quickly again
        float timeDiff = Time.time - latestHitTime;
        if (timeDiff < PANIC_THRESHOLD)
        {
            // Neurotic agent hit a dragon while panicking, big punishment the less
            // time has passed. Non neurotic agent hit a dragon in a small timeframe after
            // the latest hit, reward it
            panicReward = -state.Personality.neuroticism * Mathf.Exp(-0.5f * timeDiff) * 10f;
        }
        else
        {
            // Neurotic agent has calmed down after latest hit and now hit the dragon again
            // it will receive increasingly bigger reward the more time has passed
            // Using log to avoid increasing reward or punishment too much
            panicReward =
                state.Personality.neuroticism * Mathf.Log(timeDiff - PANIC_THRESHOLD + 1) * 2f;
        }

        latestHitTime = Time.time;

        // [OCEAN, agreeableness] cooperation
        // Agreeable agent should be rewarded for hitting dragons already
        // wounded by others, non agreeable agent should be rewarded
        // for hitting dragons that have not been hit by others yet
        bool dragonHitByOthers = dragonBehavior.whoHitMe.Any(id => id != state.Id);

        if (!dragonHitByOthers && state.Personality.agreeableness < 0)
        {
            // The agent is the only one to have hit the dragon so far
            // Reward non agreeable
            coopReward = baseHitReward * (1 - state.Personality.agreeableness);
        }
        else
        {
            // Some other agent hit the dragon before
            // Reward agreeable agent, punish non agreeable agent
            coopReward = baseHitReward * state.Personality.agreeableness * 0.5f;
        }

        // [OCEAN, Conscientiousness] Commitment
        // A conscientious agent should not be switching targets while hitting dragons
        // until it has been killed, while a non conscientious agent should be
        // easily distracted and not disciplined and constantly switch
        float factor = baseHitReward * 0.2f; // 20% of base hit reward
        float switchFactor = dragon != latestDragonHit ? -1f : 1f;
        commitmentReward = factor * state.Personality.conscientiousness * switchFactor;
        latestDragonHit = dragon;

        // Debug.Log(
        //     $"{state.Personality.name}: initiative = {initiativeReward}, panic = {panicReward}, cooperation = {coopReward}, commitment = {commitmentReward}"
        // );

        return initiativeReward + panicReward + coopReward + commitmentReward;
    }

    public void RewardKeyGrab() { }

    public float GetStepReward(float moveForward)
    {
        float diligenceReward = 0;
        float socializationReward = 0;
        float explorationReward = 0;
        float impatienceReward = 0;
        float anxietyReward = 0;
        float hesitanceReward = 0;

        var (dragonVisible, distanceFromDragon, angleWithDragon) =
            state.RaysHelper.CanSeeObjectWithTag("Dragon");

        // Debug.Log(
        //     $"Testing: dragonVisible = {dragonVisible}, distanceFromDragon = {distanceFromDragon}, angleWithDragon = {angleWithDragon}"
        // );

        // [OCEAN, conscientiousness] Diligence
        // If a conscientious agent has the key, then it should be rewarded
        // for going directly towards the exit and save everyone
        if (state.HasKey && state.Personality.conscientiousness > 0)
        {
            float distanceFromExit = state.Dungeon.NormalizedDistanceFromExit(state.Position);
            float deltaDistance = previousDistanceFromExit - distanceFromExit;
            diligenceReward = state.Personality.conscientiousness * deltaDistance * 0.1f;

            previousDistanceFromExit = distanceFromExit;
        }

        // [OCEAN, extraversion] Socialization
        // Reward introverted agent for moving away from other agents,
        // reward extroverted agent for moving closer to other agents
        float distanceFromTeam = state.Dungeon.GetAgentDistanceFromTeam(state.Id, state.Position);

        // The more the agent is extroverted, the closer the preferred radius
        // of distance to the other agents will be to minRadius. Opposite for introverts.
        // Need preferred radiuses to avoid extroverts just bumping into others and extroverts
        // running into walls to increase distance.
        float extraversion = (state.Personality.extraversion + 1f) / 2f; // map to [0, 1]
        float maxRadius = 3f;
        float minRadius = 0.5f;
        float preferredRadius = Mathf.Lerp(maxRadius, minRadius, extraversion);

        // Reward the agent for moving towards desidered distance radius,
        // punish otherwise
        float error = Mathf.Abs(distanceFromTeam - preferredRadius);
        float deltaError = previousError - error;
        // If moving towards preferred radius, delta error will be positive:
        // extrovert will be rewarded, introvert will be punished.
        // If moving away from team, delta error will be negative:
        // extrovert will be punished, introvert will be rewarded
        socializationReward = state.Personality.extraversion * deltaError;

        previousError = error;

        // [OCEAN, Openness] Exploration
        // Reward forward movement when dragon is not in view
        if (!dragonVisible && moveForward > 0 && state.AreDragonsAlive)
        {
            explorationReward = state.Personality.openness * 0.001f * moveForward;
        }

        if (dragonVisible)
        {
            // [OCEAN, Conscientiousness] Impatience
            // If low conscientiousness, rewarded for running towards the dragon
            // directly once it is in view, otherwise punished for it to encourage patience
            float cosine = (float)Mathf.Cos(angleWithDragon * Mathf.Deg2Rad);
            impatienceReward = -0.02f * state.Personality.conscientiousness * moveForward * cosine;

            // [OCEAN, neuroticism] Anxiety
            // If the dragon is visible, then reward the more distance the agent
            // keeps from it the more neurotic it is, punish the more it approaches the dragon
            float normalizedDistance = distanceFromDragon / state.Dungeon.maxDistance;
            anxietyReward = 0.05f * state.Personality.neuroticism * (normalizedDistance - 0.5f);
        }

        // [OCEAN, Neuroticism] Hesitance
        // The more hesitant, the more it is rewarded for moving slowly
        if (state.Personality.neuroticism > 0)
        {
            hesitanceReward = state.Personality.neuroticism * moveForward * -0.005f;
        }

        // Debug.Log(
        //     $"{state.Personality.name}: diligence = {diligenceReward}, socialization = {socializationReward}, exploration = {explorationReward}, impatience = {impatienceReward}, anxiety = {anxietyReward}, hesitance = {hesitanceReward}"
        // );

        return diligenceReward
            + socializationReward
            + explorationReward
            + impatienceReward
            + anxietyReward
            + hesitanceReward;
    }

    public float GetObstacleHitReward()
    {
        return state.RewardSystem.hitWall;
    }

    public float GetAgentHitReward()
    {
        // [OCEAN, conscientiousness] Panic
        // A conscientious agent should be punished for hitting
        // other agents, otherwise a low conscientiousness agent
        // should be prone to panic and hit others
        float panicReward = -0.1f * state.Personality.conscientiousness;

        // [OCEAN, agreeableness] politeness
        // Punish an agreeable agent if it obstructs another agent
        float politenessReward = -1f * state.Personality.agreeableness;

        // Debug.Log(
        //     $"{state.Personality.name}: politeness = {politenessReward}, panic = {panicReward}"
        // );

        return panicReward + politenessReward;
    }
}
