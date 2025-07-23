using System.Windows;
using System.Windows.Controls;

namespace windiw_servers
{
    public partial class NewScriptDialog : Window
    {
        public string ScriptName => ScriptNameBox.Text.Trim();
        public string ScriptType => ((ComboBoxItem)ScriptTypeComboBox.SelectedItem).Content.ToString()!.StartsWith("JavaScript") ? "JavaScript" : "PowerShell";

        public NewScriptDialog()
        {
            InitializeComponent();
            ScriptNameBox.Focus();
            ScriptNameBox.SelectAll();
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

        private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateComboBox.SelectedIndex > 0)
            {
                var templateName = ((ComboBoxItem)TemplateComboBox.SelectedItem).Content.ToString()!;
                var scriptType = ScriptType.ToLower();
                
                var suggestedName = templateName switch
                {
                    "Системная информация" => scriptType == "javascript" ? "system_info" : "SystemInfo",
                    "Управление процессами" => scriptType == "javascript" ? "process_manager" : "ProcessManager",
                    "Работа с файлами" => scriptType == "javascript" ? "file_operations" : "FileOperations",
                    _ => "new_script"
                };
                
                ScriptNameBox.Text = suggestedName;
                ScriptNameBox.SelectAll();
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(ScriptName))
            {
                MessageBox.Show("Введите имя скрипта", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ScriptNameBox.Focus();
                return false;
            }

            // Проверяем на недопустимые символы в имени файла
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (ScriptName.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.Show("Имя файла содержит недопустимые символы", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ScriptNameBox.Focus();
                return false;
            }

            return true;
        }
    }
}