using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPdm.App.Localization;
using MiniPdm.Core.Domain;
using MiniPdm.Data;

namespace MiniPdm.App.ViewModels;

/// <summary>
/// ViewModel для карточки объекта в правой панели.
/// </summary>
public partial class ObjectCardViewModel : ViewModelBase
{
    private readonly UiReadService _reads;
    private readonly PdmStateService _state;
    private readonly Action<Exception> _onError;

    public ObjectCardViewModel(UiReadService reads, PdmStateService state, Action<Exception> onError)
    {
        _reads = reads;
        _state = state;
        _onError = onError;
    }

    [ObservableProperty]
    private ObjectCard? _card;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnCardChanged(ObjectCard? value)
    {
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(StateDisplay));
        OnPropertyChanged(nameof(MassDisplay));
        OnPropertyChanged(nameof(MaterialDisplay));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanAnnul));
        OnPropertyChanged(nameof(HasSelection));
    }

    public bool HasSelection => Card is not null;

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

    public string MassDisplay => Card?.MassKg is null ? "—" : $"{Card.MassKg:0.####}";
    public string MaterialDisplay => Card?.Material is null ? "—" : Card.Material;

    public bool CanApprove =>
        Card?.CurrentState is { } approveFrom && StateRules.CanTransition(approveFrom, ObjectState.Approved);

    public bool CanAnnul =>
        Card?.CurrentState is { } annulFrom && StateRules.CanTransition(annulFrom, ObjectState.Annulled);

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
            await LoadAsync(card.Id); // перечитать карточку после смены состояния
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
    /// Загрузить карточку объекта по идентификатору.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта.</param>
    public async Task LoadAsync(long objectId)
    {
        IsBusy = true;
        try
        {
            Card = await _reads.GetCardAsync(objectId);
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
    /// Очистить карточку объекта.
    /// </summary>
    public void Clear() => Card = null;
}