# Project tooling sources

Only `Assets/`, `Packages/`, `ProjectSettings/`, and `.gitignore` are versioned. Unity imports the game and embedded packages from those folders. Root-level editor settings, workflow references, CI files, and caches are local only and ignored.

## Studio reference

The local `Claude-Code-Game-Studios/` checkout comes from [Donchitos/Claude-Code-Game-Studios](https://github.com/Donchitos/Claude-Code-Game-Studios), revision `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418`. Its original MIT licence remains in that local directory.

That checkout and its validation evidence are ignored by the game repository. A fresh clone does not include them.

The local `AGENTS.md` and `CLAUDE.md` explain how to resolve the Unity paths. They are also ignored. The nested reference does not install or activate hooks, agents, or skills in the Unity root.

## Unity editor connection

`Packages/com.coplaydev.unity-mcp/` contains the current embedded MCP for Unity editor package, version `10.3.0`, originating from [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp). Its upstream MIT licence is retained as `LICENSE.md` in the package. The committed package lock identifies the package as embedded; the source is included so a fresh clone gets the same editor tools.

VS Code settings under `.vscode/` are local only. CodeGraph's generated database, Unity's `Library/` and `Temp/`, build output, and credentials are not versioned.

## Push validation

The current map passed Unity's saved-scene validation and the console contained no C# errors. All 24 inventory/clock logic tests passed before the repository cleanup. `.github/` and `CI/` remain locally but are ignored, so automatic GitHub CI and the hosted Windows delivery workflow are no longer part of the tracked project. The Unity scene validator and Windows build method remain under `Assets/_Game/Scripts/World/Editor/`.
