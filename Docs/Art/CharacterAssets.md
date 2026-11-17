# Character assets

The player archer should be a realistically proportioned medieval human in light
clothing or armor, using For Honor and Conqueror's Blade as visual references.
Use free assets only; multiple different archer models are allowed. The prototype
review uses English Archer and Erika Archer. Design is tracked in
[issue #38](https://github.com/matheusluciano99/Bow-Man-VR/issues/38).

## Retained Templar Knight

- Creator: everlasting17th / everlastinggrey.
- [Source](https://sketchfab.com/3d-models/templar-knight-rigged-game-ready-e57edad1bb8e4dbd8573f85695ceecde),
  licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- Imported source files and attribution: `Assets/ThirdParty/TemplarKnight/`.
- Original archive: `SourceAssets/Downloads/TemplarKnight/templar-knight-rigged-game-ready.zip`
  (local cache, excluded from Git).
- Blender inspection found a 52-bone skeleton, 12,995 weighted body vertices,
  24,012 body triangles, 4K character texture maps, and no animation clips.
- Model scale, Humanoid mapping, materials, and animation behavior need preparation
  before gameplay use. The knight's heavy armor does not meet the archer design.
- Local preview, provenance, and inspection report: `Artifacts/CharacterReview/`.

## Free archer candidates

| Candidate | Source and license | Remaining checks |
| --- | --- | --- |
| English archer, count_zero | [Sketchfab](https://sketchfab.com/3d-models/english-archer-8571c31e9c934458be2e434de71f94f5), CC BY 4.0 | GLB converted to FBX with extracted textures for the prototype: 32,466 triangles, three materials, nine 4K textures. Static model; needs rigging before animation. Original DAE uninspected. |
| Medieval man, Chenuka Wijesundara | [Sketchfab](https://sketchfab.com/3d-models/game-ready-pirate-hunter-viking-medieval-man-9f51d396733b451bab550b406b067805), CC BY 4.0 | Actual Pirate.fbx has 65 bones, 15,658 weighted vertices and 24,577 triangles; Unity Humanoid Avatar is valid. Credit Chenuka Wijesundara and Leon Steiner for the [gambeson](https://sketchfab.com/3d-models/gambeson-0fc44d2876ed449ca9473e78c83a8d39), also CC BY 4.0. Bow equipment and animation remain separate. |
| Viking/Medieval Game Character, CGenesis | [Sketchfab](https://sketchfab.com/3d-models/vikingmedieval-game-character-a9e99a47d2a24957ab6c08c9e57ad48c), CC BY 4.0 | Original nested archive contains static Viking.obj, not a rig: 137,230 source triangles and no material file. Official GLB also saved with 10 materials and 30 images, no skin. Needs rigging and archery equipment; not imported into gameplay. |
| Erika Archer | [Adobe Mixamo](https://www.mixamo.com/#/?page=1&query=Erika%20Archer&type=Character), [free game use with Adobe ID](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Official FBX Binary T-pose export inspected: 67 bones, 10,754 weighted vertices and 20,526 triangles. Unity Humanoid Avatar is valid. Gameplay animation remains separate from the visual test. |

Candidate previews are local review artifacts, not imported game assets. Record
the source package's actual format and rig after inspection rather than assuming
that a preview or a filename proves game readiness.

The English Archer original is preserved unchanged under ignored
`SourceAssets/Downloads/EnglishArcher/english_archer.glb`, with checksum,
source/license metadata and attribution alongside it. Its 128.57 MiB size exceeds
GitHub's regular per-file limit. The imported preview uses a derived 1.3 MB FBX
and separate texture files, each below that limit; the original GLB is not tracked.
All four sources are now saved locally with hashes and provenance under
`SourceAssets/Downloads/<asset>/`. Rig/mesh inspection reports are under
`Artifacts/CharacterReview/Acquisition/`; neither location is committed to Git.
Source acquisition does not select a final character or establish animation readiness.

## Lab appearance test

- Active prototype assets: `Assets/ThirdParty/EnglishArcher/` and
  `Assets/ThirdParty/ErikaArcher/`, with FBXs, extracted textures, URP materials
  and source/license records. English uses base-color and normal maps; final
  packed metallic/roughness treatment remains art preparation work.
- Reusable visuals: `Assets/_Game/Prefabs/Characters/EnglishArcher.prefab`
  and `ErikaArcher.prefab`. The provisional body height is 1.75 m with feet on
  the floor. English body sizing excludes its separate weapon geometry. English
  stays in its source pose; Erika's Animator is disabled for a relaxed-arm pose.
  These are appearance-test assets, not completed archery or injury animation.
- Medieval Man remains an imported optional base at `Assets/ThirdParty/MedievalMan/`;
  it is not used in either active scene. The Viking and knight are also excluded
  from the active scene selection.
- DuelArena uses English Archer at the blue spawn and Erika at the orange spawn.
  Capsule renderers are hidden; existing capsule colliders remain for arrow tests.
- `Assets/_Game/Scenes/VRCharacterReview.unity` places both archers 3.5 m ahead of
  the existing XRPlayer, facing the viewer. No knight is placed in either scene.
- The Quest 2 Editor preview reached a focused OpenXR session and received positive
  visual feedback. Runtime stereo and head tracking were verified. This does not
  establish animation readiness or replace later bow-reach and comfort testing.

## Interactive bow

`Assets/_Game/Prefabs/Bow.prefab` references the English Archer FBX's existing
`defaultMaterial.010` grip and `defaultMaterial.011` limbs with the original
`ammo` material. The parts are repositioned around the grip, rotated into the
shooting plane, and scaled to a provisional 1.53 m limb height. The source FBX
is unchanged. Its separate static string is omitted from this prefab and replaced
with a project-authored moving line. Credit count_zero under the English Archer's
CC BY 4.0 license; source attribution remains alongside that model.
