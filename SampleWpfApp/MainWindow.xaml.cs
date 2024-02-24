namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public CurrentResXTextsViewModel LocalizationResX { get; private set; }
        public CurrentCsvTextsViewModel LocalizationCsv { get; private set; }
        
        public MainWindow()
        {
            LocalizationResX = new CurrentResXTextsViewModel();
            LocalizationCsv = new CurrentCsvTextsViewModel();
            InitializeComponent();
        }
    }
}