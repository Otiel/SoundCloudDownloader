using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace SoundCloudDownloader {

    /// <summary>
    /// Interaction logic for TrackSelector.xaml
    /// </summary>
    public partial class TrackSelectorWindow: Window {
        private SelectionList<Track> tracks = new SelectionList<Track>();

        public List<Track> SelectedTracks { get; private set; }

        public TrackSelectorWindow(List<Track> tracks) {
            InitializeComponent();
            SelectedTracks = new List<Track>();
            // Bind tracks to listBox
            this.tracks = new SelectionList<Track>(tracks);
            listBoxTracks.ItemsSource = this.tracks;
            // Select all tracks by default
            this.tracks.ForEach(track => track.IsSelected = true);
            this.tracks.PropertyChanged += new PropertyChangedEventHandler(tracks_PropertyChanged);
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e) {
            SelectedTracks.AddRange(
                this.tracks.Where(t => t.IsSelected).Select(t => t.Element));
            DialogResult = true;
            Close();
        }

        private void checkBoxSelectAll_Checked(object sender, RoutedEventArgs e) {
            this.tracks.ForEach(track => track.IsSelected = true);
        }

        private void checkBoxSelectAll_Unchecked(object sender, RoutedEventArgs e) {
            this.tracks.ForEach(track => track.IsSelected = false);
        }

        void tracks_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            int selectedNumber = this.tracks.Where(t => t.IsSelected).Count();
            if (selectedNumber >= this.tracks.Count) {
                // All tracks selected
                checkBoxSelectAll.IsChecked = true;
            } else if (selectedNumber == 0) {
                // No track selected
                checkBoxSelectAll.IsChecked = false;
            } else {
                // Some tracks selected
                checkBoxSelectAll.IsChecked = null;
            }
        }
    }
}