using System.Linq;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Translation;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for the General tab - system status and quick controls.
/// </summary>
public class GeneralTabViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    private DtrBarManager? dtrBarManager;

    public GeneralTabViewModel(
        TataruConfig configuration,
        ITranslationService translationService,
        DtrBarManager? dtrBarManager = null)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        this.dtrBarManager = dtrBarManager;

        OpenHistoryCommand = new RelayCommand(OpenHistory);
    }

    // Properties
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
                dtrBarManager?.SetVisible(value);
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(ShowDtrBar));
            }
        }
    }

    // Status properties
    public string ProviderName => translationService.ProviderName;
    public bool IsConfigured => translationService.IsConfigured;
    public int TranslationCount => dtrBarManager?.GetTranslationCount() ?? 0;
    public bool ShowInGameChat => configuration.Display.ShowInGameChat;
    public int ActiveOverlaysCount => configuration.Display.GetActiveOverlays().Count();
    public int TotalOverlaysCount => configuration.Display.OverlayWindows.Count;
    public int EnabledChatTypesCount => configuration.Chat.GetEnabledChatTypes().Count();
    public string SourceLanguage => configuration.Translation.SourceLanguage;
    public string TargetLanguage => configuration.Translation.TargetLanguage;

    // Commands
    public ICommand OpenHistoryCommand { get; }

    // Methods
    public void SetDtrBarManager(DtrBarManager? manager)
    {
        dtrBarManager = manager;
        OnPropertyChanged(nameof(TranslationCount));
    }

    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(ProviderName));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(TranslationCount));
        OnPropertyChanged(nameof(ShowInGameChat));
        OnPropertyChanged(nameof(ActiveOverlaysCount));
        OnPropertyChanged(nameof(TotalOverlaysCount));
        OnPropertyChanged(nameof(EnabledChatTypesCount));
        OnPropertyChanged(nameof(SourceLanguage));
        OnPropertyChanged(nameof(TargetLanguage));
    }

    private void OpenHistory()
    {
        Services.Service.CommandManager.ProcessCommand("/tataruhistory");
    }
}
