using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class SwingPlayer : MonoBehaviour
{
    public TwinSwingGame game;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.collider.isTrigger) return;
        game.Crash(collision.gameObject.name.Contains("Boundary") ? "Flight boundary" : "Obstacle impact");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.TryGetComponent(out TwinSwingGame.GemPickup gem)) game.CollectGem(gem);
    }
}
