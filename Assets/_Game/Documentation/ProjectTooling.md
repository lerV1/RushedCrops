# Project tooling sources

The Unity game is rooted in this repository. Code under `Assets/` and the embedded packages under `Packages/` are imported by Unity; the nested studio framework is a workflow reference.

## Studio reference

`Claude-Code-Game-Studios/` is a vendored copy of the tracked files from [Donchitos/Claude-Code-Game-Studios](https://github.com/Donchitos/Claude-Code-Game-Studios), revision `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418`. Its original MIT licence is retained in that directory.

The nested checkout's `.git` database is local only. The game repository stores the reference files directly, so a normal clone contains the guides without needing another checkout. Local session checkpoints, credentials, caches, and per-user settings are excluded by its ignore rules.

The project's own `AGENTS.md` and `CLAUDE.md` explain how to resolve the Unity paths. Shipping the nested reference does not install or activate its hooks, agents, or skills in the Unity root.

## Unity editor connection

`Packages/com.coplaydev.unity-mcp/` contains the current embedded MCP for Unity editor package, version `10.3.0`, originating from [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp). Its upstream MIT licence is retained as `LICENSE.md` in the package. The committed package lock identifies the package as embedded; the source is included so a fresh clone gets the same editor tools.

The generic VS Code Unity debug and file-association settings are shared under `.vscode/`. CodeGraph's generated database, Unity's `Library/` and `Temp/`, build output, and credentials are not versioned.

## Push validation

The current map passed Unity's saved-scene validation and the console contained no C# errors. All 24 inventory/clock logic tests passed locally. GitHub's automatic CI validates the pushed revisions. The Windows delivery workflow remains manual and needs an appropriate configured Unity licence.
