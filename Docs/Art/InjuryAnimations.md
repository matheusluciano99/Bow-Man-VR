# Authored injury animations

All fifteen agreed regions have model-specific entry clips under Assets/_Game/Animations/Injuries/<character>/. Recovery clips cover the thirteen regions with surviving-hit reactions. Full duel integration, final motion polish and Quest 2 viewpoint/comfort acceptance remain separate review.

| Region | Prepared motion | Review status |
| --- | --- | --- |
| Head | Immediate loss of posture with a fast collapse and relaxed head. | Dedicated fatal clips on both models; instant match result, no hold/recovery. Headset review pending. |
| Neck | Reach the actual neck wound, buckle the knees, then collapse without recovery. | Separate fatal clips on both models with runtime contact fitting; headset review pending. |
| Chest | Recoil and fall backward onto the back. | Separate retargeted fall/get-up clips on both models; full hold and recovery callbacks checked. Headset review pending. |
| Abdomen | Fold forward, hold the actual wound with both hands and kneel on both knees before recovering. | Own clips and runtime wound fitting; headset review pending. |
| Pelvis | Hips buckle into a supported seated fall, with both palms behind the body, then regain standing. | Captured fall and half-kneel rise fitted to both rigs; visual review pending. |
| Left shoulder | Turn away and cup the left shoulder with the right palm while the injured arm recoils. | Separate shoulder clips/contact checks on both models; headset review pending. |
| Right shoulder | Turn away and cup the right shoulder with the left palm while the injured arm recoils. | Independent shoulder clips/contact checks on both models; headset review pending. |
| Left arm | Recoil/turn away, bend the left arm and cradle its forearm with the right hand while standing. | Dedicated clips and support-contact review on both models; headset review pending. |
| Right arm | Recoil/turn away, bend the right arm and cradle its forearm with the left hand while standing. | Independent clips and support-contact review on both models; headset review pending. |
| Left hand | Jerk the left hand back, keep its fingers open and support the wrist with the right hand. | Separate hand/wrist clips and contact checks on both models; headset review pending. |
| Right hand | Jerk the right hand back, keep its fingers open and support the wrist with the left hand. | Independent hand/wrist clips and contact checks on both models; headset review pending. |
| Left leg | Kneel on the left knee, plant the left palm, hold, then regain standing. | Own kneeling clips, distinct from lifted-foot reactions; headset review pending. |
| Right leg | Kneel on the right knee, plant the right palm, hold, then regain standing. | Independent side-fitted clips with the healthy left foot planted; headset review pending. |
| Left foot | Recoil, unload onto the right leg, withdraw the left foot, brace and balance, then land before shifting weight back. | Source-based replacement passes contact, continuity and flow checks on both archers. Quest 2 review accepted the motion and timing. |
| Right foot | Recoil, unload onto the left leg, withdraw the right foot, brace and balance, then land before shifting weight back. | Source-based replacement passes contact, continuity and flow checks on both archers. Quest 2 review accepted the motion and timing. |

The foot authoring tool retargets the complete free MotionSmith right-foot
performance, with an anatomical mirrored result for the left foot on each actual
archer. These are generated source movements, not human motion capture. The
original 120-frame performance is preserved when preparing its neutral Humanoid
reference; frame zero is reference-only. See [source preparation](../../SourceAssets/CosindraAnimations/README.md)
and [provenance](AnimationSources.md).

The fitted entry lasts 1.1 seconds: load transfer begins before withdrawal, then
the original performance supplies the full-body movement. Playback fits the next three
seconds of source motion into a two-second hold. It plays the full authored recovery
in 0.8 seconds, placing the boot down before returning to neutral. The support ankle
stays planted and a
small fitted ankle pitch keeps its visible sole grounded. Toe flexion inside
boots is constrained. Contact fitting does not replace the source torso/arm
performance. Shooting stays blocked through the existing full hold/recovery,
and a foot hit cannot hand over at entry or while returning to stance.

ArcherReactionAnimation plays only the exact configured region. It reports the final entry frame to MarkInjuredPose and the final recovery frame to CompleteRecovery, using the current shot token. Death cancels surviving recovery; authored fatal head/neck entries can finish visually after the immediate match result. Embedded arrows remain children of the moving hit volume throughout. Fatal body hits play their own regional entry and then its authored full-collapse finisher, without a surviving hold or recovery.

Open Assets/_Game/Scenes/InjuryAnimationTest.unity for the close desktop review. It defaults to the left foot. Select the region and fire; an implemented reaction progresses automatically through its entry, hold and recovery. Unsupported regions retain explicit manual callback buttons in the review panel. Those buttons do not claim completed motion.

Archer 1 fires throughout the desktop animation review. The test arrow travels horizontally at the selected hit volume, and Archer 1 can fire again after a miss or a completed recovery. After a defeat the review displays the completed collapse for one second and starts a fresh round at full health with the same hit region selected.

Use Bow Man VR > Injuries > Rebuild Left Foot or Rebuild Right Foot to reproduce the independent clips against both prepared prefabs. The builder preserves clip GUIDs when rerun. These are native Unity transform clips bound to each character hierarchy; rebuilding a skeleton or moving its bones requires rebuilding and reviewing the clips. Position/quaternion groups retain constant sibling channels so Unity cannot default an unkeyed coordinate to zero.

Tests sample entry and recovery across both visible skins, require the healthy foot within 2 mm of its planted position, visible supporting-floor contact within 6 mm, correct lifted side and a continuous return to the neutral pose. Runtime checks include the full hold, delayed turn handoff, actual arrow alignment and death cancellation. The resting pose was also rendered and inspected in the real review scene.

The pose and skin checks cover fitted contacts in the desktop review. Both foot reactions have headset acceptance. Comfort of the moving local eye view remains tracked separately. See [the agreed table](../InjuryReactions.md), [turn contract](../TurnFlow.md) and [rig preparation](RigPreparation.md).

Use Bow Man VR > Injuries > Rebuild Left Arm or Rebuild Right Arm for the independent arm sequences. The injured arm flinches first. The archer looks toward the wound as the opposite palm cups the forearm from underneath. The support fingers curl around the forearm and release before the injured arm lowers. Both feet stay planted.

The arm rebuild fits both forearms against the head, neck and torso volumes through recoil, recovery and collapse. The final clips use the character's own arm lengths and anatomical palm orientation.

Use Bow Man VR > Injuries > Rebuild Left Shoulder or Rebuild Right Shoulder for the independent shoulder sequences. The opposite palm reaches the shoulder cap; it does not reuse the forearm cradle or the hand/wrist reaction.

The wrist target is derived from the point where the palm reaches the shoulder. The support hand travels in front of the chest and releases earlier on the return. During the fatal continuation, the builder checks the fitted body volumes and swivels the support elbow away from penetration before fitting floor contact.

Use Bow Man VR > Injuries > Rebuild Left Hand or Rebuild Right Hand for the independent hand/wrist sequences. Its clip explicitly binds the injured fingers to their open pose so another animation cannot leave them clenched. It is independent of shoulder cupping and arm cradling.

The injured hand withdraws and opens first as the archer looks toward it. The opposite palm cups the wrist from underneath, flexes its fingers for support, and releases before the injured hand lowers. Hand rebuilds also update the fatal continuation from the final supported pose. The PlayMode clearance check samples the forearms and hands against the torso volumes on both models in all three clips.

Use Bow Man VR > Injuries > Rebuild Chest Fall for the Mixamo-backed chest entry. It trims source time 0.5–3.2 seconds, blends the first 0.18 seconds from neutral, bakes sampled root motion into the hips and raises penetrating samples using actual visible skin vertices. The 2.7-second entry ends face-up with ground contact before the three-second hold begins. It never substitutes the rejected face-down motion. An entry can run before its recovery is authored; the review panel keeps separate manual callbacks for whichever half is missing. See [motion provenance](CharacterAssets.md#injury-motion-sources).

Use Bow Man VR > Injuries > Rebuild Chest Recovery after building the fall. It trims the get-up source to 2.0–6.8 seconds, blends the first 0.35 seconds from the exact landed bone pose and adds 0.6 seconds to regain the prefab’s neutral ready stance. The planar motion is fitted between the fallen pelvis and the standing origin; this may need further foot-slide polish during visual review. Per-sample visible-skin ground correction prevents penetration. The 5.4-second authored get-up plays in 3.6 seconds after the full three-second ground hold. The fit guides the wrists and boot around the pelvis and head. The standing pose grants the turn, and defeat cancels recovery. The faster rate remains provisional for wearer review.

Use Bow Man VR > Injuries > Rebuild Head Collapse for the separate one-second fatal head motion. It adapts the recorded fall source at a faster rate with a distinct relaxed head tilt and a 0.06-second pose transition. Match defeat is immediate at impact; the remaining collapse is visual only and never sends hold/recovery/handoff callbacks. Fatal neck motion uses its own reach, knee-buckle and collapse timing.

Use Bow Man VR > Injuries > Rebuild Neck Collapse for the separate 2.4-second neck sequence. The archer first reaches the neck, then the knees buckle into a fatal collapse and the hand releases before landing. Runtime IK fits the right palm to the actual bone-local impact point and approach direction, including off-centre wounds; it does not replace arrow placement with a generic socket. The shared measured-length arm solver is also used by the existing editor-authored limb contacts. It retains the current bend plane, or a stable perpendicular plane, when a bend hint is parallel to the target direction; unreachable goals clamp to the measured chain reach. Defeat remains immediate, independent of the visual timing, with no recovery or turn callback.

Use Bow Man VR > Injuries > Rebuild Left Leg for the left-knee and same-side ground-support sequence. The authoring tool fits the pelvis height from measured thigh/shin lengths and the visible kneepad surface, gives the moving foot clearance, bends at the hips and spine so the left palm can reach the floor, and keeps the healthy foot planted. Both clips remain separate from the foot-lift reactions. The three-second injured hold is owned by the shared turn controller; survival recovery must finish before handoff.

Use Bow Man VR > Injuries > Rebuild Right Leg for the independent right-knee sequence. It fits the right kneepad and right glove to the ground on each actual model while preserving the planted left foot. It does not reuse the left clip or substitute the right-foot lift.

Both kneels keep the support palm outside the torso and route the free arm
along the healthy knee. Both rebuild commands record the supported motions at
60 Hz and update their separate fatal continuations. The collision check samples
hand, forearm, shin and boot volumes against the core at 120 Hz on both models.

Use Bow Man VR > Injuries > Rebuild Abdomen for the abdominal sequence. The archer folds and reaches for the wound, then shifts weight with the captured descent into a fitted double kneel. After the three-second hold, the archer plants a boot and pushes through a captured rise. Runtime fitting keeps the hands near the actual bone-local wound through the hold and releases them during recovery.

The kneeling pose fits each visible kneepad and angles the boots away from the hips. The rebuild also updates the fatal continuation from the kneel. PlayMode checks sample the forearms, hands, and boots against the body volumes on both archers.

Use Bow Man VR > Injuries > Rebuild Pelvis to retarget the recorded stagger and fall into the fitted seated pose. Both palms brace behind the hips during the three-second hold. The return moves through a half-kneel with one palm supporting the pivot. The captured rise bears weight on the planted lead boot before returning to the archer's ready stance.

The rebuild fits the visible seat and ground contacts, routes the wrists outside the torso, and corrects residual forearm and rear-boot penetration against the fitted body volumes. It also rebakes the fatal continuation from the supported seat. Only a living archer completes the return and receives a turn.

Use Bow Man VR > Injuries > Rebuild Body Death Finishers after rebuilding any regional pose. Each of the thirteen surviving-hit regions has its own fatal transition from that exact injured pose to a complete collapse; it does not reuse a standing start. Forward-bent/standing injuries finish face-down, while chest and pelvis retain a back-down finish. The model-specific final poses come from the recorded Mixamo sources. Sampled visible-skin floor correction keeps the transition grounded. The prototype finisher lasts 0.8 seconds.

Fatal damage ends the match immediately, then the visual regional entry and collapse finish. Death during an existing reaction, hold or recovery interrupts get-up and blends the actual current bones into the finisher over 0.15 seconds. It never grants a turn or starts the three-second surviving hold. Head and neck keep their own complete fatal entries. Arrows remain at their actual bone-local impacts throughout.
