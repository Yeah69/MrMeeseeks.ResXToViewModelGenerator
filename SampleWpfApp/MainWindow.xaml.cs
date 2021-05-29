namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public MainWindow() => InitializeComponent();

        public ICurrentTextsViewModel Localization { get; } = new CurrentTextsViewModel();
    }
}