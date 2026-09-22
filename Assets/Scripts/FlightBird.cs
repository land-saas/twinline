using UnityEngine;

[RequireComponent(typeof(Rigidbody2D),typeof(CircleCollider2D))]
public sealed class FlightBird : MonoBehaviour
{
    public TwinFlightGame game;
    public int player;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.collider.GetComponent<FlightBird>()==null)
            game.Crash(player, collision.gameObject.name.Contains("Boundary") ? "Flight boundary" : "Tube impact");
    }
}
