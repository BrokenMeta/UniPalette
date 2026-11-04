# Editor architecture

The package is being organized into layers incrementally. One Editor assembly still contains all layers.

| Folder / namespace suffix | Responsibility | Main types |
| --- | --- | --- |
| Host | EditorWindow entry point, composition/lifetime, Editor notifications and close-before-execution | UniPaletteWindow |
| Presentation | Visual tree, recycled rows, input routing, browser formatting, session-to-view coordination and focus | PaletteView, PalettePresenter, BrowserPresentation, PaletteRow, UI assets |
| Application | Palette workflow, browser transitions, query/selection, history and source contract | PaletteSession, PaletteBrowser, ContextBrowser, CommandBrowser, PaletteHistory, ICommandSource |
| Model | Items, context identity and relationships | PaletteItem, ContextItem, asset/scene/root contexts, menu commands/categories |
| Services | Unity data access, menu discovery and native dispatch | UnityContexts, MenuCommands, NativeExecution |

Internal namespaces follow BrokenMeta.UniPalette.Editor plus the folder name. The public UniPaletteWindow remains in BrokenMeta.UniPalette.Editor to preserve its full type identity for saved Editor layouts and external references.

PaletteSession is the former PaletteState: it coordinates operations as well as holding transient data. The window owns its command source; the session borrows the source and unsubscribes on disposal. Browser objects represent immutable navigation levels retained in history. Model items describe identity/relationships; PaletteView owns actual App UI elements and callbacks. PalettePresenter borrows the view/session, subscribes to their events and maps input to session actions and rendering. The host creates an ordinary BrowserPresentationSelector for each presenter and injects it through the existing IPaletteBrowserVisitor<BrowserPresentation> contract. The selector owns its two formatting instances; there is no global formatting instance or static factory. Formatting and PaletteRow data live in Presentation; Application does not reference them. These formatting objects own no disposable resources. The host disposes the presenter before the view/session/source. Host callbacks provide close, native execution and acknowledgement of the current Editor Selection before rendering.

## Remaining stages

This layout makes current responsibilities visible; strict dependency separation is not complete. Context models still call Unity services and change Selection. Browsers still use native eligibility policy for activation. Navigation locations, search aliases and application error messages remain in Application. The host still watches Editor state, validates native execution and closes before dispatch.

Next stages can move Unity confirmation operations to their intended boundaries. Adding another browser requires its visitor dispatch and corresponding presentation selection, keeping the supported browser kinds explicit without runtime type switches in the view. No additional assembly definitions, service registry or MVVM framework are required for these stages.
