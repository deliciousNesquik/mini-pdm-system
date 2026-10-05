using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniPdm.Core.Domain;

namespace MiniPdm.App.ViewModels;

/// <summary>
/// ViewModel узла дерева объектов.
/// </summary>
public partial class TreeNodeViewModel : ViewModelBase
{
    private readonly Func<long, Task<IReadOnlyList<TreeNodeViewModel>>> _loadChildren;
    private readonly Action<Exception> _onError;

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
    }

    public long ObjectId { get; }
    public ObjectType Type { get; }
    public string? Designation { get; }
    public string Name { get; }
    public ObjectState? State { get; }
    public int Quantity { get; }
    public bool HasActiveVersion { get; }
    public bool HasChildren { get; }

    /// <summary>
    /// Отображаемое имя узла: Обозначение Наименование или просто Наименование, если обозначения нет
    /// </summary>
    public string Display => Designation is null ? Name : $"{Designation}   {Name}";

    /// <summary>
    /// Отображаемое количество: N или пустая строка, если количество 1
    /// </summary>
    public string QuantityText => Quantity > 1 ? $" ×{Quantity}" : "";

    /// <summary>
    /// Признак того, что у объекта нет активной версии
    /// </summary>
    public bool IsInactive => HasActiveVersion is false;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || !HasChildren || Children.Count > 0) return;
        _ = ExpandAsync();
    }

    private async Task ExpandAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var children = await _loadChildren(ObjectId);
            Children.Clear();
            foreach (var child in children) Children.Add(child);
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