using System;
using UnityEngine;

public class Timer : MonoBehaviour
{
    private float remainingTime;
    public event Action onTimerEndEvent;
    private bool isActive = false;
    public float maxTime;

    public void StartTimer(float time)
    {
        maxTime = time;
        remainingTime = time;
        isActive = true;
        // Debug.Log($"Timer started, {time}s to escape.");
    }

    public void StopTimer()
    {
        isActive = false;
    }

    public float TimeLeft()
    {
        return remainingTime;
    }

    public float TimeElapsed()
    {
        return maxTime - remainingTime;
    }

    void Update()
    {
        if (isActive)
        {
            if (remainingTime > 0)
            {
                remainingTime -= Time.deltaTime;
            }
            else
            {
                // Debug.Log("Time is up!");
                if (onTimerEndEvent != null)
                    onTimerEndEvent();
                isActive = false;
            }
        }
    }
}
