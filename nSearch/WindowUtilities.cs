using UnityEditor;
using UnityEngine;

public static class WindowUtilities
{
    public static void CenterWindow(EditorWindow window)
    {
        var main = EditorGUIUtility.GetMainWindowPosition();
        Rect mainRect = main;
        var pos = new Vector2(
            (mainRect.width - window.position.width) * 0.5f,
            (mainRect.height - window.position.height) * 0.5f
        );
        window.position = new Rect(pos.x + mainRect.x, pos.y + mainRect.y, window.position.width, window.position.height);
    }
}