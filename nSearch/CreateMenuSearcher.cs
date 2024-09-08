using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

public static class CreateMenuSearcher
{
    public static List<string> SearchCreateMenu(string query)
    {
        List<string> results = new List<string>();

        var menuItems = GetCreateMenuItems();
        foreach (var item in menuItems)
        {
            if (item.ToLower().Contains(query.ToLower()))
            {
                results.Add("CreateMenu: " + item);
            }
        }

        return results.Take(50).ToList();
    }

    public static void ExecuteCreateMenuItem(string menuItem)
    {
        var menuItems = GetCreateMenuItems();
        foreach (var item in menuItems)
        {
            if (string.Equals(item, menuItem, StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.ExecuteMenuItem(item);
                break;
            }
        }
    }

    private static List<string> GetCreateMenuItems()
    {
        var menuItems = new List<string>();
        var menuItemType = typeof(Editor).Assembly.GetType("UnityEditor.Menu");

        if (menuItemType != null)
        {
            var menuItemsProperty = menuItemType.GetProperty("menuItems", BindingFlags.NonPublic | BindingFlags.Static);
            var menuCommands = menuItemsProperty?.GetValue(null) as IEnumerable<string>;

            if (menuCommands != null)
            {
                foreach (var command in menuCommands)
                {
                    if (command.StartsWith("GameObject/Create Other/"))
                    {
                        menuItems.Add(command);
                    }
                }
            }
        }

        return menuItems;
    }
}