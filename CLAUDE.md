# Rushed Crops

Unity 6 (6000.5.9f1) farming game using URP and the Input System.

## Project layout

- Game code and assets live in `Assets/_Game/`. Scripts go under `Assets/_Game/Scripts/<Area>/`.
- Game scripts compile into the `RushedCrops` assembly (`Assets/_Game/Scripts/RushedCrops.asmdef`).
- Edit mode tests live in `Assets/_Game/Tests/EditMode/`.
- Player input is defined in `Assets/_Game/Input/IA_PlayerControls.inputactions`.
- Asset prefixes: `SC_` scenes, `PF_` prefabs, `IA_` input actions.

## Git workflow

- Never commit straight to `main`. Work on a branch (`feature/...`, `fix/...`) and open a pull request.
- Keep each commit to one change, and keep the project compiling at every commit.
- Commit every new asset together with its `.meta` file. CI fails if a `.meta` file is missing or orphaned.
- Run the edit mode tests before pushing when the Unity editor is closed:
  `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/test-results.xml`
- Check `.meta` files locally with `bash .github/scripts/check-meta-files.sh` (it reads staged and committed files).

## CI

`.github/workflows/ci.yml` runs on every pull request and every push to `main`:

1. `meta-files` checks that assets and `.meta` files match.
2. `tests` runs the edit mode tests with GameCI.
3. `build` makes a Windows build after a merge to `main` and uploads it as a workflow artifact.

The Unity jobs need the repository secrets `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`. Without them they are skipped with a warning.

## Commit messages

Write every commit message and pull request description with the `humanizer` skill
(`.claude/skills/humanizer/SKILL.md`), in its embedded mode: run its checks and output only the final text.

On top of the skill's rules:

- Subject line: imperative mood, under 60 characters, no trailing period. Say what changed in the game, in plain words a non-programmer can follow ("Let the player look around with the mouse", not "Implement PlayerLook component").
- Body (optional): one to three short sentences on why the change was made or what to watch for. Skip it when the subject says enough.
- No bullet lists with bold labels, no em dashes, no filler such as "This commit...", "enhance", "robust" or "seamless".
