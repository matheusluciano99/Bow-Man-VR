# Body-region hit detection

The English and Erika prefabs each have 19 solid, bone-bound volumes covering
15 anatomical regions: head, neck, chest, abdomen, pelvis, and left/right
shoulder, arm, hand, leg and foot. Arms and legs each use two capsules so their
elbows and knees can bend. There is no separate back region in the bow duel.
Equipment such as quivers and shields does not receive its own injury region.

`BodyHitRegion` identifies a collider's region and anatomical side. Central
regions report `Center`; left/right refer to the character's own body. Every
volume is a child of the corresponding animated bone. A kinematic Rigidbody on
the character root lets animation drive the compound body without gravity or
collision response moving the archer.

`ArrowFlight.BodyHit` emits one `BodyImpact` for the first solid body collider:

- `Region` and `Side`: anatomical identity.
- `Point`: the actual swept-tip impact in world coordinates.
- `Direction`: the normalized world-space travel direction at impact.
- `Collider` and `Volume`: the struck collider and its bone-bound marker.

The existing `Hit` event still reports every solid impact, including scenery.
Body impacts emit `BodyHit` first, then `Hit`, for the same stopped arrow and
point. Consumers must use one event for their outcome, rather than apply damage
from both. Subsequent physics steps do not report another hit. Trigger volumes,
the arrow's own hierarchy and the firing archer's collider hierarchy are ignored.
Nearest distance wins where volumes overlap; exact distance ties use collider
instance ID for a stable choice during that run.

The arrow synchronizes transformed colliders before its swept query. Ownership
and region checks use the collider's transform: a `RaycastHit.transform` may
instead refer to an ancestor Rigidbody. See Unity's
[transform synchronization](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SyncTransforms.html)
and [RaycastHit API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RaycastHit.html).

## Desktop review

Open `Assets/_Game/Scenes/DuelArena.unity` and enter Play. The initial test shot
aims at Erika's chest. Select any of the fifteen region buttons, then click
**Fire test arrow** or focus Game view and press Space. The panel reports the
actual region, side, impact point and travel direction, even if a different
volume intercepts the requested shot. Only one arrow is launched per press.

The test launcher computes a ballistic velocity to the selected volume centre
using a provisional 0.8-second travel time. This is a desktop verification aid,
not the VR bow aiming or shot-strength rule. Hidden capsule placeholders retain
their objects but their colliders are disabled so they cannot intercept the
anatomical volumes.

## Authoring and checks

Use **Bow Man VR > Characters > Rebuild Body Hitboxes** after changing the
prepared rigs. `Assets/_Game/Editor/ArcherHitboxBuilder.cs` modifies only the two
project-owned character prefabs. It preserves imported models and attaches the
volumes to the current bone positions. The neutral 1.75 m prefabs at origin are
the authoring reference. Joint caps overlap to maintain coverage while bending;
the central boxes share vertical boundaries.

These simple volumes and dimensions are provisional gameplay approximations,
not exact skin surfaces or organ/bone geometry. Review them in Scene view with
the Collider gizmos, especially when adding extreme injury poses. Rebuild after
changing proportions, then rerun the checks. Hit-volume validation does not
establish internal organ damage, fractures or final injury animation contacts.

Run `BowMan.Gameplay.PlayTests`, `BowMan.Characters.EditorTests` and
`BowMan.VR.EditorTests`. The anatomical checks fire real arrows at all fifteen
regions on both prefabs, sample torso/neck boundaries and limb junctions,
exercise animated hand/foot positions, and check overlaps and scenery. The
body-region tests load editor assets and run in the Editor's PlayMode runner;
they are excluded from standalone test players.

Arrow attachment, damage rules, injury selection and recovery remain separate
tasks. This system reports the actual collider impact; it does not yet attach
arrows to the moving body or select injury animations.
