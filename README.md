# UniPalette

**A keyboard-first command palette for the Unity Editor.**

Find a GameObject, asset, or folder, then search for Unity commands for that context in the same window. Start from your current Selection or browse the Hierarchy and Project independently.

**Unity 6.6** · **UPM package** · **MIT license**

[Installation](#installation) · [Quick start](#quick-start) · [Keyboard shortcuts](#keyboard-shortcuts) · [Development](#development)

## Installation

In Unity 6.6, open **Package Manager**, choose **+ → Install package from git URL**, and paste the URL below. Git must be available on your `PATH`. [Unity Package Manager guide](https://docs.unity3d.com/6000.6/Documentation/Manual/upm-ui-giturl.html)

```text
https://github.com/BrokenMeta/UniPalette.git?path=/Packages/com.brokenmeta.unipalette
```

The package targets Unity `6000.6` and the development project uses `6000.6.8f1`. Unity Package Manager resolves the App UI dependency from the package manifest.

## Quick start

1. Open **Window → UniPalette** or press <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>U</kbd>.
2. Choose **Current Selection**, **Hierarchy**, or **Project**. Type to search within the area you open.
3. Highlight a result and press <kbd>Ctrl</kbd>+<kbd>Enter</kbd> to open its commands. Search for a command, highlight it with the arrow keys, and press <kbd>Enter</kbd>.

| To do this | Choose this context | Search for |
| --- | --- | --- |
| Add a collider | A GameObject | `Box Collider` |
| Create a child object | A GameObject | `Create Empty Child` |
| Copy an asset or folder path | An asset or folder in Project | `Copy Path` |

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| <kbd>↑</kbd> / <kbd>↓</kbd> | Move the highlighted row. |
| <kbd>Enter</kbd> | Browse a context with children, open commands for a leaf context, or activate the highlighted command or category. |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd> | Open commands for the highlighted context, even if it has children. |
| <kbd>Alt</kbd>+<kbd>→</kbd> | Act like Enter in the context list. In the command list, open categories only. |
| <kbd>Alt</kbd>+<kbd>←</kbd> | Go back one level. |
| <kbd>Esc</kbd> | Go back one level or close the palette at its starting screen. |

## How commands work

Browsing contexts leaves Unity's Selection untouched. Opening commands confirms the context and updates Selection for Unity. You can also open commands for the active scene root or the project's `Assets` root.

Main-menu commands appear separately from **Scene View Actions** and **Asset Actions**. These groups expose contextual actions available through Unity's APIs. Coverage varies by object or asset type.

UniPalette validates the target before handing the command to Unity. Unity controls whether the command takes effect, along with Undo/Redo and dialogs. Some commands still require a specific Editor window or UI state.

## Development

Open the repository root as a Unity project. The package is embedded under `Packages/com.brokenmeta.unipalette`. Run the **Edit Mode** suite in Unity Test Runner.

- [Editor architecture](Packages/com.brokenmeta.unipalette/Editor/README.md)
- [Test suite and categories](Packages/com.brokenmeta.unipalette/Tests/README.md)
- [MIT license](LICENSE)
