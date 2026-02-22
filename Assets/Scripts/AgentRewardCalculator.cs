using System.Linq;
using UnityEngine;

public class AgentRewardCalculator
{
    private IAgentState state;

    // ========================================================================
    // Fields to keep track of episode state for reward calculations
    // ========================================================================

    // The last dragon that was hit
    private GameObject latestDragonHit;

    // Time of the last hit inflicted to a dragon
    private float latestHitTime;

    // Distance from team centroid at previous step
    private float previousError = 0f;

    // How far the agent was from the door on previous step
    private float previousDistanceFromExit = 0f;

    // Position where the key was grabbed, used for diligence reward
    private Vector3 keyGrabPosition;
    private float maxDistanceReachedFromKeyGrab = 0f;

    // ========================================================================
    // General constants
    // ========================================================================
    private const float INTROVERT_PREFERRED_DISTANCE = 3f;
    private const float EXTROVERT_PREFERRED_DISTANCE = 1f;

    // ========================================================================
    // Initialization
    // ========================================================================
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
    public float GetDragonHitReward(GameObject dragon, int livesLeft)
    {
        DragonBehavior dragonBehavior = dragon.GetComponent<DragonBehavior>();

        float initiativeReward = GetInitiativeReward();
        float braveryReward = GetBraveryReward();
        float coopReward = GetCooperationReward(dragonBehavior);
        float commitmentReward = GetCommitmentReward(dragon);
        float heroismReward = GetHeroismReward(livesLeft);

        latestHitTime = Time.time;
        latestDragonHit = dragon;

        // Debug.Log(
        //     $"{state.Personality.name}: initiative = {initiativeReward}, bravery = {braveryReward}, cooperation = {coopReward}, commitment = {commitmentReward}"
        // );

        return initiativeReward + braveryReward + coopReward + commitmentReward;
    }

    // [OCEAN, extraversion] Initiative
    // Extroverted agents like taking initiative when in a group,
    // which means that they will rewarded for hitting dragons when a lot
    // of agents are around. The opposite is true for introverts, they don't
    // like the attention and will be punished for acting in group

    private float GetInitiativeReward()
    {
        float initiativeReward = 0;

        // Get preferred distance based on how extroverted the agent is
        // The more the agent is introverted, the bigger the radius.
        // The more the agent is extroverted, the smaller the radius
        float preferredRadius = Mathf.Lerp(
            INTROVERT_PREFERRED_DISTANCE,
            EXTROVERT_PREFERRED_DISTANCE,
            (state.Personality.extraversion + 1f) / 2f // [0, 1]
        );

        float density = state.Dungeon.GetAgentDensityWithinRadius(
            state.Id,
            state.Position,
            preferredRadius
        );

        // Let extraversion = -1
        // If density = 0, then full reward
        // If density = 1, then - full reward
        // Opposite for extroverts
        initiativeReward =
            state.RewardSystem.hitDragon * state.Personality.extraversion * (2f * density - 1f);

        return initiativeReward;
    }

    // [OCEAN, neuroticism] Bravery
    // Punish/reward an agents based on how long it has been since
    // the last hit it has inflicted to a dragon. The more neurotic,
    // the more it is punished for hitting the dragon quickly again
    private const float BRAVERY_THRESHOLD = 5f;
    private const float BRAVERY_TIME_SCALE = -0.5f;
    private const float BRAVERY_SCALE = 10f;
    private const float BRAVERY_LOG_SCALE = 2f;

    private float GetBraveryReward()
    {
        float braveryReward = 0;
        float timeDiff = Time.time - latestHitTime;
        if (timeDiff < BRAVERY_THRESHOLD)
        {
            // Neurotic agent hit a dragon while panicking, big punishment the less
            // time has passed. Non neurotic agent hit a dragon in a small timeframe after
            // the latest hit, reward it
            braveryReward =
                -state.Personality.neuroticism
                * Mathf.Exp(BRAVERY_TIME_SCALE * timeDiff)
                * BRAVERY_SCALE;
        }
        else
        {
            // Neurotic agent has calmed down after latest hit and now hit the dragon again
            // it will receive increasingly bigger reward the more time has passed
            // Using log to avoid increasing reward or punishment too much
            braveryReward =
                state.Personality.neuroticism
                * Mathf.Log(timeDiff - BRAVERY_THRESHOLD + 1)
                * BRAVERY_LOG_SCALE;
        }

        return braveryReward;
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

    // [OCEAN, agreeableness] Heroism
    // If urgency is high, an agreeable agent should be enticed to deal the
    // final blow to the remaining dragon to help saving the team,
    // while a non agreeable agent should not be rewarded for being helpful
    private float GetHeroismReward(int livesLeft)
    {
        float heroismReward = 0;
        if (livesLeft == 0)
        {
            // The agent gets an additional hitDragon reward for landing
            // the final blow when it matters the most, if agreeable.
            // If not agreeable, it will actively try not to be the one
            // to save the team and will be punished for killing the dragon
            heroismReward =
                Mathf.Pow(state.Dungeon.urgency, 2)
                * state.Personality.agreeableness
                * state.RewardSystem.hitDragon;
        }
        return heroismReward;
    }

    // ========================================================================
    // Key grab rewards
    // ========================================================================

    public float GetKeyGrabReward()
    {
        float embarassmentReward = GetEmbarassmentReward();

        // Register the position where the key was grabbed for conscientiousness
        // reward after grabbing key
        keyGrabPosition = state.Position;

        return embarassmentReward;
    }

    // [OCEAN, Extraversion] Embarassment
    // Extroverted agents like being the center of attention,
    // so they will get rewarded for grabbing the key when other agents are
    // nearby. Introverted agents don't like the attention, they will be rewarded
    // for grabbing the key when far from the others and punished for doing it when
    // other agents are around
    private float GetEmbarassmentReward()
    {
        float embarassmentReward = 0;
        float extraversion = (state.Personality.extraversion + 1f) / 2f; // map to [0, 1]

        // Get preferred radius based on how extroverted the agent is
        float preferredRadius = Mathf.Lerp(
            INTROVERT_PREFERRED_DISTANCE,
            EXTROVERT_PREFERRED_DISTANCE,
            extraversion
        );

        // Count the agents within the preferred radius
        float density = state.Dungeon.GetAgentDensityWithinRadius(
            state.Id,
            state.Position,
            preferredRadius
        );

        // If introverted, the more dense of agent is the radius, the more
        // the agent is rewarded for pickup up a key, and punished if no one
        // is nearby. Opposite for introverts

        embarassmentReward =
            state.RewardSystem.grabKey * state.Personality.extraversion * (2f * density - 1f);

        return embarassmentReward;
    }

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
        float recklessnessReward = GetRecklessnessReward(moveForward);

        // Debug.Log(
        //     $"{state.Personality.name}: diligence = {diligenceReward}, socialization = {socializationReward}, exploration = {explorationReward}, impatience = {impatienceReward}, anxiety = {anxietyReward}, panicSpeed = {panicSpeedReward}"
        // );

        return diligenceReward
            + socializationReward
            + explorationReward
            + impatienceReward
            + anxietyReward
            + recklessnessReward;
    }

    // [OCEAN, conscientiousness] Diligence
    // If a conscientious agent has the key, then it should be rewarded
    // for going directly towards the exit and save everyone. Unconscientious
    // agents won't care, so they will get rewarded for just exploring around
    // and procrastinating the escape from the room
    private const float DILIGENCE_SCALE = 0.1f;

    private float GetDiligenceReward()
    {
        if (!state.HasKey)
            return 0;

        float diligenceReward = 0;

        if (state.Personality.conscientiousness > 0)
        {
            // Reward moving towards exit
            float distanceFromExit = state.Dungeon.NormalizedDistanceFromExit(state.Position);
            float deltaDistance = previousDistanceFromExit - distanceFromExit;
            diligenceReward = state.Personality.conscientiousness * deltaDistance * DILIGENCE_SCALE;

            previousDistanceFromExit = distanceFromExit;
        }
        else
        {
            // Reward moving away from where the key was grabbed,
            // to encourage procrastination and exploration. If the
            // current position is the furthest from the key grab position
            // so far, then reward the agent
            float distanceFromKeyGrabPosition = Vector3.Distance(state.Position, keyGrabPosition);

            float delta = distanceFromKeyGrabPosition - maxDistanceReachedFromKeyGrab;

            if (delta > 0)
            {
                // The current position is the furthest so far, reward
                diligenceReward =
                    -1f * state.Personality.conscientiousness * delta * DILIGENCE_SCALE;
                maxDistanceReachedFromKeyGrab = distanceFromKeyGrabPosition;
            }
        }

        return diligenceReward;
    }

    // [OCEAN, extraversion] Socialization
    // Reward introverted agent for moving away from other agents,
    // reward extroverted agent for moving closer to other agents
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
            INTROVERT_PREFERRED_DISTANCE,
            EXTROVERT_PREFERRED_DISTANCE,
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

    // [OCEAN, neuroticism] Recklessness
    // If the urgency is high and agent is neurotic,
    // then reward the faster the agent moves
    private const float RECKLESSNESS_SCALE = 0.001f;

    private float GetRecklessnessReward(float moveForward)
    {
        float recklessnessReward = 0;

        if (state.Personality.neuroticism > 0)
        {
            // The more urgency and the more neurotic
            // the more forward speed is rewarded
            recklessnessReward =
                state.Dungeon.urgency
                * moveForward
                * state.Personality.neuroticism
                * RECKLESSNESS_SCALE;
        }

        return recklessnessReward;
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
        float panicReward = GetPanicReward();

        // Debug.Log(
        //     $"{state.Personality.name}: politeness = {politenessReward}, panic = {panicReward}"
        // );

        return selfControlReward + politenessReward + panicReward;
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
    private const float POLITENESS_SCALE = -0.1f;

    private float GetPolitenessReward()
    {
        return POLITENESS_SCALE * state.Personality.agreeableness;
    }

    // [OCEAN, neuroticism] Panic
    // A neurotic agent should loose control and start hitting others
    // once urgency rises.
    private const float PANIC_SCALE = 0.1f;

    private float GetPanicReward()
    {
        float panicReward = 0;

        if (state.Personality.neuroticism > 0)
        {
            panicReward = state.Dungeon.urgency * state.Personality.neuroticism * PANIC_SCALE;
        }

        return panicReward;
    }
}
