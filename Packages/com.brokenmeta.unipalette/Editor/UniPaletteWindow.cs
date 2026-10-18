using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
        private readonly struct CreationEntry
        {
            public string Name { get; }
            public string Category { get; }
            
            public CreationEntry(string name, string category)
            {
                Name = name;
                Category = category;
            }
        }

        private readonly List<CreationEntry> _allEntries = new()
        {
            new CreationEntry("Cube", "3D Object"),
            new CreationEntry("Sphere", "3D Object"),
            new CreationEntry("Camera", "GameObject")
        };

        private readonly List<CreationEntry> _filteredEntries = new();
        
        [MenuItem("Window/UniPalette")]
        private static void Open()
        {
            var window = GetWindow<UniPaletteWindow>();
            window.titleContent = new GUIContent("UniPalette");
            window.Show();
        }
        
        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.brokenmeta.unipalette/Editor/UI/UniPaletteWindow.uxml");

            visualTree.CloneTree(rootVisualElement);
            
            var searchIcon = EditorGUIUtility.IconContent("Search Icon").image;

            rootVisualElement.Q<Image>("what-icon").image = searchIcon;
            rootVisualElement.Q<Image>("where-icon").image = searchIcon;

            var listView = CreateListView(
                "results-list",
                _filteredEntries,
                static (e, i) => e[i].Name);

            var whatInput = rootVisualElement.Q<TextField>("what-input");
            whatInput.RegisterCallback<ChangeEvent<string>, ListView>(OnQueryChanged, listView);
            whatInput.RegisterCallback<KeyDownEvent, ListView>(OnNavigationChanged, listView, TrickleDown.TrickleDown);
            
            FilterEntries(listView, whatInput.value);
        }

        private void OnNavigationChanged(KeyDownEvent evt, ListView listView)
        {
            if (evt.keyCode != KeyCode.UpArrow &&
                evt.keyCode != KeyCode.DownArrow)
                return;

            var count = listView.itemsSource?.Count ?? 0;
            if (count == 0)
                return;

            var currentIndex = listView.selectedIndex;

            var newIndex = evt.keyCode switch
            {
                KeyCode.UpArrow => Mathf.Max(currentIndex - 1, 0),
                KeyCode.DownArrow => Mathf.Min(currentIndex + 1, count - 1),
                _ => currentIndex
            };

            listView.selectedIndex = newIndex;
            listView.ScrollToItem(newIndex);
            
            evt.StopPropagation();
        }

        private void OnQueryChanged(ChangeEvent<string> evt, ListView listView)
        {
            FilterEntries(listView, evt.newValue);
        }

        private void FilterEntries(ListView listView, string query)
        {
            _filteredEntries.Clear();
            foreach (var entry in _allEntries)
            {
                if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    _filteredEntries.Add(entry);
            }
            
            listView.RefreshItems();
        }

        private ListView CreateListView<TEntry>(
            string nameList,
            List<TEntry> entries,
            Func<List<TEntry>, int, string> getLabelText)
        {
            var listView = rootVisualElement.Q<ListView>(nameList);
            
            listView.itemsSource = entries;
            listView.makeItem = static () => new Label();
            
            listView.bindItem = (element, index) =>
            {
                var label = (Label)element;
                label.text = getLabelText(entries, index);
            };
            
            return listView;
        }
    }
}
