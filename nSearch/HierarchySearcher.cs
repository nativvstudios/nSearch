using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HierarchySearcher
{
    public static List<string> SearchHierarchy(string query)
    {
        List<string> results = new List<string>();
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

        foreach (GameObject go in allObjects)
        {
            if (go.name.ToLower().Contains(query.ToLower()))
            {
                results.Add("Hierarchy: " + go.name);
            }
        }

        return results.Take(50).ToList();
    }

    public static GameObject FindObjectInScene(string name)
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        return allObjects.FirstOrDefault(go => go.name == name);
    }

    public static Texture2D GetHierarchyIcon(GameObject go)
    {
        string iconName = "GameObject Icon";

        if (go.GetComponent<Camera>())
        {
            iconName = "Camera Icon";
        }
        else if (go.GetComponent<Light>())
        {
            iconName = "Light Icon";
        }
        else if (go.GetComponent<MeshRenderer>() || go.GetComponent<SkinnedMeshRenderer>())
        {
            iconName = "MeshRenderer Icon";
        }
        else if (go.GetComponent<ParticleSystem>())
        {
            iconName = "ParticleSystem Icon";
        }

        return EditorGUIUtility.IconContent(iconName).image as Texture2D;
    }
}