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
headset view shows tracked hands/controllers only for now; the opponent is a
full archer. The current rig uses controller markers. Reference views and
body-region/bow design constraints are in [ArcherDesign.md](Art/ArcherDesign.md).
Both characters now have prepared Humanoid derivatives and a shared movement
check. Rig readiness and remaining per-animation review are documented in
[RigPreparation.md](Art/RigPreparation.md).

Record each source, license, attribution and modification with imported assets.
Verify skeletons, skin weights and animation compatibility in actual files;
a model title or a creator's rig claim does not establish Unity readiness.
See [character sources and preparation status](Art/CharacterAssets.md).

## D005 — PC VR on the classroom Quest 2

Target Windows PC VR through Quest Link with Unity 6000.3.25f1, OpenXR 1.16.1
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
Quest 2 before treating them as final controls or calibration.
Both left-bow/right-draw and right-bow/left-draw setups are supported. The test
panel switches hands without an Inspector edit and cancels a tracked draw before
rebinding. Final game-menu presentation remains part of menu integration.

## D009 — Distinct injury regions with a first-person eye viewpoint

Use fifteen hit regions: head, neck, chest, abdomen, pelvis and separate left/right
shoulders, arms, hands, legs and feet. The newly separated extremities and joints
have their own reactions so a foot hit cannot select the leg-kneeling animation,
or a shoulder/hand hit the supported-arm animation. Back hits are excluded from
the facing-archer bow duel; sword combat remains a conditional future idea.

The full motions and implementation cards are in the
[injury reaction table](InjuryReactions.md). Head and neck hits always kill.
Surviving hits hold the injured pose for three seconds, then complete recovery
before the injured archer receives the next turn. This keeps turn ownership
consistent with the visible recovery. Death ends the match immediately; misses
resolve at scenery impact or flight expiry, with one outcome per shot.

During local knockdown, kneeling and recovery, move the VR viewpoint with the
character's animated eyes so the player sees the injury sequence in first
person. Keep live headset tracking for looking around and calibrate the eye
reference against the player's neutral pose to avoid adding standing height
twice. The local visible-body choice remains hands/controllers for the prototype.

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

## D015 — Model-specific authored injury clips drive recovery callbacks

Bake explicit transform clips against each prepared skeleton and keep their
entry/recovery references on the character prefab. Fit supported contacts using
the model's measured bone lengths. Rebuild with the editor authoring tool;
preserve existing clip GUIDs. Runtime playback signals pose arrival and complete
recovery to the duel controller. Unsupported regions receive no generic fallback
and stay visibly pending in the review tools until their own clips exist. Retarget imported motion against its source T-pose and unit scale, bake it to each owned skeleton, and record the source/export terms in the asset document. Keep motion sampling on disposable instances so baking cannot serialize animated bone overrides into a neutral prefab.

Fit support contacts against the visible skin, clothing and equipment footprint;
a joint origin is not the surface that touches the ground. Keep palm orientation
tied to anatomical handedness through the motion.

Wound-reaching motions retain the actual impact and approach direction in the
struck volume’s bone-local space. Fit their hands after sampling the authored
pose so the contact follows the moving wound rather than a fixed world point.

## D016 — Brief in-body X-ray injury presentation

Show internal injury briefly inside the struck archer, using the actual arrow
path and affected anatomy rather than a generic anatomical panel. The X-ray
presentation must agree with the impact and subsequent body motion. Duration,
rendering treatment and headset readability remain tuning/physical review;
this decision does not claim implemented organs, fractures or visual acceptance.

## D017 — Bone fracture depends on shot strength

Distinguish intersecting a bone from fracturing it. The fracture decision depends
on the shot's strength, preserving a meaningful difference between weak and
strong draws. A prototype threshold may be tuned during testing; this choice
does not specify a clinically accurate material/energy simulation or final
balance. Organ/fracture gameplay effects remain separate tuning decisions.

## D018 — Fatal body hits finish with a full collapse

A fatal hit outside the head or neck first plays the reaction for the actual
struck region, then collapses fully and remains down. Resolve defeat and the
match result immediately; the visual sequence does not delay the result or run
a surviving hold, recovery or turn handoff. Death during an existing reaction
or recovery interrupts getting up and finishes the collapse from the current
pose. Preserve the independent head and neck fatal sequences.

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

## D021 — Unity Relay and Netcode for GameObjects for online PvP

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

## D022 — The mode menu is the game's entry scene

Start the game on `MainMenu`, where a right-controller ray and trigger choose
PvE (local duel against the AI) or PvP (online connection). Pointing at world
panels is the expected VR menu interaction and needs no new package; XR
Interaction Toolkit would add a dependency that touches the shared rig. The
menu generator puts MainMenu, LocalDuel and PvPConnectionTest first in the build
list so scenes can load each other, and other scenes stay after them. Single-scene
test players are still built explicitly. The left Menu button or Esc returns to
the menu; leaving the PvP scene ends the session because the network object
persists across scene loads.

## Open questions and provisional values

- Additional archer variants and later visual refinements remain open. The current
  prototype uses English Archer and Erika; local full-body rendering is outside
  the agreed hands/controllers presentation.
- Arena distance, bow draw distance and combat tuning remain provisional as
  described in AGENTS.md and the relevant task cards.
- Per-model hitbox dimensions, damage values, organ/fracture effects and the
  accepted brief X-ray's duration/rendering remain implementation/tuning work. The chosen eye
  viewpoint and injury motion need hardware validation.
