using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Glossary;
using TataruLink.Models;
using TataruLink.ViewModels.Collections;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for Glossary management with performance optimizations.
/// FIXES: Removes every-frame refresh, implements proper async error handling.
/// </summary>
public class GlossaryViewModel : ViewModelBase
{
    private readonly GlossaryManager glossaryManager;

    public GlossaryViewModel(GlossaryManager glossaryManager)
    {
        this.glossaryManager = glossaryManager;

        Entries = new ObservableList<GlossaryEntry>();

        // Initialize commands with proper error handling
        AddEntryCommand = new AsyncRelayCommand(
            execute: AddEntryAsync,
            canExecute: CanAddEntry,
            onError: OnCommandError);

        DeleteEntryCommand = new AsyncRelayCommand<long>(
            execute: async (id) => await DeleteEntryAsync(id),
            canExecute: _ => true,
            onError: OnCommandError);

        UpdateEntryCommand = new AsyncRelayCommand<(long Id, string Replacement)>(
            execute: async (param) => await UpdateEntryAsync(param),
            canExecute: _ => true,
            onError: OnCommandError);

        ToggleEntryCommand = new AsyncRelayCommand<long>(
            execute: async (id) => await ToggleEntryAsync(id),
            canExecute: _ => true,
            onError: OnCommandError);

        ClearAllCommand = new AsyncRelayCommand(
            execute: ClearAllAsync,
            canExecute: () => Entries.Count > 0,
            onError: OnCommandError);

        EnableAllCommand = new AsyncRelayCommand(
            execute: EnableAllAsync,
            canExecute: () => Entries.Any(e => !e.IsEnabled),
            onError: OnCommandError);

        DisableAllCommand = new AsyncRelayCommand(
            execute: DisableAllAsync,
            canExecute: () => Entries.Any(e => e.IsEnabled),
            onError: OnCommandError);

        ExportToClipboardCommand = new RelayCommand(ExportToClipboard);
        ImportFromClipboardCommand = new AsyncRelayCommand(ImportFromClipboardAsync, onError: OnCommandError);

        // Load initial data
        RefreshEntries();
    }

    // Properties
    public ObservableList<GlossaryEntry> Entries { get; }

    public bool IsEnabled
    {
        get => glossaryManager.IsEnabled;
        set
        {
            if (glossaryManager.IsEnabled != value)
            {
                glossaryManager.IsEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    public string NewOriginal
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    public string NewReplacement
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    public string SearchFilter
    {
        get => Get<string>() ?? string.Empty;
        set
        {
            if (Set(value))
            {
                ApplySearchFilter();
            }
        }
    }

    public string? ErrorMessage
    {
        get => Get<string?>();
        set => Set(value);
    }

    public int TotalCount => glossaryManager.GetStatistics().TotalEntries;
    public int EnabledCount => glossaryManager.GetStatistics().EnabledEntries;
    public int RemainingCapacity => glossaryManager.GetStatistics().RemainingCapacity;

    // Commands
    public ICommand AddEntryCommand { get; }
    public AsyncRelayCommand<long> DeleteEntryCommand { get; }
    public AsyncRelayCommand<(long Id, string Replacement)> UpdateEntryCommand { get; }
    public AsyncRelayCommand<long> ToggleEntryCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand EnableAllCommand { get; }
    public ICommand DisableAllCommand { get; }
    public ICommand ExportToClipboardCommand { get; }
    public ICommand ImportFromClipboardCommand { get; }

    // Methods
    public void RefreshEntries()
    {
        var entries = glossaryManager.GetEntries();
        Entries.ReplaceAll(entries);
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(RemainingCapacity));
    }

    private bool CanAddEntry()
    {
        return !string.IsNullOrWhiteSpace(NewOriginal) &&
               !string.IsNullOrWhiteSpace(NewReplacement) &&
               !Entries.Any(e => e.Original.Equals(NewOriginal, StringComparison.OrdinalIgnoreCase));
    }

    private async Task AddEntryAsync()
    {
        if (!CanAddEntry()) return;

        await glossaryManager.AddEntryAsync(NewOriginal.Trim(), NewReplacement.Trim());

        // Update UI on main thread
        Service.UiDispatcher.Invoke(() =>
        {
            NewOriginal = string.Empty;
            NewReplacement = string.Empty;
            ErrorMessage = null;
            RefreshEntries();
        });

        Service.PluginLog.Information($"Added glossary entry: {NewOriginal} -> {NewReplacement}");
    }

    private async Task DeleteEntryAsync(long? id)
    {
        if (!id.HasValue) return;

        await glossaryManager.DeleteEntryAsync(id.Value);

        Service.UiDispatcher.Invoke(RefreshEntries);

        Service.PluginLog.Information($"Deleted glossary entry: {id.Value}");
    }

    private async Task UpdateEntryAsync((long Id, string Replacement)? param)
    {
        if (!param.HasValue) return;

        var (id, replacement) = param.Value;
        if (string.IsNullOrWhiteSpace(replacement)) return;

        await glossaryManager.UpdateEntryAsync(id, replacement.Trim());

        Service.UiDispatcher.Invoke(RefreshEntries);

        Service.PluginLog.Information($"Updated glossary entry: {id}");
    }

    private async Task ToggleEntryAsync(long? id)
    {
        if (!id.HasValue) return;

        await glossaryManager.ToggleEntryAsync(id.Value);

        Service.UiDispatcher.Invoke(RefreshEntries);
    }

    private async Task ClearAllAsync()
    {
        await glossaryManager.ClearAllAsync();

        Service.UiDispatcher.Invoke(RefreshEntries);

        Service.PluginLog.Information("Cleared all glossary entries");
    }

    private async Task EnableAllAsync()
    {
        foreach (var entry in Entries.Where(e => !e.IsEnabled))
        {
            await glossaryManager.ToggleEntryAsync(entry.Id);
        }

        Service.UiDispatcher.Invoke(RefreshEntries);
    }

    private async Task DisableAllAsync()
    {
        foreach (var entry in Entries.Where(e => e.IsEnabled))
        {
            await glossaryManager.ToggleEntryAsync(entry.Id);
        }

        Service.UiDispatcher.Invoke(RefreshEntries);
    }

    private void ExportToClipboard()
    {
        try
        {
            var exportData = Entries.Select(e => new
            {
                e.Original,
                e.Replacement,
                e.IsEnabled
            });

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            Dalamud.Bindings.ImGui.ImGui.SetClipboardText(json);
            Service.PluginLog.Information($"Exported {Entries.Count} glossary entries to clipboard");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to export glossary to clipboard");
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task ImportFromClipboardAsync()
    {
        try
        {
            var json = Dalamud.Bindings.ImGui.ImGui.GetClipboardText();
            if (string.IsNullOrWhiteSpace(json))
            {
                ErrorMessage = "Clipboard is empty";
                return;
            }

            var importedEntries = new List<GlossaryEntry>();

            try
            {
                var newFormat = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(json);
                if (newFormat != null)
                {
                    foreach (var item in newFormat)
                    {
                        if (item.TryGetValue("Original", out var orig) &&
                            item.TryGetValue("Replacement", out var repl))
                        {
                            var entry = new GlossaryEntry
                            {
                                Original = orig.GetString() ?? string.Empty,
                                Replacement = repl.GetString() ?? string.Empty,
                                IsEnabled = item.TryGetValue("IsEnabled", out var enabled) && enabled.GetBoolean()
                            };

                            if (!string.IsNullOrWhiteSpace(entry.Original) &&
                                !string.IsNullOrWhiteSpace(entry.Replacement))
                            {
                                importedEntries.Add(entry);
                            }
                        }
                    }
                }
            }
            catch
            {
                ErrorMessage = "Failed to parse glossary data";
                return;
            }

            if (importedEntries.Count == 0)
            {
                ErrorMessage = "No valid entries found in clipboard";
                return;
            }

            // Filter out duplicates
            var toImport = importedEntries.Where(e =>
                !Entries.Any(existing =>
                    existing.Original.Equals(e.Original, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (toImport.Count > 0)
            {
                await glossaryManager.ImportEntriesAsync(toImport);

                Service.UiDispatcher.Invoke(RefreshEntries);

                Service.PluginLog.Information($"Imported {toImport.Count} new glossary entries from clipboard");
            }
            else
            {
                ErrorMessage = "No new entries to import (all duplicates)";
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import glossary from clipboard");
            ErrorMessage = $"Import failed: {ex.Message}";
        }
    }

    private void ApplySearchFilter()
    {
        var filter = SearchFilter;
        if (string.IsNullOrWhiteSpace(filter))
        {
            RefreshEntries();
            return;
        }

        var allEntries = glossaryManager.GetEntries();
        var filtered = allEntries.Where(e =>
            e.Original.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            e.Replacement.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Entries.ReplaceAll(filtered);
    }

    private void OnCommandError(Exception ex)
    {
        ErrorMessage = ex.Message;

        // Auto-clear error after 3 seconds
        Task.Delay(3000).ContinueWith(_ =>
        {
            Service.UiDispatcher.Invoke(() =>
            {
                if (ErrorMessage == ex.Message) // Only clear if it's still the same error
                {
                    ErrorMessage = null;
                }
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Entries.Clear();
        }

        base.Dispose(disposing);
    }
}
