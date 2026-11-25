namespace HWIDDiagnosticPro.App.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _badgeText = string.Empty;

    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "•";
    public string Subtitle { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string BadgeText
    {
        get => _badgeText;
        set => SetProperty(ref _badgeText, value);
    }
}
