using System.Windows;

namespace windiw_servers
{
    public partial class AddServerDialog : Window
    {
        public string ServerName => ServerNameBox.Text.Trim();
        public string ServerAddress => ServerAddressBox.Text.Trim();
        public int ServerPort => int.TryParse(ServerPortBox.Text.Trim(), out int port) ? port : 8080;
        public string ServerDescription => ServerDescriptionBox.Text.Trim();

        public AddServerDialog()
        {
            InitializeComponent();
            ServerNameBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(ServerName))
            {
                MessageBox.Show("Введите название сервера", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ServerNameBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerAddress))
            {
                MessageBox.Show("Введите адрес сервера", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ServerAddressBox.Focus();
                return false;
            }

            if (!int.TryParse(ServerPortBox.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Введите корректный порт (1-65535)", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ServerPortBox.Focus();
                return false;
            }

            return true;
        }
    }
}