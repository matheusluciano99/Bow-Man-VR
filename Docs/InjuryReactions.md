# Body-region injury reactions

Agreed design for [task #17](https://github.com/matheusluciano99/Bow-Man-VR/issues/17).
[AGENTS.md](../AGENTS.md) defines the current game requirements;
[archer design](Art/ArcherDesign.md) describes model and presentation constraints.
These are implementation requirements, not completed animations or hitboxes.

## Region inventory and reactions

The bow duel has fifteen hit regions. The archers face each other; back hits
and back injury reactions are outside this mode. Left/right always refer to
the injured character's anatomical side, independent of camera or bow hand.
Each listed region needs its own appropriate animation, with separate left/right
motions. Use the actual impact point and arrow direction for arrow placement,
wounds, organ/bone damage and blood throughout the sequence.

| Region | Reaction | Surviving recovery | Implementation card |
| --- | --- | --- | --- |
| Head | Instant kill; immediate collapse with loss of posture. | None; remain dead. | [#21](https://github.com/matheusluciano99/Bow-Man-VR/issues/21) |
| Neck | Instant kill; reach toward the actual neck wound, knees buckle, then collapse. | None; remain dead. | [#51](https://github.com/matheusluciano99/Bow-Man-VR/issues/51) |
| Chest | Fall backward and land with the back on the ground. | Hold back-down pose for 3 s, then stand if alive. | [#22](https://github.com/matheusluciano99/Bow-Man-VR/issues/22), [#23](https://github.com/matheusluciano99/Bow-Man-VR/issues/23) |
| Left shoulder | Recoil on the left, turn the torso away, cup the left shoulder with the right hand while standing. | Hold for 3 s, then release the supported pose if alive. | [#52](https://github.com/matheusluciano99/Bow-Man-VR/issues/52) |
| Right shoulder | Recoil on the right, turn the torso away, cup the right shoulder with the left hand while standing. | Hold for 3 s, then release the supported pose if alive. | [#53](https://github.com/matheusluciano99/Bow-Man-VR/issues/53) |
| Left arm | Recoil on the left, bend the injured arm and cradle it with the right hand while standing. | Hold for 3 s, then recover from the supported-arm pose if alive. | [#26](https://github.com/matheusluciano99/Bow-Man-VR/issues/26) |
| Right arm | Recoil on the right, bend the injured arm and cradle it with the left hand while standing. | Hold for 3 s, then recover from the supported-arm pose if alive. | [#27](https://github.com/matheusluciano99/Bow-Man-VR/issues/27) |
| Left hand | Jerk the left hand back, open its fingers, support the injured left wrist with the right hand. | Hold for 3 s, then regain the ready pose if alive. | [#54](https://github.com/matheusluciano99/Bow-Man-VR/issues/54) |
| Right hand | Jerk the right hand back, open its fingers, support the injured right wrist with the left hand. | Hold for 3 s, then regain the ready pose if alive. | [#55](https://github.com/matheusluciano99/Bow-Man-VR/issues/55) |
| Abdomen | Fold forward, hold the actual wound, drop to both knees. | Hold kneeling pose for 3 s, then stand if alive. | [#28](https://github.com/matheusluciano99/Bow-Man-VR/issues/28) |
| Pelvis | Hips buckle into a seated fall; both hands support the body from behind. | Hold seated pose for 3 s, then stand if alive. | [#56](https://github.com/matheusluciano99/Bow-Man-VR/issues/56) |
| Left leg | Kneel on the injured left leg and support the body with the left hand on the ground. | Hold for 3 s, then stand if alive. | [#24](https://github.com/matheusluciano99/Bow-Man-VR/issues/24) |
| Right leg | Kneel on the injured right leg and support the body with the right hand on the ground. | Hold for 3 s, then stand if alive. | [#25](https://github.com/matheusluciano99/Bow-Man-VR/issues/25) |
| Left foot | Lift the injured left foot and balance on the right leg. | Hold for 3 s, then lower the left foot and regain stance if alive. | [#57](https://github.com/matheusluciano99/Bow-Man-VR/issues/57) |
| Right foot | Lift the injured right foot and balance on the left leg. | Hold for 3 s, then lower the right foot and regain stance if alive. | [#58](https://github.com/matheusluciano99/Bow-Man-VR/issues/58) |

## Region boundaries

| Region | Intended anatomical coverage |
| --- | --- |
| Head | Skull, face and ears; neck is separate. |
| Neck | Neck between the base of the head and collar line; fatal, with its own animation. |
| Chest | Front upper torso/rib cage; abdomen begins below the rib-cage boundary. |
| Abdomen | Front lower torso above the pelvic/hip region. |
| Pelvis | Hip/groin region below the abdomen and above the leg attachments. |
| Each shoulder | Shoulder cap around its joint; arm begins below the shoulder region. |
| Each arm | Upper arm, elbow and forearm down to the wrist boundary; excludes shoulder and hand. |
| Each hand | Wrist/hand, palm and fingers below the arm boundary. |
| Each leg | Thigh, knee and shin down to the ankle boundary; excludes pelvis and foot. |
| Each foot | Foot and toes below the leg/ankle boundary. |

Fit final volumes and shared boundaries to each rigged model under
[#19](https://github.com/matheusluciano99/Bow-Man-VR/issues/19). Resolve one
nearest body intersection along the arrow path; seams must produce one region
and one injury. Equipment surfaces must not replace the actual body location.
Exact collider dimensions and armor interception/damage behavior are separate
implementation/tuning decisions.

Back geometry remains part of the character model. The back-reaction card
[#29](https://github.com/matheusluciano99/Bow-Man-VR/issues/29) is deferred and
does not block the bow duel. Reconsider it only if sword duels are later approved;
sword combat is not an agreed game mode.

## Recovery, lethality and turn flow

For a surviving hit, the three-second timer starts after reaching the injured
resting pose: on the ground, kneeling, seated, supporting an arm/hand/shoulder,
or balancing on the healthy foot. The initial reaction and recovery animation
are additional time. The three-second hold is an agreed prototype value;
validate the complete sequence on the rigged models and target headset.

1. A releases one arrow on their turn. Lock further shooting while that shot resolves.
2. Apply its one resolved hit or miss. A head/neck hit kills immediately regardless
   of health. Other regions kill when applied damage exhausts remaining health.
3. Death ends the match immediately, cancels pending recovery and prevents any new
   turn or shot by the dead archer. Death animation can finish visually without
   delaying the result; there is no three-second surviving hold or get-up.
4. If B survives a hit, play B's region/side reaction, hold the injured pose for
   three seconds, then complete the recovery animation. Give B the next turn only
   after recovery finishes. Standing reactions recover from their supported pose;
   they do not invent an extra fall/get-up.
5. A miss resolves at the arrow's first scenery impact or flight expiry, then
   gives B the turn. Keep it pending while the arrow can still strike a target.

Only the active, recovered archer may draw/fire. Cancel any nocked arrow when
injury/death interrupts the archer, and keep live local controller tracking.
Resolve each shot outcome once: later arrow destruction, repeated impact
callbacks or a cancelled recovery callback must not advance the turn again.

Damage numbers and the gameplay effects of organs/fractures remain tuning under
the combat/anatomy tasks. Do not infer additional guaranteed-fatal body regions,
fixed health values or persistent impairment rules from this animation table.

## Local VR viewpoint

During injury, the local view follows the character's animated eyes through
falling, kneeling, sitting, balancing and getting up. Keep live headset tracking
layered onto that animated eye reference so the player can still look around.
The existing prototype presentation remains tracked hands/controllers locally
and a full archer opponent; author reactions on the full archer rig.

Calibrate the animated eye reference against the headset's neutral tracking
pose; do not add the player's standing eye height a second time. Check eye
height/orientation through the full motion and supported pose, with free head
movement. Do not treat a correct opponent animation as a passed local viewpoint test.

Camera height changes are artificial locomotion with comfort implications; see
Meta's [locomotion comfort guidance](https://developers.meta.com/vr/design/locomotion-comfort-usability/).
Physical viewpoint acceptance belongs to
[#35](https://github.com/matheusluciano99/Bow-Man-VR/issues/35) on Quest 2.

## Implementation review cases

- Hit all fifteen regions separately, from both archer positions. Verify region,
  anatomical side, impact point and direction; test shoulder/arm/hand,
  leg/foot, chest/abdomen/pelvis and head/neck seams for a single resolved injury.
- Check the exact pose and support contacts in each table row. A foot hit must
  lift the correct foot; a leg hit must use the injured-side knee and same-side
  supporting hand. Check fingers and opposite-hand contact for hand/shoulder hits.
- Shoot head and neck at full health and low health. Both kill immediately and
  never recover. Nonfatal hits must respect the full hold plus recovery before
  handing over; death during recovery cancels its pending completion.
- Exercise scenery misses and flight expiry, cancelled draws, duplicate impact
  notification and arrow cleanup. Each launched shot may hand over at most once.
- Keep arrows/wounds aligned with their actual body locations during every pose.
  Verify local eye/head tracking and the full motion on Quest 2 under #35; desktop
  animation checks cannot establish comfort acceptance.
