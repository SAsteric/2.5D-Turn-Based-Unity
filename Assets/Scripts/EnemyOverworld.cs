using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes

public class EnemyOverworld : MonoBehaviour
{
    [SerializeField] private string battleSceneName = "BattleScene";

    // This runs when another object enters this object's Trigger Collider
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that hit the enemy is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player detected! Switching to battle...");
            SceneManager.LoadScene(battleSceneName);
        }
    }

    // Use this if you are using Physics Collisions instead of Triggers
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            SceneManager.LoadScene(battleSceneName);
        }
    }
}