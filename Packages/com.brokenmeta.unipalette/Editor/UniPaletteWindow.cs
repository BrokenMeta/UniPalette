using UnityEditor;
using UnityEngine;

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
        }
    }
}
