# Ordinary main-menu speech validation, 2026-10-26

The repair is scoped to the existing native Triangle/Y main menu in `MainMenuUI.cs`.
It reads the current native selection and shown UI data. It adds no input, automatic
Confirm, menu item, party/stat write, inventory change, or game-timer change.

## Observed source and installed-asset evidence

The selection route in source is `ButtonGroupState.ActiveButtonChanged` ->
`UICamera.Notify(UIManager.gameObject, "OnItemSelect", go)` ->
`UIKeyTrigger.OnItemSelect` -> the current `UIScene` -> `MainMenuUI.OnItemSelect`.
This trace is source evidence, not confirmation that a live session delivered the
event.

The installed x64 `FF9_Data/level1` was inspected read-only with UnityPy 1.25.4.
It identifies Unity 5.2.3p2 and has SHA-256
`D4AE3FD4D317FFF12344CD97FEA82104A6CDE3DB33478C0B283F0E12AB5A2D25`.
Main Menu Container is GameObject 4534. Item SubMenu is GameObject 6397; its Label
child is GameObject 6369 with a UILabel containing `Item` and UILocalize key `Item`.
The first character row is GameObject 1575. Its Content child is GameObject 1634,
and its Order target is sibling GameObject 1533. Their native groups are
`MainMenu.Character` and `MainMenu.Order`.

These observations establish why the generic descendant-label reader says only
the first character label, finds no label under an Order target, and finds none
in an empty row whose Content is inactive. They also rule out the theory that
ordinary Item has no native UILabel. `TextParser` initializes `ParsedText` from
its input, so an unparsed label is not inherently empty.

`UIScene.AfterShow` clears Loading before `MainMenuUI.AfterShowCharacter` selects
the submenu. Character bars have an independent tween callback that also clears
Loading. Consequently Loading alone does not establish that both presentations
have finished. The old reader also discarded selections suppressed by Loading,
re-read UILocalize keys instead of the displayed patched label, and used generic
collider/button disabled checks instead of the submenu availability predicate
that controls its displayed gray text.

The shared speech facade retains a one-second backend backoff after output
failure. Previously the menu did not retain a failed announcement, so a temporary
failure could leave its current selection silent until another selection event.

## Resulting behavior

The menu waits for its own current opening's fade and character tween callbacks,
and for game activation. It then reads `ButtonGroupState.ActiveButton`. Ordinary
ready selection events still read immediately. Hide, a different current scene,
and a new opening invalidate pending presentation work.

Submenus use their actual displayed UILabel text through the native parser,
including interface patches. Disabled status uses `IsSubMenuEnabled`, matching
the displayed menu state. Character and Order targets use the known
CharacterDetailHUD belonging to that native selection: shown name, level, HP,
MP, magic stones, avatar row position, and recognized rendered status icons.
Inactive Content means an empty slot; its stale labels stay unread. The reader
checks activation and the current native widget opacity through
`CalculateFinalAlpha(Time.frameCount)`, which evaluates ancestor opacity. Hidden
labels, hidden icons and transparent rows are withheld.

Failed backend delivery and temporarily unpresented selected text stay pending.
Retries occur at most once per second, resolve the latest native selection,
and remain deferred while the game is in the background. There is no per-frame
retry log. Successful delivery consumes the pending announcement once.

`Memoria.log` now records bounded main-menu opening, actual selection-event and
delivery diagnostics. Delivery records include presentation identity, group,
native object name, extracted length, backend acceptance and backend diagnostic.
Accepted speech text remains in `%LOCALAPPDATA%/FF9Access/speech.log`. These
records can distinguish event delivery, presentation readiness, extraction and
backend failure during a player-driven tester session.

## Automated evidence

Run from the source root:

```powershell
dotnet run --project tests\MenuSpeechIntegration.Tests\MenuSpeechIntegration.Tests.csproj --configuration Release
```

Final result: **26/26 scenarios passed**, exit code 0. The initial unmodified menu
run passed 5/17 and failed 12/17 for the expected behavior gaps. Added opacity,
delivery rejection, focus and temporarily unpresented-text regressions were each
observed failing before their corresponding fixes.

Coverage includes both animation completion orders; all seven ordinary ready
submenu labels; displayed patches; disabled availability; deferred loading
selection; visible character fields; Order targets and empty slots; hidden and
transparent fields/icons/rows; Hide and current-scene cancellation; callbacks
from a previous opening; submenu return; world entry; unrelated/inactive
selections; rejected delivery recovering after the real facade's cooldown;
latest selection during retry; retry cancellation and quietness; background
opening/retry; and a parent fade becoming visible after readiness.

The harness compiles the actual `MainMenuUI`, `UIScene`, `CharacterDetailHUD`,
`ScreenReaderManager` and `ISpeechBackend` sources. Only Unity/game services,
the parser boundary and native speech output use controllable stand-ins.
The actual facade's existing clock is controlled by reflection in test utilities
to exercise its real cooldown. No test helper or test-only runtime API was added
to production.

## Limits and live acceptance

These tests prove the scoped production menu formatting, lifecycle and retry
behavior under controlled boundaries. They do not execute the real Unity renderer,
ButtonGroupState/UIKeyTrigger event delivery, native text parser, field/world
transition engine, physical controller, Prism/NVDA output or audible speech.
The inspected navigation log ends on October 23; later October 25 speech entries
are separate Mono speech probes, not current main-menu gameplay evidence.

The cause of the user's complete ordinary-menu silence is not yet proven in a
fresh live session. A player-driven check must open Triangle/Y from field and
world, move through Item/Ability/Equip/Status/Order/Card/Config, select character
and Order rows, return from a submenu, close/reopen, and confirm the audible
screen-reader result while matching the new Memoria and speech records.
No fresh gameplay launch, physical-controller test, live audibility check,
Steam-launch test or recipient-PC test was performed by this menu repair task.
Build, local deployment and preservation verification belong to the combined
controller/menu delivery task.

The hierarchy interpretation is consistent with Unity's primary documentation:
[GetComponentInChildren](https://docs.unity3d.com/ScriptReference/GameObject.GetComponentInChildren.html)
searches descendants and normally excludes inactive child objects, while
[activeInHierarchy](https://docs.unity3d.com/ScriptReference/GameObject-activeInHierarchy.html)
accounts for inactive ancestors. The shipped source and installed asset above
remain the target-specific evidence. [UnityPy's official source](https://github.com/K0lb3/UnityPy)
documents its read-only asset inspection APIs.
