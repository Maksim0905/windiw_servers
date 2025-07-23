using System.Windows;

namespace windiw_servers
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LoadSettings()
        {
            // В реальном приложении здесь будет загрузка из конфига
            // Пока используем значения по умолчанию
        }

        private void SaveSettings()
        {
            // В реальном приложении здесь будет сохранение в конфиг
            MessageBox.Show("Настройки сохранены!", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}