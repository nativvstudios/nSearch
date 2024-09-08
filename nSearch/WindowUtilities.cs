using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

public static class WindowUtilities
{
#if UNITY_STANDALONE_WIN
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetActiveWindow();

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
#endif

    public static void ApplyRoundedCorners(EditorWindow window)
    {
#if UNITY_STANDALONE_WIN
        var hwnd = GetActiveWindow();
        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_CAPTION));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        RECT rect = new RECT();
        GetWindowRect(hwnd, ref rect);
        IntPtr region = CreateRoundRectRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1, 20, 20);
        SetWindowRgn(hwnd, region, true);
#elif UNITY_EDITOR_OSX
        var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        var windowInstance = parent.GetType().GetField("window", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(parent);
        var setStyleMask = windowInstance.GetType().GetMethod("setStyleMask", BindingFlags.NonPublic | BindingFlags.Instance);
        var styleMask = windowInstance.GetType().GetProperty("styleMask", BindingFlags.Public | BindingFlags.Instance);
        var currentMask = (int)styleMask.GetValue(windowInstance);
        setStyleMask.Invoke(windowInstance, new object[] { currentMask & ~0x00000001 });
#endif
    }

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