# MindHunt
MindHunt is a Reinforcement Learning environment that aims to simulate how different personality traits can influence the behavior of agents trapped in an escape room where the only way to escape is to slay all the dragons in the arena. Project inspired by [Multi-agent system for Emulating Personality Traits Using Deep Reinforcement Learning](https://mdpi.com/2076-3417/14/24/12068) paper by Georgios Liapis and Ioannis Vlahavas.

Developed as final project of the "Complex Systems: Models and Simulations" course @Unimib.

![](/img/mindhunt.png)

## Environment
The environment is an adaptation of Unity's ML-Agents "Dungeon Escape" environment, that was **rebuilt from the group up** (code and assets) to fit the needs of the project. The environment consists of a square arena with 5 fixed pillars, a door, a cave, and a user selected number of agents and dragons. The dragons always move towards the cave move using the $A^*$.

The only way for the agents to **escape** is to **retrieve the key and unlock the door**, but the key is solely dropped in the environment **if all the dragons have been killed** before they had the chance to reach their lair (the cave). To kill a dragon, the agents must hit it (by collision) a number of times equal to the one set by the user. Once the key is dropped, a timer will start and the agents will need to look for the key and escape before the time ends.


## OCEAN Traits
Each agent is characterized by a set of 5 personality traits, that are inspired by the OCEAN model of personality. The personality of an agent is represented by an array $[\phi^O, \phi^C, \phi^E, \phi^A, \phi^N]$, where each $\phi$ is a value between -1 and 1 that represents how much of that trait is part of the agent's personality.

The traits are:
- Openness: creativity, curiosity, and willingness to entertain new ideas
- Conscientiousness: self-control, diligence, and attention to detail
- Extraversion: sociability, energy
- Agreeableess: willingness to cooperate, helpfulness, kindness
- Neuroticism: proness to anxiety, depression, emotional instability

For example, a personality set to $[0, 0, -1, 0, 1]$ represents an agent that is introverted and highly neurotic.

These personalities are learned by the agents through a set of reward functions that are designed to encourage the agents to behave in accordance with the traits that they are assigned. The following is a description of the functions. The detailed implementation can be found in the `AgentRewardCalculator` class.
- Openness
    - **Exploration**: reward forward movement when dragon is not in view
- Conscientiousness
    - **Impatience**: a conscientious agent should be rewarded for being patient and waiting for the right moment to attack a dragon, while a non conscientious agent should be rewarded for locking onto a dragon from a far distance and run towards it.
    - **Self-control**: a conscientious agent should be punished for hitting other agents, a low conscientiousness agent should be prone to panic and hitting others.
    - **Commitment**: a conscientious agent should not be switching targets while hitting a dragon, until it has been killed, while a non conscientious agent should be easily distracted and not disciplined enough to commit to a single target, so it should be rewarded for switching dragons while hitting them.
    - **Diligence**: if a conscientious agent is holding the key, it should be rewarded for moving directly towards the exit and saving everyone. Unconscientious agents should not be so focused on the task at hand and prefer procrastination and exploration.
- Agreeableness
    - **Politeness**: an agreeable agents should be punished when hitting other agents, a non agreeable agent should be rude and not care about being a nuisance to others.
    - **Cooperation**: an agreeable agent should be rewarded for hitting dragons that are already wounded, helping other agents in battle, while a non agreeable agent will selfishly try to get the kills by itself and will not try to help others.
    - **Heroism**: if urgency is high, meaning that a dragon is close to escaping to the cave, an agreeable agent is rewarded for saving the group and dealing last blow to the dragon.
- Extraversion
    - **Embarassment**: extroverted agents like being the center of attention, so they will get rewarded for grabbing the key when other agents are nearby. Introverted agents don't like the attention, they will be rewarded for grabbing the key when far from the others and punished for doing it when other agents are around
    - **Socialization**: agents have a preferred distance that will be maintained from the rest of the group. The more extroverted an agent is, the smaller the distance will be, while the opposite is true for introverts.
    - **Initiative**: extroverts like taking initiative when in a group, meaing that they will rewarded for hitting dragons when a lot of agents are around. The opposite is true for introverts, they try to avoid being the center of attention and will be rewarded for acting when there are few agents around.
- Neuroticism
    - **Bravery**: a neurotic agent lacks courage to face the dragons, so after every hit inflicted, it will panic and be punished if another hit is inflicted before it has calmed down. The opposite is true for a non neurotic agent.
    - **Anxiety**: if an agent can see a dragon with its eyes, then the more neurotic it is, the more distance it will try to keep from it.
    - **Recklessness** (neurotic only): when urgency increases, a neurotic agent will start to run around faster and not care of its surroundings.
    - **Panic** (neurotic only): when urgency increases, a neurotic agent will panic and start hitting every other agent around.

## Training
The training is done through the MA-POCA algorithm. At the start of each episode:
- The agents are randomly spawned in the arena
- The cave in spawned in a random corner of the arena
- The dragons are spawned on the opposite side of the arena with respect to the cave, to avoid them being too close to their goal prematurely
- The door is spawned along a random wall of the arena

When an agent grabs the key and unlocks the door, the episode ends in success for the group. When a dragon escapes to the cave before the agents can kill it, the episode ends in failure for the group. Agents will need to act accordingly to their personalities, but also collaborate to reach the goal of escaping the dungeon by maximizing the group reward function, that is defined as follows:
- Kill a dragon: +5 
- Key grab: +2 
- Successful escape: +10 
- Failed escape: -15 

Behavior parameters for each agent:
- Observation space: a Ray Perception Sensor with separate tags for the walls, agents, key and dragons. A vector of 2 observations, one value that indicates whether there are still dragons left to slay in the arena, and another value that indicates wheter the agents is holding the key.
- Actions: two continuous actions that translate to forward movement and rotation around the Y axis.