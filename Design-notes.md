# Making the rope useful

## Research

Bread & Fred's official description makes the rope a way to swing and propel a partner, with one player anchoring while the other moves. The useful lesson for Twinline is a deliberate support role: a partner's action should create a movement opportunity. [Official game description](https://store.steampowered.com/app/1607680/Bread__Fred/?l=english).

EA describes Unravel Two's partners swinging from each other's thread and launching each other through the air. This suggests making the connection a tool that players can act on together. [EA: What Are Yarnys?](https://www.ea.com/ea-originals/news/what-are-the-yarnys).

Heavenly Bodies' technical lead describes physical feedback as a way to communicate contact and grip. Twinline uses visible tension, a ring on the pulling player, and short sound cues to make forces readable with very little text. This is our adaptation to a keyboard and monochrome presentation. [Developer article](https://blog.playstation.com/2021/10/19/sensing-your-surroundings-in-heavenly-bodies-coming-in-2021/).

Unity's maximum-distance joint allows slack within a limit. It provides the physical connection while changing the limit produces a controlled winch. Both circles remain dynamic and react through the solver. [Unity Distance Joint 2D reference](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics/joints/distance-joint-2d-reference.html).

These examples motivate design hypotheses; they do not establish that Twinline is fun.

## Implemented experiment

The previous validation route reached only 2.622 units of separation against a 3.45-unit rope. Most ordinary play never needed the tether. Fixed horizontal positions also prevented a visible sideways pull.

The new one-button vocabulary is tap, hold, release. Tapping lifts immediately. Holding reels after a short threshold, so players can pull a partner closer. Releasing extends the rope while preserving motion; soft lane forces then bring the pair apart. One player can brace and reel while the other supplies lift. Roles are temporary and can change after a gravity reversal.

The tradeoff matters: holding sacrifices repeated lift, shortens the available space, and can pull the partner into danger. Two players holding together reel faster but lose altitude without further taps. The system intentionally adds no automatic rescue, invulnerability, or bonus launch.

A small amount of contextual guidance explains an action when it becomes relevant. Monochrome movement, tension and sound carry the rest.

## Two-person playtest

1. Learn the keys and clear the first two tubes. Can both players explain tap versus hold without reading a separate manual?
2. Between tubes, let one player fall slightly lower. Have the higher player hold while the lower player taps. Can they deliberately pull together and then release?
3. Trade the pulling role after a gravity flip. Does preserving momentum feel understandable?
4. Try a late, poorly timed pull. Can both players explain the resulting failure?
5. Play several short runs. Do partners choose to reel for a reason, and want another try?

If players never choose the rope, tune obstacle timing and the value of rescue. If they hold constantly, reduce the winch advantage or increase the cost of bracing. If they cannot predict the motion, tune lane damping and feedback before adding mechanics. Human observations should decide the next revision.

## Revision after play feedback

The first reel was too subtle: much of a short hold removed slack, and the rope rarely reached its shortest length before release. The revision takes up unused slack immediately, reels at 4.2 units/second down to 1.1 units, and reduces horizontal damping so the return arc remains visible. Flowing chevrons and a stronger pulling ring make the active action apparent in both views. A safe ready-screen playground lets both players feel it before entering the obstacle course.

The former 0.7-second fault cue was also too brief and its instructional text appeared only once. The new warning is a dedicated two-second countdown before every reversal, repeated in both screens with the upcoming direction and next tap direction. Tubes slow to 30% during the countdown, providing reaction space without changing circle gravity or momentum. Pausing freezes the countdown.

## Shared-camera refinement

Both players now appear once in one continuous camera view. The divider, flight-limit lines, countdown bars, flashing edge borders and rope-direction chevrons are removed. The fixed vertical camera framing makes the flight limits coincide with the visible screen edges. The score and countdown numerals are larger, with no surrounding panels.

A sustained hold now offers finer control: it draws the pair in quickly, then continues tightening from about 1.3 units down to 0.65. The opening hint explicitly explains that holding the same key longer pulls the circles closer. Validation compares actual separation after 0.6 and 1.5 seconds of one continuous press.
