# Twinline

Minimal black-and-white cooperative flight for two players on one keyboard. One shared camera follows a solid circle and a hollow circle connected by a permanent physical rope. The view has no divider, boundary lines, HUD frames, or progress bars.

## Play

Open **Twinline.app**, or choose **Twinline** in Unity Hub (Unity 6000.3.24f1) and press Play in `Assets/Scenes/Flight.unity`.

| Input | Action |
| --- | --- |
| W | Player 1 / solid circle: tap to lift, hold to reel |
| Up Arrow | Player 2 / hollow circle: tap to lift, hold to reel |
| Release your key | Let the rope extend and keep your momentum |
| Space | Begin, retry, or resume |
| Escape | Pause / resume |

Before pressing Space, try W and Up Arrow in the safe ready-screen playground: the real rope responds, the circles spring back toward the middle, and tubes stay still. Space resets both circles into a clean flight start.

Every new press gives one lift impulse against the current gravity. After 0.18 seconds, holding starts reeling. Keep the same key down to bring the circles progressively closer: a quick initial pull is followed by slower, finer tightening. The minimum center-to-center distance is 0.65 units, leaving a visible gap between the circles. It does not repeat the lift. One player can hold to bring a falling partner closer while the other taps to keep the pair airborne. Both holding reels faster, but neither gets repeated lift: holding forever is not a way to hover.

Release to give each other space and arc back toward your lanes. Keep some room before the next tube: pulling at the wrong time can also drag your partner into a wall. Both circles must clear each tube to score. Either hitting an obstacle ends the shared run; Space retries. Losing focus pauses and clears held input.

Before **every** gravity reversal, a large, central **two-second countdown** shows the upcoming gravity direction, and the direction your next tap will push. Two short audio beats accompany the countdown. Tubes slow during this warning to leave reaction room; the circles keep their normal physics. The countdown stops while paused. Your key stays the same and always pushes against gravity. Existing velocity survives the reversal. A small chevron shows gravity direction.

## Quiet guidance

The ready playground shows each circle's key, one tap/hold instruction, and the start control. Key labels fade in flight. The initial hint explicitly says to hold the same key longer to pull closer. The score is larger, and both the start countdown and gravity countdown use prominent pulsing numerals. Short contextual hints introduce pulling and release. Those tutorials do not repeat after every retry within the session. Gravity warnings are separate and always appear before every flip; other hints cannot replace them. Pause and failure show only the relevant resume/retry control. There are no large title or results panels.

The active rope becomes slightly thicker and brighter. A small grip ring appears only while holding; brief impulse pulses provide feedback. There are no idle rings or moving chevrons. Release produces a brief wave and a visible swing apart. All graphics remain generated monochrome circles, rectangles and lines.

## Physics

Both circles stay Dynamic Rigidbody2D objects. Only rotation is frozen. Soft horizontal restoring forces provide familiar rear/lead lanes while allowing the rope to move both circles sideways. A maximum-distance DistanceJoint2D stays connected at all times.

Holding first takes up unused slack without moving the bodies, then shortens the rope from a maximum of 2.8 to 0.65 units. A single player reels at 4.2 units/second until the rope reaches 1.3 units, then at 0.65 units/second for controlled tightening; two players reel 35% faster. The holding player gets mild velocity resistance to brace. Release pays out at 3.2 units/second. Lower horizontal damping lets the return swing remain visible. The joint supplies the pull; release does not add an artificial launch impulse, teleport, reset velocity, or change body types. Lane forces supply the gentle return toward the flight lanes.

Tube scoring and gravity-fault clearance use the actual positions of both players. Tube gaps narrow from 3.65 to 3.4 units; this leaves extra clearance for cooperative sideways motion. Tubes still recycle from a fixed pool. The rendered rope has cosmetic sag; rope wrapping and collision with tubes are not simulated.

## Tuning and validation

Select `TWINLINE — press Play` in the scene to adjust gravity, lift, rope length, reel length/speed, release speed, hold threshold, horizontal spring/damping, tube gap and scroll speed. `Twinline/Validate Flight Mechanics` runs the deterministic physics checks outside Play mode. `Twinline/Build Mac App` builds the app beside this project.

Stop an existing Unity Play session before judging structural changes: the world is created when Play starts. The requested build workflow now leaves an old Play session before validating and rebuilding.

See [`Validation.txt`](../Validation.txt) for the latest results. Checks include a single full-screen camera, continued tightening from about 1.18 units at 0.6 seconds to 0.65 units at 1.5 seconds without another key press, clean exit from the ready playground, a complete two-second warning before every reversal, passive flight, a route with alternating partner reels, actual rescue displacement, two-player sustained holds, maximum rope extension, momentum on release/reversal, paired scoring, pause and clean retry. Automated routes test stability and clearance; two humans still need to judge timing and enjoyment.

See [`Design-notes.md`](../Design-notes.md) for the research and playtest questions behind this revision.

`Twinline/Capture Gameplay Checks` runs an editor-only input playback and saves eight actual rendered frames beside the project in `Twinline-checks`. It covers the ready view, short and long holds, release, the start countdown, both flip-countdown beats, and the completed flip.
