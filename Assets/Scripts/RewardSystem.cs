using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RewardSystem", menuName = "Scriptable Objects/Agent Reward System")]
public class RewardSystem : ScriptableObject
{
    [Header("Episode Outcome")]
    public float failEscape = -10f;
    public float escape = 10f;

    [Header("Combat")]
    public float hitDragon = 2f;
}
