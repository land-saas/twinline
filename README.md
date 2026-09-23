# Twinline

Swing on a rope. Collect gems. Let go at the right moment.

A minimal one-button prototype: tap **Space** near a node to attach or release. When a swing slows down, hold Space and release it to restore momentum. The world scrolls; gather gems without hitting the floor or ceiling.

![Twinline shared-camera gameplay](docs/images/ready.png)

## Play

Download **Twinline-macOS-v0.1.0.zip** from the [Releases page](https://github.com/land-saas/twinline/releases), unzip it, and open **Twinline.app**. Unity is not needed to play.

- **macOS 12 or later**, Intel or Apple silicon.
- This prototype is ad-hoc signed, not Apple-notarized; macOS may ask you to approve opening a downloaded app.

| Key | Action |
| --- | --- |
| **Space** | Begin/retry; tap to attach or release; hold while attached and release to restore momentum |

**Swing mode (default):** one circle, pendulum physics, gems for score. Nodes glow when you're close enough to attach.

## Open the Unity project

1. Clone or download the source.
2. In Unity Hub, choose **Add project from disk** and select the folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open with **Unity 6000.3.24f1**.
4. Open `Assets/Scenes/Flight.unity` and press **Play**.

The scene builds its player, nodes, gems, and sound at runtime. No external art is required.

## Prototype status

The active prototype is **rope swinging + gem collection**. The earlier co-op implementation remains in the source for reference but is not part of the one-button game.

- [Co-op flight mechanics](docs/Mechanics.md)
- [Design research and playtest questions](Design-notes.md)

Run **Twinline → Validate Flight Mechanics** to repeat co-op physics checks (spawns a validation instance if the scene is in swing mode).
