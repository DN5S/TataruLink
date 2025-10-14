using TataruLink.Configuration;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for the Filters tab - manages game state and content filtering.
/// </summary>
public class FiltersViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;

    public FiltersViewModel(TataruConfig configuration)
    {
        this.configuration = configuration;
    }

    // Game State Filters
    public bool SkipInCutscene
    {
        get => configuration.Filter.SkipInCutscene;
        set
        {
            if (configuration.Filter.SkipInCutscene != value)
            {
                configuration.Filter.SkipInCutscene = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(SkipInCutscene));
            }
        }
    }

    public bool SkipInLoading
    {
        get => configuration.Filter.SkipInLoading;
        set
        {
            if (configuration.Filter.SkipInLoading != value)
            {
                configuration.Filter.SkipInLoading = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(SkipInLoading));
            }
        }
    }

    public bool SkipInRetainer
    {
        get => configuration.Filter.SkipInRetainer;
        set
        {
            if (configuration.Filter.SkipInRetainer != value)
            {
                configuration.Filter.SkipInRetainer = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(SkipInRetainer));
            }
        }
    }

    // Content Filters
    public bool SkipAutoTranslate
    {
        get => configuration.Filter.SkipAutoTranslate;
        set
        {
            if (configuration.Filter.SkipAutoTranslate != value)
            {
                configuration.Filter.SkipAutoTranslate = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(SkipAutoTranslate));
            }
        }
    }

    // Validation Settings
    public int DuplicateDetectionPeriodMs
    {
        get => configuration.Validation.DuplicateDetectionPeriodMs;
        set
        {
            if (configuration.Validation.DuplicateDetectionPeriodMs != value)
            {
                configuration.Validation.DuplicateDetectionPeriodMs = value;
                Services.Service.Configuration.Save();
                OnPropertyChanged(nameof(DuplicateDetectionPeriodMs));
            }
        }
    }
}
