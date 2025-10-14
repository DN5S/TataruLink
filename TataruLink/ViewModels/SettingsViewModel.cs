namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel that composes all settings tab ViewModels.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(
        GeneralTabViewModel generalTab,
        TranslationViewModel translationTab,
        ChatTypesViewModel chatTypesTab,
        FiltersViewModel filtersTab,
        GlossaryViewModel glossaryTab,
        DisplayViewModel displayTab,
        OverlayViewModel overlayTab)
    {
        GeneralTab = generalTab;
        TranslationTab = translationTab;
        ChatTypesTab = chatTypesTab;
        FiltersTab = filtersTab;
        GlossaryTab = glossaryTab;
        DisplayTab = displayTab;
        OverlayTab = overlayTab;
    }

    public GeneralTabViewModel GeneralTab { get; }
    public TranslationViewModel TranslationTab { get; }
    public ChatTypesViewModel ChatTypesTab { get; }
    public FiltersViewModel FiltersTab { get; }
    public GlossaryViewModel GlossaryTab { get; }
    public DisplayViewModel DisplayTab { get; }
    public OverlayViewModel OverlayTab { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralTab.Dispose();
            TranslationTab.Dispose();
            ChatTypesTab.Dispose();
            FiltersTab.Dispose();
            GlossaryTab.Dispose();
            DisplayTab.Dispose();
            OverlayTab.Dispose();
        }

        base.Dispose(disposing);
    }
}
