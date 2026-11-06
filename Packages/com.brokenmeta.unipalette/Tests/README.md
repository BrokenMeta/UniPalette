# UniPalette tests

[Project overview](../../../README.md) · [Editor architecture](../Editor/README.md)

The suite runs in **Edit Mode**. Each test has a behavior category so you can run the checks for the part you changed.

## Choose a run

| What changed | Category | Test fixtures |
| --- | --- | --- |
| Context or command navigation | `Navigation` | [NavigationTests](Editor/NavigationTests.cs), [CommandNavigationTests](Editor/CommandNavigationTests.cs) |
| Returning to a previous level or restoring its row | `History` | [HistoryTests](Editor/HistoryTests.cs), [ContextHistoryTests](Editor/ContextHistoryTests.cs) |
| Search matching, query identity, and the read-only results list | `Search` | [SearchQueryTests](Editor/SearchQueryTests.cs) |
| Scene and Project traversal or asset identity | `ContextDiscovery` | [ContextDiscoveryTests](Editor/ContextDiscoveryTests.cs) |
| Context confirmation, Unity Selection, or Editor notifications | `ContextConfirmation` | [ContextSelectionTests](Editor/ContextSelectionTests.cs), [ContextConfirmationTests](Editor/ContextConfirmationTests.cs), [WindowContextTests](Editor/WindowContextTests.cs) |
| Menu discovery, availability, or catalog refresh and recovery | `CommandDiscovery` | [CommandDiscoveryTests](Editor/CommandDiscoveryTests.cs), [CommandEligibilityTests](Editor/CommandEligibilityTests.cs), [CommandSourceTests](Editor/CommandSourceTests.cs), [CommandDiscoveryIntegrationTests](Editor/CommandDiscoveryIntegrationTests.cs), [WindowDiscoveryTests](Editor/WindowDiscoveryTests.cs) |
| Combining command sources, notification forwarding, or disposal | `CommandSources` | [CompositeCommandSourceTests](Editor/CompositeCommandSourceTests.cs) |
| Row binding, interrupted result updates, or window/session lifecycle | `View` | [PaletteViewTests](Editor/PaletteViewTests.cs) |
| Keyboard and pointer routing or search focus | `Input` | [PaletteInputTests](Editor/PaletteInputTests.cs) |
| Subscription disposal and callback ordering | `Subscriptions` | [SubscriptionTests](Editor/SubscriptionTests.cs) |
| Target, Selection, and menu checks before dispatch | `ExecutionPreparation` | [CommandActivationTests](Editor/CommandActivationTests.cs), [ExecutionPreparationTests](Editor/ExecutionPreparationTests.cs) |
| Main-menu close, focus, and dispatch | `NativeIntegration` | [NativeIntegrationTests](Editor/NativeIntegrationTests.cs) |
| Scene View contextual discovery and dispatch | `ContextActions` | [ContextActionTests](Editor/ContextActionTests.cs) |
| Project contextual discovery and dispatch | `AssetActions` | [AssetActionTests](Editor/AssetActionTests.cs) |

### Filter by environment

| Category | What it exercises |
| --- | --- |
| `Fast` | Fixture data and injected menu availability. No temporary scenes or assets are needed. Some tests briefly change Unity Selection and restore it in teardown. |
| `UI` | Real Editor windows with synthetic keyboard and pointer events. Covers input routing, row binding, and focus. Physical input and visual appearance require separate verification. |
| `EditorIntegration` | Live Editor APIs, including scene or asset data, SearchService, notifications, focus, or command execution. Run serially because the tests share Editor state. |

Select a category under **Edit Mode** in Unity Test Runner. With Unity CLI MCP, pass the following to `test_run`. Use `filter_type: "testName"` to run a fixture or case by name. Run the full Edit Mode suite after changes that cross multiple areas.

```json
{
  "mode": "editor",
  "filter_type": "category",
  "filter": "History"
}
```

## Shared scene and test data

1. [TestSceneSession](Editor/TestSceneSession.cs) owns one [SharedTestScene](Editor/SharedTestScene.cs) for the selected run. The first test that needs scene data copies the [empty scene template](Editor/Scenes/UniPaletteTestScene.unity) to a disposable folder under `Assets` and opens it.
2. Ordinary scene tests reuse that open copy and clear their objects between cases. At the end of the run, teardown restores the previous active scene if it is still loaded, closes the copy without saving, and deletes the owned folder.
3. The closed-scene regression uses a second disposable copy. A separate regression temporarily switches the active scene. Unity Test Runner manages its own untitled scene independently.

Asset fixtures create and clean up their own temporary folders. Run data-changing command checks against fixture-owned objects and assets. Treat already-open scenes and existing project assets as user-owned. Keep the saved scene template intact.

## Adding a test

- Put a regression in the category that owns the behavior. Use the existing `*TestFixture` bases for setup and cleanup.
- Use fixture sources for workflow checks. Window tests install a session through `UniPaletteWindow.ReplaceSession`, the same method used by the real window.
- Use integration tests for Unity-specific outcomes such as target changes, focus, and command dispatch. Reuse existing coverage of Enter and click for commands that use the same route.
