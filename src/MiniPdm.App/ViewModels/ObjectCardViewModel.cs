using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPdm.App.Localization;
using MiniPdm.Core.Domain;
using MiniPdm.Data;

namespace MiniPdm.App.ViewModels;

/// <summary>Правая панель: карточка выбранного объекта, кнопки смены состояния
/// и секция «Состав (1-й уровень)». Масса сборки заполняется на месте по команде
/// «Рассчитать массу» (SetMass); неполная сумма уходит диалогом через MainViewModel.
/// Активность кнопок — чистые правила StateRules; исполнение — PdmStateService.</summary>
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
        StateOptions = new[] { Strings.State_InWork, Strings.State_Approved, Strings.State_Annulled };
    }

    /// <summary>Опции комбобокса состояния (только отображение; смена — кнопками).</summary>
    public IReadOnlyList<string> StateOptions { get; }

    /// <summary>Строки секции «Состав (1-й уровень)».</summary>
    public ObservableCollection<CompositionRow> Composition { get; } = [];

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

    /// <summary>Подзаголовок карточки: «ОБОЗНАЧЕНИЕ · Тип».</summary>
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

    /// <summary>Масса: у сборки — «— (нажмите «Рассчитать массу»)» до расчёта,
    /// затем рассчитанный итог; у детали/стандартного — значение из БД, «—» если нет.</summary>
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

    /// <summary>Секция состава видима только у сборок с непустым составом.</summary>
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

    /// <summary>Загружает карточку и состав 1-го уровня по id выбранного узла.</summary>
    public async Task LoadAsync(long objectId)
    {
        IsBusy = true;
        try
        {
            Card = await _reads.GetCardAsync(objectId);
            SelectedStateOption = StateDisplay;   // комбобокс показывает текущее состояние

            _massTotal = null; // новая карточка — прошлый расчёт неактуален
            OnPropertyChanged(nameof(MassDisplay));

            Composition.Clear();
            if (Card.Type == ObjectType.Assembly)
            {
                var children = await _reads.GetChildrenAsync(objectId);
                foreach (var child in children)
                {
                    Composition.Add(new CompositionRow(
                        child.Designation ?? "—",
                        child.Name,
                        child.QuantityOnPath,
                        child.UnitMassKg?.ToString("0.####") ?? "")); // пустая масса — по ТЗ
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

    /// <summary>Заполняет поле «Масса, кг» рассчитанным итогом (MainViewModel после BomCalculator).</summary>
    public void SetMass(decimal? totalKg)
    {
        _massTotal = totalKg;
        OnPropertyChanged(nameof(MassDisplay));
    }

    /// <summary>Сброс при снятии выделения.</summary>
    public void Clear()
    {
        Card = null;
        _massTotal = null;
        Composition.Clear();
        OnPropertyChanged(nameof(MassDisplay));
        OnPropertyChanged(nameof(HasComposition));
    }

    /// <summary>Строка состава 1-го уровня: обозначение, наименование, кол-во, масса 1 шт.</summary>
    public sealed record CompositionRow(string Designation, string Name, int Quantity, string UnitMass);
}