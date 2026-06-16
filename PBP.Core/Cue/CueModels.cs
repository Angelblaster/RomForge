using System.Diagnostics;

namespace PBP.Core.Cue;

public static class CueTrackType
{
    public const string Data  = "MODE2/2352";
    public const string Audio = "AUDIO";
}

public enum TrackTypeEnum { Data, Audio }

[DebuggerDisplay("{Minutes}:{Seconds}:{Frames}")]
public class IndexPosition
{
    public int Minutes { get; set; }
    public int Seconds { get; set; }
    public int Frames  { get; set; }

    public IndexPosition() { }
    public IndexPosition(int m, int s, int f) { Minutes = m; Seconds = s; Frames = f; }

    public static IndexPosition operator +(IndexPosition a, IndexPosition b) =>
        FromFrames(ToFrames(a) + ToFrames(b));

    public static IndexPosition operator -(IndexPosition a, IndexPosition b)
    {
        long f = ToFrames(a) - ToFrames(b);
        return f < 0 ? new IndexPosition() : FromFrames(f);
    }

    public static long ToFrames(IndexPosition p) =>
        (p.Minutes * 60L + p.Seconds) * 75 + p.Frames;

    public static IndexPosition FromFrames(long frames)
    {
        int totalSeconds = (int)(frames / 75);
        return new IndexPosition(totalSeconds / 60, totalSeconds % 60, (int)(frames % 75));
    }

    public override string ToString() => $"{Minutes:00}:{Seconds:00}:{Frames:00}";
}

public class CueIndex
{
    public int           Number   { get; set; }
    public IndexPosition Position { get; set; } = new();
}

public class CueTrack
{
    public int            Number   { get; set; }
    public string         DataType { get; set; } = string.Empty;
    public List<CueIndex> Indexes  { get; set; } = [];
    public CueFileEntry   FileEntry { get; set; } = null!;
    public CueTrack?      Next     { get; set; }
}

public class CueFileEntry
{
    public CueFile       CueFile  { get; set; } = null!;
    public string        FileName { get; set; } = string.Empty;
    public string        FileType { get; set; } = "BINARY";
    public List<CueTrack> Tracks  { get; set; } = [];
}

public class CueFile
{
    public string              Path        { get; set; } = string.Empty;
    public List<CueFileEntry>  FileEntries { get; set; } = [];

    public string GetAbsolutePath(CueFileEntry entry)
    {
        var dir = System.IO.Path.GetDirectoryName(Path) ?? ".";
        return System.IO.Path.IsPathFullyQualified(entry.FileName)
            ? entry.FileName
            : System.IO.Path.Combine(dir, entry.FileName);
    }
}
