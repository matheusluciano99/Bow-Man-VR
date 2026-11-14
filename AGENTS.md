# Bow Man VR

This is the primary source of project context and instructions for coding agents.
Keep the project brief in this file; `CLAUDE.md` references it.
Record design decisions directly, without personal narrative attribution.
Use GitHub's Assignees field to record task responsibility.

## Maintaining development decisions

- Read and follow [.agents/rules/development.md](.agents/rules/development.md).
- Record settled choices and their reasons in [Docs/Decisions.md](Docs/Decisions.md)
  and update the affected project requirements. Keep provisional choices explicit.
- Use the shared [decision-documentation skill](.agents/skills/document-development-decisions/SKILL.md)
  for decision updates. The `.claude/` rules and skill reference the same guidance.
- Put a short purpose comment above each function, describing current behavior
  in one or two lines. Keep change history in Git and pull requests.

## Repository organization

- Follow the folder roles in [README.md](README.md). Keep `Builds/` for generated
  players, `Artifacts/` for local review output, and `SourceAssets/Downloads/`
  for local source archives. The latter two are excluded from Git.
- Keep project-owned scenes, scripts, prefabs, materials, input, render settings,
  and tests under `Assets/_Game/`. Keep package-managed XR assets at `Assets/XR/`,
  the path expected by OpenXR.
- Group imported character models, textures, and attribution under
  `Assets/ThirdParty/<asset>/`. Keep original filenames when importing source files.
- Preserve existing asset GUIDs and `.meta` files when moving Unity assets.
  Models and textures use regular Git binary storage; do not add LFS pointers
  while the repository has no available LFS budget.
- Keep Unity asset files and project folders visible in VS Code.

## Game concept

- A Unity bow-and-arrow game for virtual reality (VR).
- Inspired by the classic Bow Man: two archers take turns shooting arrows at
  each other, with aiming and shot strength determining the arrow's trajectory.
- Adapt that archery duel to VR using the bow interaction described below.

## Character art

- Use free realistic medieval human archers in the visual style of For Honor
  and Conqueror's Blade, with light clothing or armor. Different archer models
  are allowed. English Archer and Erika Archer are the prototype visual choices.
- The local headset player sees tracked hands/controllers only for now; the
  opponent appears as a full archer. The injury viewpoint follows the character's
  animated eyes as agreed in task #17; physical validation remains task #35.
- Front/side/back references, bow/hit-region design review and preparation
  boundaries are in [Docs/Art/ArcherDesign.md](Docs/Art/ArcherDesign.md).
- Both active prefabs use prepared Humanoid derivatives with complete body,
  finger and eye mappings. Rig sources, import settings and validation are in
  [Docs/Art/RigPreparation.md](Docs/Art/RigPreparation.md).
- Keep the retained heavy Templar Knight separate from the player archer selection.
  Record sources, licenses and actual preparation status in
  [Docs/Art/CharacterAssets.md](Docs/Art/CharacterAssets.md).

## Game modes

- 1v1 PvP: online duels against another player over the internet, including
  players in different locations and on different networks.
- PvE: duel against an AI-controlled archer.

## Project scope

- A two-week project developed by two people.

## Target hardware

- Use the classroom-provided Meta Quest 2 for development and testing.
- Run the game on a PC with the Quest 2 connected as the VR headset.
- Use Unity OpenXR 1.16.1 for PC VR with the Oculus Touch controller profile.

## Running the VR tracking scene

- Open `Assets/_Game/Scenes/VRTrackingTest.unity` in Unity 6000.3.25f1.
- On the Windows test PC, configure [Meta Quest Link](https://developers.meta.com/vr/documentation/unity/unity-link/)
  as the active OpenXR runtime, connect the Quest 2, and launch its Link session.
- In the Meta Link desktop app, enable Settings > General > Unknown Sources
  to allow the Unity Editor and local development builds to run.
- Enter Play mode and verify head movement plus both controller markers: blue
  for left and orange for right. The rig requests floor-level tracking; check
  floor alignment and comfortable reach on the headset.
- Run the `BowMan.VR.EditorTests` EditMode tests in Test Runner before a PR.
- Build the enabled tracking scene for Windows x86_64 through Build Profiles.
  Put generated builds under `Builds/PCVR/`, which is excluded from Git.
- A successful Quest 2 test in both the Editor and Windows player satisfies
  the hardware acceptance criterion in task #2.

## Reviewing archer appearance on Quest 2

- Open `Assets/_Game/Scenes/VRCharacterReview.unity`, start Quest Link on the
  headset, then enter Play. Restart Play if Link was not active when XR initialized.
- English Archer is on the left and Erika Archer on the right, 3.5 metres from
  the rig origin. Inspect textures, face/hair transparency, floor contact and scale.
- The 1.75-metre body height is a provisional review setting. Both characters
  have valid Humanoid rigs and neutral relaxed-arm poses. Their normal prefabs
  do not play the rig-test loop; gameplay archery and injury clips remain separate.
- Source files, licenses and preparation status are in
  [Docs/Art/CharacterAssets.md](Docs/Art/CharacterAssets.md).
- The Windows review player is generated at
  `Builds/PCVRCharacterReview/BowManVR-Characters.exe`. It starts this review
  scene directly; the tracking scene's default build settings stay unchanged.

## Running the archer rig test

- Open `Assets/_Game/Scenes/ArcherRigTest.unity` and enter Play without a headset.
  The outside camera shows both archers playing the same 12-second Humanoid clip.
  Inspect head/torso motion at 2 s, shoulders/arms at 4 s, fingers at 6 s and
  left/right steps at 8/10 s. Pause to inspect skin and support contacts.
- Each character's Head has an `EyeAnchor` at the eye-bone midpoint. It follows
  the animated head for later first-person injury-camera integration; it does
  not move the current XR camera or establish headset comfort acceptance.
- Preserve original FBXs, prefab GUIDs and the derived models' `.meta` mappings.
  Blender sources/rebuild scripts live in `SourceAssets/EnglishArcher/` and
  `SourceAssets/ErikaArcher/`; runtime derivatives live under each ThirdParty asset.
- Run `BowMan.Characters.EditorTests` and `BowMan.VR.EditorTests` before a rig PR.
  Detailed preparation and review boundaries are in
  [Docs/Art/RigPreparation.md](Docs/Art/RigPreparation.md).

## Running the duel arena test scene

- Open `Assets/_Game/Scenes/DuelArena.unity` in Unity 6000.3.25f1 and enter Play
  mode to view the layout through the desktop overview camera.
- `Archer1Spawn` is at `(0, 0, -10)` and faces +Z; `Archer2Spawn` is at
  `(0, 0, 10)` and faces -Z. Both origins sit on the floor at Y=0.
- English Archer and Erika Archer mark the blue and orange positions. Each
  spawn has a floor marker and a short strip pointing toward its opponent.
- The floor is 12 by 28 metres with a clear firing lane between the archers.
  The 20-metre separation is a provisional test distance; validate final scale
  and comfort on Quest 2.
- Hidden capsule visuals retain their colliders as simple shot-test targets;
  these are not body-region hitboxes. Final character design, body-region hit
  detection, bow interaction and gameplay VR integration have their own tasks.
- A desktop test arrow fires automatically on Play. Focus the Game view and
  press Space, or click **Fire test arrow**, to repeat it. The panel reports
  shots, hits, and the most recent impact point. The default shot hits the
  opposing orange capsule; no headset is required for this test.
- `Assets/_Game/Prefabs/TestArrow.prefab` uses its root as the arrow tip and local +Z
  as forward. Instantiate it and call `ArrowFlight.Launch(velocity, owner)`;
  subscribe to `Hit` before launching to receive the first solid impact.
  Pass the firing archer's root to ignore its colliders. Trigger colliders are
  ignored. Flight uses gravity and a swept tip ray each fixed step. The arrow
  stops at impact; moving-body attachment belongs to task #20.
- Test speeds and appearance are provisional. Missed arrows expire after
  8 seconds; impacted arrows expire after 3 seconds or the remaining flight
  lifetime, whichever comes first. Run `BowMan.Gameplay.PlayTests` in PlayMode
  and `BowMan.VR.EditorTests` in EditMode before reviewing arrow changes.

## Bow interaction

- Hold the bow with one hand; pull and release the string with the other.
- Shot strength increases with draw distance, reaching full strength at a
  provisional maximum draw of 0.70 m (70 cm).
- Use 70 cm as a temporary adult-arm-length placeholder; calibrate comfortable
  draw distance when the classroom headset is available.
- Exact controller button mappings remain to be defined and tested on Quest 2.

## Combat realism and injury feedback

- Aim for realistic arrow impacts and visible injury feedback.
- An arrow hit can knock an archer to the ground. The archer stays down briefly,
  then gets up if still alive. The Last of Us is the reference for this temporary
  knockdown and recovery behavior.
- Show where the arrow hit the body and the internal damage it caused, including
  organs struck and bones broken when applicable.
- Show blood spilling from arrow wounds as part of the impact and injury effects.
- Sniper Elite is the reference for anatomical hit visualization and damage
  feedback. These are requested features, not implemented systems.

## Body-specific injury sequences

- Reactions must match the actual body part and side hit by the arrow. Every
  hittable body region needs its own appropriate injury sequence; one generic
  knockdown for all hits does not meet the requirement.
- Chest hit: fall backward and land with the back on the ground, remain down
  briefly, then get up if the hit was not lethal.
- Left-leg hit: kneel on the injured left leg, place the left hand on the ground
  for support, then recover and get up if still alive.
- Right-leg hit: kneel on the injured right leg, place the right hand on the ground
  for support, then recover and get up if still alive.
- Headshot: instant kill regardless of remaining health. Do not play a temporary
  knockdown followed by recovery for a dead archer.
- Hit markers, arrow placement, organ damage, bone fractures, and blood effects
  must correspond to the actual impact location, not a random or generic injury.
- Follow the complete fifteen-region inventory and reactions agreed in
  [task #17](https://github.com/matheusluciano99/Bow-Man-VR/issues/17).
  Additional regions and reactions require a design decision.

## Combat details still to decide

- Reactions for the remaining body regions, exact recovery durations, and how
  recovery interacts with turns and lethal damage other than headshots.
- Damage values and the gameplay effects of organ injuries and broken bones.
- How to present internal injuries in VR, including whether to use an overlay,
  an X-ray view, or another approach.
- Calibration and comfort of the agreed first-person animated-eye viewpoint
  during knockdown and recovery on the target headset.

## Task tracking

- GitHub Kanban: https://github.com/users/matheusluciano99/projects/1, linked to
  the Bow-Man-VR repository.
- Columns: To Do, In Progress, Review / Test, Done. Do not add a separate Backlog.
- Assign each issue to the developer responsible before moving it to In Progress.
  Keep active tasks assigned and verify the Assignees field when starting work.
  Each developer must assign the tasks they take responsibility for; preserve
  existing assignees unless responsibility changes explicitly.
- Split work into small, individually reviewable tasks with completion criteria
  and dependency links. Give each body-region injury sequence its own card once
  the region and its reaction are defined.

## Pull request workflow

- Start each task on a dedicated branch based on the current `origin/main`.
  Commit and push task changes to that branch; do not commit or push directly
  to `main`.
- Open a pull request targeting `main` and link the related issue.
- Move the task to Review / Test when the pull request is ready for review.
  Mark it Done and close its issue only after the pull request is merged and
  the relevant checks have passed.
- Keep the responsible developer assigned through review. Do not merge a pull
  request without explicit authorization.
- Use regular merge commits. Use squash only when explicitly requested.

## Unity editor connection

- MCP for Unity is pinned to v10.3.0 in `Packages/manifest.json`, matching the
  existing WSL server at `http://127.0.0.1:8080/mcp`.
- Codex registers that HTTP server as `unityMCP`. Restart the Codex client after
  adding the server so its tools load into the session.
- Verify the connected editor is Bow Man VR before making changes through MCP,
  especially when multiple Unity projects are open.
