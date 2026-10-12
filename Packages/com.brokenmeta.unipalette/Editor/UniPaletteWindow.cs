using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
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
        }
    }
}
