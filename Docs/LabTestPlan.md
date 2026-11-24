# Quest 2 lab test

Start Quest Link before launching the Editor scene or Windows player. Enable
Unknown Sources in the Meta Link desktop app and keep Meta Link as the active
OpenXR runtime. Use a clear standing play area and both Touch controllers.

Open MainMenu in Unity and enter Play and choose PvE or online PvP. To review just
the local scene, open `Assets/_Game/Scenes/LocalDuel.unity`. Both modes include
the fifteen regional entries, recovery, fatal collapse, attached arrows and
wound blood. After a confirmed fatal shot, check the region, organ and rig bone
labels in the 3.65-second replay from each eye. A surviving hit keeps the
ordinary injury view. Use the same revision for the two-network test.

## Controls

| Action | Quest 2 |
| --- | --- |
| Hold bow | Automatically attached to the selected hand |
| Grab, draw and release | Hold the opposite trigger near the string, pull back and release |
| Aim | Move both tracked hands |
| Switch bow hand | Press right B during your aiming turn |
| Restart a finished match | Press right A |

Check that Tab, the arrow keys and Space cannot draw or shoot in the Game view. Remove the headset during a draw and check that the arrow is cancelled when the runtime reports presence lost.

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
shooting gates active through the full injury, region-specific hold and recovery.
For regional animation review, disable AI shooting and return control to the
wearer after recovery or a miss; keep target health and existing wounds. Review
AI attacks separately. A temporary close target is an
inspection aid, not acceptance of the final duel distance.

## Regional injury checks

Use `InjuryAnimationTest.unity` for close outside views and the integrated duel
for live local-eye checks. Record **both** the visible opponent and local view;
an outside-camera pass does not establish VR acceptance. A surviving entry must
reach its own injured pose, hold for 0.2 seconds (chest), two seconds (feet) or three seconds (other regions), then finish its own recovery
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

Verify organ and bone marks,
strength-dependent fractures, the fatal-hit-only in-body X-ray and wound blood stay at the
actual struck side/location during every motion. Record these results on #35;
keep #15/#16 hardware acceptance open until their own criteria are met.

Verify a surviving hit never starts it;
head/neck kills and other regions that reduce health to zero may request it once.

## Shared PvE and PvP build

Open the saved MainMenu scene in the Editor. Build both modes with **Bow Man VR
> Build Windows Game Player**. Keep the entire resulting player folder together
when copying it to either lab PC. Record the Git revision and use the same
build on both PCs. Launch each mode from the menu and return with the left
controller's Menu button or Esc.

For the online pass, host on one network and join from a second network.
Record the two PCs, network types, participant observations and build revision.
Alternate shooters and repeat the region table above in both modes. For each
hit, compare health, region and side, arrow placement, wound and anatomy on
both peers. Confirm each living recovery precedes the next turn. Test a fatal
hit, a mutual rematch, a departure during play and a fresh connection. Record
the two developers' results and link the completed injury review from #35
before accepting the shared build.

## Tiro ao alvo, archer choice and browser play

These checks cover #133, #136, #138 and #140. Record the revision, testers,
build (Quest Link player or browser) and outcome on each issue.

| Action | Quest 2 |
| --- | --- |
| Choose English Archer or Erika before a mode | Select the Archer panel with the right trigger |
| Enter Tiro ao alvo | Select its panel |
| Choose the course before hosting or a CPU match | Select 15, 30, 50, 60 or 70 m |
| Start a solo competition | Select Play against CPU |
| Accept a rematch after twenty arrows | Press right A |
| Return to modes in the browser build | Click the left thumbstick |

- [ ] Enter PvE as English Archer, then as Erika. With each bow hand, the
  first-person hands and cuffs belong to the chosen archer and the opponent is
  the other one. The choice survives a restart and a return to the menu.
- [ ] In a duel, each archer's breathing, release effort, hurt and defeat
  voices are their own and come from their head.
- [ ] Watch both archers draw from either bow hand. The feet stay planted, the
  bow hand closes on the grip and the drawing hand clears the face.
- [ ] In online PvP, choose different archers on the two PCs, then the same
  archer on both. Both seats show the same pair of archers.
- [ ] Hold the trigger at full draw and briefly hide the drawing hand from the
  headset. The draw keeps its last pull. After tracking comes back, release
  the trigger to shoot once.
  Releasing the trigger while the hand is hidden shoots nothing.
- [ ] Against the CPU at 15 m and at 70 m, you shoot first and the CPU draws
  and shoots from your right. After ten arrows each, both totals and the
  result are readable, and A restarts at the same distance.
- [ ] A full draw reaches the scoring face at every distance with Tiro ao
  alvo's 32 m/s full-draw speed. Record where aiming stops being
  comfortable and add the result to #15.
- [ ] Online, host at 70 m while the joining player has 30 m selected. Both
  seats play both matches at 70 m.
- [ ] Each seat reads both totals, arrows left, the distance and the
  upper-right impact mark in stereo. A miss scores zero, spends the arrow and
  clears the mark. A cancelled draw keeps the arrow.
- [ ] After twenty arrows, both seats show the same winner or tie. The rematch
  starts only after both players press A. Leaving returns the other player to
  the lobby.
- [ ] Enter a join code with the in-headset keypad and record how easily each
  key can be aimed at.

Run the browser pass on the headset itself, without Quest Link. Open the
uploaded itch.io build in the Quest Browser, select **Play**, then **Enter VR**,
and record the browser version. Host on one device and join from another on a
different network. Complete a PvE duel, an online duel and one Tiro ao alvo
competition, then return to the menu with the left thumbstick.
