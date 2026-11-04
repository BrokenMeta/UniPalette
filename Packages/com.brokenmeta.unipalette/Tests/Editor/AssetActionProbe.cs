using UnityEditor;
using UnityEngine;

namespace BrokenMeta.UniPalette.Tests
{
    // Disposable test asset: its action records delivery without modifying files or user data.
    internal sealed class AssetActionProbe : ScriptableObject
    {
        internal int Calls { get; private set; }

        [MenuItem("CONTEXT/AssetActionProbe/Record Invocation")]
        private static void RecordInvocation(MenuCommand command)
        {
            if (command.context is AssetActionProbe probe)
                probe.Calls++;
        }
    }
}
