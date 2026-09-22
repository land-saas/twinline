# Twinline

Two circles. One rope. Keep each other flying.

A minimal black-and-white co-op game for **two players on one keyboard**. Fly through tubes, pull your partner closer, and adapt when gravity reverses. Both players share one continuous camera view.

![Twinline shared-camera gameplay](docs/images/ready.png)

## Play

Download **Twinline-macOS-v0.1.0.zip** from this repository's Releases page, unzip it, and open **Twinline.app**. Unity is not needed to play.

- **macOS 12 or later**, Intel or Apple silicon.
- Local two-player play on one keyboard.
- This prototype is ad-hoc signed, not Apple-notarized; macOS may ask you to approve opening a downloaded app. Only open a copy from the release you trust.

| Key | Action |
| --- | --- |
| **W** | Solid circle: tap to lift; hold to pull closer |
| **↑** | Hollow circle: tap to lift; hold to pull closer |
| Release | Let the rope extend and swing apart |
| **Space** | Begin, retry, or resume |
| **Esc** | Pause / resume |

Try the rope on the safe opening screen before pressing Space. **Hold the same key longer to bring the circles closer.** Holding does not repeat the lift; coordinate so one player pulls while the other taps.

Both circles must clear a tube to score. A large two-second warning announces **every gravity reversal**. Your key stays the same and always lifts against the current gravity.

## Open the Unity project

1. Clone or download the source.
2. In Unity Hub, choose **Add project from disk** and select the folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open with **Unity 6000.3.24f1**.
4. Open `Assets/Scenes/Flight.unity` and press **Play**.

The scene creates its circles, rope, tubes, and sound at runtime. No external art or asset downloads are required. From the editor, **Twinline → Build Mac App** builds `Twinline.app` beside the project; install the matching Mac build-support module if needed.

## Prototype status

Version **0.1.0** includes shared-camera flight, progressive rope reeling, paired scoring, gravity warnings, pause, and retry. Both circles remain dynamic Rigidbody2D bodies joined by a permanent maximum-distance joint. The rope itself does not collide with or wrap around tubes.

Physics checks passed for two 36-tube routes, continuous holds, partner rescue, momentum, pause, restart, and all 18 scheduled gravity warnings. Eight rendered gameplay states were inspected. These checks establish stability; two human players still need to judge timing and enjoyment.

- [Controls, physics, and tuning](docs/Mechanics.md)
- [Design research and playtest questions](Design-notes.md)
- [Validation results](Validation.txt)

Run **Twinline → Validate Flight Mechanics** outside Play mode to repeat the physics checks. **Twinline → Capture Gameplay Checks** captures actual gameplay frames beside the project.
