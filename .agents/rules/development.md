# Development decisions

- Read [AGENTS.md](../../AGENTS.md) for the current project requirements and
  [Docs/Decisions.md](../../Docs/Decisions.md) for the reasons behind settled choices.
- When a development choice becomes settled, update its existing decision entry
  or add a focused entry with the choice, reason and practical consequence.
  Keep alternatives and provisional values clearly marked; do not turn a suggestion
  into an accepted requirement.
- Keep enforceable project requirements in AGENTS.md, folder roles in README.md,
  and asset provenance in Docs/Art/CharacterAssets.md. Update affected instructions
  and path consumers together when a decision changes.
- Put reusable task procedures in a skill. Keep the shared procedure under
  `.agents/skills/` and the Claude entry point under `.claude/skills/` pointing to it.
  Keep `.claude/rules/` aligned through references to these shared rules.
- Record decisions directly, without personal attribution. Use GitHub Assignees
  for ownership and issues/PRs for work status; skills and rules grant no merge
  authorization and should not contain transient session state.

For the documentation procedure, use
[document-development-decisions](../skills/document-development-decisions/SKILL.md).
