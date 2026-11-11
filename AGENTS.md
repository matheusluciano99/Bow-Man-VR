# Bow Man VR

This is the primary source of project context and instructions for coding agents.
Keep the project brief in this file; `CLAUDE.md` references it.

## Game concept

- A Unity bow-and-arrow game for virtual reality (VR).
- Inspired by the classic Bow Man: two archers take turns shooting arrows at
  each other, with aiming and shot strength determining the arrow's trajectory.
- Adapt that archery duel to VR. Specific VR controls have not been decided yet.

## Game modes

- 1v1 PvP: duel against another player.
- PvE: duel against an AI-controlled archer.

## Project scope

- A two-week project developed by two people.

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
- Define the complete inventory of hittable regions and their reactions before
  implementation. Reactions for regions beyond the examples above are not yet
  specified and must not be invented as settled design decisions.

## Combat details still to decide

- Reactions for the remaining body regions, exact recovery durations, and how
  recovery interacts with turns and lethal damage other than headshots.
- Damage values and the gameplay effects of organ injuries and broken bones.
- How to present internal injuries in VR, including whether to use an overlay,
  an X-ray view, or another approach.
- How player knockdown and recovery affect the VR viewpoint; this needs a design
  decision and testing on the target headset.

## Task tracking

- GitHub Kanban: https://github.com/users/matheusluciano99/projects/1, linked to
  the Bow-Man-VR repository.
- Columns: To Do, In Progress, Review / Test, Done. Do not add a separate Backlog.
- Split work into small, individually reviewable tasks with completion criteria
  and dependency links. Give each body-region injury sequence its own card once
  the region and its reaction are defined.

## Unity editor connection

- MCP for Unity is pinned to v10.3.0 in `Packages/manifest.json`, matching the
  existing WSL server at `http://127.0.0.1:8080/mcp`.
- Codex registers that HTTP server as `unityMCP`. Restart the Codex client after
  adding the server so its tools load into the session.
- Verify the connected editor is Bow Man VR before making changes through MCP,
  especially when multiple Unity projects are open.
