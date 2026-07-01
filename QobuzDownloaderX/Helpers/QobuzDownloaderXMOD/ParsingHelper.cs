using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QobuzDownloaderX.Helpers.QobuzDownloaderXMOD
{
    // Inspired by QobuzDownloaderX-MOD source-code.
    // https://github.com/DJDoubleD/QobuzDownloaderX-MOD
    internal sealed class ParsingHelper
    {
        public static string primaryListSeparator = ", ";
        public static string listEndSeparator = " & ";

        // Max artists rendered in a file NAME before the tail collapses into
        // "& others". Compilation tracks can list 20+ artists which, joined into
        // the filename, blow past Windows MAX_PATH (260 chars) and make the file
        // unopenable in players that aren't long-path-aware. Only the file name is
        // capped (GetTrackPerformersName); the ARTIST tag uses the full
        // GetTrackPerformersArray, so every artist is still written to the tag.
        // Parity with the tiddl / OrpheusDL / deemix forks.
        public static int maxArtistsInName = 3;
        public static string othersSuffix = " & others";

        private static readonly Regex unicodeRegex = new Regex(@"\\u(?<Value>[0-9A-Fa-f]{4})", RegexOptions.Compiled );

        /// <summary>
        /// Get the Artist names with given role as an array
        /// </summary>
        /// <param name="artists"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public static string[] GetArtistNames(List<QopenAPI.ArtistsList> artists, InvolvedPersonRoleType role)
        {
            return artists.Where(artist => artist.Roles.Exists(roleString => InvolvedPersonRoleMapping.GetRoleByString(roleString) == role))
                          .Select(artist => artist.Name)
                          .ToArray();
        }

        // tiddl / Orpheus / deemix parity: the canonical artist order is sorted(MAIN) + sorted(FEATURED),
        // sorted case-sensitively (Ordinal) to match Python's default sorted(). Deterministic across tools.
        public static string[] SortArtists(string[] artists)
        {
            if (artists == null || artists.Length == 0)
                return new string[0];

            string[] sorted = (string[])artists.Clone();
            Array.Sort(sorted, StringComparer.Ordinal);
            return sorted;
        }

        public static string MergeFeaturedArtistsWithMainArtists(string[] mainArtists, string[] featuresArtists)
        {
            string[] sortedMain = SortArtists(mainArtists);

            if (featuresArtists == null || featuresArtists.Length == 0)
                return MergeDoubleDelimitedList(sortedMain, primaryListSeparator, listEndSeparator);

            string[] allArtists = sortedMain.Concat(SortArtists(featuresArtists)).ToArray();
            return MergeDoubleDelimitedList(allArtists, primaryListSeparator, listEndSeparator);
        }

        // https://github.com/DJDoubleD/QobuzDownloaderX-MOD/blob/993c708f594faaab36ca4b3a97e4a7b84676ecf2/QobuzDownloaderX/Shared/Tools/StringTools.cs#L81
        public static string MergeDoubleDelimitedList(string[] stringList, string initialDelimiter, string finalDelimiter)
        {
            if (stringList != null)
            {
                string result;
                if (stringList.Length > 1)
                {
                    result = string.Join(initialDelimiter, stringList.Take(stringList.Length - 1)) + finalDelimiter + stringList.LastOrDefault();
                }
                else
                {
                    result = stringList.FirstOrDefault();
                }

                return DecodeEncodedNonAsciiCharacters(result);
            }
            else
            {
                return "";
            }
        }

        // https://github.com/DJDoubleD/QobuzDownloaderX-MOD/blob/993c708f594faaab36ca4b3a97e4a7b84676ecf2/QobuzDownloaderX/Shared/Tools/StringTools.cs#L16C16-L16C22
        /// <summary>
        /// Decodes the encoded non ascii characters.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The decoded string.</returns>
        public static string DecodeEncodedNonAsciiCharacters(string value)
        {
            if (value == null)
                return null;

            return unicodeRegex.Replace(
                value,
                m => ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString()
            );
        }

        // Adapted by ElektroStudios from QobuzDownloaderX-MOD's source-code to use a QopenAPI.Item object.
        // Also, now it handles cases where the track title already contains " Feat. "-like words (case-insensitive)
        // and where the performer name is a composed name that already contains " Feat " word.
        // (i.e., does not add featured artists names to the resulting string.)
        // Returns the canonical, ordered list of track artists = sorted(MAIN) + sorted(FEATURED),
        // applying the same "feat. already in title" handling used for the merged name.
        // Used for the multi-value ARTIST tag (one entry per artist) — tiddl / Orpheus parity.
        public static string[] GetTrackPerformersArray(QopenAPI.Item QoItem)
        {
            PerformersParser performersParser = new PerformersParser(QoItem);

            // Get main and featured performers
            string[] mainPerformers = performersParser.GetPerformersWithRole(InvolvedPersonRoleType.MainArtist);
            string[] featuredPerformers = performersParser.GetPerformersWithRole(InvolvedPersonRoleType.FeaturedArtist);

            string title = QoItem.Title ?? "";

            string[] featPatterns = {
                "featuring ", " ft.",
                "(feat ", "(feat.",
                "[feat ", "[feat.",
                " feat ", " feat. ",
                "[ft ", "[ft.",
                "(ft ", "(ft."
            };
            // Note: using multiple IndexOf calls instead of Regex is preferable here for performance.
            bool hasFeat = featPatterns.Any(p => title.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

            if (hasFeat)
            {
                // If the title already contains "feat."-like word, set the featuredPerformers to null.
                featuredPerformers = null;

                // Also, remove any main artists that appear in the track title, except the first main artist.
                // Case: Qobuz API returns the featured artists as "Main Artist".
                if (mainPerformers != null && mainPerformers.Length > 1)
                {
                    string titleNorm = PerformersParser.Normalize(QoItem.Title);

                    // Keep the first main artist.
                    string firstArtist = mainPerformers[0];

                    // Filter the rest
                    string[] filteredArtists = mainPerformers
                        .Skip(1)
                        .Where(mp => titleNorm.IndexOf(PerformersParser.Normalize(mp), StringComparison.OrdinalIgnoreCase) < 0)
                        .ToArray();

                    // Combine first artist with the filtered rest
                    mainPerformers = new[] { firstArtist }.Concat(filteredArtists).ToArray();
                }
            }

            // Canonical order: sorted(MAIN) + sorted(FEATURED)
            string[] ordered = SortArtists(mainPerformers).Concat(SortArtists(featuredPerformers)).ToArray();

            return ordered
                .Select(DecodeEncodedNonAsciiCharacters)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToArray();
        }

        public static string GetTrackPerformersName(QopenAPI.Item QoItem)
        {
            // Build the merged name from the same canonical, ordered artist list used for the tag,
            // so the file name and the ARTIST tag are always consistent.
            string[] performers = GetTrackPerformersArray(QoItem);
            // Cap the NAME only (tag path uses the full array): first N + "& others".
            string trackArtists = performers.Length > maxArtistsInName
                ? string.Join(primaryListSeparator, performers.Take(maxArtistsInName)) + othersSuffix
                : MergeDoubleDelimitedList(performers, primaryListSeparator, listEndSeparator);

            string performerName;

            // Use merged main artists + featured artists if available
            if (!string.IsNullOrEmpty(trackArtists))
            {
                performerName = trackArtists;
            }
            else
            {
                // Fallback: single performer name from QoItem.Performer
                performerName = ParsingHelper.DecodeEncodedNonAsciiCharacters(QoItem.Performer?.Name);
            }

            // Final fallback: album artist name
            if (string.IsNullOrEmpty(performerName))
            {
                performerName = ParsingHelper.DecodeEncodedNonAsciiCharacters(QoItem.Album?.Artist?.Name);
            }

            performerName = performerName ?? "";

            // Case: the main artist name (QoItem.Performer.Name) or the name extracted from the artist role
            // is a composed name that includes a "Feat" word without a dot, for example: "David Feat Dj Mago, MainArtist".
            performerName = performerName.Replace(" Feat ", " Feat. ").
                                          Replace(" feat ", " Feat. ").
                                          Replace(" Featuring ", " Feat. ").
                                          Replace(" featuring ", " Feat. ");

            return performerName;
        }

        // Adapted by ElektroStudios from QobuzDownloaderX-MOD's source-code to use a QopenAPI.Album object.
        public static string[] GetAlbumArtistsNames(QopenAPI.Album QoAlbum)
        {
            string AlbumArtist;
            string[] AlbumArtists;
            AlbumArtists = ParsingHelper.GetArtistNames(QoAlbum.Artists, InvolvedPersonRoleType.MainArtist);
            string[] featuredArtists = ParsingHelper.GetArtistNames(QoAlbum.Artists, InvolvedPersonRoleType.FeaturedArtist);
            string albumArtists = ParsingHelper.MergeFeaturedArtistsWithMainArtists(AlbumArtists, featuredArtists);
            // Add Featured Artists to Album Artists, canonical order: sorted(MAIN) + sorted(FEATURED).
            AlbumArtists = SortArtists(AlbumArtists).Concat(SortArtists(featuredArtists)).ToArray();
            if (!string.IsNullOrEmpty(albumArtists))
            {
                // User Main-Artists by default
                AlbumArtist = albumArtists;
            }
            else
            {
                AlbumArtist = ParsingHelper.DecodeEncodedNonAsciiCharacters(QoAlbum.Artist.Name);
            }
            // Qobuz doesn't return an array of Albumartists for compilations, so use singular AlbumArtist
            if (AlbumArtists.Length < 1)
            {
                AlbumArtists = new string[] { AlbumArtist };
            }

            return AlbumArtists;
        }
    }
}
