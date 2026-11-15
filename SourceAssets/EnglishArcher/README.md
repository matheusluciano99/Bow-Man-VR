# English Archer authoring

`EnglishArcherRig.blend` contains the fitted skeleton, skinned body and 12-second
movement-check action. `BuildRig.py` reproduces them using the unchanged English
source FBX and the existing Erika skeleton as a hierarchy/orientation reference.
The Blender file links project textures with relative paths.

From the repository root in PowerShell:

```powershell
& 'D:\Tools\Blender\blender-4.5.16-windows-x64\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python 'SourceAssets/EnglishArcher/BuildRig.py'
```

The script overwrites only these derived outputs and its local review report:

- `SourceAssets/EnglishArcher/EnglishArcherRig.blend`
- `Assets/ThirdParty/EnglishArcher/Models/EnglishArcher_Rigged.fbx`
- `Assets/_Game/Animations/RigMobilityCheck.fbx`
- `Artifacts/CharacterRigging/EnglishRigWeights.json` (ignored)

Keep the Unity `.meta` files and their mappings when rebuilding. Do not overwrite
manual authoring changes by rerunning without saving them separately. The script
requires background mode so it cannot clear an interactive Blender scene.

Preparation, Unity import settings and checks are in
[RigPreparation.md](../../Docs/Art/RigPreparation.md). Credit count_zero for the
English Archer under CC BY 4.0; the imported asset's attribution remains under
`Assets/ThirdParty/EnglishArcher/`. The Erika reference comes from the existing
Adobe Mixamo asset and its recorded license.
