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
- Select English Archer or Erika Archer on the main menu with the pointer or G. Preserve each seat’s independent choice across modes and rematches. Use the selected model’s hands, captured idle and bow action, and recorded voice. The solo opponent uses the other model.
- The local headset player sees tracked hands/controllers only for now; the
  opponent appears as a full archer. During injury, the local viewpoint follows
  the animated eyes as defined in [Docs/InjuryReactions.md](Docs/InjuryReactions.md).
  Physical viewpoint validation remains task #35.
- Between shots, both standing archers breathe and shift their weight with
  planted boots. Preserve the local tracked view during idle. Give injuries
  immediate control of the pose and resume idle only after a survivor recovers.
- Front/side/back references, bow/hit-region design review and preparation
  boundaries are in [Docs/Art/ArcherDesign.md](Docs/Art/ArcherDesign.md).
- Both active prefabs use prepared Humanoid derivatives with complete body,
  finger and eye mappings. Rig sources, import settings and validation are in
  [Docs/Art/RigPreparation.md](Docs/Art/RigPreparation.md).
- Keep the retained heavy Templar Knight separate from the player archer selection.
  Record sources, licenses and actual preparation status in
  [Docs/Art/CharacterAssets.md](Docs/Art/CharacterAssets.md).

## Duel environment, arrow and hands

- Choose a castle courtyard, woodland archery range or village training yard at
  random for each new duel. Keep that setting for the entire match. In PvP the
  host chooses the setting for both players. Preserve the clear firing lane.
- Give the shared gameplay arrow a medieval head, wooden shaft, feather
  fletching and nock. Use the same model from draw through impact and preserve
  the tip origin and embedded wound position.
- Replace the playable rig’s controller markers with visible archer hands that
  follow the controllers. Support the bow grip, nock, draw and release with
  either bow hand. Keep the diagnostic markers in the tracking test.

## Game modes

- 1v1 PvP: online duels against another player over the internet, including
  players in different locations and on different networks. Players connect
  through Unity Relay with a host-issued join code; the host is Archer 1 and the
  joining player is Archer 2 (see D024).
- PvE: duel against an AI-controlled archer.
- Tiro ao alvo: two archers compete online on separate PCs. Both select the
  target competition, then host/join with a Relay code. Alternate ten arrows per
  archer at the same target. Before hosting, choose 15 (default), 30, 50, 60 or 70 metres. Keep the host’s distance for both seats and rematches. The host scores each tip contact, and both
  players see both totals, arrows remaining and the winner or tie.

## Project scope

- A two-week project developed by two people.

## Distribution and lab hardware

- Deliver an HTML game on itch.io. Players select Play, then Enter VR in a WebXR-capable browser. Use **Bow Man VR > Build itch.io WebXR Game** to generate the HTML ZIP. The Windows game player is a lab test artifact.

- Use the classroom-provided Meta Quest 2 for development and testing.
- Run PC VR lab checks with the Quest 2 connected to the test PC.
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
- Each character now has solid, bone-bound volumes for all fifteen body regions.
  Hidden placeholder capsule colliders are disabled. Review provisional volume
  fit when adding new poses; bow interaction and gameplay VR integration remain
  separate tasks.
- A desktop test arrow fires automatically on Play. Focus the Game view and
  press Space, or click **Fire test arrow**, to repeat it. Choose a body-region
  button to aim at that volume. The panel reports the actual region, anatomical
  side, impact point and direction. The default shot targets Erika's chest;
  no headset is required for this test.
- `Assets/_Game/Prefabs/TestArrow.prefab` uses its root as the flying arrow tip and local +Z
  as forward. Instantiate it and call `ArrowFlight.Launch(velocity, owner)`;
  subscribe to `Hit` before launching to receive the first solid impact.
  Pass the firing archer's root to ignore its colliders. Trigger colliders are
  ignored. Flight uses gravity and a swept tip ray each fixed step. The arrow
  stops at impact. Embedded roots anchor the entry on the struck volume while
  the visible tip penetrates along the recorded finite path (provisional 25 cm).
  Both follow the animated bone until the character is removed.
- `ArrowFlight.BodyHit` reports one anatomical `BodyImpact` before the existing
  generic `Hit` event. Use only one event to apply a shot outcome. Details,
  authoring instructions and validation are in [Docs/BodyHits.md](Docs/BodyHits.md).
- Test speeds and appearance are provisional. Missed arrows expire after
  8 seconds; scenery impacts expire after 3 seconds or the remaining flight
  lifetime, whichever comes first. Body arrows persist through injury/recovery;
  remove the character hierarchy to clean them up. Run `BowMan.Gameplay.PlayTests` in PlayMode
  and `BowMan.VR.EditorTests` in EditMode before reviewing arrow changes.
- Both spawn roots have `ArcherHealth`. The test panel shows the orange
  target's current health, Alive/Defeated state, and defeat event count.
  Maximum health is 100 and each test-arrow hit deals 25 damage; these are
  provisional test values, not final combat balance. Including the automatic
  first shot, four hits reach zero. Further hits keep health at zero and the
  defeat event count at one. Restart Play mode to reset the test.
- `ArcherHealth.TakeDamage(amount)` requires positive integer damage, clamps
  health at zero, and emits `Defeated` once. The desktop launcher applies its
  test damage to health on the struck collider or its parent. Floor impacts
  have no health target. Body-region damage, death animations, turn handoff,
  and match outcomes belong to later tasks.

## Starting the game

- `Assets/_Game/Scenes/MainMenu.unity` is the entry scene. Its panels offer
  **PvE — vs AI archer** (opens `LocalDuel`), **PvP — online duel** (opens
  `PvPConnectionTest`) and **Tiro ao alvo** (opens `TargetRange`). Point the right controller's ray at a panel and pull the
  trigger; without a headset press 1 / 2 / 3 or click a panel.
- Select the bow-hand panel or press H to choose which hand holds the bow and
  which hand draws. All modes start with that saved choice. While aiming, the
  duel HUD shows the next action for the current hand and input method.
- Open **Credits** with the pointer or C. Read the contributor pages or choose
  **Notices** for the source and license text shipped with the player. Choose
  **Menu** or press Esc to return. Arrow keys also turn pages.
- In either mode, the left controller's Menu button or Esc returns to the menu.
  Leaving the PvP scene ends the online session. Health, turn and the
  VICTORY/DEFEAT result stay on the local duel's headset text (#8).
- Run **Bow Man VR > Build Mode Menu And Game Scenes** after changing the menu,
  duel or PvP generators. It regenerates the four scenes and puts MainMenu,
  LocalDuel, PvPConnectionTest and TargetRange first in the build list (D025).
- **Bow Man VR > Build Windows Game Player** writes `Builds/PCVRGame/BowManVR.exe`
  with those four scenes; add `-desktop` to run it without a headset.
- Both duel modes show health, turn and result. PvP supports rematch after both
  seats accept with A / R. Run `BowMan.Menu.EditorTests` (EditMode) and `BowMan.Menu.PlayTests`
  (PlayMode) before a menu PR.

## Testing PvP connectivity

- One-time setup: in the Unity Cloud dashboard for the linked project
  (`cloudProjectId` in ProjectSettings), enable **Relay**. Both machines sign in
  anonymously; no Unity account login is needed in the player.
- In Unity 6000.3.25f1, run **Bow Man VR > PvP > Build Connection Test Scene**.
  It regenerates `Assets/_Game/Scenes/PvPConnectionTest.unity` and
  `Assets/_Game/Prefabs/Networking/NetworkArcherPresence.prefab`; commit both
  with their `.meta` files after changing the generator.
- Run **Bow Man VR > PvP > Build Windows Connection Player** to build
  `Builds/PCVRPvP/BowManVR-PvP.exe`. It does not change the default build scenes.
- Host: enter Play (or start the player), click **Host** in the desktop window
  and share the code shown on the panel and in the headset.
- Join: on the other machine, type the code and click **Join**. Start a player
  without a headset with `BowManVR-PvP.exe -desktop`.
- Expected: the host reads "Connected as Archer 1", the joining player
  "Connected as Archer 2", each rig stands on its spawn, and each player sees
  the opponent’s equipped archer during the match. The lobby uses head and hand
  markers for connection diagnostics. A third player is refused by
  Relay's one-opponent limit ("Connection failed: …"); "Match is full" appears
  only if Netcode approval refuses it first.
- Cross-network acceptance for #11 needs two PCs on different networks, for
  example one on a phone hotspot. Editor plus a `-desktop` player on one PC is a
  quick smoke test only.
- When the joiner receives the second seat, the host starts a duel. Each player
  sees the opponent's archer and their own bow. The host resolves shots, health,
  injuries, death and turn handoff. Both peers show the same wound and anatomy.
  Host and join controls, test setup and acceptance are in [Online duel](Docs/OnlineDuel.md).
  Run `BowMan.Networking.EditorTests` and the two-Editor Relay test before a networking PR.

## Bow interaction

- `Assets/_Game/Scenes/DuelTurnTest.unity` reviews the two-archer turn contract
  without a headset. It explicitly exposes manual animation-completion buttons;
  those controls do not claim completed injury animations. Runtime gates and
  callback ownership are documented in [Docs/TurnFlow.md](Docs/TurnFlow.md).
- `Assets/_Game/Scenes/AIArcherTest.unity` adds the AI as archer 2, using the same
  arrow and turn authority. Animation callbacks remain explicitly manual in this
  review scene. Provisional aim error, thinking delay and speed need play tuning.

- The visible opponent uses the recorded archery action on its aiming turn. Match the equipped bow hand and string to the shot in both PvE and PvP. Show a single flying projectile per accepted release. Injury and defeat take priority over the standing bow pose.
- Playable duels show the local archer’s hands on the two tracked controllers. Keep the diagnostic markers in the tracking test scene. Switching bow hands swaps grip and string poses, and losing a controller’s tracking hides its hand. Physical grip and pinch placement require Quest 2 review.
- Hold the bow with one hand; pull and release the string with the other.
- Shot strength increases with draw distance, reaching full strength at a
  provisional maximum draw of 0.70 m (70 cm).
- Use 70 cm as a temporary adult-arm-length placeholder; calibrate comfortable
  draw distance when the classroom headset is available.
- The bow stays attached to the selected tracked hand without holding grip;
  the opposite trigger grabs/releases the nearby string. Its local +Y aligns
  with controller grip +Z using a +90-degree local X rotation. Both bow hands
  are supported; left is the initial default. The test panel's **Bow hand**
  button switches sides and cancels a pending tracked draw.
- While the trigger stays held, a temporary loss of the drawing hand's pose
  holds the last valid pull relative to the bow. Losing bow-hand tracking,
  releasing while untracked, pausing, losing focus or a shooting lock cancels.
  The 0.96-metre arrow keeps its tip beyond the grip at full draw. The bow remains
  visually attached during a shooting lock. `Bow.SetShotAllowed` gates both
  input paths without changing tracked head/controller poses.

## Reviewing the medieval environments

- Use **Bow Man VR > Arenas > Rebuild medieval environments** to regenerate the three scenery prefabs under `Assets/_Game/Resources/Arenas/`. Each round places its selected prefab around the two spawns.
- Compare the three settings from both seats in the Unity Editor. Check the opponent and flight path against each backdrop. Run the two-Editor Relay test to compare the initial setting and rematch on both seats. Verify headset scale and binocular readability on Quest 2.
- Original scenery sources, author credits and Blender derivative instructions are in [the Poly Haven inventory](Assets/ThirdParty/PolyHaven/Attribution.md).

## Duel audio

- Use the arena selected for the round to choose its ambience on both seats.
  Play spatial draw and release sounds at the bow, flight on the moving arrow,
  and contact at the confirmed impact point. A cancelled draw stays quiet on release.
- The string starts creaking above 60% draw, grows louder toward full draw and
  quiets while held. Easing back to the threshold, cancelling or firing stops it.
- X on the left controller or V on the keyboard cycles game volume through mute
  and four audible levels. Keep this preference local across modes and rematches.
- Source recordings and edits are listed in [Duel audio credits](Assets/ThirdParty/DuelAudio/Credits.md).
  Verify spatial placement and comfort on Quest 2.

## Running the local VR duel

- Open `Assets/_Game/Scenes/LocalDuel.unity` after starting Quest Link. The player
  faces Erika across the existing 20-metre arena and sees tracked controllers
  plus their bow; the hidden local body still receives anatomical impacts.
- The bow stays equipped; the opposite trigger draws/releases. Right-controller B
  switches bow hands during the player's aiming turn. Right-controller A restarts
  after victory/defeat. Hand preference survives restart.
- The headset-visible text shows both health values, current flow, draw strength,
  handedness and restart controls. Flying arrows, opponent turns, injuries and
  defeat lock nocking/release through the shared bow gate.
- Without a headset, focus Game view and press Tab for desktop controls; arrow
  keys aim and Space draws/releases. H changes hands during your aiming turn;
  R restarts a finished match. Tab returns to tracked input.
- Injuries apply animated-eye displacement/rotation to the tracking origin while
  preserving live local head/controller poses. A neutral frame adds no extra
  standing height; restart restores the floor origin without restarting XR.
- In manual headset review, the wearer fires every test shot. Assistants can
  label regions and capture frames/hit data; keep automatic demonstrations
  separate from manual acceptance. In regional animation review, disable AI
  shooting and return practice control to the wearer after a miss or complete
  recovery, preserving health/wounds and all tracking, focus and injury gates.
  Review ordinary AI combat separately.
- Build this scene explicitly for Windows x86_64 under `Builds/PCVRDuel/`.
  Shared tracking-scene build settings remain unchanged. Run the lab checklist in
  [Docs/LabTestPlan.md](Docs/LabTestPlan.md); physical eye alignment, comfort,
  grip offsets and draw calibration remain pending on Quest 2.

## Running the bow interaction test

- Open `Assets/_Game/Scenes/BowInteractionTest.unity` and enter Play. For VR,
  start Quest Link first, as with the tracking scene. The bow remains in the
  selected hand; bring the other controller within 12 cm of the resting string,
  press and hold its trigger, pull backward, then release the trigger to shoot.
- Aim with the bow's orientation and the line between the two hands. Backward
  draw controls speed up to a provisional 25 m/s at 70 cm. Forward/sideways-only
  pulls and draws below 3 cm do not fire. Extra draw is capped at full strength.
- Without a headset, click **Use desktop controls** in the Game view. Arrow
  keys aim; hold Space to draw and release it to shoot. The panel shows draw
  strength, shot count, hit count and the latest impact. Click **Use VR controls**
  to return to tracked input; switching cancels the current draw.
- Erika stands ten metres from the rig with one simple capsule target. This is
  an interaction test, not the duel's final distance or body-region damage system.
  The local view uses controller markers without a full player body.
- `Assets/_Game/Prefabs/Bow.prefab` reuses English Archer's textured grip/limbs,
  replaces its static string with a moving string, and uses `ArrowFlight` for
  released arrows. Bow limb bending and character animation remain separate work.
- Run `BowMan.Gameplay.PlayTests` and `BowMan.VR.EditorTests` before a bow PR.
  Physical grip alignment, handedness, reach and release comfort still require
  Quest 2 testing; automated/desktop checks do not establish hardware acceptance.
- For a standalone test, build this scene for Windows x86_64 to
  `Builds/PCVR/BowInteraction/BowManVR-Bow.exe`. The tracking scene remains the
  default in shared build settings.

## Combat realism and injury feedback

- Aim for realistic arrow impacts and visible injury feedback.
- An arrow hit can knock an archer to the ground. The archer stays down briefly,
  then gets up if still alive. The Last of Us is the reference for this temporary
  knockdown and recovery behavior.
- Show where the arrow hit the body and the internal damage it caused, including
  organs struck and bones broken when applicable.
- Present internal injury only after a confirmed fatal arrow hit, as a brief
  X-ray finishing sequence inside the defeated archer. Surviving hits must not
  trigger this view; their normal blood and regional reactions still apply.
  The local duel shows a stereo cutaway with the recorded path, contacted
  organs and fractured rig bones. Its three-second duration and headset
  readability remain provisional until VR review.
- The shared arrow emits blood at the actual wound and follows its hit volume.
  Spilled droplets move in world space. See [wound blood](Docs/BloodEffects.md)
  for rebuild controls and provisional visual settings.
- Sniper Elite is the reference for anatomical hit visualization and damage
  feedback. Organ analysis uses fitted BodyParts3D tissue and the actual finite
  arrow path; see [Docs/Anatomy.md](Docs/Anatomy.md). Bone contacts use the
  animated game skeleton. A provisional 75% draw fractures contacted bones.
  The duel publishes `FatalityRequested`
  only for its lethal arrow, after health confirms death.

## Body-specific injury sequences

- Use free recorded human performances for revised injury motion. Review the
  full take on both archers, then choose the short action that matches the
  region, side and outcome. Record the source and license with the asset.
- Fit palms, forearms and boots on the clothed rigs. Check intermediate poses,
  actual wound placement and floor support through recovery or collapse.
  Regional source choices and rebuild commands are in [Docs/InjuryReactions.md](Docs/InjuryReactions.md).
- Review individual hits in `Assets/_Game/Scenes/InjuryAnimationTest.unity`.
  Select a region and fire the straight test arrow. The first archer keeps the
  practice turn. After a fatal collapse the review resets both characters while
  keeping the selected region. Normal duels still alternate shooters.
- Bow-duel archers face each other. Current hit regions are head, neck, chest,
  abdomen, pelvis, left/right shoulders, left/right arms, left/right hands,
  left/right legs and left/right feet. Each needs its own reaction. Back hits
  are outside the bow-duel scope; sword duels are only a future idea.
- Reactions must match the actual body part and side hit by the arrow. Every
  hittable body region needs its own appropriate injury sequence; one generic
  knockdown for all hits does not meet the requirement.
- Chest hit: fall backward and land with the back on the ground. After the
  hold, return to relaxed standing with both arms lowered if alive.
- Left-leg hit: kneel on the injured left leg, place the left hand on the ground
  for support, then recover and get up if still alive.
- Right-leg hit: kneel on the injured right leg, place the right hand on the ground
  for support, then recover and get up if still alive.
- Headshot: instant kill regardless of remaining health, with immediate collapse
  and loss of posture. A dead archer never recovers or resumes shooting.
- Neck hit: also an instant kill regardless of health. Reach toward the actual
  neck wound, let the knees buckle, then collapse without recovery.
- Left-arm hit: recoil on the left side, cradle the injured left arm with the
  right hand while standing, then recover if alive. Mirror the sides for a
  right-arm hit.
- Abdomen hit: fold forward, hold the actual wound, drop to both knees, then
  stand if alive.
- Shoulder hit: recoil on the injured side, turn the torso away and cup that
  shoulder with the opposite hand while standing; use separate left/right motion.
  A lethal shoulder shot uses one recorded opposite-hand reach and fall, mirrored for the right shoulder.
- Hand hit: jerk the injured hand back, open its fingers and support its wrist
  with the opposite hand; use separate left/right motion.
- Pelvis hit: hips buckle into a seated fall with both hands supporting the body
  from behind; stand if alive.
- Foot hit: lift the injured foot, balance on the other leg, then lower the foot
  and regain stance if alive; use separate left/right motion.
- Hit markers, arrow placement, organ damage, bone fractures, and blood effects
  must correspond to the actual impact location, not a random or generic injury.
- Follow the complete fifteen-region inventory and reactions agreed in
  [task #17](https://github.com/matheusluciano99/Bow-Man-VR/issues/17).
  Additional regions and reactions require a design decision.
- The local injury viewpoint follows the character's eyes through falling,
  kneeling and getting up, with live headset tracking so the player can look
  around. Calibrate against the tracked neutral pose and validate the moving
  viewpoint on Quest 2 before claiming comfort acceptance.
- The region inventory, reaction table and review cases are in
  [Docs/InjuryReactions.md](Docs/InjuryReactions.md).
- For surviving hits, hold for 0.75 seconds after a chest fall, two seconds
  after a foot hit, and three seconds for the other regions. The foot recovery lasts 0.8 seconds after the hold.
  Keep the original foot recoil. Block shooting until recovery
  finishes, then give the injured archer the next turn. A lethal hit ends the
  match immediately. A miss passes the turn after scenery impact or flight expiry.
- Resolve each shot outcome once; arrow cleanup must not advance the turn again.
- Keep local controller tracking live during injury. Cancel any nocked arrow
  and block drawing/firing until recovery is complete and the archer has the turn.
- Head and neck hits always kill. Other regions kill when applied damage reaches
  zero health; damage values and organ/fracture effects remain separate tuning.

## Combat details still to decide

- Exact per-model hitbox dimensions, calibrated from the region boundaries in
  the injury table and reviewed on the rigged archers.
- Damage values and the gameplay effects of organ injuries and broken bones.
- Duration, scale, contrast and headset readability of the fatal X-ray close-up.
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
