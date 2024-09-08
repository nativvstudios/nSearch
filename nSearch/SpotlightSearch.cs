using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Runtime.InteropServices;

public class SpotlightSearch : EditorWindow
{
    private string searchQuery = "";
    private Vector2 scrollPosition;
    private List<string> searchResults = new List<string>();
    private TrieNode root = new TrieNode();
    private static SpotlightSearch currentWindow;
    private bool indexingComplete = false;
    private IEnumerator<string> indexIterator = null;
    private bool displaySettings = false;

    // Settings options
    private bool option1 = false;
    private string option2 = "";
    private int option3 = 0;

    [MenuItem("Tools/Spotlight Search %#SPACE")]
    public static void ToggleWindow()
    {
        if (currentWindow != null)
        {
            currentWindow.Close();
            currentWindow = null;
        }
        else
        {
            currentWindow = CreateInstance<SpotlightSearch>();
            currentWindow.titleContent = new GUIContent("");
            currentWindow.ShowUtility();
            currentWindow.minSize = new Vector2(400, 50);
            currentWindow.maxSize = new Vector2(400, 50);

            WindowUtilities.CenterWindow(currentWindow);
            WindowUtilities.ApplyRoundedCorners(currentWindow);
        }
    }

    void OnEnable() => WindowUtilities.ApplyRoundedCorners(this);

    void OnLostFocus() => Close();

    void OnDestroy()
    {
        if (currentWindow == this)
        {
            currentWindow = null;
        }
    }

    void OnGUI()
    {
        WindowUtilities.ApplyRoundedCorners(this);
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0, 0, 0, 0.6f));
        EditorGUILayout.BeginVertical();

        GUIStyle customSearchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField)
        {
            padding = new RectOffset(20, 2, 2, 2),
            fontSize = 14,
            fixedHeight = 30,
            normal = { textColor = Color.white },
            border = new RectOffset(8, 8, 8, 8),
            overflow = new RectOffset(0, 0, 0, 0),
            imagePosition = ImagePosition.ImageLeft
        };

        GUI.SetNextControlName("SearchField");
        string newSearchQuery = EditorGUILayout.TextField(searchQuery, customSearchFieldStyle);
        EditorGUI.FocusTextInControl("SearchField");

        if (newSearchQuery != searchQuery)
        {
            searchQuery = newSearchQuery;
            displaySettings = searchQuery.StartsWith("s:");
            PerformSearch();
            AdjustWindowSize();

            if (indexIterator == null && !indexingComplete)
            {
                BuildFileIndex();
            }
        }

        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (displaySettings)
        {
            DisplaySettings();
        }
        else if (searchResults.Count > 0)
        {
            foreach (string result in searchResults)
            {
                EditorGUILayout.BeginHorizontal();

                if (result.StartsWith("Hierarchy: "))
                {
                    string gameObjectName = result.Substring("Hierarchy: ".Length).Trim();
                    GameObject go = HierarchySearcher.FindObjectInScene(gameObjectName);
                    if (go != null)
                    {
                        Texture2D icon = HierarchySearcher.GetHierarchyIcon(go);
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label(go.name, EditorStyles.label);
                        GUILayout.Label(go.GetType().Name, EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();
                    }
                }
                else if (!result.StartsWith("Result:"))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result);
                    Texture2D preview = AssetPreview.GetAssetPreview(asset) ?? FileUtilities.GetFileTypeIcon(result);
                    GUILayout.Label(preview, GUILayout.Width(50), GUILayout.Height(50));
                    EditorGUILayout.BeginVertical();
                    GUILayout.Label(Path.GetFileName(result), EditorStyles.label);
                    GUILayout.Label(asset.GetType().Name, EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    GUILayout.Label(result, EditorStyles.boldLabel);
                }

                EditorGUILayout.EndHorizontal();
                Rect lastRect = GUILayoutUtility.GetLastRect();
                if (lastRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(new Rect(lastRect.x, lastRect.y, lastRect.width, lastRect.height), new Color(0, 0.5f, 1f, 0.2f));

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        if (result.StartsWith("Hierarchy: "))
                        {
                            string gameObjectName = result.Substring("Hierarchy: ".Length).Trim();
                            GameObject go = HierarchySearcher.FindObjectInScene(gameObjectName);
                            if (go != null)
                            {
                                Selection.activeObject = go;
                                EditorGUIUtility.PingObject(go);
                            }
                        }
                        else if (!result.StartsWith("Result:"))
                        {
                            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result);
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }

                        OnLostFocus();
                    }
                }
                GUILayout.Space(5);
            }
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            EditorGUILayout.LabelField("No results found.", EditorStyles.boldLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        AdjustWindowSize();
    }

    void AdjustWindowSize()
    {
        int resultCount = Mathf.Min(searchResults.Count, 10);
        float newHeight = 50 + resultCount * 60;
        if (displaySettings)
        {
            newHeight = 300;
        }
        this.minSize = new Vector2(400, newHeight);
        this.maxSize = new Vector2(400, newHeight);
    }

    void BuildFileIndex()
    {
        root = new TrieNode();
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        EditorApplication.update += IndexNextBatch;
        indexIterator = ((IEnumerable<string>)allAssets).GetEnumerator();
    }

    void IndexNextBatch()
    {
        int indexBatchSize = 100;
        int processed = 0;

        while (indexIterator.MoveNext() && processed < indexBatchSize)
        {
            string path = indexIterator.Current;
            string fileName = Path.GetFileName(path).ToLower();
            root.Insert(fileName, path);
            processed++;
        }

        if (!indexIterator.MoveNext())
        {
            EditorApplication.update -= IndexNextBatch;
            indexingComplete = true;
        }

        if (!string.IsNullOrEmpty(searchQuery))
        {
            PerformSearch();
        }
    }

    void PerformSearch()
    {
        searchResults.Clear();
        if (string.IsNullOrEmpty(searchQuery))
            return;

        if (displaySettings)
        {
            return;
        }

        if (searchQuery.StartsWith("h:"))
        {
            searchResults = HierarchySearcher.SearchHierarchy(searchQuery.Substring(2).Trim());
        }
        else
        {
            string result = ArithmeticEvaluator.EvaluateArithmeticExpression(searchQuery);
            if (result != null)
            {
                searchResults.Add("Result: " + result);
            }
            else
            {
                searchResults = root.Search(searchQuery.ToLower(), 50);
            }
        }

        AdjustWindowSize();
    }

    private void DisplaySettings()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        if (GUILayout.Button("Clear Cache"))
        {
            Debug.Log("Cache Cleared");
        }

        GUILayout.Label("Option 1:");
        option1 = EditorGUILayout.Toggle("Enable Option 1", option1);

        GUILayout.Label("Option 2:");
        option2 = EditorGUILayout.TextField("Option 2", option2);

        GUILayout.Label("Option 3:");
        option3 = EditorGUILayout.IntSlider("Option 3", option3, 0, 100);
    }
}