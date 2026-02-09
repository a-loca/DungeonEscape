using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DragonBehavior : MonoBehaviour
{
    public event Action onDragonEscapeEvent;
    public event Action onDragonSlainEvent;
    private NavMeshAgent meshAgent;
    private GameObject cave;
    private Renderer dragonRenderer;
    private Color originalColor;
    public int maxLives = 3;
    private int lives;

    void Awake()
    {
        // Get the dragon's color to allow flashing
        // when taking a hit and getting back to original color
        dragonRenderer = transform.GetChild(0).GetComponent<Renderer>();
        originalColor = dragonRenderer.material.color;

        meshAgent = GetComponent<NavMeshAgent>();
    }

    public void SetCave(GameObject cave)
    {
        this.cave = cave;
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
            Die();
        }

        return lives;
    }

    public void Die()
    {
        Debug.Log("Dragon slain!");

        // TOGGLE: step 3

        if (onDragonSlainEvent != null)
            onDragonSlainEvent();

        StopWalking();
    }

    public void Resuscitate(Vector3 position)
    {
        meshAgent.Warp(position);
        transform.LookAt(cave.transform);

        // Heal lives
        lives = maxLives;

        // Turn color back to origin after the hit that
        // killed the dragon turned it to red
        // Needed because coroutines are stopped once SetActive(false)
        // is called, so the dragon will stay red
        dragonRenderer.material.color = originalColor;

        // Start navmesh again from the new spawn point
        gameObject.SetActive(true);

        // TOGGLE: phase 3
        StartWalking();
    }

    public void StartWalking()
    {
        meshAgent.enabled = true;

        // Instruct the dragon to walk towards the cave
        meshAgent.SetDestination(cave.transform.position);

        // Turn the dragon towards the cave
        // transform.LookAt(cave.transform);
        meshAgent.updateRotation = true;

        meshAgent.isStopped = false;
    }

    public void StopWalking()
    {
        // Reset navmesh path computed at the beginning of the episode
        meshAgent.ResetPath();

        // Stop walking
        meshAgent.isStopped = true;
        meshAgent.enabled = false;

        // Hide the dragon
        gameObject.SetActive(false);
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
        dragonRenderer.material.color = Color.red;

        // Wait for established time, then call again the function
        yield return new WaitForSeconds(flashTimeInSeconds);

        // The second time the function is called, the original
        // color of the dragon will be restored
        dragonRenderer.material.color = originalColor;
    }
}
