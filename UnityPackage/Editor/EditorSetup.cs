using reromanlee.ReactiveLocalizer.Unity;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>Plugs the editor into the runtime whenever the editor loads scripts.</summary>
    [InitializeOnLoad]
    internal static class EditorSetup
    {
        static EditorSetup()
        {
            EditorBridge.TableSource = new EditorTableSource();
        }
    }
}
