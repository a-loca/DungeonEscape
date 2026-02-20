using UnityEngine;

public interface IAgentState
{
    int Id { get; }
    bool HasKey { get; }
    bool AreDragonsAlive { get; }
    int HitsInflicted { get; }
    Vector3 Position { get; }
    Personality Personality { get; }
    RewardSystem RewardSystem { get; }
    DungeonController Dungeon { get; }
    RaysHelper RaysHelper { get; }
}
