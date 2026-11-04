namespace BrokenMeta.UniPalette.Editor.Application
{
    // Dispatches by browser behavior without referencing a presentation implementation.
    internal interface IPaletteBrowserVisitor<out TResult>
    {
        TResult Visit(ContextBrowser browser);
        TResult Visit(CommandBrowser browser);
    }
}
