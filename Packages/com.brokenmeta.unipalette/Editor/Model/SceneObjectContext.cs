using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class SceneObjectContext : ContextItem
    {
        internal override T Accept<T>(IContextVisitor<T> visitor) => visitor.Visit(this);
        internal GameObject SceneObject { get; }
        internal SceneObjectContext(GameObject sceneObject, bool selectionEntry = false) : base(selectionEntry)
        { SceneObject = sceneObject; }
        internal override Object Target => SceneObject;
        internal override string Title => SceneObject ? SceneObject.name : "Missing object";
        internal override bool IsValid => UnityContexts.IsSceneObject(SceneObject);
        internal override bool CanBrowse => IsValid && SceneObject.transform.childCount > 0;
        internal override string Path
        {
            get
            {
                if (!IsValid) return "Missing object";
                var segments = new List<string>();
                for (var t = SceneObject.transform; t; t = t.parent)
                    segments.Add(t.name + " [" + t.GetSiblingIndex() + "]");
                segments.Reverse();
                var scene = SceneObject.scene;
                return "Scene Root/" + scene.name + " [" + scene.handle + "]/" + string.Join("/", segments);
            }
        }
        internal override bool SameIdentity(PaletteItem other) => other is SceneObjectContext context
            && SelectionEntry == context.SelectionEntry && SceneObject == context.SceneObject;
        internal override bool ContainsDescendant(PaletteItem other) => !SelectionEntry
            && other is SceneObjectContext context && !context.SelectionEntry
            && SceneObject && context.SceneObject && context.SceneObject.transform.IsChildOf(SceneObject.transform);
        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) =>
            IsValid ? UnityContexts.SceneChildren(SceneObject.transform, recursive) : Array.Empty<PaletteItem>();
        internal override bool IsMenuRelevant(string path) => path == "Edit/Duplicate"
            || path.StartsWith("GameObject/", StringComparison.Ordinal) || path.StartsWith("Component/", StringComparison.Ordinal);
    }
}
