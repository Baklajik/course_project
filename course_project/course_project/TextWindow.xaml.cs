using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Windows;

namespace course_project
{
    public partial class TextWindow : Window
    {
        private string _filePath;
        private long _startOffset;

        public TextWindow(string content, string filePath, long startOffset)
        {
            InitializeComponent();
            ContentTextBox.Text = content;
            _filePath = filePath;
            _startOffset = startOffset;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentTextBox.IsReadOnly)
            {
                ContentTextBox.IsReadOnly = false;
                EditButton.Content = "Сохранить";
            }
            else
            {
                ContentTextBox.IsReadOnly = true;
                EditButton.Content = "Редактировать";
                SaveEditedContent(ContentTextBox.Text, _filePath, _startOffset);
            }
        }

        private void SaveEditedContent(string content, string filePath, long startOffset)
        {
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "mappedFile"))
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);

                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(startOffset, contentBytes.Length))
                {
                    accessor.WriteArray(0, contentBytes, 0, contentBytes.Length);
                }
            }
            MessageBox.Show("Изменения сохранены.");
        }
    }
}
