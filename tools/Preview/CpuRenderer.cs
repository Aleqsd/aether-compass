// Copyright (c) 2026 Aleqsd. MIT license; see LICENSE.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

internal static unsafe class CpuRenderer
{
    // Offline renderer for the actual ImGui draw lists. No screenshot reconstruction or HTML mockup.
    public static void Save(ImDrawDataPtr draw, int width, int height, byte* atlas, int atlasWidth, int atlasHeight, string output, Dictionary<ulong, (byte[] Pixels, int Width, int Height)> textures, bool light = false)
    {
        var atlasPixels=new byte[atlasWidth*atlasHeight*4];
        System.Runtime.InteropServices.Marshal.Copy((nint)atlas,atlasPixels,0,atlasPixels.Length);
        textures[1]=(atlasPixels,atlasWidth,atlasHeight);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = (byte)(light ? 185 : 25); pixels[i * 4 + 1] = (byte)(light ? 182 : 26); pixels[i * 4 + 2] = (byte)(light ? 178 : 31); pixels[i * 4 + 3] = 255;
        }
        for (var listIndex = 0; listIndex < draw.CmdListsCount; listIndex++)
        {
            var list = new ImDrawListPtr(draw.CmdLists[listIndex]);
            for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
            {
                var cmd = list.CmdBuffer[commandIndex];
                if (cmd.UserCallback != null) continue;
                var image=textures.GetValueOrDefault(cmd.TextureId.Handle,textures[1]);
                for (var element = 0u; element + 2 < cmd.ElemCount; element += 3)
                {
                    var a = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + element)])];
                    var b = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + element + 1)])];
                    var c = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + element + 2)])];
                    var area = Edge(a.Pos, b.Pos, c.Pos);
                    if (MathF.Abs(area) < 0.001f) continue;
                    var min = Vector2.Max(new Vector2(cmd.ClipRect.X, cmd.ClipRect.Y), Vector2.Min(a.Pos, Vector2.Min(b.Pos, c.Pos)));
                    var max = Vector2.Min(new Vector2(cmd.ClipRect.Z, cmd.ClipRect.W), Vector2.Max(a.Pos, Vector2.Max(b.Pos, c.Pos)));
                    for (var y = Math.Max(0, (int)MathF.Floor(min.Y)); y < Math.Min(height, (int)MathF.Ceiling(max.Y)); y++)
                    for (var x = Math.Max(0, (int)MathF.Floor(min.X)); x < Math.Min(width, (int)MathF.Ceiling(max.X)); x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var wa = Edge(b.Pos, c.Pos, point) / area;
                        var wb = Edge(c.Pos, a.Pos, point) / area;
                        var wc = 1 - wa - wb;
                        if (wa < 0 || wb < 0 || wc < 0) continue;
                        var uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
                        var tx=uv.X*image.Width-.5f;var ty=uv.Y*image.Height-.5f;
                        var x0=(int)MathF.Floor(tx);var y0=(int)MathF.Floor(ty);var fx=tx-x0;var fy=ty-y0;
                        float Sample(int channel){
                            float At(int xx,int yy)=>image.Pixels[(Math.Clamp(yy,0,image.Height-1)*image.Width+Math.Clamp(xx,0,image.Width-1))*4+channel];
                            return (At(x0,y0)*(1-fx)+At(x0+1,y0)*fx)*(1-fy)+(At(x0,y0+1)*(1-fx)+At(x0+1,y0+1)*fx)*fy;
                        }
                        var alpha = ((a.Col >> 24) * wa + (b.Col >> 24) * wb + (c.Col >> 24) * wc) / 255f * Sample(3) / 255f;
                        var offset = (y * width + x) * 4;
                        for (var channel = 0; channel < 3; channel++)
                        {
                            var shift = channel * 8;
                            var value = ((byte)(a.Col >> shift) * wa + (byte)(b.Col >> shift) * wb + (byte)(c.Col >> shift) * wc) * Sample(channel) / 255f;
                            pixels[offset + channel] = (byte)Math.Clamp(value * alpha + pixels[offset + channel] * (1 - alpha), 0, 255);
                        }
                    }
                }
            }
        }
        WritePng(output, width, height, pixels);
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 c) => (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);

    private static void WritePng(string output, int width, int height, byte[] rgba)
    {
        using var file = File.Create(output);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk(file, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            for (var y = 0; y < height; y++) { zlib.WriteByte(0); zlib.Write(rgba.AsSpan(y * width * 4, width * 4)); }
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void Chunk(Stream output, string name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number);
        var kind = Encoding.ASCII.GetBytes(name); output.Write(kind); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (var b in kind.Concat(data))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
    }
}
