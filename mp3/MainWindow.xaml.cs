using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using Windows.Media.Protection.PlayReady;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace mp3
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            folder = folderManager.LoadLastFolderPath();
            if (!string.IsNullOrEmpty(folder))
            {
                Read(folder);
                displayLabel.Content = folder;
            }
            else
            {
                folder = string.Empty;
                displayLabel.Content = "Select Folder";
            }
            LoadFolders();
        }

        private FolderManager folderManager = new FolderManager();
        private string folder;
        private string[] songs;
        private int counter = 0;
        private float volumetypehi = 0.5f;

        private IWavePlayer waveOutDevice;
        private AudioFileReader audioFileReader;

        private bool manualclick = false;
        private bool stopcheck = false;


        private void VolumeChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            float sliderValue = (float)e.NewValue / 100;
            if (audioFileReader != null)
            {
                Trace.WriteLine("slider:" + volumetypehi);
                volumetypehi = sliderValue;
                audioFileReader.Volume = sliderValue;
            }

        }

        private void LoadFolders()
        {
            List<string> folders = folderManager.LoadFolders();
            folderListBox.Items.Clear();

            foreach (var folder in folders)
            {
                var listBoxItem = new ListBoxItem
                {
                    Content = folder
                };

                listBoxItem.MouseLeftButtonUp += (s, e) => OpenFolder(folder);

                var contextMenu = new ContextMenu();
                var deleteMenuItem = new MenuItem { Header = "Delete" };

                deleteMenuItem.Click += (s, e) => DeleteFolder(folder);
                contextMenu.Items.Add(deleteMenuItem);

                listBoxItem.ContextMenu = contextMenu;

                folderListBox.Items.Add(listBoxItem);
            }
        }

        private void OpenFolder(string folderPath)
        {
            Read(folderPath);
            mainTabControl.SelectedIndex = 0;
        }

        private void DeleteFolder(string folderPath)
        {
            folderManager.RemoveFolder(folderPath); 
            LoadFolders();
        }

        private void AddFolderBtn(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("Select folder");
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                displayLabel.Content = dialog.FileName;
                folder = dialog.FileName;
                folderManager.AddFolder(folder);
                LoadFolders();
                Read(folder);

                if (songs.Length == 0)
                {
                    displayLabel.Content = "No MP3 files found in the selected folder.";
                }
            }
        }

        private void Read(string folder)
        {
            songs = Directory.GetFiles(folder, "*.mp3");
            counter = 0;
            displayLabel.Content = folder;
        }

        private void Play()
        {
            Trace.WriteLine("Play button pressed");

            if (songs != null && songs.Length > 0)
            {
                try
                {
                    StopPlayback();

                    audioFileReader = new AudioFileReader(songs[counter]);
                    Trace.WriteLine("playbut:" + volumetypehi);
                    audioFileReader.Volume = volumetypehi;

                    waveOutDevice = new WaveOutEvent();

                    waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
                    waveOutDevice.PlaybackStopped += OnPlaybackStopped;

                    waveOutDevice.Init(audioFileReader);
                    waveOutDevice.Play();

                    displayLabel.Content = Path.GetFileName(songs[counter]);
                    Trace.WriteLine("should be after Call Event");

                    stopcheck = true;
                    play.Content = "Pause";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("error playing audio: " + ex.Message);
                }
            }
            else
            {
                displayLabel.Content = "Select folder";
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MessageBox.Show($"error niggy: {e.Exception.Message}");
            }
            else
            {
                //Trace.WriteLine("Call Event");
                if (!manualclick)
                {
                    Trace.WriteLine("AUTOMATIC");
                    Next();
                }
                else if (manualclick) {
                    Trace.WriteLine("MANUAL");
                    manualclick = false;
                }
            }
        }

        private void StopPlayback()
        {
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
                waveOutDevice = null;
            }

            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }
        }

        private void Next()
        {
            Trace.WriteLine("Next");
            counter++;
            if (counter == songs.Length)
            {
                counter = 0;
            }
            Play();
        }

        private void Prev()
        {
            Trace.WriteLine("Previous");
            counter--;
            if (counter == -1)
            {
                counter = songs.Length - 1;
            }
            Play();
        }

        private void NextBtn(object sender, RoutedEventArgs e)
        {
            manualclick = true;
            Next();
        }

        private void PrevBtn(object sender, RoutedEventArgs e)
        {
            manualclick = true;
            Prev();
        }

        private void PlayBtn(object sender, RoutedEventArgs e)
        {
            manualclick = true;
            Play();
        }

        private void ShuffleBtn(object sender, RoutedEventArgs e)
        {
            if (songs != null && songs.Length > 1)
            {
                manualclick = true;
                Trace.WriteLine("test");
                Random rng = new Random();
                rng.Shuffle(songs);
                counter = 0;
                Play();
            }
        }

    }
    public static class Extensions
    {
        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }

    public class FolderData
    {
        public string LastFolderPath { get; set; }
        public List<string> Folders { get; set; } = new List<string>();
    }

    public class FolderManager
    {
        private static readonly string FolderDataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mp3app",
            "folderData.json"
        );

        public FolderManager()
        {
            var directory = Path.GetDirectoryName(FolderDataFile);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void SaveFolderData(FolderData folderData)
        {
            var json = JsonSerializer.Serialize(folderData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FolderDataFile, json);
        }

        public FolderData LoadFolderData()
        {
            if (File.Exists(FolderDataFile))
            {
                var json = File.ReadAllText(FolderDataFile);
                return JsonSerializer.Deserialize<FolderData>(json) ?? new FolderData();
            }
            return new FolderData();
        }

        public void SaveLastFolderPath(string path)
        {
            var folderData = LoadFolderData();
            folderData.LastFolderPath = path;
            SaveFolderData(folderData);
        }

        public string LoadLastFolderPath()
        {
            var folderData = LoadFolderData();
            return folderData.LastFolderPath ?? string.Empty;
        }

        public List<string> LoadFolders()
        {
            var folderData = LoadFolderData();
            return folderData.Folders ?? new List<string>();
        }

        public void AddFolder(string path)
        {
            var folderData = LoadFolderData();

            folderData.LastFolderPath = path;

            if (!folderData.Folders.Contains(path))
            {
                folderData.Folders.Add(path);
            }

            SaveFolderData(folderData);
        }

        public void RemoveFolder(string path)
        {
            var folderData = LoadFolderData();

            if (folderData.Folders.Contains(path))
            {
                folderData.Folders.Remove(path);

                if (folderData.LastFolderPath == path)
                {
                    folderData.LastFolderPath = string.Empty;
                }

                SaveFolderData(folderData);
            }
        }
    }
}