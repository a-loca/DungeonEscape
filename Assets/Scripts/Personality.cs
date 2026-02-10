using UnityEngine;

[CreateAssetMenu(fileName = "Personality", menuName = "Scriptable Objects/OCEAN Personality")]
public class Personality : ScriptableObject
{
    public string personalityName;

    [Header("OCEAN Personality")]
    public float openness;
    public float conscientiousness;
    public float extraversion;
    public float agreeableness;
    public float neuroticism;
}
