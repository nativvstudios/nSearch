using System.Collections.Generic;
using System.Linq;

public class TrieNode
{
    private Dictionary<char, TrieNode> children = new Dictionary<char, TrieNode>();
    private List<string> files = new List<string>();

    public void Insert(string key, string filePath)
    {
        TrieNode current = this;
        foreach (char c in key)
        {
            if (!current.children.ContainsKey(c))
            {
                current.children[c] = new TrieNode();
            }
            current = current.children[c];
        }
        current.files.Add(filePath);
    }

    public List<string> Search(string query, int limit)
    {
        List<string> results = new List<string>();
        SearchRecursive(this, query, "", results, ref limit);
        return results;
    }

    private void SearchRecursive(TrieNode node, string query, string current, List<string> results, ref int limit)
    {
        if (limit <= 0) return;

        if (query.Length == 0)
        {
            CollectFiles(node, results, ref limit);
            return;
        }

        foreach (var child in node.children)
        {
            if (query[0] == child.Key)
            {
                SearchRecursive(child.Value, query.Substring(1), current + child.Key, results, ref limit);
            }
            else if (FuzzyMatch(query, child.Key))
            {
                SearchRecursive(child.Value, query, current + child.Key, results, ref limit);
            }
        }
    }

    private void CollectFiles(TrieNode node, List<string> results, ref int limit)
    {
        foreach (string file in node.files)
        {
            if (limit <= 0) return;
            results.Add(file);
            limit--;
        }

        foreach (var child in node.children)
        {
            CollectFiles(child.Value, results, ref limit);
        }
    }

    private bool FuzzyMatch(string query, char c) => query.Contains(c);
}