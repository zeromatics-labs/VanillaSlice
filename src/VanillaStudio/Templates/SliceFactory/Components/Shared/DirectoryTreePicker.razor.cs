using Microsoft.AspNetCore.Components;

namespace {{RootNamespace}}.SliceFactory.Components.Shared;

public partial class DirectoryTreePicker
{
    [Parameter] public IReadOnlyList<string> KnownPaths { get; set; } = Array.Empty<string>();
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> OnPathSelected { get; set; }

    private List<DirectoryNode> _roots = new();
    private string? _selectedPath;
    private string _newFolderName = "";
    private bool _showNewFolderInput = false;

    protected override void OnParametersSet()
    {
        _roots = BuildTree(KnownPaths);
        _selectedPath = Value;
    }

    private List<DirectoryNode> BuildTree(IReadOnlyList<string> paths)
    {
        var roots = new List<DirectoryNode>();

        foreach (var path in paths.Distinct())
        {
            var segments = path.Split('/', '\\')
                               .Where(s => !string.IsNullOrEmpty(s))
                               .ToArray();
            var current = roots;
            var currentPath = "";

            foreach (var segment in segments)
            {
                currentPath = currentPath.Length == 0 ? segment : $"{currentPath}/{segment}";
                var node = current.FirstOrDefault(n => n.Name == segment);
                if (node == null)
                {
                    node = new DirectoryNode(segment, currentPath);
                    current.Add(node);
                }
                current = node.Children;
            }
        }

        return roots;
    }

    private async Task SelectNode(DirectoryNode node)
    {
        _selectedPath = node.FullPath;
        _showNewFolderInput = true;
        _newFolderName = "";
        await OnPathSelected.InvokeAsync(node.FullPath);
    }

    private void ToggleExpand(DirectoryNode node) => node.IsExpanded = !node.IsExpanded;

    private async Task ConfirmNewFolder()
    {
        if (string.IsNullOrWhiteSpace(_newFolderName)) return;

        var newPath = string.IsNullOrEmpty(_selectedPath)
            ? _newFolderName.Trim()
            : $"{_selectedPath}/{_newFolderName.Trim()}";

        // Add to tree
        var parent = FindNode(_roots, _selectedPath);
        var newNode = new DirectoryNode(_newFolderName.Trim(), newPath);
        if (parent != null)
        {
            parent.IsExpanded = true;
            parent.Children.Add(newNode);
        }
        else
        {
            _roots.Add(newNode);
        }

        _selectedPath = newPath;
        _showNewFolderInput = false;
        _newFolderName = "";
        await OnPathSelected.InvokeAsync(newPath);
    }

    private void CancelNewFolder()
    {
        _showNewFolderInput = false;
        _newFolderName = "";
    }

    private static DirectoryNode? FindNode(List<DirectoryNode> nodes, string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        foreach (var node in nodes)
        {
            if (node.FullPath == fullPath) return node;
            var found = FindNode(node.Children, fullPath);
            if (found != null) return found;
        }
        return null;
    }

    private class DirectoryNode
    {
        public string Name { get; }
        public string FullPath { get; }
        public List<DirectoryNode> Children { get; } = new();
        public bool IsExpanded { get; set; }

        public DirectoryNode(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}
