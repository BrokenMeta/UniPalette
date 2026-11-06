using System;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class EditorContextObserver : IDisposable
    {
        private const double ContextCheckInterval = 0.1;

        private readonly SubscriptionScope _subscriptions = new();

        private readonly Func<PaletteItem> _getContext;

        private readonly Action _invalidate;

        private double _nextContextCheck;

        private Object _observedSelection;

        private int _observedSelectionCount;

        private bool HasSelectionChanged => !ReferenceEquals(_observedSelection, Selection.activeObject)
            || _observedSelectionCount != Selection.count;

        internal EditorContextObserver(Func<PaletteItem> getContext, Action invalidate)
        {
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
            AcknowledgeSelection();

            try
            {
                Subscription
                    .Create(
                        () => Selection.selectionChanged += OnSelectionChanged,
                        () => Selection.selectionChanged -= OnSelectionChanged
                    )
                    .AddTo(_subscriptions);

                Subscription
                    .Create(
                        () => EditorApplication.hierarchyChanged += _invalidate.Invoke,
                        () => EditorApplication.hierarchyChanged -= _invalidate.Invoke
                    )
                    .AddTo(_subscriptions);

                Subscription
                    .Create(
                        () => EditorApplication.projectChanged += _invalidate.Invoke,
                        () => EditorApplication.projectChanged -= _invalidate.Invoke
                    )
                    .AddTo(_subscriptions);

                Subscription
                    .Create(
                        () => EditorApplication.update += CheckContextChanges,
                        () => EditorApplication.update -= CheckContextChanges
                    )
                    .AddTo(_subscriptions);
            }
            catch
            {
                _subscriptions.Dispose();
                throw;
            }
        }

        private void OnSelectionChanged()
        {
            // A delayed notification can describe a Selection the window has already acknowledged.
            if (HasSelectionChanged)
                _invalidate();
        }

        private void CheckContextChanges()
        {
            var now = EditorApplication.timeSinceStartup;

            if (now < _nextContextCheck)
                return;

            _nextContextCheck = now + ContextCheckInterval;

            // Selection changes through Unity's API may not deliver selectionChanged.
            if (HasSelectionChanged || _getContext() is { IsValid: false, })
                _invalidate();
        }

        internal void AcknowledgeSelection()
        {
            _observedSelection = Selection.activeObject;
            _observedSelectionCount = Selection.count;
        }

        public void Dispose() =>
            _subscriptions.Dispose();
    }
}