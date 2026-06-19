namespace PBP.Core.Services;

public static class PbpStreamExtensions
{
    public static void Read(this Stream stream, uint[] buffer, int count)
    {
        var uintBuffer = new byte[sizeof(uint)];

        for (var i = 0; i < count; i++)
        {
            stream.Read(uintBuffer, 0, 4);
            buffer[i] = BitConverter.ToUInt32(uintBuffer, 0);
        }
    }

    public static void WriteResource(this Stream stream, byte[]? resource)
    {
        if (resource is null || resource.Length == 0) 
            return;

        stream.Write(resource, 0, resource.Length);
    }
}