# Erika Archer skin preparation

`BuildSkin.py` preserves the downloaded FBX and exports a derivative with repaired
right-hand skin weights. It uses the corresponding left-hand surface and swaps
left/right bone names. Geometry, UVs and materials are retained.

From the repository root in PowerShell:

```powershell
& 'D:\Tools\Blender\blender-4.5.16-windows-x64\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python 'SourceAssets/ErikaArcher/BuildSkin.py'
```

The script overwrites
`Assets/ThirdParty/ErikaArcher/Models/ErikaArcher_Rigged.fbx` and the ignored
`Artifacts/CharacterRigging/ErikaSkinWeights.json` report. Preserve the Unity
`.meta` file and its explicit Humanoid/material mappings. Background mode is
required to protect interactive Blender scenes.

The source and license remain under `Assets/ThirdParty/ErikaArcher/`.
Preparation and validation are in
[RigPreparation.md](../../Docs/Art/RigPreparation.md).
