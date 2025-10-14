using TataruLink.Configuration;

namespace TataruLink.ViewModels;

/// <summary>
/// Root ViewModel that orchestrates the entire application state.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;

    public MainViewModel(
        TataruConfig configuration,
        SettingsViewModel settingsViewModel,
        HistoryViewModel historyViewModel)
    {
        this.configuration = configuration;
        Settings = settingsViewModel;
        History = historyViewModel;
    }

    public SettingsViewModel Settings { get; }
    public HistoryViewModel History { get; }

    // Global state
    public bool IsEnabled
    {
        get => configuration.IsEnabled;
        set
        {
            if (configuration.IsEnabled != value)
            {
                configuration.IsEnabled = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    public bool ShowDtrBar
    {
        get => configuration.ShowDtrBar;
        set
        {
            if (configuration.ShowDtrBar != value)
            {
                configuration.ShowDtrBar = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(ShowDtrBar));
            }
        }
    }

    // Window visibility
    public bool IsSettingsOpen
    {
        get => Get<bool>(nameof(IsSettingsOpen));
        set => Set(value, nameof(IsSettingsOpen));
    }

    public bool IsHistoryOpen
    {
        get => Get<bool>(nameof(IsHistoryOpen));
        set => Set(value, nameof(IsHistoryOpen));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Settings.Dispose();
            History.Dispose();
        }

        base.Dispose(disposing);
    }
}
