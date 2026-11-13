# Character assets

The player archer should be a realistically proportioned medieval human in light
clothing or armor, using For Honor and Conqueror's Blade as visual references.
Use free assets only. A player model and its first-person presentation have not
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
| English archer, count_zero | [Sketchfab](https://sketchfab.com/3d-models/english-archer-8571c31e9c934458be2e434de71f94f5), CC BY 4.0 | Male archer with cloth/leather and some mail; source rig and textures unverified. |
| Medieval man, Chenuka Wijesundara | [Sketchfab](https://sketchfab.com/3d-models/game-ready-pirate-hunter-viking-medieval-man-9f51d396733b451bab550b406b067805), CC BY 4.0 | Padded-clothing base; needs bow/quiver, rig checks, and the author's jacket attribution. |
| Erika Archer | [Adobe Mixamo](https://www.mixamo.com/#/?page=1&query=Erika%20Archer&type=Character), [free game use with Adobe ID](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Hooded female archer; official source download and rig inspection pending. |

Candidate previews are local review artifacts, not imported game assets. Record
the source package's actual format and rig after inspection rather than assuming
that a preview or a filename proves game readiness.
