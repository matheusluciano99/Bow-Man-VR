---
name: document-development-decisions
description: Record or revise Bow Man VR development decisions and keep project instructions, shared agent rules and Claude guidance consistent.
---

# Document development decisions

Read the root [project instructions](../../../AGENTS.md), shared
[development rules](../../rules/development.md), and
[decision log](../../../Docs/Decisions.md). Inspect the code, configuration or
source documentation affected by the choice before recording it as current fact.

For a settled choice, record what was chosen, why it fits this project and what
future work must preserve. Update an existing entry for the same topic rather
than adding contradictory guidance. When replacing a choice, retain its reason
as superseded context if it explains a meaningful tradeoff.

Put product requirements in AGENTS.md, folder roles in README.md, and asset
sources/licenses in Docs/Art/CharacterAssets.md; link to those canonical details.
Leave unapproved alternatives and temporary tuning under the decision log's open
questions. Keep task ownership/status on GitHub, without copying them into skills.

Update affected paths and entry points together. Keep the shared procedure here;
the Claude skill and rule files should point to the shared guidance rather than
copying it. Validate both skill entry points and check their local links. For a
documentation-only change, no additional Unity runtime test is needed; complete
the required PR checks from AGENTS.md.

Follow the repository branch/PR workflow. Documentation changes do not authorize
merging a pull request or importing a proposed character.
