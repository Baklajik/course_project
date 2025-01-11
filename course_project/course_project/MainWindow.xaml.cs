using System.IO.MemoryMappedFiles;
using System.IO;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Text.RegularExpressions;

namespace course_project
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); 
        }
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string fileExtension = System.IO.Path.GetExtension(openFileDialog.FileName);
                if (fileExtension.ToLower() == ".txt")
                {
                    FilePathTextBox.Text = openFileDialog.FileName;
                }
                else
                {
                    MessageBox.Show("Пожалуйста, выберите текстовый файл (.txt)");
                }
            }
        }

        private async void StartProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = FilePathTextBox.Text;
            if (string.IsNullOrEmpty(filePath))
            {
                MessageBox.Show("Пожалуйста, выберите файл.");
                return;
            }

            Regex regex = new Regex(@"^\d{1,3}(,\d)?$"); 
            if (!regex.IsMatch(StartPercentageTextBox.Text) || !regex.IsMatch(EndPercentageTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите процент в формате до одной десятичной цифры (например, 0,1 или 50,0).");
                return;
            }

            if (!double.TryParse(StartPercentageTextBox.Text, out double startPercentage) || startPercentage < 0 || startPercentage > 100 ||
                !double.TryParse(EndPercentageTextBox.Text, out double endPercentage) || endPercentage < 0 || endPercentage > 100 ||
                startPercentage >= endPercentage)
            {
                MessageBox.Show("Пожалуйста, введите корректные значения диапазона процентов (от 0 до 100) и убедитесь, что начальное значение меньше (и не равно) конечного.");
                return;
            }

            long fileSize = new FileInfo(filePath).Length;
            long startOffset = (long)((fileSize * startPercentage) / 100);
            long endOffset = (long)((fileSize * endPercentage) / 100);
            long rangeSize = endOffset - startOffset;

            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "mappedFile"))
            {
                long[] offsets = new long[4];
                offsets[0] = AlignToUtf8CharacterStart(mmf, startOffset, fileSize);

                for (int i = 1; i <= 3; i++)
                {
                    offsets[i] = AlignToUtf8CharacterStart(mmf, startOffset + (i * (rangeSize / 3)), fileSize);
                }

                Task<string> process1 = Task.Run(() => ReadFromMemoryMappedFile(mmf, offsets[0], offsets[1] - offsets[0]));
                Task<string> process2 = Task.Run(() => ReadFromMemoryMappedFile(mmf, offsets[1], offsets[2] - offsets[1]));
                Task<string> process3 = Task.Run(() => ReadFromMemoryMappedFile(mmf, offsets[2], offsets[3] - offsets[2]));

                string[] results = await Task.WhenAll(process1, process2, process3);
                string finalContent = string.Join(string.Empty, results);

                ShowTextWindow(finalContent, filePath, startOffset);
            }
        }

        static long AlignToUtf8CharacterStart(MemoryMappedFile mmf, long offset, long fileSize)
        {
            if (offset >= fileSize) return fileSize;

            using (var stream = mmf.CreateViewStream(offset > 3 ? offset - 3 : 0, Math.Min(6, fileSize - (offset > 3 ? offset - 3 : 0))))
            {
                byte[] buffer = new byte[6];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < bytesRead; i++)
                {
                    byte currentByte = buffer[i];
                    if ((currentByte & 0b10000000) == 0 ||
                        (currentByte & 0b11100000) == 0b11000000 ||
                        (currentByte & 0b11110000) == 0b11100000 ||
                        (currentByte & 0b11111000) == 0b11110000)
                    {
                        return offset - 3 + i;
                    }
                }

                return offset;
            }
        }

        static async Task<string> ReadFromMemoryMappedFile(MemoryMappedFile mmf, long offset, long size)
        {
            StringBuilder contentBuilder = new StringBuilder();

            using (MemoryMappedViewStream stream = mmf.CreateViewStream(offset, size))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                char[] buffer = new char[4096];
                int charsRead;

                while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    contentBuilder.Append(buffer, 0, charsRead);
                }
            }

            return contentBuilder.ToString();
        }

        private void ShowTextWindow(string content, string filePath, long startOffset)
        {
            TextWindow textWindow = new TextWindow(content, filePath, startOffset);
            textWindow.Show();
        }
    }
}
