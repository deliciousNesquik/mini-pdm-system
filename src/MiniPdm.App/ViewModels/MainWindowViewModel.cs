using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniPdm.App.Localization;
using MiniPdm.Core.Bom;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;
using MiniPdm.Data;
using Serilog;

namespace MiniPdm.App.ViewModels;

/// <summary>Главный экран: корни дерева + ленивое раскрытие, поиск, импорт
/// с прогрессом и отменой, карточка, расчёты. UI-события (диалоги, окна отчётов)
/// — события MessageRequested / ImportCompleted / SummaryRequested; подписчик — View.
/// Никакого Npgsql: чтение — UiReadService, запись — оркестраторы Data.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly UiReadService _reads;
    private readonly ImportService _import;
    private readonly Func<Task<string?>> _pickFolder;
    private CancellationTokenSource? _importCts;
    private int _searchSequence;

    public MainViewModel(
        UiReadService reads,
        ImportService import,
        PdmStateService state,
        Func<Task<string?>> pickFolder)
    {
        _reads = reads;
        _import = import;
        _pickFolder = pickFolder;
        Card = new ObjectCardViewModel(reads, state, HandleError);
    }

    public ObjectCardViewModel Card { get; }

    /// <summary>Заголовок сообщения / текст. Подписчик: MainWindow (шаг 3).</summary>
    public event Action<string, string>? MessageRequested;

    /// <summary>Импорт завершён — View показывает окно отчёта (рисунок 2 ТЗ).</summary>
    public event Action<ImportAnalysis>? ImportCompleted;

    /// <summary>Запрошена сводная спецификация — View показывает таблицу.</summary>
    public event Action<IReadOnlyList<SummaryLine>>? SummaryRequested;

    public ObservableCollection<TreeNodeViewModel> Roots { get; } = [];

    public bool HasNoRoots => Roots.Count == 0;

    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelImportCommand))]
    private bool _isImporting;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _progressValue;

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        if (value is null)
        {
            Card.Clear();
            return;
        }

        _ = Card.LoadAsync(value.ObjectId); // ошибки маршрутизируются внутри CardViewModel
    }

    partial void OnSearchTextChanged(string value) => _ = ApplySearchAsync(value);

    /// <summary>Вызывается из App после создания окна: начальная загрузка корней.</summary>
    public Task InitializeAsync() => ApplySearchAsync(SearchText);

    // ---------- дерево ----------

    private TreeNodeViewModel ToRootNode(PdmObjectListItem item) => new(
        item.Id,
        item.Type,
        item.Designation,
        item.Name,
        item.CurrentState,
        quantity: 1,
        hasActiveVersion: item.CurrentState is not null,
        hasChildren: item.Type == ObjectType.Assembly && item.CurrentState is not null,
        loadChildren: LoadChildrenAsync,
        onError: HandleError);

    private TreeNodeViewModel ToChildNode(BomRow row) => new(
        row.Path[^1],
        row.Type,
        row.Designation,
        row.Name,
        row.State,
        row.QuantityOnPath,
        row.HasActiveVersion,
        row.Type == ObjectType.Assembly && row.HasActiveVersion,
        LoadChildrenAsync,
        HandleError);

    private async Task<IReadOnlyList<TreeNodeViewModel>> LoadChildrenAsync(long parentId)
    {
        var rows = await _reads.GetChildrenAsync(parentId);
        return rows.Select(ToChildNode).ToList();
    }

    private async Task ApplySearchAsync(string query)
    {
        var sequence = ++_searchSequence;
        await Task.Delay(250); // debounce набора текста
        if (sequence != _searchSequence) return; // устаревший запрос — молча пропустить

        try
        {
            var items = string.IsNullOrWhiteSpace(query)
                ? await _reads.GetRootsAsync()
                : await _reads.SearchAsync(query);

            ReplaceRoots(items);
        }
        catch (Exception e)
        {
            HandleError(e);
        }
    }

    private void ReplaceRoots(IReadOnlyList<PdmObjectListItem> items)
    {
        Roots.Clear();
        foreach (var item in items) Roots.Add(ToRootNode(item));
        OnPropertyChanged(nameof(HasNoRoots));
    }

    // ---------- импорт ----------

    private bool CanRunImport() => !IsImporting;
    private bool CanCancelImport() => IsImporting;

    [RelayCommand(CanExecute = nameof(CanRunImport))]
    private async Task ImportFolderAsync()
    {
        var folder = await _pickFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;

        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();
        IsImporting = true;
        try
        {
            // Progress<T> захватывает SynchronizationContext на момент создания
            // (UI-поток) — Report маршализуется сам, диспетчеризация вручную не нужна.
            var progress = new Progress<ImportProgress>(ReportProgress);
            var analysis = await _import.ImportFolderAsync(folder, progress, _importCts.Token);

            Log.Information("Импорт завершён: принято {Accepted}, отклонено {Rejected}, предупреждений {Warnings}",
                analysis.AcceptedCount, analysis.RejectedCount, analysis.WarningCount);

            ImportCompleted?.Invoke(analysis);
            await ApplySearchAsync(SearchText); // дерево могло обновиться
        }
        catch (OperationCanceledException)
        {
            Log.Information("Импорт отменён пользователем");
            MessageRequested?.Invoke(Strings.Import_Folder, Strings.Import_Cancelled);
        }
        catch (Exception e)
        {
            HandleError(e);
        }
        finally
        {
            IsImporting = false;
            ProgressText = "";
            ProgressValue = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelImport))]
    private void CancelImport() => _importCts?.Cancel();

    private void ReportProgress(ImportProgress p)
    {
        var phase = p.Phase switch
        {
            ImportPhase.Reading => Strings.Phase_Reading,
            ImportPhase.Analyzing => Strings.Phase_Analyzing,
            ImportPhase.Writing => Strings.Phase_Writing,
            ImportPhase.Committing => Strings.Phase_Committing,
            _ => Strings.Phase_Done
        };

        ProgressText = p.Total is { } total
            ? $"{phase} {p.Current}/{total}" + (p.CurrentFile is { } file ? $" — {file}" : "")
            : phase;

        ProgressValue = p.Total is { } t && t > 0
            ? Math.Min(100, p.Current * 100.0 / t)
            : 0;
    }

    // ---------- расчёты ----------

    [RelayCommand]
    private async Task CalcMassAsync()
    {
        if (SelectedNode is not { } node || node.Type != ObjectType.Assembly)
        {
            MessageRequested?.Invoke(Strings.Mass_Result_Title, Strings.Mass_NoSelection);
            return;
        }

        try
        {
            var rows = await _reads.GetTreeAsync(node.ObjectId);
            var calc = BomCalculator.TotalMass(rows);

            if (calc.TotalMassKg is { } total)
            {
                MessageRequested?.Invoke(Strings.Mass_Result_Title,
                    string.Format(Strings.Mass_Total, total.ToString("0.####")));
            }
            else
            {
                // ТЗ: назвать виновников, а не вернуть неполную сумму.
                var lines = calc.Problems
                    .Select(p => $"{p.Display} — {ReasonOf(p.Kind)}");
                MessageRequested?.Invoke(Strings.Mass_Incomplete_Title,
                    string.Join(Environment.NewLine, lines));
            }
        }
        catch (Exception e)
        {
            HandleError(e);
        }
    }

    [RelayCommand]
    private async Task CalcSummaryAsync()
    {
        if (SelectedNode is not { } node || node.Type != ObjectType.Assembly)
        {
            MessageRequested?.Invoke(Strings.Calc_Summary, Strings.Mass_NoSelection);
            return;
        }

        try
        {
            var rows = await _reads.GetTreeAsync(node.ObjectId);
            SummaryRequested?.Invoke(BomCalculator.Summary(rows));
        }
        catch (Exception e)
        {
            HandleError(e);
        }
    }

    private static string ReasonOf(MassProblemKind kind) => kind switch
    {
        MassProblemKind.MissingMass => Strings.Mass_Problem_MissingMass,
        _ => Strings.Mass_Problem_NoActiveVersion
    };

    // ---------- ошибки ----------

    private void HandleError(Exception e)
    {
        Log.Error(e, "Ошибка в интерфейсе");
        MessageRequested?.Invoke(Strings.Error_Title, e.Message);
    }
}