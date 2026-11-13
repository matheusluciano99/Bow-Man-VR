# Character assets

The player archer should be a realistically proportioned medieval human in light
clothing or armor, using For Honor and Conqueror's Blade as visual references.
Use free assets only; multiple different archer models are allowed. A player
model and its first-person presentation have not
been selected; design is tracked in [issue #38](https://github.com/matheusluciano99/Bow-Man-VR/issues/38).

## Retained Templar Knight

- Creator: everlasting17th / everlastinggrey.
- [Source](https://sketchfab.com/3d-models/templar-knight-rigged-game-ready-e57edad1bb8e4dbd8573f85695ceecde),
  licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- Imported source files and attribution: `Assets/ThirdParty/TemplarKnight/`.
- Original archive: `SourceAssets/Downloads/TemplarKnight/templar-knight-rigged-game-ready.zip`
  (local cache, excluded from Git).
- Blender inspection found a 52-bone skeleton, 12,995 weighted body vertices,
  24,012 body triangles, 4K character texture maps, and no animation clips.
- Model scale, Humanoid mapping, materials, and animation behavior need preparation
  before gameplay use. The knight's heavy armor does not meet the archer design.
- Local preview, provenance, and inspection report: `Artifacts/CharacterReview/`.

## Free archer candidates

| Candidate | Source and license | Remaining checks |
| --- | --- | --- |
| English archer, count_zero | [Sketchfab](https://sketchfab.com/3d-models/english-archer-8571c31e9c934458be2e434de71f94f5), CC BY 4.0 | Downloaded GLB inspected: 32,466 triangles, three materials, nine 4K textures, no skin weights, skeleton or animation. Needs rigging; original DAE uninspected. |
| Medieval man, Chenuka Wijesundara | [Sketchfab](https://sketchfab.com/3d-models/game-ready-pirate-hunter-viking-medieval-man-9f51d396733b451bab550b406b067805), CC BY 4.0 | Padded-clothing base; needs source download, bow/quiver and rig checks. Credit Chenuka Wijesundara and Leon Steiner for the [gambeson](https://sketchfab.com/3d-models/gambeson-0fc44d2876ed449ca9473e78c83a8d39), also CC BY 4.0. |
| Viking/Medieval Game Character, CGenesis | [Sketchfab](https://sketchfab.com/3d-models/vikingmedieval-game-character-a9e99a47d2a24957ab6c08c9e57ad48c), CC BY 4.0 | Leather/cloth male base; creator says rigged, actual source uninspected. Needs source download, rig/finger checks and replacement of melee equipment with bow/quiver. |
| Erika Archer | [Adobe Mixamo](https://www.mixamo.com/#/?page=1&query=Erika%20Archer&type=Character), [free game use with Adobe ID](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Hooded female archer; official source download and rig inspection pending. |

Candidate previews are local review artifacts, not imported game assets. Record
the source package's actual format and rig after inspection rather than assuming
that a preview or a filename proves game readiness.

The English Archer original is preserved unchanged under ignored
`SourceAssets/Downloads/EnglishArcher/english_archer.glb`, with checksum,
source/license metadata and attribution alongside it. Its 128.57 MiB size exceeds
GitHub's regular per-file limit; export smaller runtime files before Git import.
The remaining official Sketchfab and Mixamo downloads need account authentication.
Source acquisition does not select a final character or establish animation readiness.
