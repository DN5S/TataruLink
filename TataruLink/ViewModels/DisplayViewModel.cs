using TataruLink.Configuration;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for the Display tab - manages chat display settings.
/// </summary>
public class DisplayViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;

    public DisplayViewModel(TataruConfig configuration)
    {
        this.configuration = configuration;
    }

    public bool ShowInGameChat
    {
        get => configuration.Display.ShowInGameChat;
        set
        {
            if (configuration.Display.ShowInGameChat != value)
            {
                configuration.Display.ShowInGameChat = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(ShowInGameChat));
            }
        }
    }

    public bool ShowSenderName
    {
        get => configuration.Display.ShowSenderName;
        set
        {
            if (configuration.Display.ShowSenderName != value)
            {
                configuration.Display.ShowSenderName = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(ShowSenderName));
            }
        }
    }

    public bool ShowChatType
    {
        get => configuration.Display.ShowChatType;
        set
        {
            if (configuration.Display.ShowChatType != value)
            {
                configuration.Display.ShowChatType = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(ShowChatType));
            }
        }
    }
}
