namespace BrokenMeta.UniPalette.Editor.Model
{
    internal interface IContextVisitor<out T>
    {
        T Visit(SceneObjectContext context);
        T Visit(AssetContext context);
        T Visit(SceneRootContext context);
        T Visit(AssetsRootContext context);
    }
}