using System;
using UnityEngine;

public class Timer : MonoBehaviour
{
    private float remainingTime;
    public event Action onTimerEndEvent;
    private bool isActive = false;

    public void StartTimer(float time)
    {
        remainingTime = time;
        isActive = true;
        Debug.Log($"Timer started, {time}s to escape.");
    }

    public void StopTimer()
    {
        isActive = false;
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
                Debug.Log("Time is up!");
                if (onTimerEndEvent != null)
                    onTimerEndEvent();
                isActive = false;
            }
        }
    }
}
