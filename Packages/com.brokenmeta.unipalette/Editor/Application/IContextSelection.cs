using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    internal interface IContextSelection
    {
        bool TryConfirm(ContextItem context, out string error);
        bool MatchesSelection(PaletteItem context);
    }
}
