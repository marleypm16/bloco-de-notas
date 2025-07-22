using System.Windows;

namespace NotePad
{
    public partial class FindDialog : Window
    {
        private MainWindow parentWindow;

        public FindDialog(MainWindow parent)
        {
            InitializeComponent();
            parentWindow = parent;
            Owner = parent;
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            string searchText = searchTextBox.Text;
            if (!string.IsNullOrEmpty(searchText))
            {
                bool matchCase = matchCaseCheckBox.IsChecked ?? false;
                parentWindow.FindText(searchText, matchCase);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnActivated(System.EventArgs e)
        {
            base.OnActivated(e);
            searchTextBox.Focus();
            searchTextBox.SelectAll();
        }
    }
}