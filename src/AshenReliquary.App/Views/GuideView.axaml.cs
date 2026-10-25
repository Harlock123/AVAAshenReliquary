using AshenReliquary.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AshenReliquary.App.Views;

/// <summary>
/// The in-game guide: a list of pages on the left, the page on the right, read from the top whenever it changes;
/// Escape goes back, as the Back button does.
/// </summary>
public partial class GuideView : UserControl
{
    public GuideView()
    {
        InitializeComponent();
        PageList.SelectionChanged += (_, _) => PageScroll.Offset = new Vector(0, 0);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && DataContext is GuideViewModel vm)
            {
                vm.BackCommand.Execute(null);
                e.Handled = true;
            }
        };
    }
}
