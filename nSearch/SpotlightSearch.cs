using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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
    private int selectedIndex = -1;
    private int hoveredIndex = -1;
    private float itemHeight = 55f;
    private float spacing = 5f;
    private Rect lastScrollViewRect;
    
    // Fixed window dimensions
    private readonly float initialWindowWidth = 500f;
    private readonly float minWindowHeight = 100f;
    private readonly float maxWindowHeight = 500f;
    private readonly float searchFieldHeight = 40f;
    private readonly float searchFieldSpacing = 10f;
    
    // Cache for smooth transitions
    private float targetWindowHeight;
    private float currentWindowHeight;
    private float smoothTime = 0.2f;
    private float heightVelocity = 0f;

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
            currentWindow.titleContent = new GUIContent("nSearch");
            
            // Set fixed initial dimensions
            currentWindow.minSize = new Vector2(currentWindow.initialWindowWidth, currentWindow.minWindowHeight);
            currentWindow.maxSize = new Vector2(currentWindow.initialWindowWidth, currentWindow.maxWindowHeight);
            currentWindow.targetWindowHeight = currentWindow.minWindowHeight;
            currentWindow.currentWindowHeight = currentWindow.minWindowHeight;
            
            currentWindow.ShowUtility();
            WindowUtilities.CenterWindow(currentWindow);
        }
    }

    void OnDestroy()
    {
        if (currentWindow == this)
        {
            currentWindow = null;
        }
    }
    
    void OnEnable()
    {
        // Start indexing when window is opened
        if (!indexingComplete && indexIterator == null)
        {
            BuildFileIndex();
        }
        
        // Force initial repaint to ensure drawing occurs immediately
        Repaint();
    }
    
    void OnFocus() 
    {
        // Repaint on focus to ensure hover state is updated
        Repaint();
    }
    
    void Update()
    {
        // Smooth window resizing
        if (Math.Abs(currentWindowHeight - targetWindowHeight) > 0.1f)
        {
            currentWindowHeight = Mathf.SmoothDamp(currentWindowHeight, targetWindowHeight, ref heightVelocity, smoothTime);
            minSize = new Vector2(initialWindowWidth, currentWindowHeight);
            maxSize = new Vector2(initialWindowWidth, currentWindowHeight);
            Repaint();
        }
    }

    void SelectResult(string result)
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
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
        Close();
    }

    void OnGUI()
    {
        HandleKeyboardInput();

        // Draw background
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.15f, 0.15f, 0.15f, 0.95f));
        
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        
        // Search field with custom style
        GUIStyle customSearchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField)
        {
            padding = new RectOffset(20, 2, 2, 2),
            fontSize = 14,
            fixedHeight = searchFieldHeight - 10,
            normal = { textColor = Color.white },
            border = new RectOffset(8, 8, 8, 8),
            overflow = new RectOffset(0, 0, 0, 0),
            imagePosition = ImagePosition.ImageLeft
        };

        EditorGUILayout.Space(5); // Top padding
        
        // Handle tab key to switch focus between search field and results
        bool isFocusingSearchField = GUI.GetNameOfFocusedControl() == "SearchField";
        
        GUI.SetNextControlName("SearchField");
        string newSearchQuery = EditorGUILayout.TextField(searchQuery, customSearchFieldStyle, GUILayout.ExpandWidth(true));
        
        // Focus search field on first draw
        if (GUI.GetNameOfFocusedControl() == "")
        {
            EditorGUI.FocusTextInControl("SearchField");
        }

        if (newSearchQuery != searchQuery)
        {
            searchQuery = newSearchQuery;
            // Check for settings command
            displaySettings = searchQuery.StartsWith("s:");
            selectedIndex = -1;
            hoveredIndex = -1;
            PerformSearch();
            CalculateTargetWindowHeight();

            if (indexIterator == null && !indexingComplete)
            {
                BuildFileIndex();
            }
        }
        
        // Always check for settings command to handle cases where it's already set
        if (searchQuery.StartsWith("s:"))
        {
            displaySettings = true;
        }

        EditorGUILayout.Space(searchFieldSpacing);

        // Calculate the remaining space for scroll view
        float viewHeight = position.height - searchFieldHeight - searchFieldSpacing - 10; // 10 for bottom padding
        viewHeight = Mathf.Max(viewHeight, 0); // Ensure we don't go negative
        
        // Draw the scroll view with fixed rect to avoid layout changes
        lastScrollViewRect = EditorGUILayout.GetControlRect(false, viewHeight);
        
        // Create content height based on number of items
        float contentHeight = GetContentHeight();
        
        // Create content rect with appropriate width (accounting for scrollbar)
        Rect contentRect = new Rect(0, 0, lastScrollViewRect.width - 16, contentHeight);
        
        // Use GUILayout.BeginScrollView for more stability with mouse input
        scrollPosition = GUI.BeginScrollView(lastScrollViewRect, scrollPosition, contentRect, false, true);
        
        float currentY = 0;
        if (displaySettings)
        {
            DisplaySettings();
        }
        else if (searchResults.Count > 0)
        {
            // If we have results and selectedIndex is -1, select first item for tab navigation
            if (selectedIndex == -1 && searchResults.Count > 0)
            {
                selectedIndex = 0;
            }
            
            DrawSearchResults(contentRect, currentY);
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            GUI.Label(new Rect(5, 0, contentRect.width - 10, 20), "No results found.", EditorStyles.boldLabel);
        }

        GUI.EndScrollView();
        EditorGUILayout.EndVertical();

        // Add a hint about tab navigation at the bottom
        if (searchResults.Count > 0)
        {
            EditorGUI.LabelField(
                new Rect(10, position.height - 18, position.width - 20, 16),
                "Tab/Shift+Tab: Navigate, Enter: Select, Esc: Close", 
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } }
            );
        }

        // Auto-scroll to keep selected item visible
        EnsureSelectedItemVisible(viewHeight);
    }
    
    private void DrawSearchResults(Rect contentRect, float currentY)
    {
        // This version uses absolute GUI calls instead of relative positioning
        // This should fix the scrolling issues with hover detection
        
        // Get the current mouse position (in window space)
        Vector2 mousePos = Event.current.mousePosition;
        
        // Reset hover detection
        int newHoveredIndex = -1;
        
        // Track if we need to repaint (for smooth hover effects)
        bool needsRepaint = false;
        
        // Draw all visible results
        for (int i = 0; i < searchResults.Count; i++)
        {
            string result = searchResults[i];
            
            // Calculate the item rectangle in scroll content space
            Rect itemRect = new Rect(0, currentY, contentRect.width, itemHeight);
            
            // Check if this item is visible in the scroll view
            // (Unity automatically culls drawing for items outside the scroll view)
            bool isVisible = (currentY + itemHeight >= scrollPosition.y) && 
                             (currentY <= scrollPosition.y + lastScrollViewRect.height);
            
            if (isVisible)
            {
                // For hit detection, we need to calculate where this item appears on screen
                // First, get position relative to scroll view:
                float relativeY = currentY - scrollPosition.y;
                
                // Then convert to window coordinates by adding scroll view position:
                Rect screenRect = new Rect(
                    lastScrollViewRect.x,
                    lastScrollViewRect.y + relativeY,
                    contentRect.width,
                    itemHeight
                );
                
                // Check if mouse is over this item (in window coordinates)
                if (screenRect.Contains(mousePos))
                {
                    newHoveredIndex = i;
                    EditorGUIUtility.AddCursorRect(screenRect, MouseCursor.Link);
                    
                    // Handle clicks
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        selectedIndex = i;
                        SelectResult(result);
                        Event.current.Use();
                    }
                }
                
                // Draw selection highlight (both for keyboard selection and mouse hover)
                if (i == selectedIndex || i == newHoveredIndex)
                {
                    Color highlightColor = (i == selectedIndex) ? 
                        new Color(0.2f, 0.4f, 0.9f, 0.4f) : // Selection - brighter blue
                        new Color(0.3f, 0.5f, 0.9f, 0.3f);  // Hover - lighter blue
                    
                    EditorGUI.DrawRect(itemRect, highlightColor);
                }
                
                // Draw item content
                DrawResultItem(result, itemRect);
            }
            
            // Move to next item position
            currentY += itemHeight + spacing;
        }
        
        // Update hover state if it changed
        if (hoveredIndex != newHoveredIndex)
        {
            hoveredIndex = newHoveredIndex;
            needsRepaint = true;
        }
        
        // Force repaint if needed (for smooth hover transitions)
        if (needsRepaint || Event.current.type == EventType.MouseMove)
        {
            Repaint();
        }
    }
    
    private void DrawResultItem(string result, Rect itemRect)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12 };
        GUIStyle miniLabelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };
        
        if (result.StartsWith("Hierarchy: "))
        {
            string gameObjectName = result.Substring("Hierarchy: ".Length).Trim();
            GameObject go = HierarchySearcher.FindObjectInScene(gameObjectName);
            if (go != null)
            {
                Texture2D icon = HierarchySearcher.GetHierarchyIcon(go);
                GUI.Label(new Rect(itemRect.x + 10, itemRect.y + 17.5f, 20, 20), icon);
                GUI.Label(new Rect(itemRect.x + 40, itemRect.y + 10, itemRect.width - 50, 20), go.name, labelStyle);
                GUI.Label(new Rect(itemRect.x + 40, itemRect.y + 30, itemRect.width - 50, 15), go.GetType().Name, miniLabelStyle);
            }
        }
        else if (!result.StartsWith("Result:"))
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result);
            if (asset != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(asset) ?? FileUtilities.GetFileTypeIcon(result);
                GUI.Label(new Rect(itemRect.x + 10, itemRect.y + 2.5f, 50, 50), preview);
                GUI.Label(new Rect(itemRect.x + 70, itemRect.y + 10, itemRect.width - 80, 20), Path.GetFileName(result), labelStyle);
                GUI.Label(new Rect(itemRect.x + 70, itemRect.y + 30, itemRect.width - 80, 15), asset.GetType().Name, miniLabelStyle);
            }
        }
        else
        {
            GUI.Label(new Rect(itemRect.x + 10, itemRect.y + 17.5f, itemRect.width - 20, 20), result, EditorStyles.boldLabel);
        }
    }

    float GetContentHeight()
    {
        if (displaySettings)
            return 300;
        return Math.Max(1, searchResults.Count) * (itemHeight + spacing);
    }
    
    void EnsureSelectedItemVisible(float viewHeight)
    {
        if (selectedIndex >= 0)
        {
            float selectedItemY = selectedIndex * (itemHeight + spacing);
            float selectedItemHeight = itemHeight;
            
            if (selectedItemY + selectedItemHeight > scrollPosition.y + viewHeight)
            {
                scrollPosition.y = selectedItemY - viewHeight + selectedItemHeight + spacing;
            }
            else if (selectedItemY < scrollPosition.y)
            {
                scrollPosition.y = selectedItemY;
            }
        }
    }

    void HandleKeyboardInput()
    {
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown)
        {
            switch (currentEvent.keyCode)
            {
                case KeyCode.DownArrow:
                    selectedIndex = Mathf.Min(selectedIndex + 1, searchResults.Count - 1);
                    hoveredIndex = -1;
                    currentEvent.Use();
                    Repaint();
                    break;
                case KeyCode.UpArrow:
                    selectedIndex = Mathf.Max(selectedIndex - 1, 0);
                    hoveredIndex = -1;
                    currentEvent.Use();
                    Repaint();
                    break;
                case KeyCode.Tab:
                    // Tab navigation - forward or backward based on shift key
                    if (currentEvent.shift)
                    {
                        // Shift+Tab - move backward
                        if (selectedIndex <= 0)
                            selectedIndex = searchResults.Count - 1; // Wrap to end
                        else
                            selectedIndex--;
                    }
                    else
                    {
                        // Tab - move forward
                        if (selectedIndex >= searchResults.Count - 1)
                            selectedIndex = 0; // Wrap to beginning
                        else
                            selectedIndex++;
                    }
                    
                    hoveredIndex = -1;
                    currentEvent.Use();
                    Repaint();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (selectedIndex >= 0 && selectedIndex < searchResults.Count)
                    {
                        SelectResult(searchResults[selectedIndex]);
                        currentEvent.Use();
                    }
                    else if (hoveredIndex >= 0 && hoveredIndex < searchResults.Count)
                    {
                        // If no selection but something is hovered, use that
                        SelectResult(searchResults[hoveredIndex]);
                        currentEvent.Use();
                    }
                    break;
                case KeyCode.Escape:
                    Close();
                    currentEvent.Use();
                    break;
            }
        }
    }

    void CalculateTargetWindowHeight()
    {
        float contentHeight = searchFieldHeight + searchFieldSpacing;
        
        if (displaySettings)
        {
            // Fixed height for settings panel
            contentHeight += 300;
        }
        else if (searchResults.Count > 0)
        {
            // Limit to 8 visible results to avoid huge window
            int resultCount = Mathf.Min(searchResults.Count, 8);
            contentHeight += resultCount * (itemHeight + spacing);
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            contentHeight += 40; // Height for "No results found"
        }
        
        // Add top and bottom padding
        contentHeight += 20;
        
        // Clamp to min/max values
        targetWindowHeight = Mathf.Clamp(contentHeight, minWindowHeight, maxWindowHeight);
        
        // Debug.Log($"Target window height: {targetWindowHeight}, displaySettings: {displaySettings}, results: {searchResults.Count}");
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
            indexIterator = null;
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

        // Skip regular search if we're showing settings
        // But don't return yet, as we still need to calculate the window height
        if (displaySettings)
        {
            searchResults.Clear();
            // Force height calculation
            CalculateTargetWindowHeight();
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

        // Force recalculation of window height
        CalculateTargetWindowHeight();
    }

    private void DisplaySettings()
    {
        // Create a fixed rect for the settings content to avoid IMGUI layout issues
        Rect settingsRect = new Rect(10, 0, lastScrollViewRect.width - 30, 300);
        
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { 
            fontSize = 14,
            normal = { textColor = Color.white }
        };
        
        // Draw header
        GUI.Label(new Rect(settingsRect.x, settingsRect.y, settingsRect.width, 20), "Settings", headerStyle);
        
        // Use a consistent style for all controls
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { 
            fixedHeight = 30,
            margin = new RectOffset(0, 0, 0, 10),
            alignment = TextAnchor.MiddleCenter
        };
        
        GUIStyle labelStyle = new GUIStyle(EditorStyles.label) {
            normal = { textColor = Color.white },
            fontStyle = FontStyle.Bold
        };
        
        // Clear cache button
        if (GUI.Button(new Rect(settingsRect.x, settingsRect.y + 30, settingsRect.width - 20, 30), "Clear Cache", buttonStyle))
        {
            Debug.Log("Cache Cleared");
            indexingComplete = false;
            root = new TrieNode();
            BuildFileIndex();
        }

        // Option 1
        GUI.Label(new Rect(settingsRect.x, settingsRect.y + 80, settingsRect.width, 20), "Option 1:", labelStyle);
        option1 = EditorGUI.Toggle(new Rect(settingsRect.x, settingsRect.y + 100, settingsRect.width - 20, 20), "Enable Option 1", option1);

        // Option 2
        GUI.Label(new Rect(settingsRect.x, settingsRect.y + 140, settingsRect.width, 20), "Option 2:", labelStyle);
        option2 = EditorGUI.TextField(new Rect(settingsRect.x, settingsRect.y + 160, settingsRect.width - 20, 20), option2);

        // Option 3
        GUI.Label(new Rect(settingsRect.x, settingsRect.y + 200, settingsRect.width, 20), "Option 3:", labelStyle);
        option3 = EditorGUI.IntSlider(new Rect(settingsRect.x, settingsRect.y + 220, settingsRect.width - 20, 20), option3, 0, 100);
        
        // Status display
        string statusText = indexingComplete ? "Indexing complete" : "Indexing in progress...";
        GUI.Label(new Rect(settingsRect.x, settingsRect.y + 260, settingsRect.width, 20), statusText, 
            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = indexingComplete ? Color.green : Color.yellow } });
    }
}