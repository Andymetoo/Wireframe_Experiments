using IceResurfacer.Presentation;
using UnityEditor;

namespace IceResurfacer.Editor
{
    [InitializeOnLoad]
    static class RouteManagerEditorBridge
    {
        static RouteManagerEditorBridge()
        {
            IceRouteGuide.EditRouteRequested=path=>EditorApplication.delayCall+=()=>
            {TrainingRouteEditorWindow.Open();EditorWindow.GetWindow<TrainingRouteEditorWindow>().LoadGuide(path);};
        }
    }
}
