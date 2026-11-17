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
English still needs rigging; this choice does not establish animation readiness.

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
Cancel rather than fire when grip, tracking or application focus is lost.

Use the English Archer's existing textured bow parts and a moving string in the
project-owned bow prefab. Preserve its original model and attribution. A dedicated
test scene keeps this work independent of later turn/match integration and makes
draw strength testable without the classroom headset.

Grip/trigger mappings, default left bow hand, grip offsets, 12 cm string grab
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

## Open questions and provisional values

- Additional archer variants and later visual refinements remain open. The current
  prototype uses English Archer and Erika; local full-body rendering is outside
  the agreed hands/controllers presentation.
- Arena distance, bow draw distance and combat tuning remain provisional as
  described in AGENTS.md and the relevant task cards.
- Per-model hitbox dimensions, damage values, organ/fracture effects and internal
  injury presentation remain implementation/tuning work. The chosen eye
  viewpoint and injury motion need hardware validation.
