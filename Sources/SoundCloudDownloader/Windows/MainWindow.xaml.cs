using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace SoundCloudDownloader {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window {

        #region Fields

        /// <summary>
        /// The files to download, or being downloaded, or downloaded.
        /// Used to compute the current received bytes and the total bytes to download.
        /// </summary>
        private List<File> filesDownload;

        /// <summary>
        /// Used to compute and display the download speed.
        /// </summary>
        private DateTime lastDownloadSpeedUpdate;

        /// <summary>
        /// Used to compute and display the download speed.
        /// </summary>
        private long lastTotalReceivedBytes = 0;

        /// <summary>
        /// Used when user clicks on 'Cancel' to abort all current downloads.
        /// </summary>
        private List<WebClient> pendingDownloads;

        /// <summary>
        /// Used when user clicks on 'Cancel' to manage the cancelation (UI...).
        /// </summary>
        private Boolean userCancelled;
        #endregion Fields

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            // Increase the maximum of concurrent connections to be able to download more than 2
            // (which is the default value) files at the same time
            ServicePointManager.DefaultConnectionLimit = 50;
            // Default options
            textBoxDownloadsLocation.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\SC tracks\";
            // Hints
            textBoxUrls.Text = Constants.UrlsHint;
            textBoxUrls.Foreground = new SolidColorBrush(Colors.DarkGray);
            // Version
            labelVersion.Content = "v " + Assembly.GetEntryAssembly().GetName().Version;
        }

        #endregion Constructor

        #region Methods
        private void DownloadAndTagTrack(String downloadDirPath, Track track, Boolean tagTrack) {
            // Set location to save the file
            String trackPath = downloadDirPath + track.GetFileName();

            var doneEvent = new AutoResetEvent(false);

            using (var webClient = new WebClient()) {
                // Update progress bar when downloading
                webClient.DownloadProgressChanged += (s, e) => {
                    UpdateProgress(track.Mp3Url, e.BytesReceived);
                };

                webClient.DownloadFileCompleted += (s, e) => {
                    if (!e.Cancelled && e.Error == null) {
                        if (tagTrack) {
                            // Tag (ID3) the file when downloaded
                            TagLib.File tagFile = TagLib.File.Create(trackPath);
                            tagFile.Tag.Performers = new String[1] { track.Artist };
                            tagFile.Tag.Title = track.Title;
                            tagFile.Save();
                        }

                        Log("Downloaded track \"" + track.GetFileName() + "\"", Brushes.Green);
                    } else if (!e.Cancelled && e.Error != null) {
                        Log("Unable to download the track \"" + track.GetFileName() + "\"", Brushes.Red);
                    } // Else the download has been cancelled (by the user)

                    doneEvent.Set();
                };

                lock (this.pendingDownloads) {
                    if (this.userCancelled) {
                        // Abort
                        return;
                    }
                    // Register current download
                    this.pendingDownloads.Add(webClient);
                    // Start download
                    webClient.DownloadFileAsync(new Uri(track.Mp3Url), trackPath);
                }
            }

            // Wait for download to be finished
            doneEvent.WaitOne();
        }

        private List<File> GetFilesToDownload(List<Track> tracks) {
            var files = new List<File>();
            foreach (Track track in tracks) {
                if (this.userCancelled) {
                    // Abort
                    return new List<File>();
                }

                Log("Computing size for track \"" + track.Title + "\"", Brushes.Black);

                long size = 0;
                try {
                    size = FileHelper.GetFileSize(track.Mp3Url, "HEAD");
                } catch {
                    Log("Failed to retrieve the size of the MP3 file for the track \"" + track.Title +
                        "\". Progress update may be wrong.", Brushes.OrangeRed);
                }

                files.Add(new File(track.Mp3Url, 0, size));
            }
            return files;
        }

        private List<Track> GetTracks(List<String> urls, Boolean getOnlyMainTrack) {
            var tracks = new List<Track>();

            foreach (String url in urls) {
                if (this.userCancelled) {
                    // Abort
                    return new List<Track>();
                }

                Log("Retrieving tracks on " + url, Brushes.Black);

                // Retrieve URL HTML source code
                String htmlCode = "";
                using (var webClient = new WebClient() { Encoding = Encoding.UTF8 }) {
                    try {
                        htmlCode = webClient.DownloadString(url);
                    } catch {
                        Log("Could not retrieve tracks on " + url, Brushes.Red);
                        continue;
                    }
                }

                // Get info on tracks
                try {
                    List<Track> tracksOnPage = SoundCloudHelper.GetTracks(htmlCode);
                    if (getOnlyMainTrack) {
                        tracks.Add(tracksOnPage.First());
                    } else {
                        tracks.AddRange(tracksOnPage);
                    }
                } catch {
                    Log("Could not retrieve tracks on " + url, Brushes.Red);
                    continue;
                }
            }

            return tracks;
        }
        /// <summary>
        /// Displays the specified message in the log.
        /// </summary>
        /// <param name="message">The message.</param>
        private void Log(String message, Brush color) {
            this.Dispatcher.Invoke(new Action(() => {
                // Time
                var textRange = new TextRange(richTextBoxLog.Document.ContentEnd, richTextBoxLog.Document.ContentEnd);
                textRange.Text = DateTime.Now.ToString("HH:mm:ss") + " ";
                textRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Gray);
                // Message
                textRange = new TextRange(richTextBoxLog.Document.ContentEnd, richTextBoxLog.Document.ContentEnd);
                textRange.Text = message;
                textRange.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                // Line break
                richTextBoxLog.ScrollToEnd();
                richTextBoxLog.AppendText(Environment.NewLine);
            }));
        }

        /// <summary>
        /// Updates the state of the controls.
        /// </summary>
        /// <param name="downloadStarted">True if the download just started, false if it just
        /// stopped.</param>
        private void UpdateControlsState(Boolean downloadStarted) {
            this.Dispatcher.Invoke(new Action(() => {
                if (downloadStarted) {
                    // We just started the download
                    richTextBoxLog.Document.Blocks.Clear();
                    labelProgress.Content = "";
                    progressBar.IsIndeterminate = true;
                    progressBar.Value = progressBar.Minimum;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                    TaskbarItemInfo.ProgressValue = 0;
                    buttonStart.IsEnabled = false;
                    buttonStop.IsEnabled = true;
                    buttonBrowse.IsEnabled = false;
                    textBoxUrls.IsReadOnly = true;
                    textBoxDownloadsLocation.IsReadOnly = true;
                    checkBoxTag.IsEnabled = false;
                    checkBoxOneTrackAtATime.IsEnabled = false;
                    checkBoxDownloadOnlyMainTrack.IsEnabled = false;
                } else {
                    // We just finished the download (or user has cancelled)
                    buttonStart.IsEnabled = true;
                    buttonStop.IsEnabled = false;
                    buttonBrowse.IsEnabled = true;
                    textBoxUrls.IsReadOnly = false;
                    progressBar.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#FF01D328")); // Green
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = progressBar.Minimum;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TaskbarItemInfo.ProgressValue = 0;
                    textBoxDownloadsLocation.IsReadOnly = false;
                    checkBoxTag.IsEnabled = true;
                    checkBoxOneTrackAtATime.IsEnabled = true;
                    labelDownloadSpeed.Content = "";
                    checkBoxDownloadOnlyMainTrack.IsEnabled = true;
                }
            }));
        }

        /// <summary>
        /// Updates the progress messages and the progressbar.
        /// </summary>
        /// <param name="fileUrl">The URL of the file that just progressed.</param>
        /// <param name="bytesReceived">The received bytes for the specified file.</param>
        private void UpdateProgress(String fileUrl, long bytesReceived) {
            DateTime now = DateTime.Now;

            lock (this.filesDownload) {
                // Compute new progress values
                File currentFile = this.filesDownload.Where(f => f.Url == fileUrl).First();
                currentFile.BytesReceived = bytesReceived;
                long totalReceivedBytes = this.filesDownload.Sum(f => f.BytesReceived);
                long bytesToDownload = this.filesDownload.Sum(f => f.Size);

                Double bytesPerSecond;
                if (this.lastTotalReceivedBytes == 0) {
                    // First time we update the progress
                    bytesPerSecond = 0;
                    this.lastTotalReceivedBytes = totalReceivedBytes;
                    this.lastDownloadSpeedUpdate = now;
                } else if (( now - this.lastDownloadSpeedUpdate ).TotalMilliseconds > 500) {
                    // Last update of progress happened more than 500 milliseconds ago
                    // We only update the download speed every 500+ milliseconds
                    bytesPerSecond =
                        ( (Double) ( totalReceivedBytes - this.lastTotalReceivedBytes ) ) /
                        ( now - this.lastDownloadSpeedUpdate ).TotalSeconds;
                    this.lastTotalReceivedBytes = totalReceivedBytes;
                    this.lastDownloadSpeedUpdate = now;

                    // Update UI
                    this.Dispatcher.Invoke(new Action(() => {
                        // Update download speed
                        labelDownloadSpeed.Content = ( bytesPerSecond / 1024 ).ToString("0.0") + " kB/s";
                    }));
                }

                // Update UI
                this.Dispatcher.Invoke(new Action(() => {
                    if (!this.userCancelled) {
                        // Update progress label
                        labelProgress.Content =
                            ( (Double) totalReceivedBytes / ( 1024 * 1024 ) ).ToString("0.00") + " MB / " +
                            ( (Double) bytesToDownload / ( 1024 * 1024 ) ).ToString("0.00") + " MB";
                        // Update progress bar
                        progressBar.Value = totalReceivedBytes;
                        // Taskbar progress is between 0 and 1
                        TaskbarItemInfo.ProgressValue = totalReceivedBytes / progressBar.Maximum;
                    }
                }));
            }
        }

        #endregion Methods

        #region Events
        private void buttonBrowse_Click(object sender, RoutedEventArgs e) {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select the folder to save tracks";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                textBoxDownloadsLocation.Text = dialog.SelectedPath;
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            if (textBoxUrls.Text == Constants.UrlsHint) {
                // No URL to look
                Log("Paste some SoundCloud URLs containing tracks to be downloaded", Brushes.Red);
                return;
            }

            this.userCancelled = false;

            // Get options
            Boolean tagTracks = checkBoxTag.IsChecked.Value;
            Boolean oneTrackAtATime = checkBoxOneTrackAtATime.IsChecked.Value;
            Boolean onlyMainTrack = checkBoxDownloadOnlyMainTrack.IsChecked.Value;
            String downloadsFolder = textBoxDownloadsLocation.Text;
            this.pendingDownloads = new List<WebClient>();

            // Set controls to "downloading..." state
            UpdateControlsState(true);

            Log("Starting download...", Brushes.Black);

            // Get user inputs
            List<String> urls = textBoxUrls.Text.Split(new String[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries).ToList();
            urls = urls.Distinct().ToList();

            var tracks = new List<Track>();

            Task.Factory.StartNew(() => {
                // Get info on albums
                tracks = GetTracks(urls, onlyMainTrack);
            }).ContinueWith(x => {
                // Create directory to place track files
                String directoryPath = downloadsFolder + "\\";
                try {
                    Directory.CreateDirectory(directoryPath);
                } catch {
                    Log("An error occured when creating the album folder. Make sure you have " +
                        "the rights to write files in the folder you chose", Brushes.Red);
                    return;
                }
            }).ContinueWith(x => {
                if (!this.userCancelled) {
                    // Ask the user to select the tracks to download
                    var trackSelector = new TrackSelectorWindow(tracks) { Owner = this };
                    Boolean? validated = trackSelector.ShowDialog();
                    tracks = trackSelector.SelectedTracks;
                    if (!validated.Value) {
                        Log("Downloads cancelled by user", Brushes.Black);
                    } else if (validated.Value && trackSelector.SelectedTracks.Count == 0) {
                        Log("No track to download selected", Brushes.Black);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()
            ).ContinueWith(x => {
                // Save files to download (we'll need the list to update the progressBar)
                this.filesDownload = GetFilesToDownload(tracks);
            }).ContinueWith(x => {
                // Set progressBar max value
                long bytesToDownload = this.filesDownload.Sum(f => f.Size);
                if (bytesToDownload > 0) {
                    this.Dispatcher.Invoke(new Action(() => {
                        progressBar.IsIndeterminate = false;
                        progressBar.Maximum = bytesToDownload;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    }));
                }
            }).ContinueWith(x => {
                // Start downloading tracks
                if (oneTrackAtATime) {
                    // Download one track at a time
                    foreach (Track track in tracks) {
                        DownloadAndTagTrack(downloadsFolder, track, tagTracks);
                    }
                } else {
                    // Parallel download
                    Task[] tasks = new Task[tracks.Count];
                    for (int i = 0; i < tracks.Count; i++) {
                        Track track = tracks[i]; // Mandatory or else => race condition
                        tasks[i] = Task.Factory.StartNew(() => DownloadAndTagTrack(downloadsFolder, track, tagTracks));
                    }
                    // Wait for all tracks to be downloaded
                    Task.WaitAll(tasks);
                }
            }).ContinueWith(x => {
                if (this.userCancelled) {
                    // Display message if user cancelled
                    Log("Downloads cancelled by user", Brushes.Black);
                }
                // Set controls to "ready" state
                UpdateControlsState(false);
                // Play a sound
                try {
                    ( new SoundPlayer(@"C:\Windows\Media\Windows Ding.wav") ).Play();
                } catch {
                }
            });
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            Log("Cancelling downloads. Please wait...", Brushes.Black);
            buttonStop.IsEnabled = false;
            progressBar.Foreground = System.Windows.Media.Brushes.Red;
            progressBar.IsIndeterminate = true;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            TaskbarItemInfo.ProgressValue = 0;
            lock (this.pendingDownloads) {
                this.userCancelled = true;
                // Stop current downloads
                foreach (WebClient webClient in this.pendingDownloads) {
                    webClient.CancelAsync();
                }
            }
        }

        private void labelVersion_MouseDown(object sender, MouseButtonEventArgs e) {
            Process.Start(Constants.ProjectWebsite);
        }

        private void textBoxUrls_GotFocus(object sender, RoutedEventArgs e) {
            if (textBoxUrls.Text == Constants.UrlsHint) {
                // Erase the hint message
                textBoxUrls.Text = "";
                textBoxUrls.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void textBoxUrls_LostFocus(object sender, RoutedEventArgs e) {
            if (textBoxUrls.Text == "") {
                // Show the hint message
                textBoxUrls.Text = Constants.UrlsHint;
                textBoxUrls.Foreground = new SolidColorBrush(Colors.DarkGray);
            }
        }

        #endregion Events
    }
}