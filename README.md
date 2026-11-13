# Bow Man VR

A turn-based VR archery duel for PC, using a Meta Quest 2 through Quest Link.
Open the project with Unity **6000.3.25f1**. Game requirements, scene instructions,
hardware checks, and the contribution workflow are in [AGENTS.md](AGENTS.md).

## Repository layout

| Location | Purpose |
| --- | --- |
| `Assets/Scripts/` | Game code and its runtime assembly. |
| `Assets/Scenes/` | Game and test scenes. |
| `Assets/Prefabs/`, `Assets/Materials/` | Project-authored reusable assets. |
| `Assets/ThirdParty/<asset>/` | Imported models, textures, and attribution grouped by asset. |
| `Assets/Tests/Editor/`, `Assets/Tests/PlayMode/` | Unity EditMode and PlayMode tests. |
| `Assets/Settings/`, `Assets/XR/` | Render-pipeline and XR configuration. |
| `Assets/TutorialInfo/` | Unity template help content. |
| `Packages/`, `ProjectSettings/` | Shared dependencies and Unity project settings. |
| `Docs/Art/` | Character candidates, sources, and preparation status. |
| `SourceAssets/` | Authoring originals such as Blender scenes or layered texture files. |
| `SourceAssets/Downloads/` | Local download archives; ignored by Git and never imported by Unity. |
| `Artifacts/` | Local screenshots and validation reports; ignored by Git. |
| `Builds/PCVR/` | Generated Windows players; ignored by Git. |

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
