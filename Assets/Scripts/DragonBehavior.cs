using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DragonBehavior : MonoBehaviour
{
    public event Action onDragonEscapeEvent;
    private NavMeshAgent meshAgent;
    private GameObject cave;
    private Renderer renderer;
    private Color originalColor;
    public int maxLives = 3;
    private int lives;

    void Start()
    {
        lives = maxLives;

        // Get the dragon's color to allow flashing
        // when taking a hit and getting back to original color
        renderer = transform.GetChild(0).GetComponent<Renderer>();
        originalColor = renderer.material.color;
    }

    public void FullHeal()
    {
        lives = maxLives;
    }

    public void SetCave(GameObject cave)
    {
        this.cave = cave;

        meshAgent = GetComponent<NavMeshAgent>();

        // Instruct the dragon to walk towards the cave
        meshAgent.SetDestination(cave.transform.position);

        // Turn the dragon towards the cave
        transform.LookAt(cave.transform);
    }

    void OnCollisionEnter(Collision collision)
    {
        // When the dragon reaches its destination the episode should end
        if (collision.gameObject.name == cave.name)
        {
            if (onDragonEscapeEvent != null)
                onDragonEscapeEvent();
        }
    }

    public int TakeAHit()
    {
        // Agent hit the dragon

        // Take damage
        lives--;

        // Flash red to indicate the damage taken
        Flash();
        Debug.Log($"Dragon took a hit! {lives} lives left.");

        // Check if the dragon has been slain
        if (lives == 0)
        {
            Debug.Log("Dragon slain!");
            Die();
        }

        return lives;
    }

    public void Die()
    {
        meshAgent.isStopped = true;
        gameObject.SetActive(false);
    }

    public void Resuscitate()
    {
        renderer.material.color = originalColor;
        gameObject.SetActive(true);
        meshAgent.SetDestination(cave.transform.position);
        meshAgent.isStopped = false;
    }

    private void Flash()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRed());
    }

    private IEnumerator FlashRed()
    {
        float flashTimeInSeconds = 0.1f;

        // The first time the function is called by the coroutine
        // the material will turn red
        renderer.material.color = Color.red;

        // Wait for established time, then call again the function
        yield return new WaitForSeconds(flashTimeInSeconds);

        // The second time the function is called, the original
        // color of the dragon will be restored
        renderer.material.color = originalColor;
    }
}
