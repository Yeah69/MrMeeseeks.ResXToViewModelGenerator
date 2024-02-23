namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public CurrentTextsViewModel Localization { get; private set; }
        
        public MainWindow()
        {
            Localization = new CurrentTextsViewModel();
            InitializeComponent();
        }
    }
}