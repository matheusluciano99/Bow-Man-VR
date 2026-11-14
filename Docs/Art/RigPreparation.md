# Archer rig preparation

English Archer and Erika Archer have prepared Humanoid derivatives for the
injury-animation work in task #18. Their source FBXs remain unchanged. Each
project-owned character prefab keeps its existing GUID and uses a rigged
derivative with the original textures/materials.

| Character | Runtime model | Unity vertices / triangles | Skin / bone bindings |
| --- | --- | --- | --- |
| English Archer | `Assets/ThirdParty/EnglishArcher/Models/EnglishArcher_Rigged.fbx` | 17,929 / 30,761 | One skin, 54 deform bones |
| Erika Archer | `Assets/ThirdParty/ErikaArcher/Models/ErikaArcher_Rigged.fbx` | 11,971 / 20,526 | Four skins, 67 source bones |

Both Avatars explicitly map all 54 Humanoid bones supported by these skeletons,
including shoulders, neck, eyes and all finger joints. Jaw is absent. The mapping
is stored in the models' `.meta` files; preserve it when rebuilding. An automatic
mapping can omit optional finger joints even when the Avatar is reported valid.

The neutral prefabs are provisionally 1.75 m tall with feet on Y=0. Each has an
`EyeAnchor` under its Head bone, at the midpoint of the eye bones and facing the
character's forward direction. This supplies an animated eye reference for the
agreed first-person injury view; it does not attach the headset camera or
establish physical comfort acceptance.

## English Archer

The source is static. The derivative fits a skeleton to its A-pose, skins the
body, and bakes a T-pose for Humanoid import. The existing Erika skeleton supplies
bone hierarchy/orientation references; no Erika mesh is copied into English.
Actual English wrist openings and fingertips determine the hand/finger fit.

Body sections are weighted only to their anatomical bones. Rigid equipment,
helmet and eyes retain rigid attachments. Coincident UV-seam vertices have
equal weights to prevent cracks; eight unweighted seam vertices inherit weights
from the same surface position. Skinning uses at most four normalized influences.
Opposite-leg influences are removed from the lower legs, and the left boot sole
is levelled by about 1.5 cm with a smooth ankle transition for the neutral stance.

The original detached bow/arrow meshes stay in the source FBX and are omitted
from the rigged body. This preserves the mesh references used by the separate
interactive bow. Original UVs, textures and material names are retained.

Authoring files are under `SourceAssets/EnglishArcher/`: `EnglishArcherRig.blend`
and the reproducible `BuildRig.py`. The Blender source includes a movement-check
action; individual injury animations remain their own cards.

## Erika Archer

The source already has a skeleton and skin. Its right index-finger bones had no
effective skin weights. `SourceAssets/ErikaArcher/BuildSkin.py` transfers the
left-hand weights to corresponding right-hand surface vertices, swapping the
anatomical bone names, then limits/normalizes the influences. The corrected
derivative preserves geometry, UVs and materials; the downloaded FBX is unchanged.

The repair covers 686 right-hand vertices. The nearest mirrored surface match is
required to be within 1.5 cm; the current maximum is about 1.15 cm. Automated
checks verify that all finger joints affect their own hand and do not pull
distant body geometry.

## Desktop movement check

Open `Assets/_Game/Scenes/ArcherRigTest.unity` and enter Play. The two characters
share `Assets/_Game/Animations/RigMobilityCheck.controller` and a 12-second
Humanoid clip imported from `RigMobilityCheck.fbx`:

- 2 s: torso, neck and head movement.
- 4 s: shoulder/arm movement.
- 6 s: finger flexion.
- 8 s / 10 s: left/right steps, keeping the other foot planted.
- 12 s: loop to the neutral pose.

Pause Play to inspect joints, skin and support contacts in the Scene view.
Select either Head's `EyeAnchor` to inspect its motion. The outside camera is a
desktop rig-review tool; the game's local view remains the agreed first-person
presentation. Normal character prefabs do not run this test loop automatically.

The animation-only FBX preserves its armature hierarchy and copies the English
Humanoid Avatar. Its baked foot goals are used by the controller's foot IK.
Keep hierarchy preservation enabled: flattening the single armature root changes
the skeleton layout and breaks copied-Avatar retargeting.

## Rebuild and validation

Use the existing Blender 4.5.16 LTS installation. The scripts require background
mode and can be run from the repository root; see each authoring directory's
README for the command and generated files. They preserve source FBXs and Unity
`.meta` files. The Blender file uses relative project texture paths.

After rebuilding, let Unity reimport. Preserve the explicit Humanoid mappings,
material remaps and animation-only hierarchy/Avatar settings. If bone names or
rest landmarks change, review the Avatar mapping and T-pose in the Rig Inspector,
then recalibrate the neutral prefab scale/floor and `EyeAnchor` against the eyes.

Run `BowMan.Characters.EditorTests` and the required `BowMan.VR.EditorTests`.
The character checks cover complete mappings/weights, anatomical sides, scale,
floor/eye alignment, localized finger deformation, and the shared clip's body,
hand and alternating-foot movement. The planted foot must remain stationary;
the visible supporting sole stays within 4 mm of the floor during this check.

Per-region animations, precise hitboxes, extreme-pose contact/deformation review
and Quest 2 injury-viewpoint testing remain the corresponding implementation
cards. Passing this movement check does not implement those sequences or prove
headset comfort. Sources/licenses remain in [CharacterAssets.md](CharacterAssets.md).
