# Mode selection and VR match feedback design (#14)

## Intent

Give the game an entry point where the player picks PvE or PvP in the headset,
and a way back to that choice after or during a match. Health, active turn and
the VICTORY/DEFEAT result are already shown in the headset by the local duel
(#8); this task makes the mode choice and navigation complete around it.

## Goals

- `MainMenu.unity` starts the game: two large panels in front of the player,
  **PvE — vs AI archer** and **PvP — online**.
- Selecting: point the right controller's ray at a panel and pull the trigger.
  The hovered panel highlights. Desktop fallback: keys 1/2 or a mouse click.
- PvE opens `LocalDuel`; PvP opens `PvPConnectionTest` (Host/Join from #11).
- From either mode, the left controller's Menu button or Esc returns to the menu.
  The finished-match hint in the duel mentions it.
- One-click generation of the menu scene and the build list
  (MainMenu, LocalDuel, PvPConnectionTest), and a Windows game player.

## Non-goals

- PvP duel synchronization (#12) and rematch flow (#13): PvP still opens the
  connection test until those land.
- Redesigning the duel HUD from #8 (health/turn/result text stays as built).
- XR Interaction Toolkit or new packages.

## Constraints

- Branch stacks on `feat/8-local-vr-duel` plus `feat/11-pvp-connectivity`
  (pre-main-update commit `f50a96d`); #8 and #11 are unmerged dependencies.
- Unity 6000.3.25f1, Input System actions as in `VRBowInput`
  (`<XRController>{RightHand}/triggerPressed`, `<XRController>{LeftHand}/menu`).
- NetworkManager is DontDestroyOnLoad: leaving the PvP scene must shut the
  session down and destroy the network object.

## Approaches

| Approach | Trade-off |
| --- | --- |
| **Controller ray + trigger on world panels (chosen)** | Natural VR menu; small custom raycast with no new package. |
| Controller buttons (X = PvE, A = PvP) | Least code, but not how VR menus usually work. |
| XR Interaction Toolkit | Complete UI interaction; new dependency that touches the #8 rig. |

## Design

- Assembly `BowMan.Menu` (`Assets/_Game/Scripts/Menu/`), referencing
  `Unity.InputSystem` and `BowMan.Networking` (for the `-desktop` XR switch).
- Pure: `GameMode { PvE, PvP }`; `GameScenes` (scene names, `For(mode)`,
  `ModeForKey(char)`). EditMode tests.
- `ModeOption` (panel: mode + highlight), `ControllerPointer` (right-hand ray,
  line, hovered option, trigger confirm; mouse ray when no XR device),
  `ModeMenu` (keys 1/2, loads the scene once), `ReturnToMenu` (Menu/Esc).
  PlayMode tests for pointer hover and the menu's load request.
- `PvPConnectionController` records its scene in `Start` and, when the active
  scene changes away from it, leaves the session and destroys its object.
- `DesktopXR.ApplyCommandLine()` (moved out of `LocalRigBinder`) so `-desktop`
  works from the menu, the first scene.
- Editor: `ModeMenuBuilder` → `Bow Man VR/Build Mode Menu And Game Scenes`
  (menu scene, LocalDuel and PvP scenes, build list) and
  `Bow Man VR/Build Windows Game Player` (`Builds/PCVRGame/BowManVR.exe`).
  `LocalDuelBuilder` and the PvP builder add `ReturnToMenu`.
- Docs: AGENTS "Starting the game" section; decision D022 (menu as the entry
  scene in the build list).

## Left to the builder

Panel size/placement, colors, ray length, exact copy on the panels.
