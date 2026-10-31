# Rushed Crops - Project Instructions

These instructions supplement the user's global instructions. Use Claude Code
Game Studios (CCGS) as the project's game-development workflow reference.

## Project and framework roots

- The Unity project root is the directory containing this file, `Assets/`,
  `Packages/`, and `ProjectSettings/`.
- The studio framework root is `Claude-Code-Game-Studios/`. Its `.claude/`,
  `project.yaml`, `design/`, `docs/`, and `production/` paths are relative to
  that directory, not the Unity root.
- Read the actual engine version from `ProjectSettings/ProjectVersion.txt` and
  installed dependencies from `Packages/manifest.json` before API or package work.
- This is an existing Unity/C# project. The nested template's `[CHOOSE]` fields,
  Godot reference import, and `src/` examples do not describe this game.
- Put game code under `Assets/_Game/Scripts/`, grouped by system. The farm scene
  is `Assets/_Game/Scenes/SC_Farm.unity`. Preserve the existing project layout.
- Put Unity tests under `Assets/Tests/EditMode/` or `Assets/Tests/PlayMode/`, with
  appropriate test assemblies. Code or tests in the framework's `src/` or
  `tests/` directories are not compiled by this Unity project.

## Using Claude Code Game Studios

For a studio workflow, read the matching file under
`Claude-Code-Game-Studios/.claude/skills/<skill-name>/SKILL.md` and only the
references needed for that task. These guides are the shared references:

| Task | Reference under `Claude-Code-Game-Studios/` |
| --- | --- |
| Resolve engine paths | `.claude/docs/code-root-resolution.md` |
| Resolve configured workflow and automation settings | `.claude/docs/config-resolution.md`, `.claude/docs/automation-modes.md`, `.claude/docs/workflow-modes.md` |
| Coordinate an invoked agent workflow | `.claude/docs/coordination-rules.md` |
| Implement or review code | `.claude/docs/coding-standards.md` |
| Validate player-visible behavior | `.claude/docs/run-and-observe.md` |
| Resume after compaction or interruption | `.claude/docs/context-management.md` and `production/session-state/active.md`, if present |

Read `project.local.yaml`, when present, and `project.yaml` in the studio root
through the framework's config-resolution procedure. Do not silently change
automation modes or treat template defaults as completed Unity configuration.
If a workflow resolves this game's engine or code root incorrectly, resolve that
mismatch before allowing it to write game files.

Use the workflow appropriate to the request: `/design-system` for unresolved
system design, `/create-stories` for implementation breakdown, `/dev-story` for
an approved story, and `/code-review` and `/story-done` for review and completion.
Apply the configured rigor; keep small coursework features small.

When the user invokes a workflow that calls for delegation, use its named roles:
`unity-specialist` for engine integration, `gameplay-programmer` for gameplay,
`systems-designer` or `economy-designer` for resource rules, `ui-programmer` for
displays, and `qa-tester` for verification. Read the corresponding agent file
under `.claude/agents/`. Give each delegated task clear ownership and acceptance
criteria, and preserve other contributors' edits.

**Discovery limitation:** the Unity root currently has no `.claude/` directory.
Reading this file or importing it through `CLAUDE.md` does not install the nested
framework's skills, agents, hooks, or permission settings. Do not claim those
features are active without checking the running session. Framework installation
or relocation is a separate setup task; keep the Unity root and game paths intact.

## Implementation conventions

- Build beginner-readable C# using the project's existing conventions. Prefer
  small components, explicit references, and composition.
- Keep tunable starting quantities, prices, yields, and repair costs editable
  through serialized fields or suitable data assets.
- Inventory owns shared resource counts. Farming, trading, and repairs use that
  inventory's API rather than maintaining separate copies of the counts.
- Spending validates amounts and availability before mutation. Multi-resource
  transactions succeed completely or leave every count unchanged.
- Separate health and equipment behavior from resource accounting unless the
  requested design explicitly combines them.
- Preserve movement, camera, input bindings, existing scenes, and unrelated work.
  Preserve Unity `.meta` files and asset GUIDs when moving or changing assets.
- Keep generated caches (`Library/`, `Temp/`, `Logs/`, `obj/`) out of authored
  source changes. Let Unity regenerate its solution and project files.

## Validation and delivery

- Confirm compilation before reporting an implementation as working.
- For resource logic, exercise adding, successful spending, insufficient funds,
  invalid amounts, and all-or-nothing multi-resource costs when applicable.
- For visible changes, run the relevant scene and inspect the actual result.
  Follow the studio's run-and-observe procedure and retain evidence when used.
- If Unity or an interaction cannot be tested, state exactly what was checked
  and provide the remaining manual steps. Source inspection is not a playtest.
- Finish with changed files, necessary Inspector setup, validation results, and
  any remaining work. Explain new APIs with short examples when helpful.
- Commit or push only when the user requests it. Add RCS co-author attribution
  only when explicitly requested.
