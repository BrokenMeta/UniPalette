# UniPalette tests

Tests are grouped by the behavior they own. NUnit categories select a mechanic independently of the class/file layout. All tests run in Edit Mode in the project's connected Unity Editor; none require Play Mode.

| Category | Responsibility | Fixtures |
| --- | --- | --- |
| Navigation | Forward transitions, context/command browsing, query selection | NavigationTests, CommandNavigationTests |
| History | Back navigation, entry identity, visible ancestors, removed entries | HistoryTests, ContextHistoryTests |
| ContextDiscovery | Scene/project search scope, paths, asset identity | ContextDiscoveryTests |
| ContextConfirmation | Adopting Selection, real children, editor notifications | ContextConfirmationTests, WindowContextTests |
| CommandDiscovery | Menu normalization/tree, family relevance, availability, live SearchService | CommandDiscoveryTests, CommandEligibilityTests, CommandDiscoveryIntegrationTests, WindowDiscoveryTests |
| ExecutionPreparation | Target/Selection/menu validation, categories, consumed requests | ExecutionPreparationTests, CommandActivationTests |
| View | Row rebinding, visual metadata, callback disposal and GUI recreation | PaletteViewTests |
| Input | Keyboard/pointer routing, modifiers, list selection and search focus | PaletteInputTests |
| NativeIntegration | Real dispatch, pre-close failure in the window, close/focus ordering, representative native results | NativeIntegrationTests |

Additional categories describe the environment:

- **Fast**: fixture data and injected menu availability; no temporary scenes/assets or native command execution. Some cases confirm a virtual root and temporarily change Unity Selection, which the fixture restores. These are still Editor tests, not isolated parallel tests.
- **UI**: real Editor window and synthetic keyboard/pointer events. Includes view/input and the window integration fixtures. Does not prove physical input or screenshot appearance.
- **EditorIntegration**: live scene/assets, SearchService, editor notifications or native commands. Run in the verified project Editor; shared global Editor state makes parallel execution inappropriate.

## Choosing a run

Use a mechanic category while changing that behavior. Use **Fast** for the inexpensive model/discovery checks, **UI** for window/input work, and **NativeIntegration** when changing command dispatch or focused editor preparation. Run the full suite after a change crosses several of these boundaries or when reorganizing the tests themselves.

The Unity Test Runner exposes NUnit categories. With the Unity CLI MCP test_run tool, select a category using `mode: "editor"`, `filter_type: "category"`, and `filter: "History"` (or another category above). A category may include both fast and integration fixtures; use the individual fixture name with a testName filter when only that fixture is relevant.

## Ownership rules

Keep each behavior's detailed variants in its owning group. UI tests verify routing and focus; they should not repeat every native command outcome. Native integration retains one result scenario per command/context variant, plus representative Enter/click paths for scene objects, assets and virtual roots. Keep target deletion, Selection changes, current menu state, consumed requests and special root-scene validation covered.

Shared `*TestFixture` classes contain setup/cleanup and helpers, never test cases. The namespace-level `TestSceneSession` owns one temporary scene for the entire selected run, including runs spanning multiple fixtures. `SharedTestScene` copies the saved empty template into a disposable Assets folder lazily on first use, opens that copy once, leaves it loaded/active between ordinary cases, and clears only its own objects between cases. At the end it restores the previous active scene, closes the copy without saving test mutations, and deletes the owned temporary folder. Fast and UI-only runs that do not need scene data create no scene through this helper.

The closed-scene regression deliberately creates/closes a separate disposable scene; it never closes the main session scene. The changed-scene regression temporarily switches the active scene. Native integration also focuses Hierarchy/Project and opens/closes the palette as required by the execution contract, so one scene session does not eliminate window/focus changes during those tests.

Asset tests create disposable folders and clean them up. Tests must not operate on user scenes/assets or turn Delete/Reimport/Save into production-data integration checks. The saved scene template under Editor/Scenes is copied, never overwritten or used for test mutations; any already-open scene is treated as user-owned. Unity Test Runner also manages its own untitled scene around the run. Its setup/restoration is separate from the package's scene session.

Add a case only for a distinct contract or regression. Name it by behavior rather than an implementation milestone. A new command using the same dispatch path does not require another Enter/click matrix.
