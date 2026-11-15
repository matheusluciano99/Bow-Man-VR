# Authored injury animations

Implemented sequences have distinct model-specific entry and recovery clips, stored under Assets/_Game/Animations/Injuries/<character>/. The remaining region motions in the agreed reaction table are still implementation work; there is no generic replacement animation.

| Region | Prepared motion | Review status |
| --- | --- | --- |
| Right leg | Kneel on the right knee, plant the right palm, hold, then regain standing. | Independent side-fitted clips with the healthy left foot planted; headset review pending. |
| Left leg | Kneel on the left knee, plant the left palm, hold, then regain standing. | Own kneeling clips, distinct from lifted-foot reactions; headset review pending. |
| Neck | Reach the actual neck wound, buckle the knees, then collapse without recovery. | Separate fatal clips on both models with runtime contact fitting; headset review pending. |
| Head | Immediate loss of posture with a fast collapse and relaxed head. | Dedicated fatal clips on both models; instant match result, no hold/recovery. Headset review pending. |
| Chest | Recoil and fall backward onto the back. | Separate retargeted fall/get-up clips on both models; full hold and recovery callbacks checked. Headset review pending. |
| Left hand | Jerk the left hand back, keep its fingers open and support the wrist with the right hand. | Separate hand/wrist clips and contact checks on both models; headset review pending. |
| Right hand | Jerk the right hand back, keep its fingers open and support the wrist with the left hand. | Independent hand/wrist clips and contact checks on both models; headset review pending. |
| Left shoulder | Turn away and cup the left shoulder with the right palm while the injured arm recoils. | Separate shoulder clips/contact checks on both models; headset review pending. |
| Right shoulder | Turn away and cup the right shoulder with the left palm while the injured arm recoils. | Independent shoulder clips/contact checks on both models; headset review pending. |
| Left arm | Recoil/turn away, bend the left arm and cradle its forearm with the right hand while standing. | Dedicated clips and support-contact review on both models; headset review pending. |
| Right arm | Recoil/turn away, bend the right arm and cradle its forearm with the left hand while standing. | Independent clips and support-contact review on both models; headset review pending. |
| Left foot | Shift weight to the right leg, lift the left foot 14 cm, hold, then lower it and regain stance. | Desktop clip/contact and automatic flow checks pass on both archers; headset review pending. |
| Right foot | Shift weight to the left leg, lift the right foot 14 cm, hold, then lower it and regain stance. | Independent clips and contact checks on both archers; headset review pending. |

The editor authoring tool uses the skeleton’s measured segment lengths and two-bone IK to keep the healthy foot planted. Both entry and return last a provisional 0.9 seconds; the agreed three-second injured hold is additional time owned by DuelTurnFlow. A foot hit therefore cannot hand over at entry or while returning to stance.

ArcherReactionAnimation plays only the exact configured region. It reports the final entry frame to MarkInjuredPose and the final recovery frame to CompleteRecovery, using the current shot token. Death cancels surviving recovery; authored fatal head/neck entries can finish visually after the immediate match result. Embedded arrows remain children of the moving hit volume throughout. Other lethal collapse/death sequences remain their own animation cards.

Open Assets/_Game/Scenes/InjuryAnimationTest.unity for the close desktop review. It defaults to the left foot. Select the region and fire; an implemented reaction progresses automatically through its entry, hold and recovery. Unsupported regions retain explicit manual callback buttons in the review panel. Those buttons do not claim completed motion.

Use Bow Man VR > Injuries > Rebuild Left Foot or Rebuild Right Foot to reproduce the independent clips against both prepared prefabs. The builder preserves clip GUIDs when rerun. These are native Unity transform clips bound to each character hierarchy; rebuilding a skeleton or moving its bones requires rebuilding and reviewing the clips. Position/quaternion groups retain constant sibling channels so Unity cannot default an unkeyed coordinate to zero.

Tests sample entry and recovery across both visible skins, require the healthy foot within 2 mm of its planted position, visible supporting-floor contact within 6 mm, correct lifted side and a continuous return to the neutral pose. Runtime checks include the full hold, delayed turn handoff, actual arrow alignment and death cancellation. The resting pose was also rendered and inspected in the real review scene.

The pose/skin/contact checks establish desktop prototype readiness, not final animation polish or Quest 2 comfort. The local eye-view integration and each remaining anatomical reaction retain their own acceptance. See [the agreed table](../InjuryReactions.md), [turn contract](../TurnFlow.md) and [rig preparation](RigPreparation.md).

Use Bow Man VR > Injuries > Rebuild Left Arm or Rebuild Right Arm for the independent arm sequences. Its opposite-hand wrist support is fitted to the injured forearm; both feet remain in their neutral planted positions. The arm reaction is independent of shoulder and hand reactions.

Use Bow Man VR > Injuries > Rebuild Left Shoulder or Rebuild Right Shoulder for the independent shoulder sequences. The opposite palm reaches the shoulder cap; it does not reuse the forearm cradle or the hand/wrist reaction.

Use Bow Man VR > Injuries > Rebuild Left Hand or Rebuild Right Hand for the independent hand/wrist sequences. Its clip explicitly binds the injured fingers to their open pose so another animation cannot leave them clenched. It is independent of shoulder cupping and arm cradling.

Use Bow Man VR > Injuries > Rebuild Chest Fall for the Mixamo-backed chest entry. It trims source time 0.5–3.2 seconds, blends the first 0.18 seconds from neutral, bakes sampled root motion into the hips and raises penetrating samples using actual visible skin vertices. The 2.7-second entry ends face-up with ground contact before the three-second hold begins. It never substitutes the rejected face-down motion. An entry can run before its recovery is authored; the review panel keeps separate manual callbacks for whichever half is missing. See [motion provenance](CharacterAssets.md#injury-motion-sources).

Use Bow Man VR > Injuries > Rebuild Chest Recovery after building the fall. It trims the get-up source to 2.0–6.8 seconds, blends the first 0.35 seconds from the exact landed bone pose and adds 0.6 seconds to regain the prefab’s neutral ready stance. The planar motion is fitted between the fallen pelvis and the standing origin; this may need further foot-slide polish during visual review. Per-sample visible-skin ground correction prevents penetration. The complete recovery lasts 5.4 seconds; it begins only after the full three-second hold and grants the next turn only on its final frame. Defeat cancels it.

Use Bow Man VR > Injuries > Rebuild Head Collapse for the separate one-second fatal head motion. It adapts the recorded fall source at a faster rate with a distinct relaxed head tilt and a 0.06-second pose transition. Match defeat is immediate at impact; the remaining collapse is visual only and never sends hold/recovery/handoff callbacks. Fatal neck motion uses its own reach, knee-buckle and collapse timing.

Use Bow Man VR > Injuries > Rebuild Neck Collapse for the separate 2.4-second neck sequence. The archer first reaches the neck, then the knees buckle into a fatal collapse and the hand releases before landing. Runtime IK fits the right palm to the actual bone-local impact point and approach direction, including off-centre wounds; it does not replace arrow placement with a generic socket. The shared measured-length arm solver is also used by the existing editor-authored limb contacts. Defeat remains immediate, independent of the visual timing, with no recovery or turn callback.

Use Bow Man VR > Injuries > Rebuild Left Leg for the left-knee and same-side ground-support sequence. The authoring tool fits the pelvis height from measured thigh/shin lengths and the visible kneepad surface, gives the moving foot clearance, bends at the hips and spine so the left palm can reach the floor, and keeps the healthy foot planted. Both clips remain separate from the foot-lift reactions. The three-second injured hold is owned by the shared turn controller; survival recovery must finish before handoff.

Use Bow Man VR > Injuries > Rebuild Right Leg for the independent right-knee sequence. It fits the right kneepad and right glove to the ground on each actual model while preserving the planted left foot. It does not reuse the left clip or substitute the right-foot lift.
