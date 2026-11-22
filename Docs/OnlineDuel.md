# Online duel

Open the saved PvPConnectionTest scene on both peers. Host on one PC and enter
its Relay join code on the other. The second seat starts the duel. Archer 1
shoots first. Each player sees their own tracked bow and the opponent's archer.
The opposite trigger draws and releases. Right B switches bow hands during
your aiming turn. Tab enables desktop controls, the arrow keys aim and Space
draws and releases. Desktop aim uses the assigned seat's facing direction.

The host accepts one release from the active, living seat for the current
round and turn. It runs the shared projectile, health, regional animation and
recovery gates. The other peer displays the approved flight and receives the
host's phase, health and result. Each body contact carries its prefab volume,
local entry and direction, draw strength, organ contacts and rig bone results.
These coordinates attach to the same bones on either peer through the reaction.
Only a confirmed fatal impact opens the internal view. A scenery impact retains
the arrow at the reported contact for its remaining lifetime.

## Two-Editor verification

Use the same revision in separate project folders. Start Unity in batch Play
Mode on each, filtering to `PvPRelayMatchTests.TwoEditorsAgreeOnTheDuel`.
Pass `-duelTestRole host` on one, `-duelTestRole join` on the other, and point
`-duelTestFolder` at the same new empty directory. The test uses the linked
project's authenticated Relay service. It compares both clients' shot tokens,
region and side, local entry, anatomy, health, turn handoff and final result.
The empty directory keeps previous join codes and test observations separate.
Physical Quest 2 review and the two-network session use the lab checklist.

## Online target competition

Select **Tiro ao alvo** (keyboard **3**) on both PCs. Host on one and join with
its code on the other. Each archer sees the target from the same firing line.
Use the usual bow controls, or aim with the arrow keys and hold/release Space
on desktop. Tab toggles desktop input. The host shoots first. The bow unlocks
for the other seat once the arrow lands or expires. After twenty shots, each
player can press A or R to accept a new match. Menu or Esc leaves the session.

To run the paired competition check, use the same two-Editor procedure above
with the filter `OnlineTargetRelayTests.TwoEditorsScoreTheMatch` and a fresh
shared test directory. Both Editors check every score update and the final HUD
for a win and a tie.
