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

        // ------------------------------------------------------------------
        // Feat handling — ported 1:1 from the tiddl fork
        // (tiddl/core/utils/format.py: _KEYWORDS_PATTERN / _RE_ANTI_FEAT /
        // clean_track_title) so both tools produce identical names:
        //   "NN. Main Artist / Featured Artist - Clean Title"
        // (the "/" becomes fullwidth "／" in file names via MakeValidWindowsFileName)
        // ------------------------------------------------------------------

        private const string featKeywordsPattern =
            // English / Universal
            @"f(?:ea)?t(?:\.|uring)?|with|w/|starring|guest(?: vocals:?)?|vocals?(?::| by)|" +
            @"prod(?:\.|uced by)|(?:remix|edit|mix) by|" +
            @"vs\.?|x|×|pres(?:en)?t(?:s|a|e)?|" +
            @"collab(?:oration)?|" +
            // Spanish
            @"con|junto a|y|col(?:\.|aboraci[oó]n)?|invitado|voz(?: de)?|producido por|remix de|" +
            // German / French
            @"mit|avec|et";

        private static readonly Regex antiFeatRegex = new Regex(
            // Option 1: Parentheses/Brackets — requires closing bracket
            @"(?:\s*[\(\[\{]\s*(?:" + featKeywordsPattern + @")\s+([^)\}\]]+?)\s*[\)\]\}])" +
            @"|" +
            // Option 2: Dash separator — consumes rest of string
            @"(?:\s+[-–]\s+\s*(?:" + featKeywordsPattern + @")\s+(.*))" +
            @"|" +
            // Option 3: Bare feat/ft/featuring (no brackets/dash). Restricted to the
            // unambiguous feat keyword; IsKnownArtist() still protects titles like "6 Ft. 7 Ft.".
            @"(?:\s+f(?:ea)?t(?:\.|uring)?\s+(.*))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Separators between artist names inside a feat segment: "X, Y & Z"
        private static readonly Regex featContentSeparatorRegex = new Regex(
            @"\s*(?:,|&|\+| and | y | et | und | con | with )\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Normalized (PerformersParser.Normalize) contents of every feat segment
        // found in a title: "Una noche (feat. The Corrs)" -> ["the corrs"].
        private static List<string> ExtractFeatSegments(string title)
        {
            List<string> segments = new List<string>();
            if (string.IsNullOrWhiteSpace(title))
                return segments;

            foreach (Match m in antiFeatRegex.Matches(title))
            {
                for (int g = 1; g <= 3; g++)
                {
                    if (m.Groups[g].Success)
                    {
                        string norm = PerformersParser.Normalize(m.Groups[g].Value);
                        if (!string.IsNullOrEmpty(norm))
                            segments.Add(norm);
                        break;
                    }
                }
            }
            return segments;
        }

        private static bool NameAppearsInSegments(string name, List<string> normalizedSegments)
        {
            string n = PerformersParser.Normalize(name);
            if (string.IsNullOrEmpty(n))
                return false;

            Regex wordBoundary = new Regex(@"\b" + Regex.Escape(n) + @"\b", RegexOptions.IgnoreCase);
            foreach (string seg in normalizedSegments)
            {
                if (wordBoundary.IsMatch(seg))
                    return true;
            }
            return false;
        }

        private static bool IsKnownArtist(string name, List<string> normalizedMetaArtists)
        {
            string n = PerformersParser.Normalize(name);
            if (string.IsNullOrEmpty(n))
                return true; // ignore empty parts

            if (normalizedMetaArtists.Contains(n))
                return true;

            // Word-boundary match inside any meta artist:
            // meta="Lil Wayne", feat="Lil" -> match; meta="Lily Allen", feat="Lil" -> no match.
            Regex pattern = new Regex(@"\b" + Regex.Escape(n) + @"\b", RegexOptions.IgnoreCase);
            foreach (string ma in normalizedMetaArtists)
            {
                if (pattern.IsMatch(ma))
                    return true;
            }
            return false;
        }

        // Removes "(feat. X)" / "- feat X" / bare "feat X" segments from a track title
        // when X is a KNOWN artist (present in the artist metadata), because those
        // artists are rendered in the artist part of the file name / ARTIST tag instead.
        // Unknown names are kept untouched (protects titles like "6 Ft. 7 Ft." and
        // feats that Qobuz never lists as performers). tiddl clean_track_title parity.
        public static string CleanTrackTitle(string trackTitle, IEnumerable<string> knownArtists)
        {
            if (string.IsNullOrWhiteSpace(trackTitle))
                return trackTitle;

            List<string> metaArtists = (knownArtists ?? Enumerable.Empty<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(PerformersParser.Normalize)
                .Where(a => a.Length > 0)
                .ToList();

            string result = antiFeatRegex.Replace(trackTitle, delegate (Match match)
            {
                string content = null;
                for (int g = 1; g <= 3; g++)
                {
                    if (match.Groups[g].Success)
                    {
                        content = match.Groups[g].Value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(content))
                    return match.Value;

                string[] parts = featContentSeparatorRegex.Split(content);

                List<string> unknownParts = new List<string>();
                foreach (string p in parts)
                {
                    if (!IsKnownArtist(p, metaArtists))
                        unknownParts.Add(p.Trim());
                }

                if (unknownParts.Count == 0)
                    return ""; // every feat'd name is a known artist -> drop the segment

                if (unknownParts.Count == parts.Length)
                    return match.Value; // none known -> not an artist credit, keep as-is

                // Partial: keep only the unknown names, preserving the wrapper (parens etc.)
                return match.Value.Replace(content, string.Join(", ", unknownParts.ToArray()));
            });

            result = result.Trim();

            // Safety: a title that was ONLY a feat segment must not end up empty.
            return result.Length > 0 ? result : trackTitle.Trim();
        }

        // Convenience overload: clean the track title using the track's own
        // performers (main + featured) as the known-artist list.
        public static string GetCleanTrackTitle(QopenAPI.Item QoItem)
        {
            PerformersParser parser = new PerformersParser(QoItem);
            string[] main = parser.GetPerformersWithRole(InvolvedPersonRoleType.MainArtist);
            string[] featured = parser.GetPerformersWithRole(InvolvedPersonRoleType.FeaturedArtist);
            return CleanTrackTitle(QoItem.Title ?? "", main.Concat(featured));
        }

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
        // Returns the canonical, ordered list of track artists = sorted(MAIN) + sorted(FEATURED).
        // Used for the multi-value ARTIST tag (one entry per artist) — tiddl / Orpheus parity.
        //
        // tiddl parity: featured artists BELONG in the artist part
        // ("NN. Main ／ Feat - Clean Title"). When the title carries a
        // "(feat. X)"-like segment we KEEP the featured performers (the old code
        // dropped them, producing "Main - Title (feat. X)") and reclassify main
        // artists that Qobuz misfiled as MainArtist but are really the feat'd
        // guest. The "(feat. X)" text itself is stripped from the title by
        // CleanTrackTitle() at the callers (file name via RenameTemplates.cs,
        // TITLE tag via TagFile.cs).
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
                // Extract the feat-segment contents ("The Corrs" from
                // "Una noche (feat. The Corrs)"), normalized for comparison.
                List<string> featSegments = ExtractFeatSegments(title);

                // Case: Qobuz API returns the featured artists as "Main Artist".
                // MOVE main artists (except the first) that appear inside a feat
                // segment of the title to the FEATURED list. The old code removed
                // them entirely, losing them from the file name and ARTIST tag.
                if (mainPerformers != null && mainPerformers.Length > 1 && featSegments.Count > 0)
                {
                    List<string> mainKeep = new List<string>();
                    List<string> moved = new List<string>();

                    // Keep the first main artist.
                    mainKeep.Add(mainPerformers[0]);

                    foreach (string mp in mainPerformers.Skip(1))
                    {
                        if (NameAppearsInSegments(mp, featSegments))
                            moved.Add(mp);
                        else
                            mainKeep.Add(mp);
                    }

                    mainPerformers = mainKeep.ToArray();

                    // Merge into featured, avoiding duplicates (normalized compare).
                    List<string> featuredList = (featuredPerformers ?? new string[0]).ToList();
                    foreach (string mv in moved)
                    {
                        string mvNorm = PerformersParser.Normalize(mv);
                        if (!featuredList.Any(f => PerformersParser.Normalize(f) == mvNorm))
                            featuredList.Add(mv);
                    }
                    featuredPerformers = featuredList.ToArray();
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
