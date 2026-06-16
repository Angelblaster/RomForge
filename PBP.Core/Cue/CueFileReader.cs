using System.Text.RegularExpressions;

namespace PBP.Core.Cue;

/// <summary>
/// CUE 파일 파서
/// PSXPackager CueFileReader 포팅
/// </summary>
public static class CueFileReader
{
    private static readonly Regex FileRegex  = new(@"^FILE ""(.*?)"" (.*?)\s*$");
    private static readonly Regex TrackRegex = new(@"^\s*TRACK (\d+) (.*?)\s*$");
    private static readonly Regex IndexRegex = new(@"^\s*INDEX (\d+) (\d+:\d+:\d+)\s*$");

    public static CueFile Read(string cuePath)
    {
        var cueFile = new CueFile { Path = cuePath };

        CueFileEntry? currentEntry = null;
        CueTrack?     currentTrack = null;
        CueTrack?     lastTrack    = null;

        foreach (var line in File.ReadAllLines(cuePath))
        {
            var fileMatch  = FileRegex.Match(line);
            var trackMatch = TrackRegex.Match(line);
            var indexMatch = IndexRegex.Match(line);

            if (fileMatch.Success)
            {
                currentEntry = new CueFileEntry
                {
                    CueFile  = cueFile,
                    FileName = fileMatch.Groups[1].Value,
                    FileType = fileMatch.Groups[2].Value,
                    Tracks   = []
                };
                cueFile.FileEntries.Add(currentEntry);
                lastTrack = null;
            }
            else if (trackMatch.Success && currentEntry != null)
            {
                currentTrack = new CueTrack
                {
                    FileEntry = currentEntry,
                    Number    = int.Parse(trackMatch.Groups[1].Value),
                    DataType  = trackMatch.Groups[2].Value,
                    Indexes   = []
                };
                currentEntry.Tracks.Add(currentTrack);

                if (lastTrack != null) lastTrack.Next = currentTrack;
                lastTrack = currentTrack;
            }
            else if (indexMatch.Success && currentTrack != null)
            {
                var parts = indexMatch.Groups[2].Value.Split(':');
                currentTrack.Indexes.Add(new CueIndex
                {
                    Number   = int.Parse(indexMatch.Groups[1].Value),
                    Position = new IndexPosition(
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        int.Parse(parts[2]))
                });
            }
        }

        return cueFile;
    }

    /// <summary>
    /// 단일 데이터 트랙 더미 CUE (ISO/CHD용)
    /// </summary>
    public static CueFile Dummy(string fileName) => new()
    {
        FileEntries =
        [
            new CueFileEntry
            {
                FileName = fileName,
                FileType = "BINARY",
                Tracks   =
                [
                    new CueTrack
                    {
                        Number   = 1,
                        DataType = CueTrackType.Data,
                        Indexes  =
                        [
                            new CueIndex
                            {
                                Number   = 1,
                                Position = new IndexPosition(0, 0, 0)
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
