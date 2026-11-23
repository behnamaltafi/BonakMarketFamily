using BonakMarket.Events;

namespace BonakMarket.Core.Theming;

public class ThemeSwitchedEvent : IEventMessage
{
    public string OldTheme { get; init; }
    public string NewTheme { get; init; }
}