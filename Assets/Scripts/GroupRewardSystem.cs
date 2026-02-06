using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GroupRewardSystem",
    menuName = "Scriptable Objects/Group Reward System"
)]
public class GroupRewardSystem : ScriptableObject
{
    [Header("Episode Outcome")]
    public float dragonEscape = -10f;
    public float allAgentsEscape = 10f;
}
