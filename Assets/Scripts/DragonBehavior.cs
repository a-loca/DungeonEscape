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

    void Awake()
    {
        // Get the dragon's color to allow flashing
        // when taking a hit and getting back to original color
        renderer = transform.GetChild(0).GetComponent<Renderer>();
        originalColor = renderer.material.color;

        meshAgent = GetComponent<NavMeshAgent>();
    }

    public void SetCave(GameObject cave)
    {
        this.cave = cave;
    }

    public void StartWalking()
    {
        // Instruct the dragon to walk towards the cave
        meshAgent.SetDestination(cave.transform.position);

        // Turn the dragon towards the cave
        transform.LookAt(cave.transform);

        meshAgent.isStopped = false;
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
        // Stop walking
        meshAgent.isStopped = true;

        // Hide the dragon
        gameObject.SetActive(false);
    }

    public void Resuscitate()
    {
        // Heal lives
        lives = maxLives;

        // Turn color back to origin after the hit that
        // killed the dragon turned it to red
        // Needed because coroutines are stopped once SetActive(false)
        // is called, so the dragon will stay red
        renderer.material.color = originalColor;

        // Start navmesh again from the new spawn point
        gameObject.SetActive(true);
        StartWalking();
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
