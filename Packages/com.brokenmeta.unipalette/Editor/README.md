# Editor architecture

This is the first stage of organizing the package into layers. One Editor assembly still contains all layers.

| Folder / namespace suffix | Responsibility | Main types |
| --- | --- | --- |
| Host | EditorWindow entry point, composition and lifetime | UniPaletteWindow |
| Presentation | Visual tree, recycled rows, input events and focus | PaletteView, UI assets |
| Application | Palette workflow, browser transitions, query/selection, history and source contract | PaletteSession, PaletteBrowser, ContextBrowser, CommandBrowser, PaletteHistory, ICommandSource |
| Model | Items, context identity and relationships | PaletteItem, ContextItem, asset/scene/root contexts, menu commands/categories |
| Services | Unity data access, menu discovery and native dispatch | UnityContexts, MenuCommands, NativeExecution |

Internal namespaces follow BrokenMeta.UniPalette.Editor plus the folder name. The public UniPaletteWindow remains in BrokenMeta.UniPalette.Editor to preserve its full type identity for saved Editor layouts and external references.

PaletteSession is the former PaletteState: it coordinates operations as well as holding transient data. The window owns its command source; the session borrows the source and unsubscribes on disposal. Browser objects represent immutable navigation levels retained in history. Model items describe identity/relationships; PaletteView owns actual App UI elements and callbacks.

## Remaining stages

This layout makes current responsibilities visible; strict dependency separation is not complete. Context models still call Unity services and change Selection. Browsers still provide captions/row descriptions and use native eligibility policy. The host still routes input, renders changes, watches Editor state and dispatches commands.

Next: extract the presenter from the host. Later stages can move presentation descriptions and Unity confirmation operations to their intended boundaries. Keep these behavioral moves separate from the initial folder/namespace migration. No additional assembly definitions, service registry or MVVM framework are required for this stage.
