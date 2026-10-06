using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniPdm.Core.Domain;

namespace MiniPdm.App.ViewModels;

/// <summary>
/// ViewModel для узла дерева.
/// </summary>
public partial class TreeNodeViewModel : ViewModelBase
{
    private readonly Func<long, Task<IReadOnlyList<TreeNodeViewModel>>> _loadChildren;
    private readonly Action<Exception> _onError;
    private bool _childrenLoaded;

    private TreeNodeViewModel()
    {
        ObjectId = 0;
        Type = ObjectType.Part;
        Designation = null;
        Name = "…";
        State = null;
        Quantity = 0;
        HasActiveVersion = true;
        HasChildren = false;
        _loadChildren = _ => Task.FromResult<IReadOnlyList<TreeNodeViewModel>>([]);
        _onError = _ => { };
    }

    private static readonly TreeNodeViewModel PlaceholderChild = new();

    public TreeNodeViewModel(
        long objectId,
        ObjectType type,
        string? designation,
        string name,
        ObjectState? state,
        int quantity,
        bool hasActiveVersion,
        bool hasChildren,
        Func<long, Task<IReadOnlyList<TreeNodeViewModel>>> loadChildren,
        Action<Exception> onError)
    {
        ObjectId = objectId;
        Type = type;
        Designation = designation;
        Name = name;
        State = state;
        Quantity = quantity;
        HasActiveVersion = hasActiveVersion;
        HasChildren = hasChildren;
        _loadChildren = loadChildren;
        _onError = onError;

        if (hasChildren)
        {
            Children.Add(PlaceholderChild);
        }
    }

    public long ObjectId { get; }
    public ObjectType Type { get; }
    public string? Designation { get; }
    public string Name { get; }
    public ObjectState? State { get; }
    public int Quantity { get; }
    public bool HasActiveVersion { get; }
    public bool HasChildren { get; }

    public string Display => Designation is null ? Name : $"{Designation}   {Name}";
    public string QuantityText => $" ×{Quantity}";
    public bool IsInactive => !HasActiveVersion;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || !HasChildren || _childrenLoaded) return;
        _ = ExpandAsync();
    }

    /// <summary>
    /// Раскрывает узел, загружая его детей.
    /// </summary>
    private async Task ExpandAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var children = await _loadChildren(ObjectId);
            Children.Clear();
            if (children.Count == 0)
            {
                _childrenLoaded = true;
                return;
            }

            foreach (var child in children)
            {
                Children.Add(child);
            }
            _childrenLoaded = true;
        }
        catch (Exception e)
        {
            _onError(e);
        }
        finally
        {
            IsBusy = false;
        }
    }
}