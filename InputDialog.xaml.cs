using System.Windows;

namespace ChatGalvanometer
{
    public partial class InputDialog : Window
    {
        public string Value => InputBox.Text.Trim();

        public InputDialog(string prompt)
        {
            InitializeComponent();
            PromptText.Text = prompt;
            Loaded += (_, _) => InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) DialogResult = true;
        }
    }
}
