# Duel turn flow

`DuelTurnFlow.Initialize(first, second)` starts two distinct living archers with archer 0 active. `CanShoot(index)` is the gate for drawing and firing. After a bow launches, call `TryTrackShot(index, arrow)` before the arrow’s next physics step; registration locks both archers. Only one arrow/outcome may be pending.

Scenery or the eight-second flight expiry passes the turn once. A body hit applies provisional 25 damage, or immediate fatal damage for head/neck. Defeat emits one result immediately and cancels pending recovery. Existing health remains clamped at zero with one defeat event.

For a surviving body hit, `ReactionRequested(target, impact, lethal, shotId)` starts the region-specific reaction. Call `MarkInjuredPose(shotId)` only when its resting pose is reached. The flow holds that pose for three seconds, then emits `RecoveryRequested(target, shotId)`. Call `CompleteRecovery(shotId)` only when the get-up or standing recovery finishes. Until then neither archer may shoot. Stale, repeated, premature or post-death callbacks cannot pass the turn.

This controller does not substitute a timer for the entry/get-up animation. Animation clips and their completion callbacks belong to their own tasks. Head and neck share fatal gameplay rules while requiring different visual death sequences. A match host must cancel nocked bows when the gate closes; the later bow integration owns that connection.

## Desktop review

Open `Assets/_Game/Scenes/DuelTurnTest.unity`. Select a region and fire the active archer. For surviving hits, use **Animation reached injured pose**, wait the full three seconds, then **Recovery animation finished**. These buttons explicitly simulate animation callbacks; the scene does not claim completed injury motion. Head/neck hits end the match immediately. Restart Play to create fresh health.

The original DuelArena still provides the independent hit/health test. Its test-damage listener must not run alongside the duel controller’s damage consumer. The turn-review scene replaces that launcher with DuelTurnReview, preserving both original review flows.

Run Gameplay PlayMode and required VR EditMode checks. Tests include death during the hold, exact one-time handoff, scenery, expiry, fatal head/neck, invalid shooter/actor/arrow registration and early/stale recovery callbacks.
