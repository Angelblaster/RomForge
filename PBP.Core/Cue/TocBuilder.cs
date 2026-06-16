namespace PBP.Core.Cue;

/// <summary>
/// CUE → TOC 바이너리 빌더
/// PSXPackager TOCHelper + CueFileExtensions.GetTOCData 포팅
/// </summary>
public static class TocBuilder
{
    /// <summary>
    /// CUE 파일 경로 + ISO 크기(바이트) → TOC 바이너리
    /// </summary>
    public static byte[] FromCue(string cuePath, long isoSize)
    {
        var cue = CueFileReader.Read(cuePath);
        return BuildToc(cue, isoSize);
    }

    /// <summary>
    /// 더미 CUE (ISO/CHD 단일 데이터 트랙) → TOC 바이너리
    /// </summary>
    public static byte[] FromDummy(long isoSize)
    {
        var cue = CueFileReader.Dummy("dummy.bin");
        return BuildToc(cue, isoSize);
    }

    private static byte[] BuildToc(CueFile cue, long isoSize)
    {
        var tracks = cue.FileEntries.SelectMany(f => f.Tracks).ToList();

        // TOC 엔트리: 첫트랙 + 마지막트랙 + leadout + 트랙별
        byte[] tocData = new byte[0xA * (tracks.Count + 3)];
        var buf = new byte[0xA];
        int ctr = 0;

        long frames = isoSize / 2352;
        var leadOut = IndexPosition.FromFrames(frames);

        // ── A0: 첫 트랙 번호 ──
        buf[0] = TrackTypeByte(tracks.First().DataType);
        buf[1] = 0x00;
        buf[2] = 0xA0;
        buf[3] = 0x00;
        buf[4] = 0x00;
        buf[5] = 0x00;
        buf[6] = 0x00;
        buf[7] = ToBcd(tracks.First().Number);
        buf[8] = ToBcd(0x20); // PS1 포맷 식별자
        buf[9] = 0x00;
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        // ── A1: 마지막 트랙 번호 ──
        buf[0] = TrackTypeByte(tracks.Last().DataType);
        buf[2] = 0xA1;
        buf[7] = ToBcd(tracks.Last().Number);
        buf[8] = 0x00;
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        // ── A2: Lead-out 위치 ──
        buf[0] = 0x01;
        buf[2] = 0xA2;
        buf[7] = ToBcd(leadOut.Minutes);
        buf[8] = ToBcd(leadOut.Seconds);
        buf[9] = ToBcd(leadOut.Frames);
        Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;

        // ── 트랙별 엔트리 ──
        foreach (var track in tracks)
        {
            var idx1 = track.Indexes.First(i => i.Number == 1).Position;
            // 2초 lead-in 오프셋 추가
            var relative = idx1 + new IndexPosition(0, 2, 0);

            buf[0] = TrackTypeByte(track.DataType);
            buf[1] = 0x00;
            buf[2] = ToBcd(track.Number);
            buf[3] = ToBcd(idx1.Minutes);
            buf[4] = ToBcd(idx1.Seconds);
            buf[5] = ToBcd(idx1.Frames);
            buf[6] = 0x00;
            buf[7] = ToBcd(relative.Minutes);
            buf[8] = ToBcd(relative.Seconds);
            buf[9] = ToBcd(relative.Frames);
            Array.Copy(buf, 0, tocData, ctr, 0xA); ctr += 0xA;
        }

        return tocData;
    }

    // 데이터=0x41, 오디오=0x01
    private static byte TrackTypeByte(string dataType) =>
        dataType == CueTrackType.Audio ? (byte)0x01 : (byte)0x41;

    // BCD 변환 (Binary Coded Decimal)
    private static byte ToBcd(int value)
    {
        var ones = value % 10;
        var tens = value / 10;
        return (byte)(tens * 0x10 + ones);
    }
}
