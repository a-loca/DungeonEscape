using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RewardSystem", menuName = "Scriptable Objects/Agent Reward System")]
public class RewardSystem : ScriptableObject
{
    [Header("Episode Outcome")]
    public float grabKey = 1f;

    [Header("Combat")]
    public float hitDragon = 2f;

    [Header("Roaming around")]
    public float hitWall = -0.5f;
    public float hitClosedDoor = -1f;
}
