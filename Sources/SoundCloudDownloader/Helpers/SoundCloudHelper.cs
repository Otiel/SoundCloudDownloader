using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SoundCloudDownloader {

    internal static class SoundCloudHelper {
        /// <summary>
        /// Returns the tracks available at the page with the specified source code.
        /// </summary>
        /// <param name="htmlCode">The HTML code of the page.</param>
        /// <returns>The list of available tracks.</returns>
        public static List<Track> GetTracks(String htmlCode) {
            List<String> mp3Urls = GetMediaUrls(htmlCode);
            List<String> titles = GetTitles(htmlCode);
            List<String> artists = GetArtists(htmlCode);

            var tracks = new List<Track>();

            if (mp3Urls.Count != titles.Count || mp3Urls.Count != artists.Count ||
                titles.Count != artists.Count) {
                // We won't be able to assign the artist and the title to every urls we have
                // Create tracks with only the url

                foreach (String mp3Url in mp3Urls) {
                    tracks.Add(new Track("Unknown", "Unknown", mp3Url));
                }
            } else {
                using (var e1 = mp3Urls.GetEnumerator())
                using (var e2 = titles.GetEnumerator())
                using (var e3 = artists.GetEnumerator()) {
                    while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext()) {
                        var mp3Url = e1.Current;
                        var title = e2.Current;
                        var artist = e3.Current;

                        tracks.Add(new Track(artist, title, mp3Url));
                    }
                }
            }

            return tracks;
        }

        private static List<String> GetMediaUrls(String htmlCode) {
            // "streamUrl":"http://media.soundcloud.com/stream/blabla"
            var regex = new Regex(@"""streamUrl"":""(?<url>http://media\.soundcloud\.com/stream/[^""]+)""");

            var urls = new List<String>();
            foreach (Match match in regex.Matches(htmlCode)) {
                urls.Add(match.Groups["url"].Value);
            }

            return urls;
        }

        private static List<String> GetTitles(String htmlCode) {
            // "title":"blabla"
            var regex = new Regex(@"""title"":""(?<title>[^""]+)""");

            var titles = new List<String>();
            foreach (Match match in regex.Matches(htmlCode)) {
                String title = ConvertUnicodeStrings(match.Groups["title"].Value);
                titles.Add(title);
            }

            return titles;
        }

        private static List<String> GetArtists(String htmlCode) {
            // "username":"blabla"
            var regex = new Regex(@"""username"":""(?<artist>[^""]*)""");

            var artists = new List<String>();
            foreach (Match match in regex.Matches(htmlCode)) {
                String artist = ConvertUnicodeStrings(match.Groups["artist"].Value);
                artists.Add(artist);
            }

            return artists;
        }

        private static String ConvertUnicodeStrings(String str) {
            // Unicode characters appear as "_uXXXX" in SoundClound html source.
            // For instance, we have "_u00e9" for 'é'
            // We will replace these "_uXXXX" occurences by the real character
            return Regex.Replace(str, @"\\u([0-9A-Fa-f]{4})", m => ( (char) Convert.ToInt32(m.Groups[1].Value, 16) ).ToString());
        }
    }
}