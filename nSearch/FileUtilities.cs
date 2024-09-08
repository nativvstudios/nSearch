using System.IO;
using UnityEditor;
using UnityEngine;

public static class FileUtilities
{
    public static Texture2D GetFileTypeIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        string iconName = "DefaultAsset Icon";

        switch (extension)
        {
            case ".cs":
                iconName = "cs Script Icon";
                break;
            case ".png":
            case ".jpg":
                iconName = "Texture Icon";
                break;
            case ".mat":
                iconName = "Material Icon";
                break;
            case ".prefab":
                iconName = "Prefab Icon";
                break;
        }

        return EditorGUIUtility.IconContent(iconName).image as Texture2D;
    }
}