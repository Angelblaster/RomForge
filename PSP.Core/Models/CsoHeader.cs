using System.Runtime.InteropServices;

namespace PSP.Core.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CsoHeader
{
    public static readonly byte[] MagicCSO = "CISO"u8.ToArray();
    public static readonly byte[] MagicZSO = "ZISO"u8.ToArray();

    public uint HeaderSize;      // 0x18
    public ulong UncompressedSize;
    public uint BlockSize;       // 보통 2048
    public byte Version;         // 1 = CSO v1, 2 = CSO v2
    public byte IndexShift;
    public ushort Unused;
}