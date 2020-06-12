using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VoiceMaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int desiredAge;

        public int DesiredAge { get { return desiredAge; } set { desiredAge = value; OnPropertyChanged(); } }

        public string SaveLocation { get { return Properties.Settings.Default.saveloc; } set { Properties.Settings.Default.saveloc = value; OnPropertyChanged(); } }

        private SpeechSynthesizer speechSynth;

        private Prompt speechPrompt;

        private VoiceInfo[] availableVoices;

        public VoiceInfo[] AvailableVoices { get { return availableVoices; } set { availableVoices = value; OnPropertyChanged(); } }

        private int selectedVoiceIndex;

        private bool canSpeak;

        private string path;

        private double generationProgress;

        private bool synthDisposed;

        public double GenerationProgress { get { return generationProgress; } set { generationProgress = value; OnPropertyChanged(); } }

        public bool CanSpeak { get { return canSpeak; } set { canSpeak = value; OnPropertyChanged(); } }

        public int SelectedVoiceIndex { get { return selectedVoiceIndex; }set { selectedVoiceIndex = value; OnPropertyChanged(); } }

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            if (Properties.Settings.Default.saveloc == "<unset>")
            {
                try
                {
                    var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NileVoices");
                    System.IO.Directory.CreateDirectory(folder);
                    SaveLocation = folder;
                }
                catch (Exception e)
                {
                    Info("Could not create the default folder. Reason: " + e.Message);
                }
            }

            speechSynth = new SpeechSynthesizer();

            AvailableVoices = (from voice in speechSynth.GetInstalledVoices() where voice.Enabled == true select voice.VoiceInfo).ToArray();
            if(AvailableVoices.Length < 1)
            {
                Info("There are no voices available. The program will not be able to accomplish anything and will now exit. Get some voices and try again.");
                Close();
            }
            SelectedVoiceIndex = 0;
            CanSpeak = true;
            speechSynth.SpeakCompleted += SpeechSynth_SpeakCompleted;
            speechSynth.SpeakProgress += SpeechSynth_SpeakProgress;
        }

        private void SpeechSynth_SpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            GenerationProgress = (double)e.CharacterPosition / textBox.Text.Length;
        }

        private void SpeechSynth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            CanSpeak = true;
            genBtn.Content = "Generate file";

            if (!e.Cancelled && e.Error != null)
            {
                Info("Speech generation stopped. Reason: " + e.Error.Message);
            }

            if(!synthDisposed && speechPrompt.IsCompleted)
                speechSynth.SetOutputToNull();

            if(!e.Cancelled)
                GenerationProgress = 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void Info(object o)
        {
            MessageBox.Show(o.ToString(), "Nile Voice", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //gen wav file click
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            GenerationProgress = 0;

            if(CanSpeak)
            {
                if (speechPrompt != null && !speechPrompt.IsCompleted)
                {
                    speechSynth.SpeakAsyncCancel(speechPrompt);
                }

                while (speechPrompt != null && !speechPrompt.IsCompleted)
                    await Task.Delay(10);

                CanSpeak = false;

                btn.Content = "Stop generation";

                if (speechPrompt != null && !speechPrompt.IsCompleted)
                {
                    speechSynth.SpeakAsyncCancel(speechPrompt);
                }

                while (speechPrompt != null && !speechPrompt.IsCompleted)
                    await Task.Delay(10);

                speechSynth.SelectVoice(AvailableVoices[SelectedVoiceIndex].Name);

                try
                {
                    path = System.IO.Path.Combine(
                        SaveLocation,
                        "NileVoice_" + Directory.GetFiles(SaveLocation, "NileVoice_*.wav").Count() + ".wav");

                    speechSynth.SetOutputToWaveFile(path);
                    speechPrompt = speechSynth.SpeakAsync(textBox.Text);
                }
                catch (Exception ex)
                {
                    Info("Writing the speech on disk failed. Reason: " + ex.Message);
                }
            }
            else
            {
                btn.Content = "Generate file";
                CanSpeak = true;

                if (speechPrompt != null && !speechPrompt.IsCompleted)
                {
                    speechSynth.SpeakAsyncCancel(speechPrompt);
                }

                while (speechPrompt != null && !speechPrompt.IsCompleted)
                    await Task.Delay(10);

                try
                {
                    File.Delete(path);
                }
                catch(Exception ex)
                {
                    Info("Deleting the file after generation cancellation failed. Reason: " + ex.Message);
                }
            }
        }

        //open saves click
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", SaveLocation);
            }
            catch (Exception ex)
            {
                Info("Opening the saveed location failed. Reason: " + ex.Message);
            }
        }

        //set save folder click
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "NileVoice save location selector";
            if(dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SaveLocation = dlg.SelectedPath;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (speechPrompt != null && !speechPrompt.IsCompleted)
            {
                speechSynth.SpeakAsyncCancel(speechPrompt);
            }

            while (speechPrompt != null && !speechPrompt.IsCompleted)
                Task.Delay(10);

            synthDisposed = true;
            speechSynth.Dispose();
            Properties.Settings.Default.Save();
        }

        //speak text click
        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            GenerationProgress = 0;

            if (speechPrompt != null && !speechPrompt.IsCompleted)
            {
                speechSynth.SpeakAsyncCancel(speechPrompt);
            }

            while (speechPrompt != null && !speechPrompt.IsCompleted)
                await Task.Delay(10);

            speechSynth.SelectVoice(AvailableVoices[SelectedVoiceIndex].Name);
            speechSynth.SetOutputToDefaultAudioDevice();
            speechPrompt = speechSynth.SpeakAsync(textBox.Text);
        }

        //detect available voices
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            AvailableVoices = (from voice in speechSynth.GetInstalledVoices() where voice.Enabled == true select voice.VoiceInfo).ToArray();
            if (AvailableVoices.Length < 1)
            {
                Info("There are no voices available. The program will not be able to accomplish anything and will now exit. Get some voices and try again.");
                Close();
            }

            SelectedVoiceIndex = 0;
        }

        //speack text double click
        private void Button_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(CanSpeak)
            {
                speechSynth.SpeakAsyncCancel(speechPrompt);
                e.Handled = true;
            }
        }
    }
}
