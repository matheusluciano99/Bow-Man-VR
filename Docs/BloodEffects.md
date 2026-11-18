# Arrow wound blood

The shared `TestArrow.prefab` has `ArrowWoundBlood`, which listens only to
`ArrowFlight.BodyHit`. It spawns `ArrowWoundBlood.prefab` at the actual reported
entry point, facing along the actual struck surface normal. The emitter becomes a
child of that exact bone-bound hit volume, preserving its world pose and scale.
It does not choose a new body part or apply another damage outcome.

Emission follows the wound as the character reacts. Its 1.5 cm outward shape
offset clears the droplets' collision radius while retaining the exact entry anchor. Already emitted droplets
simulate in world space, fall under gravity and expire on collision; they do not
teleport with the bone. The emitter destroys itself after its final particles
finish. Removing the character during restart also removes any live emitter;
this cleanup leaves embedded arrows and the character intact during normal play.
Scenery contacts never create an emitter.

The prototype uses a two-second emission, a small initial burst and droplets
lasting 0.25–0.5 seconds, capped at 80 particles per wound. These are provisional
visual settings for lab review. It uses mesh droplets with a project-authored wet
red URP material; there are no downloaded textures or additional packages. Blood
pools, spreading stains and final art polish are outside this emitter's scope.

Use **Bow Man VR > Injuries > Rebuild Wound Blood** to reproduce the effect and
its shared-arrow reference. Existing asset GUIDs are preserved. In DuelArena, use
the region picker and test arrow to inspect different wounds. Review the actual
wound position and visibility during standing, kneeling and falling in the
integrated duel and close injury scene. Stereo readability and perceived amount
remain part of [the lab test](LabTestPlan.md).
