namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            new CurrentTextsViewModel().CurrentTexts.AsSettable().Foo = "buz";
            //var asdf = new CurrentTextsViewModel()
            //var blah = asdf;
            //Test.Text = blah;
        }
    }
}