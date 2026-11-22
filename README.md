# Bow Man VR

VR archery for release on itch.io. Duel against the AI or an
online opponent. For **Tiro ao alvo**, select the target competition on both PCs.
One player selects **Host**, the other enters the code and selects **Join**.
Alternate ten arrows each. The highest total wins.

For lab tests, use Meta Quest 2 through Quest Link with the Windows review player.
Open the project with Unity **6000.3.25f1**. Game requirements, scene instructions,
hardware checks, and the contribution workflow are in [AGENTS.md](AGENTS.md).

## Repository layout

| Location | Purpose |
| --- | --- |
| `Assets/_Game/Scripts/` | Game code and its runtime assembly. |
| `Assets/_Game/Editor/` | Project-owned Unity authoring tools, excluded from players. |
| `Assets/_Game/Scenes/` | Game and test scenes. |
| `Assets/_Game/Prefabs/`, `Assets/_Game/Materials/` | Project-authored reusable assets. |
| `Assets/_Game/Models/` | Project-generated native geometry, including fitted anatomy. |
| `Assets/_Game/Input/` | Game input actions. |
| `Assets/_Game/Animations/` | Project-owned clips, animation imports and controllers. |
| `Assets/ThirdParty/<asset>/` | Imported models, textures, and attribution grouped by asset. |
| `Assets/_Game/Tests/Editor/`, `Assets/_Game/Tests/PlayMode/` | Unity EditMode and PlayMode tests. |
| `Assets/_Game/Settings/` | Game render-pipeline configuration. |
| `Assets/XR/` | Package-managed XR configuration; OpenXR expects this location. |
| `Assets/TutorialInfo/` | Unity template help content. |
| `Packages/`, `ProjectSettings/` | Shared dependencies and Unity project settings. |
| `Docs/Art/` | Character candidates, sources, and preparation status. |
| `Docs/Decisions.md` | Settled development choices, reasons, and open questions. |
| `Docs/InjuryReactions.md` | Fifteen injury regions, reaction/turn rules and review cases. |
| `.agents/`, `.claude/` | Shared development rules and decision-documentation skill entry points. |
| `SourceAssets/` | Authoring originals such as Blender scenes or layered texture files. |
| `SourceAssets/Downloads/` | Local download archives; ignored by Git and never imported by Unity. |
| `Artifacts/` | Local screenshots and validation reports; ignored by Git. |
| `Builds/PCVR/`, `Builds/PCVRCharacterReview/` | Generated Windows tracking and character-review players; ignored by Git. |

Keep project-owned Unity assets under `Assets/_Game` and imported assets under
`Assets/ThirdParty`. Package-managed XR configuration stays at `Assets/XR`;
Unity template help remains under `Assets/TutorialInfo`.
Keep download ZIPs and review files out of `Assets` and `Builds`. Create feature
folders as they become useful; keep each imported character's files together.

## Asset handling

- Commit an asset and its `.meta` file together. Move existing Unity assets
  through the Editor, preserving their GUIDs and references.
- Record the creator, source URL, license, and modifications alongside imported
  third-party assets. Character assets must be free to acquire and usable in games.
- Models and textures use regular Git binary storage. Keep individual files small
  enough for [GitHub file limits](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github).
  Binary revisions enlarge Git history; revisit storage before frequent large art updates.
- Keep original download archives in the local download cache. Commit the files
  Unity actually imports and any authoring originals needed to reproduce changes.
- Keep files visible in VS Code; do not hide models, textures, `.meta` files,
  scenes, or project folders with workspace exclusion filters.

The retained Templar Knight and the archer candidates are documented in
[character assets](Docs/Art/CharacterAssets.md). The knight is not the selected
player archer.

Prototype character choices, front/side/back references and the first-person
presentation are in [archer design](Docs/Art/ArcherDesign.md).

Prepared Humanoid models, the desktop movement check and reproducible Blender
work are documented in [rig preparation](Docs/Art/RigPreparation.md).

Anatomical hit volumes, the arena test controls and impact events are documented
in [body-region hit detection](Docs/BodyHits.md). For the impact effect, see
[wound blood](Docs/BloodEffects.md).

Shot ownership, defeat and injury/recovery handoff are documented in
[duel turn flow](Docs/TurnFlow.md).
