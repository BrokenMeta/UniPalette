# Editor architecture

[Project overview](../../../README.md) · [Test guide](../Tests/README.md)

UniPalette runs in one Unity Editor assembly. Each window owns its palette. The session coordinates browsing and search, and the window hands prepared commands to Unity.

## Where to make a change

| Area | Owns | Start here |
| --- | --- | --- |
| **Host** | Window lifecycle, dependency wiring, and closing the palette before dispatch | [UniPaletteWindow](Host/UniPaletteWindow.cs) |
| **Presentation** | UI Toolkit/App UI elements, input, focus, and row formatting | [PaletteView](Presentation/PaletteView.cs), [PalettePresenter](Presentation/PalettePresenter.cs), [BrowserPresentationSelector](Presentation/BrowserPresentationSelector.cs) |
| **Application** | Navigation, query, selected row, history, and result updates | [PaletteSession](Application/PaletteSession.cs), [ContextBrowser](Application/ContextBrowser.cs), [CommandBrowser](Application/CommandBrowser.cs) |
| **Model** | Item identity, context relationships, command descriptors, and search input | [PaletteItem](Model/PaletteItem.cs), [ContextItem](Model/ContextItem.cs), [SearchQuery](Model/SearchQuery.cs) |
| **Services** | Unity Selection, Editor notifications, command discovery, and native dispatch | [EditorContextObserver](Services/EditorContextObserver.cs), [UnityContextSelection](Services/UnityContextSelection.cs), [PaletteCommands](Services/PaletteCommands.cs), [CommandExecutionPreparation](Services/CommandExecutionPreparation.cs) |
| **Lifetime** | Synchronous notifications and disposable subscription handles | [Signal](Lifetime/Signal.cs), [SubscriptionScope](Lifetime/SubscriptionScope.cs) |

Context models read live Unity data for identity, validity, and display. Unity Selection synchronization belongs to `UnityContextSelection`. The public `UniPaletteWindow` keeps its existing namespace for saved Editor layouts.

## Browse and search

1. `PaletteView` translates input into actions. `PalettePresenter` forwards those actions to `PaletteSession` and renders the resulting state.
2. The active browser defines the search scope. `ContextBrowser` reads live results through [IContextSource](Application/IContextSource.cs). `CommandBrowser` filters its loaded command tree locally.
3. The session collects a complete set of rows before applying the new browser, query, and selected index. [PaletteResult](Application/PaletteResult.cs) captures title and path once per rebuild so the presenter can reuse the text.
4. If a source throws mid-update, the session retains the last complete screen. A failed search restores the previous query. A failed back transition keeps its history entry. A failed refresh remains visible while the user filters the previous rows and clears after a successful refresh or transition.

Browsing and moving the highlighted row leave Unity Selection unchanged. Opening the command browser confirms the context and synchronizes Selection **before** requesting commands. If discovery throws during that transition, the previous palette screen stays visible and Unity retains the confirmed Selection.

## Discover and run commands

[PaletteCommands](Services/PaletteCommands.cs) combines command trees in source order and forwards loading state, errors, and change notifications. The host supplies the sources and shares each contextual catalog with its executor.

| Route | Discovery | Dispatch |
| --- | --- | --- |
| **Main menu** | [MenuCommands](Services/MenuCommands.cs) finds paths through SearchService. [MenuRelevance](Services/MenuRelevance.cs) supplies the context rules shared with preparation. | [NativeExecution](Services/NativeExecution.cs) focuses Hierarchy or Project, checks live availability, then calls `ExecuteMenuItem`. |
| **Scene View Actions** | [SceneViewActionCatalog](Services/SceneViewActionCatalog.cs) collects GameObject actions into an in-memory menu. The group can be shown when empty. | [SceneViewActionExecution](Services/SceneViewActionExecution.cs) focuses the Scene View captured when the palette was enabled. |
| **Asset Actions** | [AssetActionCatalog](Services/AssetActionCatalog.cs) collects actions for the actual asset type. The source hides an empty group. | [AssetActionExecution](Services/AssetActionExecution.cs) focuses Project and revalidates the captured asset. |

Both contextual routes use the same generic adapter, [ContextActionSource](Services/ContextActionSource.cs). They keep descriptors in the command tree and resolve a fresh handler after focus. Virtual roots use the main-menu route with explicit Selection state. Scene Root requires empty Selection and captures the active main-stage scene during execution preparation. Assets Root selects and opens the real `Assets` folder.

When the user activates a command leaf, `PaletteSession` sends the execution request directly to `UniPaletteWindow`. The window prepares the request while the palette is open. Once preparation succeeds, it closes the palette and invokes the prepared executor. The executor focuses the target window, rechecks the relevant state, and hands off the command at most once. Preparation failures appear in the palette. Failures after closing go to the Unity Console.

Menu availability is a preliminary check. Unity owns command effects, deferred work, Undo/Redo, and dialogs. Contextual groups reflect actions exposed by the public APIs, and coverage varies by target type and Editor state.

## Ownership and refresh

| Owner | Responsibility |
| --- | --- |
| **UniPaletteWindow** | Owns the view, presenter, session, command source, and context observer. Subscribes directly to context confirmation and execution requests. |
| **EditorContextObserver** | Subscribes to Selection, hierarchy, and project changes. Its periodic check catches Selection changes Unity did not report and targets that are no longer valid. The window decides when to refresh the session. |
| **PaletteSession** | Borrows its sources and owns its command-source subscription. Owns the active browser, history, and committed result state. |
| **PaletteCommands** | Owns its child command sources and the handles used to forward their notifications. |
| **PalettePresenter** | Borrows the view and session. Owns its input and rendering subscriptions. |

`UniPaletteWindow.ReplaceSession` releases the old presenter, window-to-session subscriptions, session, and owned source before wiring the replacement. Unity's normal window setup and the window tests use this same method. The observer lives with the window, so replacing a session keeps one set of Editor subscriptions. When the session confirms a context, the window acknowledges the new Selection so a delayed notification does not reset the selected row.

## Verification

Edit Mode coverage includes search recovery, history restoration, Selection synchronization, session replacement, and real close/focus/dispatch routes for scene and asset commands. Integration fixtures use disposable scenes and assets. See the [test guide](../Tests/README.md) for categories and setup.
