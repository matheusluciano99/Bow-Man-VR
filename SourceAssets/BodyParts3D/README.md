# Anatomical surface preparation

Run `python3 SourceAssets/BodyParts3D/PrepareAnatomy.py` from the repository root.
It uses the pinned public GitHub redistribution of BodyParts3D 4.3, verifies each
real OBJ against its Git LFS size/digest, preserves original filenames under
`Assets/ThirdParty/BodyParts3D/Models/` and records the explicit tissue inventory
in `Sources.json`. Local metadata/download caches remain ignored. It installs
nothing and does not use the Anatomography viewer's undocumented endpoints.

In Unity, run **Bow Man VR > Injuries > Rebuild Organ Anatomy**. The authoring
tool converts patient-left/mm/Z-up coordinates to Unity-right/metres/Y-up,
measures source vitreous-body/proximal-femoral landmarks and fits them against
each prepared Humanoid. Torso width stays inside the authored body envelope;
brain tissue stays inside the head/upper-neck envelope, with frontal depth
measured against the actual visible cranial skin. These are provisional
per-character fits for visual review. Original source files are unchanged.

The generated native meshes preserve the actual separate tissue surfaces.
Groups follow their assigned animated bone and do not add physics colliders.
Meshes remain readable for exact finite-segment triangle/containment queries.
Regeneration preserves native mesh/subasset identities and character GUIDs.
The two intestine groups follow torso and pelvis separately while reporting one
large-intestine organ. Coordinate fitting is not a soft-body tissue simulation.

The current publisher license and full credits are preserved alongside the
[imported source](../../Assets/ThirdParty/BodyParts3D/ATTRIBUTION.txt).
