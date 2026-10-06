using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPdm.App.Localization;
using MiniPdm.App.Models;
using MiniPdm.Core.Domain;
using MiniPdm.Data.Models;
using MiniPdm.Data.Services;

namespace MiniPdm.App.ViewModels;

/// <summary>
/// ViewModel карточки объекта (деталь/сборка/стандартная деталь) с отображением состава 1-го уровня.
/// </summary>
public partial class ObjectCardViewModel : ViewModelBase
{
    private readonly UiReadService _reads;
    private readonly PdmStateService _state;
    private readonly Action<Exception> _onError;
    private decimal? _massTotal;

    public ObjectCardViewModel(UiReadService reads, PdmStateService state, Action<Exception> onError)
    {
        _reads = reads;
        _state = state;
        _onError = onError;
        StateOptions = [Strings.State_InWork, Strings.State_Approved, Strings.State_Annulled];
    }

    /// <summary>
    /// Список строк для комбобокса выбора состояния: "В работе", "Утверждена", "Аннулирована".
    /// </summary>
    public IReadOnlyList<string> StateOptions { get; }

    /// <summary>
    /// Состав 1-го уровня: строки с обозначением, наименованием, количеством и массой 1 шт. (для сборок).
    /// </summary>
    public ObservableCollection<Models.CompositionRow> Composition { get; } = [];

    [ObservableProperty]
    private ObjectCard? _card;

    [ObservableProperty]
    private bool _isBusy;
    
    [ObservableProperty]
    private string? _selectedStateOption;

    partial void OnSelectedStateOptionChanged(string? value)
    {
        if (Card?.CurrentState is not { } current) return;

        var target = value switch
        {
            var s when s == Strings.State_InWork => ObjectState.InWork,
            var s when s == Strings.State_Approved => ObjectState.Approved,
            var s when s == Strings.State_Annulled => ObjectState.Annulled,
            _ => (ObjectState?)null
        };
        
        if (target is null || target == current) return;
        if (!StateRules.CanTransition(current, target.Value))
        {
            SelectedStateOption = StateDisplay;
            return;
        }

        _ = ChangeStateAsync(target.Value);
    }
    
    partial void OnCardChanged(ObjectCard? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(StateDisplay));
        OnPropertyChanged(nameof(MassDisplay));
        OnPropertyChanged(nameof(MaterialDisplay));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanAnnul));
        OnPropertyChanged(nameof(HasComposition));
    }

    public bool HasSelection => Card is not null;
    
    public string Subtitle => Card is null ? "" : $"{Card.Designation ?? "—"} · {TypeDisplay}";

    public string TypeDisplay => Card is null ? "" : Card.Type switch
    {
        ObjectType.Assembly => Strings.Type_Assembly,
        ObjectType.Part => Strings.Type_Part,
        ObjectType.StandardPart => Strings.Type_StandardPart,
        _ => Card.Type.ToString()
    };

    public string VersionDisplay => Card?.CurrentVersionNo is null
        ? Strings.Card_NoActiveVersion
        : $"{Strings.Card_Label_Version} {Card.CurrentVersionNo}";

    public string StateDisplay => Card?.CurrentState switch
    {
        ObjectState.InWork => Strings.State_InWork,
        ObjectState.Approved => Strings.State_Approved,
        ObjectState.Annulled => Strings.State_Annulled,
        _ => "—"
    };
    
    public string MassDisplay
    {
        get
        {
            if (Card is null) return "";
            if (Card.Type == ObjectType.Assembly)
                return _massTotal?.ToString("0.####") ?? Strings.Card_Mass_Hint;
            return Card.MassKg?.ToString("0.####") ?? "—";
        }
    }

    public string MaterialDisplay => Card?.Material is null ? "—" : Card.Material;

    public bool CanApprove =>
        Card?.CurrentState is { } approveFrom && StateRules.CanTransition(approveFrom, ObjectState.Approved);

    public bool CanAnnul =>
        Card?.CurrentState is { } annulFrom && StateRules.CanTransition(annulFrom, ObjectState.Annulled);
    
    public bool HasComposition =>
        Card is { } card && card.Type == ObjectType.Assembly && Composition.Count > 0;

    [RelayCommand]
    private async Task ApproveAsync() => await ChangeStateAsync(ObjectState.Approved);

    [RelayCommand]
    private async Task AnnulAsync() => await ChangeStateAsync(ObjectState.Annulled);

    private async Task ChangeStateAsync(ObjectState to)
    {
        if (Card is not { } card) return;
        if (card.CurrentVersionId is not { } versionId) return;
        if (card.CurrentState is not { } current || !StateRules.CanTransition(current, to)) return;

        IsBusy = true;
        try
        {
            await _state.ChangeStateAsync(versionId, to);
            await LoadAsync(card.Id);
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
    
    /// <summary>
    /// Загружает карточку объекта и состав 1-го уровня (для сборок) по идентификатору.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта</param>
    public async Task LoadAsync(long objectId)
    {
        IsBusy = true;
        try
        {
            Clear();
            
            Card = await _reads.GetCardAsync(objectId);
            SelectedStateOption = StateDisplay;   // комбобокс показывает текущее состояние
            
            if (Card.Type == ObjectType.Assembly)
            {
                var children = await _reads.GetChildrenAsync(objectId);
                foreach (var child in children)
                {
                    Composition.Add(new Models.CompositionRow(
                        child.Designation ?? "—",
                        child.Name,
                        child.QuantityOnPath,
                        child.UnitMassKg?.ToString("0.####") ?? ""));
                }
            }

            OnPropertyChanged(nameof(HasComposition));
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

    /// <summary>
    /// Устанавливает рассчитанную суммарную массу сборки (для отображения в MassDisplay).
    /// </summary>
    /// <param name="totalKg">Рассчитанная суммарная масса</param>
    public void SetMass(decimal? totalKg)
    {
        _massTotal = totalKg;
        OnPropertyChanged(nameof(MassDisplay));
    }

    /// <summary>
    /// Очищает текущую карточку и состав, сбрасывает рассчитанную суммарную массу.
    /// </summary>
    private void Clear()
    {
        Card = null;
        _massTotal = null;
        Composition.Clear();
        OnPropertyChanged(nameof(MassDisplay));
        OnPropertyChanged(nameof(HasComposition));
    }
}