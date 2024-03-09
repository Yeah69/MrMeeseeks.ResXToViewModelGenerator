namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public CurrentResXTextsViewModel LocalizationResX { get; }
        public CurrentCsvTextsViewModel LocalizationCsv { get; }
        public CurrentJsonTextsViewModel LocalizationJson { get; }
        
        public MainWindow()
        {
            LocalizationResX = new CurrentResXTextsViewModel();
            LocalizationCsv = new CurrentCsvTextsViewModel();
            LocalizationJson = new CurrentJsonTextsViewModel();
            InitializeComponent();
        }
    }
}