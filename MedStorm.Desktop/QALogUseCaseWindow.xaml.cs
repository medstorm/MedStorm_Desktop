using System.Windows;

namespace MedStorm.Desktop
{
    public partial class QALogUseCaseWindow : Window
    {
        public QALogUseCase? SelectedUseCase { get; private set; }

        public QALogUseCaseWindow()
        {
            InitializeComponent();
        } 

        private void UseCase_Click(object sender, RoutedEventArgs e)
        {
            var tag = ((FrameworkElement)sender).Tag as string;
            SelectedUseCase = tag switch
            {
                "PretermInfants" => QALogUseCase.PretermInfants,
                "ICU" => QALogUseCase.ICU,
                "OperatingTheatre" => QALogUseCase.OperatingTheatre,
                _ => null
            };
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
