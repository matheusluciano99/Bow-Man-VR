# Arrow-path organ analysis

Both active archer prefabs carry 14 tissue groups representing 13 organs:
brain, heart, left/right lung, liver, stomach, spleen, pancreas, left/right
kidney, small intestine, large intestine and bladder. Large intestine spans
two bone groups but is reported once. Tissue uses readable native BodyParts3D
meshes, attached to the head, chest, spine or pelvis; it adds no colliders or
visible renderers. The existing 19 body volumes remain the physical targets.

`ArrowFlight.BodyHit` records actual entry, side, direction and finite penetration.
`ArcherAnatomy.Trace(impact)` intersects that segment against the current tissue
surfaces and returns ordered `OrganHit` contacts. No organ is selected randomly
or inferred solely from its region or bounding box. A path through soft tissue,
a real hole, or one ending before an organ returns no contact with that organ.
Closed-tissue containment includes a path starting inside the tissue.

`ArrowAnatomy.LastImpact` and `Resolved` expose per-arrow analysis data, independent
of health. `OrganHit.LocalPoint` and `Point` retain the contact on the tissue's
own animated bone. The entry snapshot remains the original physical impact.
Neither analysis component applies damage, changes turns or creates an X-ray.
Immutable geometry is shared and prepared at actor spawn; bounded triangle
batches prune exact queries. The cache clears before Play or an editor rebake.

## Rig bone contacts

Each physical hit volume contains a smaller box fixed to its Humanoid bone.
The boxes represent the game skeleton at rig granularity, including a single
forearm bone per side. Each finite arrow segment is tested against every box
in its current pose. `AnatomicalImpact.Bones` reports contacts in path order,
once per rig bone, with side and a point bound to the animated volume. A shot
can pass through a body hitbox without touching any bone box.

The bow records normalized draw strength at release. Fixed-strength AI and
review shots use full strength. At the provisional 75% cutoff, a contacted
bone is marked fractured. Below the cutoff the same contact is still reported.
This is a game feedback rule. Health and animation use the existing duel rules.

## Fatal finishing presentation

The internal view is reserved for a confirmed lethal arrow, like a finishing
sequence. `DuelTurnFlow.FatalityRequested(targetIndex, impact)` publishes the
actual anatomy after health confirms death and before regional death motion.
Surviving hits, misses, and unrelated later deaths cannot request that view.
The event does not delay the match result or alter the existing death sequence.
The local duel displays a stereoscopic close-up of the interior intersected by
the fatal game arrow. Cyan surfaces are the contacted organ meshes. Orange
volumes mark fractured rig bones and pale volumes show bone contact below the
fracture threshold. The gold line reveals the actual finite path from entry to
tip. All structures use their source frames through the fall. The HUD names
the region, organs and fractures, including empty results.

The cutaway lasts a provisional three seconds. Restart dismisses it immediately.
Head and hand tracking continue throughout the presentation. Headset readability
and comfort require the physical Quest 2 pass. Organ- and fracture-specific
health effects remain separate work.

## Sources and authoring

See [asset provenance](Art/CharacterAssets.md) and
[reproduction steps](../SourceAssets/BodyParts3D/README.md).
Run **Bow Man VR > Injuries > Rebuild Organ Anatomy** after changing proportions.
Original OBJ filenames and bytes are preserved; generated mesh GUIDs/subasset
identities survive a rebuild. Reference eyes/hips align axes and scale; torso
width fits the body envelope and brain depth fits the visible cranial skin.
The fixed 25 cm arrow penetration and rigid bone binding are prototype limits.
Deforming tissue and individual anatomical proportions are not simulated.

## Verification

PlayMode checks use real shared arrows through all 13 organs on both prefabs,
including a rotated rig, moving contacts, empty tissue and short penetration.
Surface checks cover closed containment, finite endpoints and real holes.
A separate real-shot duel check covers a surviving chest shot, fatal head and
neck shots, and fatal accumulated body damage; only lethal arrows request a
finishing view. Frontal brain samples must sit behind the visible head skin.
Review local front/side renders under `Artifacts/AnatomyReview/`; visual fitting
and CPU query timings do not establish Quest 2 performance or VR acceptance.
