using MrMeeseeks.ResXToViewModelGenerator;

namespace SampleMaui
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        
        public ICurrentTextsViewModel CurrentTextsViewModel { get; }

        public MainPage()
        {
            CurrentTextsViewModel = new CurrentTextsViewModel();
            InitializeComponent();
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = string.Format(CurrentTextsViewModel.CurrentTexts.ClickMeCountSingle, count);
            else
                CounterBtn.Text = string.Format(CurrentTextsViewModel.CurrentTexts.ClickMeCountMultiple, count);

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is ITextsOptionViewModel option)
            { 
                CurrentTextsViewModel.CurrentOption = option;
            }
        }
    }
}