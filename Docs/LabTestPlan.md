# Quest 2 lab test

Start Quest Link before launching the Editor scene or Windows player. Enable
Unknown Sources in the Meta Link desktop app and keep Meta Link as the active
OpenXR runtime. Use a clear standing play area and both Touch controllers.

Open `Assets/_Game/Scenes/LocalDuel.unity`, or launch
`Builds/PCVRDuel/BowManVR.exe`. This cut combines the tracked bow, real anatomical
body hits, attached arrows, turn flow, AI, all fifteen regional injury entries,
surviving recovery, fatal collapse, actual-wound blood and finite-path organ
analysis. Organ analysis currently produces data; the finishing X-ray and bone
visualization have their own tasks and must be verified in a cut containing them.
Confirm the generated player's source revision in `Artifacts/LabBuild/manifest.json`.

## Controls

| Action | Quest 2 | Desktop check |
| --- | --- | --- |
| Hold bow | Automatically attached to selected hand | Tab enables desktop input |
| Grab/draw/release string | Opposite trigger near string; hold, pull, release | Hold/release Space |
| Aim | Both tracked hand positions | Arrow keys |
| Switch bow hand | Right B during your aiming turn | H during your aiming turn |
| Restart finished match | Right A | R |
| Return to VR input | — | Tab |

The initial bow hand is left for a right-handed archer; right-bow/left-draw is
also available. Bow hand preference survives match restart.

## Match and comfort checks

- [ ] Head motion and both controller markers track correctly; floor and body scale match the real room.
- [ ] Grip alignment, nearby-string grab, short/half/full draw and release feel natural with each bow hand.
- [ ] The bow stays in hand without gripping, including other turns and injury; grip release does not cancel a draw.
- [ ] Losing tracking or app focus cancels without an accidental shot.
- [ ] The full opposing archer is textured and correctly scaled; the local view shows controllers/bow only.
- [ ] Both health values, turn state and draw feedback are readable in stereo.
- [ ] One released arrow locks shooting until its outcome and any living recovery finish; misses pass the turn.
- [ ] The AI takes one legal turn and cannot shoot during another arrow, injured hold or recovery.
- [ ] A fatal hit announces exactly one result; the corpse finishes collapsing and stays down.
- [ ] Restart restores full health, clears old arrows/actors and restores standing view without leaving Quest Link.

## Manual headset review

The headset wearer fires each acceptance shot using the real bow controls.
Region labels and captured frames/hit logs can guide the review, but must not
replace the wearer's shooting with automatic demonstrations. Keep ordinary
shooting gates and turn handoffs active. A temporary close target is an
inspection aid, not acceptance of the final duel distance.

## Regional injury checks

Use `InjuryAnimationTest.unity` for close outside views and the integrated duel
for live local-eye checks. Record **both** the visible opponent and local view;
an outside-camera pass does not establish VR acceptance. A surviving entry must
reach its own injured pose, hold three seconds, then finish its own recovery
before that archer receives a turn. Head/neck never recover. Fatal body hits
play their own reaction then collapse fully, with an immediate match result.

| Region | Opponent motion / impact fit | Local eyes / live tracking / comfort |
| --- | --- | --- |
| Head | Pending | Pending |
| Neck | Pending | Pending |
| Chest | Pending | Pending |
| Abdomen | Pending | Pending |
| Pelvis | Pending | Pending |
| Left shoulder | Pending | Pending |
| Right shoulder | Pending | Pending |
| Left arm | Pending | Pending |
| Right arm | Pending | Pending |
| Left hand | Pending | Pending |
| Right hand | Pending | Pending |
| Left leg | Pending | Pending |
| Right leg | Pending | Pending |
| Left foot | Pending | Pending |
| Right foot | Pending | Pending |

During each full entry/hold/recovery, turn your head and move both controllers.
The viewpoint should follow the animated eyes while retaining live tracked
movement, with no doubled standing height or extra vertical jump at recovery.
Check seated/kneeling/fallen floor contact and eye height against your actual
standing height; physical alignment and forced-motion comfort remain unaccepted
until this headset session. Test head/neck at full and low health separately.

When the anatomy tasks are integrated, also verify organ and bone marks,
strength-dependent fractures, the fatal-hit-only in-body X-ray and wound blood stay at the
actual struck side/location during every motion. Record these results on #35;
keep #15/#16 hardware acceptance open until their own criteria are met.

Once the finishing view is implemented, verify a surviving hit never starts it;
head/neck kills and other regions that reduce health to zero may request it once.
