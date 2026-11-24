# Development decisions

Recorded 2026-11-13. This log explains settled development choices.
[AGENTS.md](../AGENTS.md) defines current requirements;
[README.md](../README.md) defines folder roles. Work status and responsibility
belong in GitHub issues, pull requests and Assignees.

## D001 — Project-owned assets under Assets/_Game

Group scenes, scripts, prefabs, materials, input actions, render settings and
tests under `Assets/_Game/`. Keep imported assets under `Assets/ThirdParty/<asset>/`.
This separates game-owned work from external content and keeps the Assets root
easy to navigate. Keep Unity template help separate from game-owned assets.

Keep package-managed XR configuration at `Assets/XR/`: the installed OpenXR
package expects this location and recreates it when moved. Moving it into
`_Game` would leave regenerated folders and increase configuration confusion.

## D002 — Separate runtime assets, source downloads and generated output

Keep imported runtime assets in Assets, authoring originals in SourceAssets,
local source downloads in ignored `SourceAssets/Downloads/`, review output in
ignored Artifacts, and generated players in ignored `Builds/PCVR/`. This keeps
archives and screenshots out of builds and prevents generated files entering Git.
An asset's original download is distinct from its prepared Unity version.

## D003 — Preserve Unity identity and use regular Git binary storage

Move Unity assets through the Editor with their existing `.meta` files and GUIDs.
Update hardcoded paths and build settings with the move; stable GUIDs preserve
scene, prefab, material and configuration references.

Store models and textures as regular Git binaries because the repository has no
available Git LFS budget. Keep runtime files within GitHub file limits. Large
originals such as the English Archer GLB stay in the ignored source cache until
smaller runtime files are exported. Binary revisions grow Git history; revisit
storage before frequent large art updates.

## D004 — Free realistic archers with room for model variety

Use free medieval human characters with realistic proportions and light clothing
or armor, matching the visual direction of For Honor and Conqueror's Blade.
Multiple different archer models are allowed. The retained heavy Templar Knight
is a separate asset, not the selected player archer.

The prototype visual choices are English Archer and Erika Archer. The local
headset view shows the selected archer’s tracked hands. The opponent is a
full archer. Menu
and lobby navigation use a pointing right hand and a relaxed left hand so the
player can see where the fingertip is aiming. The tracking test uses colored
controller markers. Reference views and body-region and bow design constraints
are in [ArcherDesign.md](Art/ArcherDesign.md).
Both characters now have prepared Humanoid derivatives and a shared movement
check. Rig readiness and remaining per-animation review are documented in
[RigPreparation.md](Art/RigPreparation.md).

Record each source, license, attribution and modification with imported assets.
Verify skeletons, skin weights and animation compatibility in actual files;
a model title or a creator's rig claim does not establish Unity readiness.
See [character sources and preparation status](Art/CharacterAssets.md).

### Medieval duel presentation

The planned arenas are a castle courtyard, a woodland archery range and a
village training yard. Pick one setting at the start of each duel to give
repeat matches variety while keeping the same floor and firing lane. The PvP
host shares the choice with the joiner so both players see the same scene.

Carry the archers’ visual style into the arrow and the player’s hands. Use one
finished arrow from nock through impact so its visible tip stays aligned with
the hit. The bow hand grips the bow and the drawing hand follows the string,
with roles swapped for left-handed play. Bake each character’s hand and cuff surfaces from their own prepared model so skin, clothing and scale match the chosen archer. Keep the blue and orange markers in the tracking test scene.

Use three baked scenery prefabs around the same level spawns. The host sends the selected setting in the round-start message, including on rematch. Shared scenery materials and combined meshes reduce draw calls. The distant woodland uses two profiles rendered from a full-resolution fir. The [environment art inventory](../Assets/ThirdParty/PolyHaven/Attribution.md) records each source and license.

## D005 — Browser VR release and Quest 2 lab testing

Playing a match requires a headset and tracked controllers, both in the interactive Unity Editor and in the released game. This keeps physical draw, aim and release integral to each mode. The itch.io page offers VR entry, and leaving immersive play cannot transfer control to the keyboard. Standalone diagnostic scenes and explicitly configured unattended tests can simulate input.
Use WebXR Export and WebXR Interactions 0.25.0 for stereo rendering and tracked controller input. Browser multiplayer uses secure WebSockets through Relay. The online lobby accepts join codes through the controller so a headset user can join without typing on the PC. For classroom testing, use Windows PC VR
through Quest Link with Unity 6000.3.25f1, OpenXR 1.16.1
and the Oculus Touch profile. This matches the available test hardware. Keep
desktop scene checks useful when the headset is unavailable, and distinguish
automated/editor validation from physical tracking, reach and comfort checks.
The accepted tracking criterion is one successful Quest 2 test in both Editor
and Windows player; a second developer's duplicate test is not required.

## D006 — Small assigned tasks and explicit merge authorization

Use a dedicated branch from current origin/main, an assigned issue and a PR into
main. Keep tasks in Review / Test until the PR is merged and relevant checks pass.
Use regular merge commits and obtain explicit authorization for each merge;
squash requires a specific request. This keeps changes individually reviewable
and preserves their history. See AGENTS.md for the checks appropriate to a change.

## D007 — Short function comments and shared agent guidance

Put a one- or two-line purpose comment above each function. Describe current
behavior; keep change history in Git and PRs. This makes the code easy to follow
without filling source files with development narrative.

Maintain shared rules and skills under `.agents/`, with `.claude/` entry points
that reference the same guidance. Record settled decisions here and update their
canonical requirements at the same time. This prevents the two agents from
following contradictory copies of a rule.

## D008 — Reuse arrow flight for an independently testable bow interaction

Keep draw/nock/release state in `Bow`, tracked controller bindings in `VRBowInput`,
and desktop simulation in `DesktopBowTest`. Both input paths drive the same bow
and existing `ArrowFlight`; no additional interaction package is required.
Backward draw controls launch speed, and the line from the drawn string to its
resting nock controls aim. Ignore short, forward-only and sideways-only pulls.
Keep the bow attached to the selected tracked hand throughout the duel without
holding grip, including shooting locks and injury. Cancel rather than fire when
tracking or application focus is lost. This supersedes the provisional
grip-to-equip mapping: continuous grip is not required for this duel.

Use the English Archer's existing textured bow parts and a moving string in the
project-owned bow prefab. Preserve its original model and attribution. A dedicated
test scene keeps this work independent of later turn/match integration and makes
draw strength testable without the classroom headset.

The opposite trigger grabs/releases the nearby string. Align the bow's local +Y
with the Unity grip pose's +Z using a +90-degree local X rotation, consistent with
the [OpenXR grip axes](https://registry.khronos.org/OpenXR/specs/1.1-khr/html/xrspec.html#semantic-paths-standard-pose-identifiers).
Both hand alignments were confirmed in the Quest 2 Editor lab review. Draw
distance and positional offsets remain provisional until final hardware tuning.

Default left bow hand, grip offsets, 12 cm string grab
radius, 3 cm minimum draw, 70 cm maximum draw and 25 m/s full-draw speed are
prototype settings. Validate handedness, grip alignment and comfortable draw on
the target headset before treating them as final controls or calibration.
Both left-bow/right-draw and right-bow/left-draw setups are supported. The test
panel switches hands without an Inspector edit and cancels a tracked draw before
rebinding. Final game-menu presentation remains part of menu integration.

Show **FULL DRAW** at the shot's actual power cap and pulse the drawing
controller so the player can keep looking at the target. Rearm below 95% to
avoid repeated pulses from small tracking changes near the cap. The HUD gives
the same confirmation when controller vibration is unavailable.

Let an already-held trigger grab the string when the controller reaches it.
Confirm the grab with a lighter pulse and keep the visible hand on the string
when a brief tracking loss preserves the draw. A turn lock requires a fresh
trigger press before the next grab.

## D009 — Distinct injury regions with a first-person eye viewpoint

Use fifteen hit regions: head, neck, chest, abdomen, pelvis and separate left/right
shoulders, arms, hands, legs and feet. The newly separated extremities and joints
have their own reactions so a foot hit cannot select the leg-kneeling animation,
or a shoulder/hand hit the supported-arm animation. Back hits are excluded from
the facing-archer bow duel; sword combat remains a conditional future idea.

The full motions and implementation cards are in the
[injury reaction table](InjuryReactions.md). Head and neck hits always kill.
The surviving chest fall preserves the first 1.8 seconds of the impact and
shortens the landing settle by 0.35 seconds. A 0.2-second ground pause and a
2.6-second get-up keep the archer moving once the impact has played. Foot hits
hold for two seconds and other regions for three. The injured archer receives the next
turn after recovery. This keeps turn ownership
consistent with the visible recovery. Death ends the match immediately; misses
resolve at scenery impact or flight expiry, with one outcome per shot.

Ease the live pelvis offset out over the first 0.12 seconds of a surviving
reaction. Hold the boots at their sampled contacts so the recorded motion can
start from the archer's current weight shift without a jump or a slide.

During local knockdown, kneeling and recovery, move the VR viewpoint with the
character's animated eyes so the player sees the injury sequence in first
person. Keep live headset tracking for looking around and calibrate the eye
reference against the player's neutral pose to avoid adding standing height
twice. The local body remains hidden while the tracked hands are visible.

This preserves the first-person experience through injury. Camera-height changes
are artificial locomotion with comfort implications, as described in Meta's
[locomotion comfort guidance](https://developers.meta.com/vr/design/locomotion-comfort-usability/).
Physical acceptance requires Quest 2 testing under task #35; the design choice
does not establish that the moving viewpoint is comfortable.

## D010 — Preserve source art and import prepared Humanoid derivatives

Keep downloaded FBXs intact and use separately named rigged derivatives in the
existing character prefabs. This preserves provenance and original prop-mesh
references while allowing fitted skinning and source-weight repairs. Store
Blender authoring/rebuild work in SourceAssets and import FBXs into Unity so
opening the game does not require a Blender import process.

Store explicit body, eye and finger mappings in the derived `.meta` files;
automatic Humanoid validity alone does not prove optional fingers are mapped or
weighted. Check both characters with one baked Humanoid movement clip, including
support-foot contact, and preserve the animation-only armature hierarchy when
copying its Avatar. Per-region injury animations and headset acceptance remain
separate reviews. See the [rig procedure](Art/RigPreparation.md).

## D011 — Bone-bound anatomical hit volumes

Use project-owned solid volumes on the prepared skeletons for the fifteen
agreed regions. Limb capsules follow both joint segments; central boxes divide
the torso, neck and head. This makes region detection follow animation while
keeping original art intact and avoiding a skinned-mesh collider rebuild each
physics step. Geometry fit remains provisional and needs pose-specific review.

Report the first swept impact once, preserving its point and travel direction.
Keep the generic impact event for scenery and add an anatomical event for body
consumers. A consumer uses one event to resolve its outcome. Disable the old
arena capsule targets so they cannot mask the new regions. See
[body hit detection](BodyHits.md) for event ordering, authoring and checks.

## D012 — Embedded arrows follow their actual bone-bound impact

Parent a stopped body arrow to its struck volume with its world pose preserved.
This carries the exact tip and impact orientation through animation without a
generic wound socket or a per-frame follow script. Keep embedded arrows until
their character is removed so a flight/scenery timer cannot erase them midway
through injury recovery. Flight and scenery keep bounded cleanup. Match reset
must remove the old character hierarchy or clear its embedded arrows.

## D013 — Resolve a shot once and wait for actual recovery completion

The duel controller owns damage and turn resolution for one registered arrow.
Separate reaction entry, the agreed three-second injured hold and recovery;
animation callbacks identify the current shot so stale completion cannot grant
a turn. End the match immediately on defeat, including fatal head/neck damage.
Keep explicit manual callbacks in the desktop state-review scene until the
individual visual sequences are connected; do not disguise them as animations.
See [turn flow](TurnFlow.md) for the contract and tests.

## D014 — AI uses the same shot and turn authority as the player

The opponent waits for its legal turn, then fires the existing arrow using a
fixed-speed ballistic arc and bounded aim error. It cannot bypass injury holds,
recovery or defeat. Keep provisional difficulty values separate from the rules.
The AI test host supplies explicit animation callbacks until the visual sequences
are integrated; this does not complete the full PvE mode's anatomy/blood scope.

## D021 — Blood starts at the actual wound and follows its bone

Use the existing typed body-impact event for wound emission without applying a
second damage outcome. Anchor the emitter at the actual entry point on its
bone-bound volume; let released droplets move in world space under gravity.
Scenery hits create no blood. Emission finishes and removes itself, and match
cleanup removes any live wound with its character. Droplet size, quantity and
the prototype two-second emission remain provisional visual settings for VR
review, separate from final blood/stain art.

## Foot motion and support

Use the free generated MotionSmith right-foot performance on both Humanoid rigs.
Mirror its anatomical rotation deltas for the left foot. Keep the healthy boot
planted throughout the balance and land the injured boot before returning to
stance. Fit both boots to the visible skin and retain measured limb lengths.

Quest 2 review accepted both revised reactions. Keep the 1.1-second recoil,
two-second balance and 0.8-second recovery. Shooting stays locked until standing
recovery finishes. Moving first-person eye comfort remains tracked by #35.

## D022 — Fitted tissue geometry and finite arrow penetration

Use the free BodyParts3D 4.3 tissue atlas for both prepared archers, preserving
unchanged original files, pinned digests and the publisher's current CC BY 4.0
credit. Fit real separate tissue meshes against each rig's eyes, hips and
interior body envelope. Brain depth uses the visible cranial skin rather than
the face-inclusive hit volume. Keep native derivatives under `_Game/Models`.

Trace from the actual body entry along the recorded direction to the embedded
visual tip. The 25 cm penetration is a provisional gameplay depth, not a
ballistics or clinical claim. Exact finite triangle/closed-tissue queries avoid
inventing contacts from organ bounding boxes; cache immutable geometry and prune
triangle batches. Bone-bound contacts follow subsequent motion. Anatomy never
applies another health outcome. The finishing presentation listens to the
confirmed lethal-shot gate, not the general per-arrow analysis event.

Keep rigid tissue binding as a prototype limit. Deforming anatomical motion
remains separate work. Preserve source
filenames, mesh GUIDs and native subasset identities when regenerating.

## D015 — Fit recorded injury performances on both game archers

Each body region has its own entry. Surviving reactions use the actual entry, hold and recovery completion to hand over the turn. Fatal hits lock the result immediately and allow the visible collapse to finish.

Use free human performance recordings with verified game-use terms. Review the full source on both rigs, then select the short movement that matches the wound. Preserve the impact recoil and measured limb lengths when fitting the hand or boot. For live wound contact, use the baked pose as a cleared starting point and limit the change against the model’s fitted colliders. This keeps the glove near the actual arrow while the archer moves. The per-region sources, timings and authoring commands are in [Injury reactions](InjuryReactions.md).

## D016 — Cinematic replay of the finishing arrow

Start the replay only after the duel confirms a lethal arrow. Open on the
original projectile approaching a frozen pose of the struck archer, cut to its
contact inside the body, then pull out to the duel. Use the exact shot path and
the fitted game anatomy. The contacted bone receives an impact accent when the match marks it fractured. Play the close-up on a surface at a fixed depth in front of the
viewer so their own head movement remains live throughout. The match result and
regional defeat animation proceed in parallel. Timing, reading distance and
headset comfort remain subject to Quest 2 testing.

## D017 — Bone fracture depends on shot strength

A shot reports each game bone its finite path intersects. A weak draw can
contact the same bone as a strong draw. Only a draw at or above the prototype
75% cutoff marks it fractured. The simplified volumes follow the Humanoid rig
so the two players can share the same bone identities throughout animation.
The cutoff and volume proportions are provisional game settings. Changes to
health or recovery from a fracture require their own balance decision.

## D018 — Fatal body hits finish with a full collapse

A fatal hit outside the head or neck first plays the reaction for the actual
struck region, then collapses fully and remains down. Resolve defeat and the
match result immediately; the visual sequence does not delay the result or run
a surviving hold, recovery or turn handoff. Death during an existing reaction
or recovery interrupts getting up and finishes the collapse from the current
pose. Preserve the independent head and neck fatal sequences.

For a lethal shoulder shot, use the complete captured recoil, opposite-hand reach
and fall from *Shoulder Hit And Fall*. Mirroring lets the same performance
react to the correct side. The fitted fatal fall passed the Unity injury review on November 20, 2026. The standing shoulder reaction needs its own
performance that includes the impact and a supported hold.

## D020 — Keep one XR session and one shot authority for the local duel

Use the existing tracked bow and AI with DuelTurnFlow; every released arrow
registers there before physics, and the bow rejects nocking/release outside the
local aiming turn. Rebuild match-owned actors and arrows on restart while keeping
the XR rig, live Link session and handedness. Keep the local body's renderers
hidden while its bone-bound volumes and injury clips remain active.

Apply the animated eyes' change from their standing reference to the tracking
origin after animation. Preserve each tracked head/controller's local position
and rotation, including live movement during a fall; do not add eye height to
floor-level tracking. Restore the origin on restart. This implements the chosen
eye-following presentation; physical height alignment and motion comfort still
require the Quest 2 checklist.

## D023 — Keep manual headset acceptance under the wearer's control

The headset wearer fires each regional test using the actual controller input,
draw and aim. Passive hit logs, frame captures and region labels guide the
review without firing arrows or skipping injury/recovery on the wearer's behalf. This
keeps visual review tied to the real interaction being accepted. Scripted
projectile/animation checks remain separate automated evidence. A temporary
close opponent helps inspect contacts without settling the arena distance.

Regional animation review uses a passive opponent that never shoots. After a
miss or complete surviving recovery, return practice control to the wearer
without healing the target or resetting its pose. Keep the actual trigger,
tracking, focus and injury gates; exercise AI combat separately. This avoids
waiting for unrelated attacks while comparing regional motion.

## D024 — Unity Relay and Netcode for GameObjects for online PvP

Use Netcode for GameObjects 2.14.0 with Unity Gaming Services Multiplayer 2.5.0:
anonymous Authentication plus a Unity Relay allocation for one opponent, joined
with a short code. Relay traverses home and campus NATs without port forwarding,
which the "different locations and networks" PvP requirement needs, and the
project is already linked to Unity Cloud. Photon Fusion would add a separate
account and API; direct-IP Mirror/FishNet would need port forwarding or a VPN.

The host is Archer 1 and the first joining client Archer 2; the host's
connection approval refuses any further client, and Relay allows one remote
connection. Each process signs in with its own auth profile so two instances on
one PC can connect. Online play therefore depends on UGS availability, the free
Relay quota and Relay being enabled for the project. Lobby/matchmaking, dedicated
servers and duel synchronization are outside this choice (#12, #13).

## D025 — The mode menu is the game's entry scene

Start the game on `MainMenu`, where a right-controller ray and trigger choose
PvE (local duel against the AI), PvP (online duel) or Tiro ao alvo (CPU or online target competition). Pointing at world
panels is the expected VR menu interaction and needs no new package; XR
Interaction Toolkit would add a dependency that touches the shared rig. The
menu generator puts MainMenu, LocalDuel, PvPConnectionTest and TargetRange first in the build
list so scenes can load each other, and other scenes stay after them. Single-scene
test players are still built explicitly. The left Menu button returns to
the menu; leaving the PvP scene ends the session because the network object
persists across scene loads.

Players choose English Archer or Erika Archer on the mode menu. Save the choice independently from the bow hand. In PvE the other model plays the opponent. In online play each seat chooses freely, including matching characters. Each archer has a distinct recorded idle, bow performance, breath, exertion, injury and defeat voice. The shared injury rules and shot timing keep both choices equivalent in play.

A player chooses the bow hand on the mode menu before entering either duel.
The panel names the bow hand and the match prompts name the drawing hand. Save that choice locally so
entering the other mode or returning on another day preserves it. The duel HUD
shows the next step when the player can shoot and a waiting prompt during the
opponent’s turn.

Keep release credits inside the mode menu so the player can read them and return
with the same controller. Source and license notices are also packaged
as plain text in `StreamingAssets/BowMan-Credits.txt` for offline access.

Use the same dark panels, brass focus accents and readable type throughout the
spatial interface. Show choices on the main menu and a separate six-character
join screen. In Tiro ao alvo, show every available distance as a direct choice.
Keep score, turn prompts and the last impact off the center of the firing lane
so the target and the drawing hand remain visible.

## Standing archers between shots

Use recorded breathing, shoulder movement and small weight shifts on both standing duel rigs. The visible opponent should still look alive while the player aims. Keep the feet planted and the local headset tracking origin steady. The hit reaction takes over on impact, the survivor returns to idle after recovery, and a defeated archer stays down. Both PvP seats use these rules. The [standing motion source and rebuild instructions](../Assets/ThirdParty/Rokoko/Attribution.md) accompany the recording.

Sample the visible opponent’s recorded bow action from the AI turn clock in PvE and from the owner’s draw in PvP. This lets a human pause or cancel a draw and switch bow hands. A mesh represents the nocked arrow on the observing side. The existing duel authority creates the flying projectile when it accepts the release.

Both archers use recorded pelvis motion and planted-foot leg movement while standing and drawing. The equipped hand closes around the grip and the string hand clears the face. Retain the last valid draw and hand pose during drawing-hand occlusion. Give a missing trigger signal up to 750 ms to return, including when the pose recovers first. Resume with a tracked, held trigger and fire on a subsequent tracked release. Measure power from the backward hand displacement before clamping the string visual. The Quest Pro review uses the original 70 cm full-power threshold after the wearer reported stable tracking. The earlier Quest 2 review used 36 cm because 42.5 cm still reached its tracking limit. The shared 0.96-metre arrow keeps its tip visible at full draw.

## Duel sound

Use recorded spatial cues for the two archers and their equipment. Start the string creak above 60% of maximum draw. Increase pitch and volume toward full draw, with a softer sustained sound while the player holds tension. Falling back to 60% or below, cancelling or releasing stops the stretch cue. Launch and impact sounds follow the shot the duel accepts. The observing client uses the host’s surface and contact to preserve the sound of each shot across both seats. Each round plays its selected arena’s field recording. The local volume control persists across rematches. Sound levels and falloff remain provisional until Quest 2 listening tests.

## Target competition

A player can compete locally against the other archer model without signing in or finding another player. The player shoots first. The CPU plays its recorded draw and releases one simulated arrow at the same target. Score the actual impact with the shared ring rules. In solo play the player can accept a rematch immediately.

For online competition, two players connect on separate PCs using the existing Relay host and join-code flow.
Alternate shots so both archers see each outcome and shoot at the same distance.
Each archer releases ten arrows. The host scores the tip contact on ten concentric
rings, awarding a dividing line to the higher ring. A miss or flight expiry earns
zero. Cancelling a draw preserves the arrow. Both players see the same scores
and winner or tie. Both must accept a rematch. The host chooses 15, 30, 50, 60 or 70 metres before connecting. The first two options give new players shorter courses before the three long-distance challenges. Both seats and their rematch keep the host’s selection. Target mode uses one full-draw speed (32 game metres per second) across every distance so the longest course remains reachable. Headset readability still needs a lab check.

## Open questions and provisional values

- Additional archer variants and later visual refinements remain open. The current
  prototype uses English Archer and Erika. Local full-body rendering is outside
  the agreed first-person hand presentation.
- Arena distance, bow draw distance and combat tuning remain provisional as
  described in AGENTS.md and the relevant task cards.
- Per-model hitbox dimensions, damage values, organ/fracture effects and internal
  injury presentation remain implementation/tuning work. The chosen eye
  viewpoint and injury motion need hardware validation.

### Fatal shoulder motion

A lethal shoulder hit plays the complete recorded reach and fall as a single motion. This preserves the timing between the opposite hand reaching the injured shoulder and the body losing balance. The fitted take was accepted in the Unity injury review on November 20, 2026. Surviving shoulder hits require a separate standing reaction.
