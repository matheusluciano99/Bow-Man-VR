# Foot motion preparation

The selected free MotionSmith right-foot take binds in its first injured pose.
`PrepareFoot.py` constructs a neutral reference with measured limb lengths and a
natural ankle-to-toe slope, then re-expresses every original animation frame in
that reference. Assertions compare all 89 joints across all 120 source frames;
preparation fails if any world position changes by 0.1 mm or orientation by
0.002 radians. The source file is never overwritten.

Run from Windows using the installed Blender:

```powershell
& 'D:\Tools\Blender\blender-4.5.16-windows-x64\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python 'D:\UnityProjects\Bow Man VR\SourceAssets\CosindraAnimations\PrepareFoot.py'
```

The raw take is versioned under `Assets/ThirdParty/CosindraAnimations/Motions/`;
the prepared FBX is under `Derived/`. Frame zero is the neutral reference;
frames 1–120 retain the original performance at 30 fps. The preparation report
is local review output under `Artifacts/FootReview/Prepared/preparation.json`.

After export/Unity refresh, use **Bow Man VR > Injuries > Rebuild Left Foot** and
**Rebuild Right Foot**. The builder imports a Humanoid using the explicit neutral
reference, fits the complete performance to each archer, and mirrors anatomical
rotation deltas on the target skeleton for the left side. It preserves clip GUIDs
and rebuilds the affected fatal continuations. Toe flexion is constrained inside
the boots. Standing entry, planted support and final landing/return are fitted
contacts; this source is generated animation, not human motion capture.

Source provenance: [asset notice](../../Assets/ThirdParty/CosindraAnimations/Attribution.md).
Mechanical checks and desktop previews do not establish headset fluidity or comfort.
