# Character assets

The player archer should be a realistically proportioned medieval human in light
clothing or armor, using For Honor and Conqueror's Blade as visual references.
Use free assets only; multiple different archer models are allowed. The prototype
review uses English Archer and Erika Archer. Design is tracked in
[issue #38](https://github.com/matheusluciano99/Bow-Man-VR/issues/38).

## Standing motion

Each duel archer uses a different eight-second excerpt from Sam Lazarus’s standing motion capture. The boots stay planted while the shoulders, arms, spine and head follow the recording. The local tracking origin follows the headset during idle and the animated eyes during injury. Source and rebuild details are in the [Rokoko attribution](../../Assets/ThirdParty/Rokoko/Attribution.md).

For a shot, the opposing archer reaches, draws and releases with an upper-body segment of the free Rokoko archery recording. English and Erika use different draws from that performance, each baked for both bow hands. The equipment follows the grip and the drawing fingers meet the active string. The source and Unity bake menu are documented in the [archery recording attribution](../../Assets/ThirdParty/Rokoko/Attribution.md#bow-action-recording).

First-person hands use the selected archer’s own skin, cuffs and materials. Unity extracts each rig’s hand and cuff triangles and bakes relaxed, grip and pinch shapes into four hand prefabs. Rebuild them with **Bow Man VR > Characters > Bake first-person hands**. The English Archer and Mixamo sources and terms are listed below.

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
| English archer, count_zero | [Sketchfab](https://sketchfab.com/3d-models/english-archer-8571c31e9c934458be2e434de71f94f5), CC BY 4.0 | Static source retained: 32,466 triangles, three materials, nine 4K textures. Active rigged derivative: 30,409 body/equipment triangles, 54 deform bones, valid Humanoid. Full injury animation remains separate. Original DAE uninspected. |
| Medieval man, Chenuka Wijesundara | [Sketchfab](https://sketchfab.com/3d-models/game-ready-pirate-hunter-viking-medieval-man-9f51d396733b451bab550b406b067805), CC BY 4.0 | Actual Pirate.fbx has 65 bones, 15,658 weighted vertices and 24,577 triangles; Unity Humanoid Avatar is valid. Credit Chenuka Wijesundara and Leon Steiner for the [gambeson](https://sketchfab.com/3d-models/gambeson-0fc44d2876ed449ca9473e78c83a8d39), also CC BY 4.0. Bow equipment and animation remain separate. |
| Viking/Medieval Game Character, CGenesis | [Sketchfab](https://sketchfab.com/3d-models/vikingmedieval-game-character-a9e99a47d2a24957ab6c08c9e57ad48c), CC BY 4.0 | Original nested archive contains static Viking.obj, not a rig: 137,230 source triangles and no material file. Official GLB also saved with 10 materials and 30 images, no skin. Needs rigging and archery equipment; not imported into gameplay. |
| Erika Archer | [Adobe Mixamo](https://www.mixamo.com/#/?page=1&query=Erika%20Archer&type=Character), [free game use with Adobe ID](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Official T-pose FBX retained: 67 bones, 10,754 source vertices, 20,526 triangles. Active derivative repairs right-hand weights and explicitly maps all body/finger/eye bones. Valid Humanoid; full gameplay animation remains separate. |

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
  the floor. Both now use prepared Humanoid derivatives with relaxed-arm neutral
  poses. Detached English bow/arrow props remain in the original source FBX.
  The normal prefabs do not play the rig-check loop automatically; these are not
  completed gameplay archery or injury animations.
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

## Prepared rig derivatives

`EnglishArcher_Rigged.fbx` adds a fitted skeleton and four-influence skinning,
combines body/equipment surfaces, equalizes UV-seam weights and levels the neutral
boot soles. It retains the original textures/UVs/material names and omits detached
bow/arrow props from the animated body. `ErikaArcher_Rigged.fbx` repairs right-hand
weights using the mirrored left-hand surface; geometry and materials are retained.
Both original FBXs and source licenses remain unchanged.

Blender authoring/rebuild files live under `SourceAssets/EnglishArcher/` and
`SourceAssets/ErikaArcher/`. The shared desktop movement scene, complete mappings,
eye references and checks are documented in [RigPreparation.md](RigPreparation.md).
This preparation enables individual animation work; each injury sequence still
requires its own implementation and review.

## Interactive bow

`Assets/_Game/Prefabs/Bow.prefab` references the English Archer FBX's existing
`defaultMaterial.010` grip and `defaultMaterial.011` limbs with the original
`ammo` material. The parts are repositioned around the grip, rotated into the
shooting plane, and scaled to a provisional 1.53 m limb height. The source FBX
is unchanged. Its separate static string is omitted from this prefab and replaced
with a project-authored moving line. Credit count_zero under the English Archer's
CC BY 4.0 license; source attribution remains alongside that model.

## Headshot collapse

The one-second headshot collapse is baked separately for English Archer and
Erika from Adobe Mixamo’s Dying Backwards motion. The source FBX is kept with
the third-party motion assets. Use **Bow Man VR > Injuries > Rebuild Head
Collapse** to regenerate the clips. Open the existing desktop DuelTurnTest,
select Head and fire. A headshot resolves defeat at impact while the defeated
archer completes its collapse.

The clips are baked at 60 Hz. Floor fitting uses the visible skin and a
4 mm allowance between the grounded start and end poses. The English rig
keeps its separate equipment and omits the loose baked display arrow.

## Foot reactions

Foot reactions use the free generated **MotionSmith pain right foot, take 1**
performance across the body, anatomically mirrored for the left foot on each
archer. This replaces the procedural adaptation of chest-fall torso deltas.
The unchanged original is `Assets/ThirdParty/CosindraAnimations/Motions/MotionSmith_pain_right_foot_take1.fbx`
(1,255,020 bytes; SHA-256 `e49bfb9c5c4ac2a8bcc5945c07d19a881f91718d98ff390e198ab9d918b95e7d`).
A prepared neutral reference preserves every source frame; final native clips
fit boot contact and standing transitions at 60 Hz. This is AI-generated catalog
motion, not human motion capture. See [asset provenance](../../Assets/ThirdParty/CosindraAnimations/Attribution.md)
and [rebuild instructions](../../SourceAssets/CosindraAnimations/README.md).
Both replacement foot reactions and their timing were accepted in Quest 2.

Use **Bow Man VR > Injuries > Rebuild Left Foot** or **Rebuild Right Foot**
to regenerate the existing clip assets for both skeletons. Open
`Assets/_Game/Scenes/DuelTurnTest.unity` and select either foot to review the
recoil, supported balance and standing recovery. A lethal foot hit ends the
match at impact and plays the foot reaction before its fatal continuation.

## Fatal neck reaction

English Archer and Erika have separate 2.4-second neck clips based on Mixamo's
Dying Backwards motion. The archer reaches toward the actual wound before the
knees buckle. The match ends on impact and the archer stays down once the
collapse completes. Use **Bow Man VR > Injuries > Rebuild Neck Collapse** to
regenerate the clips, then choose **Neck** in **DuelTurnTest**. First-person
headset comfort is tracked in #35.

## Internal anatomical tissue

Both active archer prefabs contain fitted BodyParts3D 4.3 tissue, without runtime
renderers or extra collision targets. The 166 unchanged OBJ originals and full
credit live under `Assets/ThirdParty/BodyParts3D/`; native fitted derivatives are
under `Assets/_Game/Models/Anatomy/`. Sources, pinned SHA-256 values and rebuild
steps are in [SourceAssets/BodyParts3D](../../SourceAssets/BodyParts3D/README.md).

BodyParts3D, © The Database Center for Life Science licensed under CC Attribution
4.0 International. The [current publisher README](https://dbarchive.biosciencedbc.jp/data/bodyparts3d/LATEST/README_e.html)
records the February 2025 license update; a copy is preserved with the imported
assets, including the older redistribution's license context. Axes/units and
per-rig fitting are documented modifications. These are game registration
approximations, not subject-specific clinical anatomy.

## Chest reactions

Chest fall uses Adobe Mixamo **Dying Backwards**, catalog description “Shot To The Chest Falling Backwards”, downloaded on Erika Archer through the official catalog. Export: FBX Binary, Without Skin, 30 fps, no key reduction. The unchanged source is `Assets/ThirdParty/MixamoAnimations/Motions/Dying Backwards.fbx` (678,592 bytes; SHA-256 `612a2981bf1c2382d78085f79d89bd4670649e6f1dd3871ba52b8e01211cb2e8`). [Mixamo terms/FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) permit use in games with an Adobe ID; the source belongs to this game project, not a standalone asset pack.

The source is retargeted using the original Erika T-pose in metres; the prepared derivative uses centimetre bone coordinates and cannot supply this motion importer’s reference skeleton unchanged. Per-model native clips preserve prefab roots and correct visible floor penetration. The earlier “Standing Death Backward 01” candidate ended face-down and was rejected for chest hits.

Chest recovery uses Mixamo **Getting Up**, the first catalog variant described as “Getting Up From Back”, downloaded on Erika Archer with the same export settings. Unchanged source: `Assets/ThirdParty/MixamoAnimations/Motions/Getting Up.fbx` (999,888 bytes; SHA-256 `2413541e9b26256399ab8793f33630d783462b04d205e4a29ebf73e4bd43a21a`). The same Mixamo game-use terms apply.

The fall lasts 2.7 seconds. A surviving archer stays down for 0.75 seconds,
then gets up with arms lowering to relaxed standing over 3.6 seconds. The
get-up bake guides the supporting limbs around the pelvis and head while
keeping the original rig lengths and grounding the posed skin. A fatal
chest hit ends the match at impact and plays its grounded collapse without
starting the get-up. Use **Bow Man VR > Injuries > Rebuild Chest Fall**
and **Rebuild Chest Recovery** to regenerate the clips. Select **Chest**
in **DuelTurnTest** to review the motion.

## Fatal shoulder reaction

A lethal shoulder arrow plays Adobe Mixamo’s **Shoulder Hit And Fall** from impact to collapse. Its right hand reaches the left shoulder. The right-shoulder clip mirrors the take on the target rig. The fitted fall passed visual review in the Unity injury scene. Surviving hits use the reach from the same recording with planted feet and the opposite palm fitted to the struck shoulder. Rebuild the four model-specific clips with **Bow Man VR > Injuries > Rebuild Fatal Shoulders**. The source FBX is under `Assets/ThirdParty/MixamoAnimations/Motions/`. [Mixamo game-use FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html)

### Standing wrist support

The hand reactions use Hamada’s recorded opposite-hand wrist guard and finger extension from the bracing hand in Adobe Mixamo’s **Getting Up**. The full Humanoid body take, conversion credit and license are in [Eyes, JAPAN attribution](../../Assets/ThirdParty/EyesJapan/Source.md).

## Playable arrow

The shaft and feather geometry comes from count_zero’s English Archer and keeps the equipment texture atlas and UVs. The source model and credit are recorded in the [English Archer attribution](../../Assets/ThirdParty/EnglishArcher/ATTRIBUTION.txt). The game adds its own faceted iron head and notched horn nock. Blender can regenerate the textured mesh from the retained FBX by running `SourceAssets/EnglishArcher/ExportArrow.py` in background mode.

The three meshes are children of the existing TestArrow prefab. Its root is the impact tip and the nock meets the string at local Z −0.70 m. The bow, AI and online replica use that same prefab.

The [duel audio credits](../../Assets/ThirdParty/DuelAudio/Credits.md) list the free source recordings used for each archer’s breathing, release effort, hurt and defeat, plus bow handling, arrow flight, impacts and arena ambience.
