using QobuzDownloaderX.Helpers.QobuzDownloaderXMOD;
using QobuzDownloaderX.Properties;
using QopenAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZetaLongPaths;

namespace QobuzDownloaderX.Helpers
{
    internal sealed class RenameTemplates
    {
        internal static readonly Regex percentRegex = new Regex(@"%(.*?)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        internal static readonly Regex spacesRegex = new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        internal static readonly Regex repeatedParenthesesRegex = new Regex(@"\(([^()]+)\)\s*(\(\1\))+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex spacesBeforeBackslashRegex = new Regex(@"\s+\\", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        // Cached once — this array never changes at runtime
        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

        // "Various Artists" known variations
        readonly string[] variousArtistsNames = new[]
        {
            "Various Artists, Array",
            "Various Artists",
            "Various Aritsts", // not our typo.
            "Various Artist",
            "Various Interpreters",
            "Various Interpreter",
            "Various Interprets"
        };

        public string GetSafeFilename(string filename)
        {
            string safe = RenameTemplates.MakeValidWindowsFileName(filename);
            string safeTruncated = RenameTemplates.TruncateLongName(safe, (Byte)".flac".Length); // ".flac" = largest possible known file extension length on this application.
            return safeTruncated;
        }

        public string GetReleaseArtists(Album QoAlbum, bool updateAlbumInfoLabels)
        {
            if (updateAlbumInfoLabels || (Settings.Default.mergeArtistNames && Settings.Default.mergeArtistNamesInDirectoryNamesToo))
            {
                var mainArtists = QoAlbum.Artists.Where(a => a.Roles.Contains("main-artist")).ToList();
                if (mainArtists.Count > 1)
                {
                    var allButLastArtist = string.Join(", ", mainArtists.Take(mainArtists.Count - 1).Select(a => a.Name));
                    var lastArtist = mainArtists.Last().Name;
                    return $"{allButLastArtist} & {lastArtist}";
                }
            }

            return QoAlbum.Artist.Name;
        }

        /// Returns the four-digit year string extracted from a Qobuz release date.
        /// Returns "" if the date is null, empty, or shorter than 4 characters.
        /// </summary>
        private static string SafeYear(string releaseDate)
        {
            var s = releaseDate?.Trim();
            return s != null && s.Length >= 4 ? s.Substring(0, 4) : "";
        }

        /// <summary>
        /// Returns the A-Z initial for the artist name, or "#" for digits, symbols,
        /// and all non-Latin scripts (Cyrillic, CJK, Arabic, etc.).
        /// Accented Latin letters are normalized: É→E, Ñ→N, Ü→U, etc.
        /// Mirrors tiddl's get_alpha_bucket() behavior.
        /// </summary>
        private static string GetArtistInitial(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                return "#";

            // Decompose the first character (NFD strips combining marks: É → E + ́)
            string firstUpper = artistName[0].ToString().ToUpperInvariant();
            string decomposed = firstUpper.Normalize(NormalizationForm.FormD);
            string baseChar = new string(decomposed
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            // Only A-Z (Basic Latin) get their own folder — everything else goes to #
            return (baseChar.Length == 1 && baseChar[0] >= 'A' && baseChar[0] <= 'Z')
                ? baseChar
                : "#";
        }

        private string ReplaceParentalWarningTags(string template, bool isExplicit)
        {
            // Fast path: skip all 36 Replace() calls when the template contains no PA tags.
            // IndexOf with Ordinal is the fastest string scan available in .NET.
            if (template.IndexOf("%trackpa", StringComparison.Ordinal) < 0 &&
                template.IndexOf("%albumpa", StringComparison.Ordinal) < 0)
                return template;

            template = template.Replace("%trackpa%", isExplicit ? "Explicit" : "Clean");
            template = template.Replace("%trackpashort%", isExplicit ? "E" : "C");
            template = template.Replace("%trackpaifex%", isExplicit ? "Explicit" : "");
            template = template.Replace("%trackpaifexshort%", isExplicit ? "E" : "");
            template = template.Replace("%trackpaifcl%", isExplicit ? "" : "Clean");
            template = template.Replace("%trackpaifclshort%", isExplicit ? "" : "C");
            template = template.Replace("%trackpaenclosed%", isExplicit ? $"(Explicit)" : $"(Clean)");
            template = template.Replace("%trackpaenclosed[]%", isExplicit ? $"[Explicit]" : $"[Clean]");
            template = template.Replace("%trackpaenclosedshort%", isExplicit ? $"(E)" : $"(C)");
            template = template.Replace("%trackpaenclosedshort[]%", isExplicit ? $"[E]" : $"[C]");
            // Lowercase to match tiddl/Orpheus/deemix file-name explicit suffix " (explicit)".
            template = template.Replace("%trackpaifexenclosed%", isExplicit ? $"(explicit)" : $"");
            template = template.Replace("%trackpaifexenclosed[]%", isExplicit ? $"[explicit]" : $"");
            template = template.Replace("%trackpaifexenclosedshort%", isExplicit ? $"(E)" : $"");
            template = template.Replace("%trackpaifexenclosedshort[]%", isExplicit ? $"[E]" : $"");
            template = template.Replace("%trackpaifclenclosed%", isExplicit ? $"" : $"(Clean)");
            template = template.Replace("%trackpaifclenclosed[]%", isExplicit ? $"" : $"[Clean]");
            template = template.Replace("%trackpaifclenclosedshort%", isExplicit ? $"" : $"(C)");
            template = template.Replace("%trackpaifclenclosedshort[]%", isExplicit ? $"" : $"[C]");
            template = template.Replace("%albumpa%", isExplicit ? "Explicit" : "Clean");
            template = template.Replace("%albumpashort%", isExplicit ? "E" : "C");
            template = template.Replace("%albumpaifex%", isExplicit ? "Explicit" : "");
            template = template.Replace("%albumpaifexshort%", isExplicit ? "E" : "");
            template = template.Replace("%albumpaifcl%", isExplicit ? "" : "Clean");
            template = template.Replace("%albumpaifclshort%", isExplicit ? "" : "C");
            template = template.Replace("%albumpaenclosed%", isExplicit ? $"(Explicit)" : $"(Clean)");
            template = template.Replace("%albumpaenclosed[]%", isExplicit ? $"[Explicit]" : $"[Clean]");
            template = template.Replace("%albumpaenclosedshort%", isExplicit ? $"(E)" : $"(C)");
            template = template.Replace("%albumpaenclosedshort[]%", isExplicit ? $"[E]" : $"[C]");
            template = template.Replace("%albumpaifexenclosed%", isExplicit ? $"(explicit)" : $"");
            template = template.Replace("%albumpaifexenclosed[]%", isExplicit ? $"[explicit]" : $"");
            template = template.Replace("%albumpaifexenclosedshort%", isExplicit ? $"(E)" : $"");
            template = template.Replace("%albumpaifexenclosedshort[]%", isExplicit ? $"[E]" : $"");
            template = template.Replace("%albumpaifclenclosed%", isExplicit ? $"" : $"(Clean)");
            template = template.Replace("%albumpaifclenclosed[]%", isExplicit ? $"" : $"[Clean]");
            template = template.Replace("%albumpaifclenclosedshort%", isExplicit ? $"" : $"(C)");
            template = template.Replace("%albumpaifclenclosedshort[]%", isExplicit ? $"" : $"[C]");
            return template;
        }

        private string RenameFormatTemplate(string template, string formatId, string fileFormat, int maximumBitDepth, double maximumSamplingRate, string formatWithHiresQualityPlaceholder, string formatWithQualityPlaceholder)
        {
            fileFormat = fileFormat.ToUpper().TrimStart('.');

            switch (formatId)
            {
                case "5":
                    template = template
                        .Replace(formatWithHiresQualityPlaceholder, fileFormat)
                        .Replace(formatWithQualityPlaceholder, fileFormat);
                    break;

                case "6":
                    template = template
                        .Replace(formatWithHiresQualityPlaceholder, fileFormat)
                        .Replace(formatWithQualityPlaceholder, $"{fileFormat} ({maximumBitDepth}bit-{maximumSamplingRate}kHz)");
                    break;

                case "7":
                case "27":
                    if (maximumBitDepth == 16)
                    {
                        template = template
                            .Replace(formatWithHiresQualityPlaceholder, fileFormat)
                            .Replace(formatWithQualityPlaceholder, $"{fileFormat} ({maximumBitDepth}bit-{maximumSamplingRate}kHz)");
                    }
                    else if (maximumSamplingRate < 192)
                    {
                        template = template.Replace(formatWithQualityPlaceholder, formatWithHiresQualityPlaceholder);

                        if (maximumSamplingRate < 96)
                        {
                            template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} ({maximumBitDepth}bit-{maximumSamplingRate}kHz)");
                        }
                        else if (maximumSamplingRate > 96 && maximumSamplingRate < 192)
                        {
                            if (formatId == "7" && maximumSamplingRate == 176.4)
                            {
                                template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} (24bit-88.2kHz)");
                            }
                            else if (formatId == "7")
                            {
                                template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} (24bit-96kHz)");
                            }
                            else
                            {
                                template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} ({maximumBitDepth}bit-{maximumSamplingRate}kHz)");
                            }
                        }
                        else
                        {
                            template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} (24bit-96kHz)");
                        }
                    }
                    else
                    {
                        template = template.Replace(formatWithQualityPlaceholder, formatWithHiresQualityPlaceholder);
                        template = template.Replace(formatWithHiresQualityPlaceholder, $"{fileFormat} (24bit-192kHz)");
                    }
                    break;
            }

            return template;
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "I don’t feel like changing this and it doesn’t matter")]
        public string renameTemplates(string template, int paddedTrackLength, int paddedDiscLength, string fileFormat, Album QoAlbum, Item QoItem, Playlist QoPlaylist)
        {
            qbdlxForm._qbdlxForm.logger.Debug("Renaming user template - " + template);

            // Convert all text between % symbols to lowercase
            template = percentRegex.Replace(template, match => match.Value.ToLower());

            // Keep backslashes to be used to make new folders
            if (template.Contains(ZlpPathHelper.DirectorySeparatorChar))
            {
                template = template.Replace(@"\", "{backslash}").Replace(@"/", "{forwardslash}");
            }

            // Artist Templates
            /* bro there ain't shit here */

            // Track Templates
            if (QoItem != null)
            {
                if (QoAlbum != null)
                {
                    string artistsNames = GetReleaseArtists(QoAlbum, updateAlbumInfoLabels: false) ?? "";
                    // Only apply the VA track template when the current template actually uses %artistname%.
                    // Folder-path templates (e.g. !playlists\%PlaylistTitle%\) do NOT contain %artistname%,
                    // so replacing the whole template with the VA track template would overwrite the folder
                    // path with the track-naming pattern — producing wrong output like "0189. Reik - Ciego"
                    // as the playlist folder instead of "!playlists\RADIO LUNA DE MIEL\".
                    if (template.Contains("%artistname%") &&
                        variousArtistsNames.Any(name => artistsNames.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Convert all text between % symbols to lowercase
                        template = percentRegex.Replace(Settings.Default.savedVaTrackTemplate, match => match.Value.ToLower());

                        template = template.Replace("%artistname%", "%trackartist%");
                    }
                }

                template = ReplaceParentalWarningTags(template, QoItem.ParentalWarning);
                template = template
                    .Replace("%trackid%", QoItem.Id.ToString())
                    .Replace("%trackcomposer%", QoItem?.Composer?.Name?.ToString())
                    .Replace("%tracknumber%", QoItem.TrackNumber.ToString().PadLeft(paddedTrackLength, '0'))
                    .Replace("%discnumber%", QoItem.MediaNumber.ToString().PadLeft(paddedDiscLength, '0'))
                    .Replace("%trackversion%", QoItem.Version?.ToString() ?? "")
                    .Replace("%isrc%", QoItem.ISRC.ToString())
                    .Replace("%trackbitdepth%", QoItem.MaximumBitDepth.ToString())
                    .Replace("%tracksamplerate%", QoItem.MaximumSamplingRate.ToString());

                string titleFormatted = QoItem.Version == null
                                        ? QoItem.Title
                                        : $"{QoItem.Title.TrimEnd()} ({QoItem.Version})";
                titleFormatted = repeatedParenthesesRegex.Replace(titleFormatted, "($1)");
                template = template.Replace("%tracktitle%", titleFormatted);

                if (Settings.Default.mergeArtistNames)
                {
                    string performerNames = ParsingHelper.GetTrackPerformersName(QoItem);
                    template = template.Replace("%artistname%", performerNames);
                    template = template.Replace("%trackartist%", performerNames);
                }
                else
                {
                    template = template.Replace("%trackartist%", QoItem?.Performer?.Name?.ToString());
                }

                // Track Format Templates
                template = template.Replace("%trackformat%", fileFormat.ToUpper().TrimStart('.'));
                template = RenameFormatTemplate(template, qbdlxForm._qbdlxForm.format_id, fileFormat, QoItem.MaximumBitDepth, QoItem.MaximumSamplingRate, "%trackformatwithhiresquality%", "%trackformatwithquality%");
            }

            // Album Templates
            if (QoAlbum != null)
            {
                template = ReplaceParentalWarningTags(template, QoAlbum.ParentalWarning);

                string albumArtistName = GetReleaseArtists(QoAlbum, updateAlbumInfoLabels: false) ?? "";
                string artistInitial = GetArtistInitial(albumArtistName);

                template = template
                    .Replace("%albumid%", QoAlbum.Id.ToString())
                    .Replace("%albumurl%", QoAlbum.Url?.ToString() ?? "")
                    .Replace("%artistname%", albumArtistName)
                    .Replace("%artistinitial%", artistInitial)
                    .Replace("%artistid%", QoAlbum.Artist?.Id.ToString() ?? "")
                    .Replace("%albumgenre%", QoAlbum?.Genre?.Name ?? "")
                    .Replace("%albumcomposer%", QoAlbum?.Composer?.Name?.ToString() ?? "")
                    .Replace("%label%", spacesRegex.Replace(QoAlbum.Label?.Name ?? "", " "))
                    .Replace("%copyright%", QoAlbum.Copyright ?? "")
                    .Replace("%upc%", QoAlbum.UPC ?? "")
                    .Replace("%releasedate%", QoAlbum.ReleaseDateOriginal?.Trim() ?? "")
                    .Replace("%year%", SafeYear(QoAlbum.ReleaseDateOriginal))
                    .Replace("%releasetype%", QoAlbum.ProductType?.ToUpperInvariant() ?? "")
                    .Replace("%bitdepth%", QoAlbum.MaximumBitDepth.ToString() ?? "")
                    .Replace("%samplerate%", QoAlbum.MaximumSamplingRate.ToString() ?? "")
                    .Replace("%totaldiscs%", QoAlbum.MediaCount.ToString())
                    .Replace("%totaltracks%", QoAlbum.TracksCount.ToString())
                    .Replace("%albumdescription%", QoAlbum.Description ?? "")
                    .Replace("%albumtitle%", QoAlbum.Version == null ? QoAlbum.Title : $"{QoAlbum.Title?.TrimEnd()} ({QoAlbum.Version})")
                    .Replace("%format%", fileFormat.ToUpper().TrimStart('.'));
            }

            if (QoPlaylist == null)
            {
                // Release Format Templates
                template = RenameFormatTemplate(template, qbdlxForm._qbdlxForm.format_id, fileFormat, QoAlbum.MaximumBitDepth, QoAlbum.MaximumSamplingRate, "%formatwithhiresquality%", "%formatwithquality%");
            }
            else
            {
                // Playlist Templates
                template = template
                    .Replace("%playlistid%", QoPlaylist.Id.ToString())
                    .Replace("%playlisttitle%", QoPlaylist.Name)
                    .Replace("%format%", fileFormat.ToUpper().TrimStart('.'))
                    .Replace("%formatwithhiresquality%", fileFormat.ToUpper().TrimStart('.'))
                    .Replace("%formatwithquality%", fileFormat.ToUpper().TrimStart('.')); 

                if (QoItem != null)
                {
                    // Album Template for playlist path
                    template = template
                        .Replace("%albumid%", QoAlbum.Id?.ToString() ?? "")
                        .Replace("%albumurl%", QoAlbum.Url?.ToString() ?? "")
                        .Replace("%artistname%", GetReleaseArtists(QoAlbum, updateAlbumInfoLabels: false) ?? "")
                        .Replace("%albumgenre%", QoAlbum?.Genre?.Name ?? "")
                        .Replace("%albumcomposer%", QoAlbum?.Composer?.Name?.ToString() ?? "")
                        .Replace("%label%", spacesRegex.Replace(QoAlbum.Label?.Name ?? "", " ")) // Qobuz sometimes has multiple spaces where a single one should be
                        .Replace("%copyright%", QoAlbum.Copyright ?? "")
                        .Replace("%upc%", QoAlbum.UPC ?? "")
                        .Replace("%releasedate%", QoAlbum.ReleaseDateOriginal?.Trim() ?? "")
                        .Replace("%year%", SafeYear(QoAlbum.ReleaseDateOriginal))
                        .Replace("%releasetype%", QoAlbum.ProductType?.ToUpperInvariant() ?? "")
                        .Replace("%bitdepth%", QoAlbum.MaximumBitDepth.ToString() ?? "")
                        .Replace("%samplerate%", QoAlbum.MaximumSamplingRate.ToString() ?? "")
                        .Replace("%albumtitle%", QoAlbum.Version == null ? QoAlbum.Title : $"{QoAlbum.Title?.TrimEnd()} ({QoAlbum.Version})")
                        .Replace("%format%", fileFormat.ToUpper().TrimStart('.'));
                }
            }

            // GetSafeFilename call to make sure path will be valid
            template = GetSafeFilename(template);

            // Trim leading/trailing whitespace (including Unicode NBSP U+00A0) and dots from
            // each path segment while the {backslash} placeholder is still in place.
            // Qobuz API data occasionally returns artist/album names with trailing non-breaking
            // spaces, which Trim(' ', '.') in MakeValidWindowsFileName does not catch, causing
            // Windows to fail creating directories like "Z:\R\Ram Sampath \...".
            template = string.Join("{backslash}",
                template.Split(new[] { "{backslash}" }, StringSplitOptions.None)
                        .Select(seg => seg.Trim().Trim('.')));

            // Remove any double spaces
            template = spacesBeforeBackslashRegex.Replace(
                           spacesRegex.Replace(
                               template.Replace("{backslash}", @"\").Replace("{forwardslash}", @"/").Replace(@" \", @"\"),
                               " "),
                           " "); // Replace slash placeholders & remove double spaces

            // Replace long ellipsis
            template = template.Replace("...", "…");

            qbdlxForm._qbdlxForm.logger.Debug("Template output - " + template);
            return template;
        }

        // Replacement glyphs for Windows-forbidden characters. Use the SAME full-width
        // Unicode forms as tiddl (CHAR_TO_FULL_WIDTH in tiddl/core/utils/strings.py) so
        // file names are byte-identical across tools (otherwise visually-equal names like
        // "11∶11" vs "11：11" don't overwrite each other when copied).
        public static string MakeValidWindowsFileName(
            string fileName,
            char asteriskChar = '＊',     // U+FF0A
            char colonChar = '：',        // U+FF1A
            char questionMarkChar = '？', // U+FF1F
            char verticalBarChar = '｜',  // U+FF5C
            char quoteChar = '＂',        // U+FF02
            char backSlashChar = '＼',    // U+FF3C
            char forwardSlashChar = '／', // U+FF0F
            char lessThanChar = '＜',     // U+FF1C
            char greaterThanChar = '＞')  // U+FF1E
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return fileName;

            char[] invalidFileNameChars = _invalidFileNameChars;

            if (invalidFileNameChars.Contains(asteriskChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(asteriskChar)}.", nameof(asteriskChar));
            if (invalidFileNameChars.Contains(colonChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(colonChar)}.", nameof(colonChar));
            if (invalidFileNameChars.Contains(questionMarkChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(questionMarkChar)}.", nameof(questionMarkChar));
            if (invalidFileNameChars.Contains(verticalBarChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(verticalBarChar)}.", nameof(verticalBarChar));
            if (invalidFileNameChars.Contains(quoteChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(quoteChar)}.", nameof(quoteChar));
            if (invalidFileNameChars.Contains(backSlashChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(backSlashChar)}.", nameof(backSlashChar));
            if (invalidFileNameChars.Contains(forwardSlashChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(forwardSlashChar)}.", nameof(forwardSlashChar));
            if (invalidFileNameChars.Contains(lessThanChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(lessThanChar)}.", nameof(lessThanChar));
            if (invalidFileNameChars.Contains(greaterThanChar))
                throw new ArgumentException($"Invalid replacement character for {nameof(greaterThanChar)}.", nameof(greaterThanChar));

            var replacements = new Dictionary<char, char>
            {
                { '*', asteriskChar },
                { ':', colonChar },
                { '?', questionMarkChar },
                { '|', verticalBarChar },
                { '"', quoteChar },
                { '<', lessThanChar },
                { '>', greaterThanChar },
                { '\\', backSlashChar },
                { '/', forwardSlashChar }
            };

            fileName = fileName.Trim(new char[] {' ', '.'});

            var sb = new StringBuilder(fileName.Length);
            foreach (char c in fileName)
            {
                if (replacements.ContainsKey(c))
                    sb.Append(replacements[c]);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public static string TruncateLongName(string name, byte extLen, byte maxFileNameLength = 255)
        {

            if (string.IsNullOrEmpty(name))
                return name;

            if (maxFileNameLength == 0)
                throw new ArgumentException("Value must be greater than zero.", nameof(maxFileNameLength));

            if ((name.Length + extLen) >= maxFileNameLength)
            {
                name = name.Substring(0, maxFileNameLength - 1 - extLen) + '…';
            }

            return name;
        }

    }
}
