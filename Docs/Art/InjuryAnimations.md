# Authored injury animations

Implemented sequences have distinct model-specific entry and recovery clips, stored under Assets/_Game/Animations/Injuries/<character>/. The remaining region motions in the agreed reaction table are still implementation work; there is no generic replacement animation.

| Region | Prepared motion | Review status |
| --- | --- | --- |
| Left foot | Shift weight to the right leg, lift the left foot 14 cm, hold, then lower it and regain stance. | Desktop clip/contact and automatic flow checks pass on both archers; headset review pending. |

The editor authoring tool uses the skeleton’s measured segment lengths and two-bone IK to keep the healthy foot planted. Both entry and return last a provisional 0.9 seconds; the agreed three-second injured hold is additional time owned by DuelTurnFlow. A foot hit therefore cannot hand over at entry or while returning to stance.

ArcherReactionAnimation plays only the exact configured region. It reports the final entry frame to MarkInjuredPose and the final recovery frame to CompleteRecovery, using the current shot token. Death cancels surviving recovery. Embedded arrows remain children of the moving hit volume throughout. Lethal collapse/death sequences remain their own animation cards.

Open Assets/_Game/Scenes/InjuryAnimationTest.unity for the close desktop review. It defaults to the left foot. Select the region and fire; an implemented reaction progresses automatically through its entry, hold and recovery. Unsupported regions retain explicit manual callback buttons in the review panel. Those buttons do not claim completed motion.

Use Bow Man VR > Injuries > Rebuild Left Foot to reproduce the clips against both prepared prefabs. The builder preserves clip GUIDs when rerun. These are native Unity transform clips bound to each character hierarchy; rebuilding a skeleton or moving its bones requires rebuilding and reviewing the clips. Position/quaternion groups retain constant sibling channels so Unity cannot default an unkeyed coordinate to zero.

Tests sample entry and recovery across both visible skins, require the healthy foot within 2 mm of its planted position, visible supporting-floor contact within 6 mm, correct lifted side and a continuous return to the neutral pose. Runtime checks include the full hold, delayed turn handoff, actual arrow alignment and death cancellation. The resting pose was also rendered and inspected in the real review scene.

The pose/skin/contact checks establish desktop prototype readiness, not final animation polish or Quest 2 comfort. The local eye-view integration and each remaining anatomical reaction retain their own acceptance. See [the agreed table](../InjuryReactions.md), [turn contract](../TurnFlow.md) and [rig preparation](RigPreparation.md).
