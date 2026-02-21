using System.Linq;
using UnityEngine;

public class AgentRewardCalculator
{
    private IAgentState state;

    // Max radius within which an agent is considered in proximity of another
    private const float MAX_PROXIMITY_RADIUS = 1f;

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

    // ========================================================================
    // General utility functions
    // ========================================================================
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

    // ========================================================================
    // Hittin dragons rewards
    // ========================================================================
    public float GetDragonHitReward(GameObject dragon)
    {
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();

        float initiativeReward = GetInitiativeReward();
        float panicReward = GetPanicReward();
        float coopReward = GetCooperationReward(dragonBehavior);
        float commitmentReward = GetCommitmentReward(dragon);

        latestHitTime = Time.time;
        latestDragonHit = dragon;

        // Debug.Log(
        //     $"{state.Personality.name}: initiative = {initiativeReward}, panic = {panicReward}, cooperation = {coopReward}, commitment = {commitmentReward}"
        // );

        return initiativeReward + panicReward + coopReward + commitmentReward;
    }

    // [OCEAN, extraversion] Initiative
    // If extroverted, rewarded progressively more for every hit inflicted.
    // For introverted agents, reward them for hitting dragons when going on
    // a solo mission. Full reward when solo, reduced when around other
    // agents, punish when crowded
    private const float INITIATIVE_EXTROVERT_SCALE = 0.5f;
    private const float INITIATIVE_INTROVERT_PENALTY_PER_AGENT = 0.5f;
    private const float INITIATIVE_INTROVERT_MAX_AGENT_FACTOR = 1f;

    private float GetInitiativeReward()
    {
        float initiativeReward = 0;

        if (state.Personality.extraversion > 0)
            initiativeReward =
                state.RewardSystem.hitDragon
                * state.Personality.extraversion
                * state.HitsInflicted
                * INITIATIVE_EXTROVERT_SCALE;
        else if (state.Personality.extraversion < 0)
        {
            int nearbyAgents = state.Dungeon.CountNearbyAgents(
                state.Id,
                state.Position,
                MAX_PROXIMITY_RADIUS
            );

            float agentsFactor =
                INITIATIVE_INTROVERT_MAX_AGENT_FACTOR
                - (nearbyAgents * INITIATIVE_INTROVERT_PENALTY_PER_AGENT);
            initiativeReward =
                state.RewardSystem.hitDragon * -state.Personality.extraversion * agentsFactor;
        }

        return initiativeReward;
    }

    // [OCEAN, neuroticism] Panic
    // Punish/reward an agents based on how long it has been since
    // the last hit it has inflicted to a dragon. The more neurotic,
    // the more it is punished for hitting the dragon quickly again
    private const float PANIC_THRESHOLD = 5f;
    private const float PANIC_TIME_SCALE = -0.5f;
    private const float PANIC_SCALE = 10f;
    private const float PANIC_LOG_SCALE = 2f;

    private float GetPanicReward()
    {
        float panicReward = 0;
        float timeDiff = Time.time - latestHitTime;
        if (timeDiff < PANIC_THRESHOLD)
        {
            // Neurotic agent hit a dragon while panicking, big punishment the less
            // time has passed. Non neurotic agent hit a dragon in a small timeframe after
            // the latest hit, reward it
            panicReward =
                -state.Personality.neuroticism
                * Mathf.Exp(PANIC_TIME_SCALE * timeDiff)
                * PANIC_SCALE;
        }
        else
        {
            // Neurotic agent has calmed down after latest hit and now hit the dragon again
            // it will receive increasingly bigger reward the more time has passed
            // Using log to avoid increasing reward or punishment too much
            panicReward =
                state.Personality.neuroticism
                * Mathf.Log(timeDiff - PANIC_THRESHOLD + 1)
                * PANIC_LOG_SCALE;
        }

        return panicReward;
    }

    // [OCEAN, agreeableness] Cooperation
    // Agreeable agent should be rewarded for hitting dragons already
    // wounded by others, non agreeable agent should be rewarded
    // for hitting dragons that have not been hit by others yet
    private const float COOP_SCALE = 0.5f;

    private float GetCooperationReward(DragonBehavior dragonBehavior)
    {
        float coopReward = 0;
        bool dragonHitByOthers = dragonBehavior.whoHitMe.Any(id => id != state.Id);

        if (!dragonHitByOthers && state.Personality.agreeableness < 0)
        {
            // The agent is the only one to have hit the dragon so far
            // Reward non agreeable
            coopReward = state.RewardSystem.hitDragon * (1 - state.Personality.agreeableness);
        }
        else
        {
            // Some other agent hit the dragon before
            // Reward agreeable agent, punish non agreeable agent
            coopReward =
                state.RewardSystem.hitDragon * state.Personality.agreeableness * COOP_SCALE;
        }

        return coopReward;
    }

    // [OCEAN, Conscientiousness] Commitment
    // A conscientious agent should not be switching targets while hitting
    // dragons until it has been killed, while a non conscientious agent
    // should be easily distracted and not disciplined and constantly switch
    private const float COMMITMENT_BASE_HIT_PERCENT = 0.2f;

    private float GetCommitmentReward(GameObject dragon)
    {
        float factor = state.RewardSystem.hitDragon * COMMITMENT_BASE_HIT_PERCENT;
        float switchFactor = dragon != latestDragonHit ? -1f : 1f;
        float commitmentReward = factor * state.Personality.conscientiousness * switchFactor;

        return commitmentReward;
    }

    // ========================================================================
    // Key grab rewards
    // ========================================================================

    public void RewardKeyGrab() { }

    // ========================================================================
    // Step rewards
    // ========================================================================
    public float GetStepReward(float moveForward)
    {
        var (dragonVisible, distanceFromDragon, angleWithDragon) =
            state.RaysHelper.CanSeeObjectWithTag("Dragon");

        // Debug.Log(
        //     $"Testing: dragonVisible = {dragonVisible}, distanceFromDragon = {distanceFromDragon}, angleWithDragon = {angleWithDragon}"
        // );

        float diligenceReward = GetDiligenceReward();
        float socializationReward = GetSocializationReward();
        float explorationReward = GetExplorationReward(dragonVisible, moveForward);
        float impatienceReward = GetImpatienceReward(dragonVisible, angleWithDragon, moveForward);
        float anxietyReward = GetAnxietyReward(dragonVisible, distanceFromDragon);
        float hesitanceReward = GetHesitanceReward(moveForward);

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

    // [OCEAN, conscientiousness] Diligence
    // If a conscientious agent has the key, then it should be rewarded
    // for going directly towards the exit and save everyone
    private const float DILIGENCE_SCALE = 0.1f;

    private float GetDiligenceReward()
    {
        float diligenceReward = 0;

        if (state.HasKey && state.Personality.conscientiousness > 0)
        {
            float distanceFromExit = state.Dungeon.NormalizedDistanceFromExit(state.Position);
            float deltaDistance = previousDistanceFromExit - distanceFromExit;
            diligenceReward = state.Personality.conscientiousness * deltaDistance * DILIGENCE_SCALE;

            previousDistanceFromExit = distanceFromExit;
        }

        return diligenceReward;
    }

    // [OCEAN, extraversion] Socialization
    // Reward introverted agent for moving away from other agents,
    // reward extroverted agent for moving closer to other agents
    private const float SOCIALIZATION_MAX_RADIUS = 3f;
    private const float SOCIALIZATION_MIN_RADIUS = 0.5f;

    private float GetSocializationReward()
    {
        float socializationReward = 0;

        float distanceFromTeam = state.Dungeon.GetAgentDistanceFromTeam(state.Id, state.Position);

        // The more the agent is extroverted, the closer the preferred radius
        // of distance to the other agents will be to minRadius. Opposite for introverts.
        // Need preferred radiuses to avoid extroverts just bumping into others and extroverts
        // running into walls to increase distance.
        float extraversion = (state.Personality.extraversion + 1f) / 2f; // map to [0, 1]
        float preferredRadius = Mathf.Lerp(
            SOCIALIZATION_MAX_RADIUS,
            SOCIALIZATION_MIN_RADIUS,
            extraversion
        );

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

        return socializationReward;
    }

    // [OCEAN, Openness] Exploration
    // Reward forward movement when dragon is not in view
    private const float EXPLORATION_SCALE = 0.001f;

    private float GetExplorationReward(bool dragonVisible, float moveForward)
    {
        float explorationReward = 0;

        if (!dragonVisible && moveForward > 0 && state.AreDragonsAlive)
        {
            explorationReward = state.Personality.openness * EXPLORATION_SCALE * moveForward;
        }

        return explorationReward;
    }

    // [OCEAN, Conscientiousness] Impatience
    // If low conscientiousness, rewarded for running towards the dragon when visible
    // directly once it is in view, otherwise punished for it to encourage patience
    private const float IMPATIENCE_SCALE = -0.02f;

    private float GetImpatienceReward(bool dragonVisible, float angleWithDragon, float moveForward)
    {
        float impatienceReward = 0;

        if (dragonVisible)
        {
            float cosine = Mathf.Cos(angleWithDragon * Mathf.Deg2Rad);
            impatienceReward =
                IMPATIENCE_SCALE * state.Personality.conscientiousness * moveForward * cosine;
        }

        return impatienceReward;
    }

    // [OCEAN, neuroticism] Anxiety
    // If the dragon is visible, then reward the more distance the agent
    // keeps from it the more neurotic it is, punish the more it approaches the dragon
    private float ANXIETY_SCALE = 0.05f;

    private float GetAnxietyReward(bool dragonVisible, float distanceFromDragon)
    {
        float anxietyReward = 0;
        if (dragonVisible)
        {
            float normalizedDistance = distanceFromDragon / state.Dungeon.maxDistance;
            anxietyReward =
                ANXIETY_SCALE * state.Personality.neuroticism * (normalizedDistance - 0.5f);
        }

        return anxietyReward;
    }

    // [OCEAN, Neuroticism] Hesitance
    // The more hesitant, the more it is rewarded for moving slowly
    private const float HESITANCE_SCALE = -0.005f;

    private float GetHesitanceReward(float moveForward)
    {
        float hesitanceReward = 0;
        if (state.Personality.neuroticism > 0)
        {
            hesitanceReward = state.Personality.neuroticism * moveForward * HESITANCE_SCALE;
        }

        return hesitanceReward;
    }

    // ========================================================================
    // Hitting obstacles rewards
    // ========================================================================
    public float GetObstacleHitReward()
    {
        return state.RewardSystem.hitWall;
    }

    // ========================================================================
    // Hitting agents rewards
    // ========================================================================
    public float GetAgentHitReward()
    {
        float selfControlReward = GetSelfControlReward();
        float politenessReward = GetPolitenessReward();

        // Debug.Log(
        //     $"{state.Personality.name}: politeness = {politenessReward}, panic = {panicReward}"
        // );

        return selfControlReward + politenessReward;
    }

    // [OCEAN, conscientiousness] Self control
    // A conscientious agent should be punished for hitting
    // other agents, otherwise a low conscientiousness agent
    // should be prone to panic and hit others
    private const float SELF_CONTROL_SCALE = -0.1f;

    private float GetSelfControlReward()
    {
        return SELF_CONTROL_SCALE * state.Personality.conscientiousness;
    }

    // [OCEAN, agreeableness] Politeness
    // Punish an agreeable agent if it obstructs another agent
    private const float POLITENESS_SCALE = -1f;

    private float GetPolitenessReward()
    {
        return POLITENESS_SCALE * state.Personality.agreeableness;
    }
}
