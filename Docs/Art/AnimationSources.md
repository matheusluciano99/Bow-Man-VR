# Free animation source review

Only free-to-download, free-to-use candidates are eligible. File validity and a
matching catalog title do not establish convincing animation or acceptance.
Retain the captured Mixamo death/chest motions as the visual comparison.

## Acquired candidates

24 individual Cosindra MotionSmith FBXs (29,541,344 bytes total) and the right-foot
GLB reference are cached locally under `SourceAssets/Downloads/Cosindra/`.
The selected right-foot source and its prepared Humanoid derivative are now
versioned under `Assets/ThirdParty/CosindraAnimations/`. Both fitted foot reactions
passed Quest 2 review. Other MotionSmith candidates remain unimported. Original filenames are
preserved; `manifest.json` in that cache records byte counts and SHA-256 hashes.

These are AI-generated catalog motions, not human motion capture. Each selected
download was explicitly marked free on the official page, whose game-use notice
states commercial use is permitted on the free plan, in any engine, without
attribution. Only the designated free takes were acquired; no paid plan or
additional generation was used. See the publisher's [terms](https://cosindra.ai/terms)
and [license](https://cosindra.ai/license).

All 24 files open in Blender 4.5.16, contain 89 bones and finite animation samples
at 30 fps. They contain skeleton/animation data without a character mesh. Several
files bind in their first animated pose, including lying or kneeling; a shared
standing bind-pose assumption distorts previews. Inspect native motion or prepare
the source rig correctly before retargeting. No current clip is declared Unity
Humanoid-ready solely from the Blender file check.

| Downloaded source | Frames | Free take / file |
| --- | --- | --- |
| [Choking](https://cosindra.ai/motionsmith/motions/choking) | 120 | `MotionSmith_choking_take1.fbx` |
| [Collapse to the knees](https://cosindra.ai/motionsmith/motions/collapse-knees) | 120 | `MotionSmith_collapse_knees_take1.fbx` |
| [Shot and fall backwards](https://cosindra.ai/motionsmith/motions/death-backward) | 120 | `MotionSmith_death_backward_take5.fbx` |
| [Death fall](https://cosindra.ai/motionsmith/motions/death-fall) | 120 | `MotionSmith_death_fall_take1.fbx` |
| [Headshot collapse](https://cosindra.ai/motionsmith/motions/death-headshot) | 120 | `MotionSmith_death_headshot_take1.fbx` |
| [Fall to the knees and die](https://cosindra.ai/motionsmith/motions/death-kneel-fall) | 150 | `MotionSmith_death_kneel_fall_take1.fbx` |
| [Left arm flinch](https://cosindra.ai/motionsmith/motions/flinch-left-arm) | 90 | `MotionSmith_flinch_left_arm_take2.fbx` |
| [Right arm flinch](https://cosindra.ai/motionsmith/motions/flinch-right-arm) | 90 | `MotionSmith_flinch_right_arm_take1.fbx` |
| [Get up from lying face up](https://cosindra.ai/motionsmith/motions/get-up-from-back) | 150 | `MotionSmith_get_up_from_back_take1.fbx` |
| [Get up from the left side](https://cosindra.ai/motionsmith/motions/get-up-from-side-left) | 150 | `MotionSmith_get_up_from_side_left_take1.fbx` |
| [Get up from the right side](https://cosindra.ai/motionsmith/motions/get-up-from-side-right) | 150 | `MotionSmith_get_up_from_side_right_take1.fbx` |
| [Get up from the stomach](https://cosindra.ai/motionsmith/motions/get-up-front) | 150 | `MotionSmith_get_up_front_take2.fbx` |
| [Hit in the stomach](https://cosindra.ai/motionsmith/motions/gut-punch-react) | 90 | `MotionSmith_gut_punch_react_take1.fbx` |
| [Hit in the hip](https://cosindra.ai/motionsmith/motions/hip-hit-recoil) | 90 | `MotionSmith_hip_hit_recoil_take1.fbx` |
| [Hit on the right hand](https://cosindra.ai/motionsmith/motions/hit-right-hand-open) | 90 | `MotionSmith_hit_right_hand_open_take1.fbx` |
| [Kneel down](https://cosindra.ai/motionsmith/motions/kneel-down) | 90 | `MotionSmith_kneel_down_take1.fbx` |
| [Knees buckle and recover](https://cosindra.ai/motionsmith/motions/knees-buckle-recover) | 120 | `MotionSmith_knees_buckle_recover_take1.fbx` |
| [Left foot pain](https://cosindra.ai/motionsmith/motions/pain-left-foot) | 120 | `MotionSmith_pain_left_foot_take1.fbx` |
| [Right foot pain](https://cosindra.ai/motionsmith/motions/pain-right-foot) | 120 | `MotionSmith_pain_right_foot_take1.fbx` |
| [Shot in the right shoulder](https://cosindra.ai/motionsmith/motions/shot-right-shoulder-clutch) | 90 | `MotionSmith_shot_right_shoulder_clutch_take4.fbx` |
| [Shot in the shoulder](https://cosindra.ai/motionsmith/motions/shot-shoulder-clutch) | 90 | `MotionSmith_shot_shoulder_clutch_take1.fbx` |
| [Sit on the floor](https://cosindra.ai/motionsmith/motions/sit-floor) | 150 | `MotionSmith_sit_floor_take1.fbx` |
| [Stand up from the floor](https://cosindra.ai/motionsmith/motions/stand-up-floor) | 150 | `MotionSmith_stand_up_floor_take1.fbx` |
| [Stand up from a kneel](https://cosindra.ai/motionsmith/motions/stand-up-kneel) | 90 | `MotionSmith_stand_up_kneel_take1.fbx` |

## Additional captured candidates acquired

Eleven free Adobe Mixamo clips are cached under
`SourceAssets/Downloads/Mixamo/Motions/` (6,631,008 bytes total): Injured Idle,
Injured Hurting Idle, Falling Down, Stumble Backwards, Kneeling Down, Standing,
Arm Stretching, Injured Walking, Standing Draw Arrow, Standing Aim Idle 01 and
Standing Aim Recoil. Original filenames are retained. These are captured motion
candidates, distinct from MotionSmith's generated performances. Adobe's
[Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) covers free
use of animations in games; raw-source resale/redistribution is outside that use.

All eleven files were inspected: 67 bones and finite sampled motion at 30 fps.
File identities and native tracks are recorded in the local
`Artifacts/FootReview/captured-motion-inspection.json`. The captured skinned Erika
reference in the ignored `Preview/` cache is for inspection, not another playable
character.

`Falling Down` drives the pelvis stagger and drop. The builder blends out its
late floor penetration into a fitted, two-palm seat. `Standing` drives the rise
after an authored pivot to one knee. `Kneeling Down` drives the abdominal weight transfer into the fitted double kneel. `Standing` also drives its planted-foot rise.
`Injured Hurting Idle` holds the upper thigh; it is not a raised-foot reaction.
`Injured Idle` does not supply the required forearm cradle. Arm Stretching and
Injured Walking contain useful support movement but do not by themselves complete
the agreed arm/wrist sequences. The three bow sources cover reload/aim/recoil;
they are alternatives to the unavailable itch archives, not injury replacements.

### Recorded shoulder impact candidates

Mixamo's bow catalog supplied **Standing React Large From Left** and **Standing React Large From Right**, exported on Erika Archer as FBX Binary, without skin,
at 30 fps with no key reduction. The originals are in the local
`SourceAssets/Downloads/Mixamo/Motions/` cache. The left file's SHA-256 is
`2859c39a3af154b567589ad06a8ff8aa4f20f471e536ede765ffd4d2a5e6afdf`.
The right file's SHA-256 is
`20b933bf698cbd461f1447c93deb38e6270fe31096f6af7209ef06e561e503a5`.
Both one-second takes were compared on both archers. Firing the real practice
arrows at Erika's left and right shoulder gave almost frontal impacts. At 100 ms
the recorded wound positions had moved 24 cm and 32 cm sideways, respectively,
and moved toward the shooter on both hits. Both takes were rejected for this
frontal duel. The local comparison video is
`Artifacts/PRReview/CapturedShoulders/Shoulder comparison.mp4`.

Two more bow takes, **Standing React Small From Front** and **Standing React
Large From Front**, were acquired with the same export settings. Their SHA-256
hashes are `19e2df8a0edb4fb80f12001e008238f69001a5fb5d4b97f7e3572a65832824ea`
and `cda00eab48c1e6aba37124dca784377cd6dd0befef593a4d40b6861e2e3512cb`,
respectively. The small take also moved Erika's wound toward the shooter in
the first 100 ms. The large take moved it in the arrow's direction. The
large take supplies the initial impact in the local shoulder trial, followed
by the existing wound hold and recovery. The takes contain no shoulder-clutch
performance. In the desktop trial, 40% of the captured motion is blended over the standing
pose. The right hit mirrors the left after retargeting. At 100 ms, both models
and both sides moved the wound 13.5–13.7 cm in the arrow direction and
0.5 cm sideways. The struck shoulder moved 1.8–2.1 cm farther back than its
opposite. The saved practice scene returned shooting control after both real
shoulder hits. The visual review rejected this trial. The hands and forearms switched into
the old pose after the recorded recoil, which broke the visible continuity.
A replacement needs a single recorded flinch, reach, and supported hold.
The local trial is excluded from the game.
[Mixamo catalog](https://www.mixamo.com/#/?query=react%20bow&type=Motion%2CMotionPack)
and [Adobe's game-use terms](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html).

## Coverage against the agreed reactions

These are source candidates and adaptation requirements, not completed replacement
sequences. All fifteen regions retain the behavior in [InjuryReactions.md](../InjuryReactions.md).

| Region | Available source direction | Remaining fit or source gap |
| --- | --- | --- |
| Head | Existing captured collapse; downloaded Headshot collapse for comparison. | Compare complete movement before choosing any replacement. |
| Neck | Choking plus Fall to the knees and die candidates. | Actual neck-wound reach, knee buckle and fatal transition need one coherent sequence; no recovery. |
| Chest | Existing captured back fall and Getting Up; downloaded backward fall/get-up alternatives. | Retain the back-down landing and full hold; do not replace an accepted motion simply to use a new source. |
| Abdomen | Captured Mixamo Kneeling Down and Standing. | Both rigs combine the captured weight transfer with a fitted double kneel and wound brace. Visual acceptance in the review scene remains open. |
| Pelvis | Captured Mixamo Falling Down and Standing. | Both rigs use the fitted seat and a half-kneel rise. Visual acceptance in the review scene remains open. |
| Left shoulder | Shot in the shoulder (left). | Fit the right palm to the left shoulder and preserve standing balance. |
| Right shoulder | Shot in the right shoulder. | Fit the left palm to the right shoulder; check full motion and recovery. |
| Left arm | Left arm flinch. | Opposite-hand cradling is absent from the catalog description; seek a suitable injured idle/support source. |
| Right arm | Right arm flinch. | Same gap for left-hand support; do not substitute shoulder cupping. |
| Left hand | Right-hand hit is a potential mirrored source. | A derived left-hand motion has not been authored; wrist support and finger opening still need fitting. |
| Right hand | Hit on the right hand. | Actual opposite-hand wrist support is not established by this download. |
| Left leg | Kneel down / stand up from a kneel, potential mirrored source. | The downloaded kneel is right-sided. Left knee and left palm support need their own fitted result. |
| Right leg | Kneel down / stand up from a kneel. | Source kneels on the right knee but does not establish right-palm ground support. |
| Left foot | Left foot pain; potential mirrored right-foot source. | Left take travels about 1.6 m: unsuitable as supplied for the stationary reaction. The mirrored right-foot performance passed Quest 2 review. |
| Right foot | Right foot pain. | Withdrawal, balance and landing passed Quest 2 review with a two-second hold and 0.8-second return to stance. |

Fatal hits outside head/neck still require the regional reaction followed by a
complete collapse. Capture-based existing finishers remain the comparison.
Foot sliding, hand contacts, limb/body penetration and the animated-eye viewpoint
must be checked on both actual archer meshes; skeleton previews cannot establish
these properties.

## Additional free sources

- [Adobe Mixamo](https://www.mixamo.com/): existing captured death/chest clips are
  already retained. The eleven additional captured candidates above were downloaded
  through the official signed-in catalog and inspected; regional fitting remains pending.
- [Kevin Iglesias Human Archer Animations FREE](https://kevdev.itch.io/human-archer-animations-free):
  publisher lists 136 files, masculine/feminine bow idles, load/hold/release and
  other transitions. This is a candidate for normal archery movement, not a
  regional-injury replacement. It has not been downloaded or inspected locally.
  [Official free animation list](https://www.keviniglesias.com/documentation/Human%20Archer%20Animations%20FREE.pdf).
- [Quaternius Universal Animation Library](https://quaternius.itch.io/universal-animation-library):
  only its free Standard archive is eligible. CC0 library; no local archive has
  been acquired or checked. Do not confuse the paid editions with the free set.

## Local inspection artifacts

`Artifacts/FootReview/all-downloaded-motion-inspection.json` records native joint
samples and file identity. `source-review.html` replays those exact samples with
speed, scrubbing and viewing-angle controls. This is a skeleton diagnostic,
not a new character model or a claim of finished archer animation. The viewer's
initial playback and finite sample data were checked in the existing Edge browser.

Original source-page/license observations are in `acquisition.jsonl`. Preview
images made with an unprepared shared bind pose are invalid visual evidence and
must not be used for selection.
