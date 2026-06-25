# Template Tags — QobuzDownloaderX

All tags are case-insensitive. Use `\` to create subfolders in templates.

**Example:**
```
%ArtistInitial%\%ArtistName%\(%Year%) %AlbumTitle%\%DiscNumber%-%TrackNumber% - %TrackTitle%
```

---

## Release (album-level)

| Tag | Output | Example |
|-----|--------|---------|
| `%AlbumID%` | Internal Qobuz album ID | `0060254755791` |
| `%AlbumURL%` | Full Qobuz album URL | `https://www.qobuz.com/album/...` |
| `%AlbumTitle%` | Album title + version in parentheses if present | `The Dark Side of the Moon (2023 Remaster)` |
| `%ArtistName%` | Main album artist | `Pink Floyd` |
| `%ArtistInitial%` | First letter of artist (numbers → `#`) | `P` / `#` |
| `%ArtistID%` | Qobuz artist ID | `12345` |
| `%AlbumGenre%` | Album genre | `Rock` |
| `%AlbumComposer%` | Album-level composer (useful for classical) | `Ludwig van Beethoven` |
| `%Label%` | Record label | `Warner Music` |
| `%Copyright%` | Copyright string | `℗ 2023 Pink Floyd Music` |
| `%UPC%` | Universal Product Code / barcode | `0060254755791` |
| `%ReleaseDate%` | Full release date | `1973-03-01` |
| `%Year%` | Release year only | `1973` |
| `%ReleaseType%` | Release type (first letter capitalized) | `Album` / `Single` / `Ep` |
| `%BitDepth%` | Maximum album bit depth | `24` |
| `%SampleRate%` | Maximum album sample rate in kHz | `96` |
| `%TotalDiscs%` | Total number of discs | `2` |
| `%TotalTracks%` | Total number of tracks | `10` |
| `%AlbumDescription%` | Editorial album description | *(long text)* |
| `%Format%` | File format | `FLAC` / `MP3` |
| `%FormatWithQuality%` | Format + quality info whenever available | `FLAC (24bit-96kHz)` |
| `%FormatWithHiResQuality%` | Format + max album quality (as advertised by Qobuz) | `FLAC (24bit-96kHz)` |

### `%FormatWithQuality%` vs `%FormatWithHiResQuality%`

For standard CD quality (16bit/44.1kHz):
- `%FormatWithQuality%` → `FLAC (16bit-44.1kHz)`
- `%FormatWithHiResQuality%` → `FLAC`

For Hi-Res they produce the same result. Use `%FormatWithHiResQuality%` if you want clean folder names for CD albums without the `16bit-44.1kHz` suffix, but still want quality info for Hi-Res.

---

## Track (track-level)

| Tag | Output | Example |
|-----|--------|---------|
| `%TrackID%` | Internal Qobuz track ID | `54321` |
| `%TrackNumber%` | Track number, zero-padded to match total | `03` |
| `%TrackTitle%` | Track title + version in parentheses if present | `Money (2023 Remaster)` |
| `%TrackVersion%` | Version only, without the title | `2023 Remaster` |
| `%TrackArtist%` | Track performer (may differ from album artist in compilations) | `Roger Waters` |
| `%TrackComposer%` | Track composer | `Roger Waters` |
| `%ISRC%` | International Standard Recording Code | `GBCBR7310007` |
| `%DiscNumber%` | Disc number, zero-padded | `01` |
| `%TrackBitDepth%` | Bit depth of this specific track | `24` |
| `%TrackSampleRate%` | Sample rate of this specific track in kHz | `96` |
| `%TrackFormat%` | File format | `FLAC` |
| `%TrackFormatWithQuality%` | Same as `%FormatWithQuality%` but at track level | `FLAC (24bit-96kHz)` |
| `%TrackFormatWithHiResQuality%` | Same as `%FormatWithHiResQuality%` but at track level | `FLAC (24bit-96kHz)` |

---

## Parental Advisory

18 variants available for both album and track level. Replace `Track` with `Album` for album-level tags.

| Suffix | If **Explicit** | If **Clean** |
|--------|----------------|--------------|
| `PA` | `Explicit` | `Clean` |
| `PAShort` | `E` | `C` |
| `PAifEx` | `Explicit` | *(empty)* |
| `PAifExShort` | `E` | *(empty)* |
| `PAifCl` | *(empty)* | `Clean` |
| `PAifClShort` | *(empty)* | `C` |
| `PAEnclosed` | `(Explicit)` | `(Clean)` |
| `PAEnclosed[]` | `[Explicit]` | `[Clean]` |
| `PAEnclosedShort` | `(E)` | `(C)` |
| `PAEnclosedShort[]` | `[E]` | `[C]` |
| `PAifExEnclosed` | `(explicit)` | *(empty)* |
| `PAifExEnclosed[]` | `[explicit]` | *(empty)* |
| `PAifExEnclosedShort` | `(E)` | *(empty)* |
| `PAifExEnclosedShort[]` | `[E]` | *(empty)* |
| `PAifClEnclosed` | *(empty)* | `(Clean)` |
| `PAifClEnclosed[]` | *(empty)* | `[Clean]` |
| `PAifClEnclosedShort` | *(empty)* | `(C)` |
| `PAifClEnclosedShort[]` | *(empty)* | `[C]` |

> **Note:** The `PAifExEnclosed` / `PAifExEnclosed[]` variants render in **lowercase** (`(explicit)` / `[explicit]`) on purpose, to match the file-name explicit suffix used by tiddl / OrpheusDL / deemix. The default track template uses `%TrackPAifexenclosed%`.

**Most useful in practice:** `%TrackPAifEx%` — appends `Explicit` only when the track is explicit, leaving clean tracks unaffected.

```
%TrackNumber% - %TrackTitle% %TrackPAifEx%
→  03 - Money Explicit
→  04 - Us and Them
```

---

## Playlist

| Tag | Output |
|-----|--------|
| `%PlaylistID%` | Qobuz playlist ID |
| `%PlaylistTitle%` | Playlist name |
| `%Format%` | File format |
| `%FormatWithQuality%` | Format + quality |
| `%FormatWithHiResQuality%` | Format + Hi-Res quality |

---

## CD template

| Tag | Output |
|-----|--------|
| `%DiscNumber%` | Disc number, zero-padded — separates content per disc |

---

## Template examples

### Standard A-Z library
```
%ArtistInitial%\%ArtistName%\(%Year%) %AlbumTitle%\%TrackNumber% - %TrackTitle%
```

### Multi-disc album
```
%ArtistName%\(%Year%) %AlbumTitle%\CD%DiscNumber%\%TrackNumber% - %TrackTitle%
```

### With format and quality in folder
```
%ArtistName%\(%Year%) %AlbumTitle% [%FormatWithHiResQuality%]\%TrackNumber% - %TrackTitle%
```

### Compilation / Various Artists (V/A template)
```
%AlbumTitle%\%TrackNumber% - %TrackArtist% - %TrackTitle%
```

### With explicit advisory
```
%ArtistName%\(%Year%) %AlbumTitle% %AlbumPAifEx%\%TrackNumber% - %TrackTitle% %TrackPAifEx%
```
