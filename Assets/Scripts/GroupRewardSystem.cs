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
    public float dragonEscape = -15f;
    public float allAgentsEscape = 10f;

    [Header("Combat")]
    public float killDragon = 5f;
}
