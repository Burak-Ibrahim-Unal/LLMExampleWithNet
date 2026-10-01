using System.Runtime.InteropServices;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>Stores embedding vectors as compact BLOBs (4 bytes per dimension) instead of JSON text.</summary>
public static class EmbeddingBlob
{
    public static byte[] ToBytes(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static float[] FromBytes(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}
