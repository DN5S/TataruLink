using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.History;
using TataruLink.Models;
using TataruLink.ViewModels.Collections;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for translation history with incremental updates.
/// FIXES: Inefficient refresh on every record add, uses incremental updates instead.
/// </summary>
public class HistoryViewModel : ViewModelBase
{
    private readonly SessionHistoryManager historyManager;

    public HistoryViewModel(SessionHistoryManager historyManager)
    {
        this.historyManager = historyManager;

        DisplayedRecords = new ObservableList<TranslationRecord>();
        SelectedIds = new ObservableList<long>();

        // Initialize commands
        RefreshCommand = new RelayCommand(RefreshRecords);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedIds.Count > 0);
        ClearAllCommand = new AsyncRelayCommand(ClearAllAsync);
        DeleteRecordCommand = new RelayCommand<long>(id => DeleteRecord(id));
        SaveEditCommand = new RelayCommand<(long Id, string Translation)>(param => SaveTranslationEdit(param));
        CancelEditCommand = new RelayCommand(CancelEdit);

        // Subscribe to history events for incremental updates
        historyManager.RecordAdded += OnRecordAddedIncremental;

        // Load initial data
        RefreshRecords();
    }

    // Properties
    public ObservableList<TranslationRecord> DisplayedRecords { get; }
    public ObservableList<long> SelectedIds { get; }

    public string SearchText
    {
        get => Get<string>(nameof(SearchText)) ?? string.Empty;
        set
        {
            if (Set(value, nameof(SearchText)))
            {
                RefreshRecords();
            }
        }
    }

    public long? EditingId
    {
        get => Get<long?>(nameof(EditingId));
        set => Set(value, nameof(EditingId));
    }

    public string EditingText
    {
        get => Get<string>(nameof(EditingText)) ?? string.Empty;
        set => Set(value, nameof(EditingText));
    }

    public int TotalCount => historyManager.GetRecordCount();
    public int DisplayedCount => DisplayedRecords.Count;
    public int SelectedCount => SelectedIds.Count;

    // Commands (exposed with specific types for SetParameter support)
    public ICommand RefreshCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ClearAllCommand { get; }
    public RelayCommand<long> DeleteRecordCommand { get; }
    public RelayCommand<(long Id, string Translation)> SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    // Methods
    public void RefreshRecords()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            var records = historyManager.GetRecords(1000);
            DisplayedRecords.ReplaceAll(records);
        }
        else
        {
            var records = historyManager.SearchRecords(SearchText, 1000);
            DisplayedRecords.ReplaceAll(records);
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(DisplayedCount));
    }

    private void DeleteRecord(long? id)
    {
        if (!id.HasValue) return;

        historyManager.DeleteRecord(id.Value);
        SelectedIds.Remove(id.Value);
        RefreshRecords();
        Services.Service.PluginLog.Debug($"Deleted record {id.Value}");
    }

    private void DeleteSelected()
    {
        if (SelectedIds.Count == 0) return;

        historyManager.DeleteRecords(SelectedIds.ToArray());
        Services.Service.PluginLog.Information($"Deleted {SelectedIds.Count} records");
        SelectedIds.Clear();
        RefreshRecords();

        OnPropertyChanged(nameof(SelectedCount));
    }

    private async Task ClearAllAsync()
    {
        var count = historyManager.Clear();
        SelectedIds.Clear();
        RefreshRecords();

        OnPropertyChanged(nameof(SelectedCount));
        Services.Service.PluginLog.Information($"Cleared all history: {count} records deleted");

        await Task.CompletedTask;
    }

    private void SaveTranslationEdit((long Id, string Translation)? param)
    {
        if (!param.HasValue) return;

        var (id, newTranslation) = param.Value;

        if (historyManager.UpdateTranslation(id, newTranslation))
        {
            RefreshRecords();
            EditingId = null;
            Services.Service.PluginLog.Information($"Updated translation for record {id}");
        }
        else
        {
            Services.Service.PluginLog.Warning($"Failed to update translation for record {id}");
        }
    }

    private void CancelEdit()
    {
        EditingId = null;
        EditingText = string.Empty;
    }

    public void StartEdit(long id, string currentTranslation)
    {
        EditingId = id;
        EditingText = currentTranslation;
    }

    public void ToggleSelection(long id)
    {
        if (SelectedIds.Contains(id))
        {
            SelectedIds.Remove(id);
        }
        else
        {
            SelectedIds.Add(id);
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    public bool IsSelected(long id)
    {
        return SelectedIds.Contains(id);
    }

    /// <summary>
    /// Incremental update: add new record without full refresh.
    /// PERFORMANCE FIX: Only adds the new record instead of reloading everything.
    /// </summary>
    private void OnRecordAddedIncremental(object? sender, TranslationRecord record)
    {
        // Only update if the record matches current filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var matchesSearch =
                record.OriginalContent.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                record.TranslatedContent.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                record.SenderName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            if (!matchesSearch)
                return;
        }

        // Add the new record incrementally (prepend to top)
        Services.Service.UiDispatcher.Invoke(() =>
        {
            // Insert at the beginning since history is typically newest-first
            var snapshot = DisplayedRecords.GetSnapshot();
            snapshot.Insert(0, record);

            // Limit to 1000 displayed records
            if (snapshot.Count > 1000)
            {
                snapshot.RemoveAt(snapshot.Count - 1);
            }

            DisplayedRecords.ReplaceAll(snapshot);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(DisplayedCount));
        });

        Services.Service.PluginLog.Debug($"History incrementally updated with new record: {record.Id}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events to prevent memory leak
            historyManager.RecordAdded -= OnRecordAddedIncremental;
            DisplayedRecords.Clear();
            SelectedIds.Clear();
        }

        base.Dispose(disposing);
    }
}
