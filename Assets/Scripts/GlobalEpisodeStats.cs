using System.Linq;
using UnityEngine;

public enum FailureReason
{
    Timer,
    DragonEscaped,
}

public class GlobalEpisodeStats
{
    private DungeonController dungeon;

    public FailureReason failReason;

    // The outcome of an episode
    public bool win = false;

    // How long an episode lasts
    public float episodeDuration = 0f;

    // Time spent to kill dragons
    public float timeToKillAllDragons = 0f;

    // Time elapsed from key spawn to key being picked up
    public float timeToGrabKey = 0f;

    // Time elapsed from key getting picked up to escaping
    public float timeFromKeyGrabToEscape = 0f;

    public GlobalEpisodeStats(DungeonController dungeon)
    {
        this.dungeon = dungeon;
    }

    public string ToCSVString(int episodeCounter)
    {
        // Make the CSV string
        string[] values = new string[]
        {
            episodeCounter.ToString(),
            win ? "1" : "0",
            !win ? failReason.ToString() : "null",
            episodeDuration.ToString("0.000"),
            timeToKillAllDragons.ToString("0.000"),
            timeToGrabKey.ToString("0.000"),
            timeFromKeyGrabToEscape.ToString("0.000"),
        };

        return string.Join(";", values);
    }

    public void UpdateTimedStats()
    {
        // Time the duration of an episode
        episodeDuration += Time.fixedDeltaTime;

        // Time from start of episode to all dragons being killed
        if (dungeon.remainingDragons > 0)
        {
            timeToKillAllDragons += Time.fixedDeltaTime;
        }

        // Time elapsed from key dropping in the dungeon to
        // an agent picking it up
        if (dungeon.key != null)
        {
            timeToGrabKey += Time.fixedDeltaTime;
        }

        // Time elapsed from key being picked up to escaping the dungeon
        if (dungeon.keyGrabbedByAgent)
        {
            timeFromKeyGrabToEscape += Time.fixedDeltaTime;
        }
    }
}
