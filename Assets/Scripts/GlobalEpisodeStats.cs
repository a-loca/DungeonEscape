public class GlobalEpisodeStats
{
    public enum FailureReason
    {
        Timer,
        DragonEscaped,
    }

    public FailureReason FailReason;
    public bool win;
    public float episodeDuration;
    public float timeToKillAllDragons;
    public float timeToPickupKey;
    public float timeToEscapeFromGettingKey;
    public int numberOfDragons;
    public int numberOfAgents;

    public string ToCSVString(int episodeNumber)
    {
        return "";
    }
}