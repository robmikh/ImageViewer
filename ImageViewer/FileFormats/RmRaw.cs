using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace ImageViewer.FileFormats
{
    enum RmRawPixelFormat : uint
    {
        BGRA8 = 0,
        RGB8 = 1,
    }

    struct RmRawHeader
    {
        public byte[] Magic;
        public uint Version;
        public uint Width;
        public uint Height;
        public RmRawPixelFormat PixelFormat;
    }

    class RmRawImage
    {
        public uint Width { get; }
        public uint Height { get; }
        public RmRawPixelFormat PixelFormat { get; }
        public byte[] PixelBytes { get; }

        internal RmRawImage(RmRawHeader header, byte[] bytes)
        {
            Width = header.Width;
            Height = header.Height;
            PixelFormat = header.PixelFormat;
            PixelBytes = bytes;
        }
    }

    static class RmRaw
    {
        // "rmraw\0"
        static byte[] MAGIC = new byte[] { 114, 109, 114, 97, 119, 0 };
        static uint MAX_SUPPORTED_VERSION = 1;

        public static async Task WriteImageAsync(IRandomAccessStream stream, uint width, uint height, RmRawPixelFormat format, byte[] bytes)
        {
            if (bytes.Length != width * height * BytesPerPixel(format))
            {
                throw new ArgumentException();
            }
            
            using (var writer = new DataWriter(stream))
            {
                WriteHeader(writer, width, height, format);
                WritePixels(writer, bytes);

                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
        }

        static void WriteHeader(DataWriter writer, uint width, uint height, RmRawPixelFormat format)
        {
            writer.WriteBytes(MAGIC);
            writer.WriteUInt32(MAX_SUPPORTED_VERSION);
            writer.WriteUInt32(width);
            writer.WriteUInt32(height);
            writer.WriteUInt32((uint)format);
        }

        static void WritePixels(DataWriter writer, byte[] pixels)
        {
            writer.WriteBytes(pixels);
        }

        public static async Task<RmRawImage> ReadImageAsync(IRandomAccessStream stream)
        {
            RmRawHeader header;
            byte[] bytes;
            using (var reader = new DataReader(stream))
            {
                header = await ReadHeaderAsync(reader);

                if (header.Magic.Length != MAGIC.Length ||
                    header.Magic[0] != MAGIC[0] ||
                    header.Magic[1] != MAGIC[1] ||
                    header.Magic[2] != MAGIC[2] ||
                    header.Magic[3] != MAGIC[3] ||
                    header.Magic[4] != MAGIC[4] ||
                    header.Magic[5] != MAGIC[5])
                {
                    throw new Exception();
                }

                if (header.Version > MAX_SUPPORTED_VERSION)
                {
                    throw new Exception();
                }

                bytes = new byte[header.Width * header.Height * BytesPerPixel(header.PixelFormat)];
                await reader.LoadAsync((uint)bytes.Length);
                reader.ReadBytes(bytes);

                reader.DetachStream();
            }

            return new RmRawImage(header, bytes);
        }
        
        static async Task<RmRawHeader> ReadHeaderAsync(DataReader reader)
        {
            await reader.LoadAsync(6 + 4 + 4 + 4 + 4);
            var magic = new byte[6];
            reader.ReadBytes(magic);
            var version = reader.ReadUInt32();
            var width = reader.ReadUInt32();
            var height = reader.ReadUInt32();
            var format = (RmRawPixelFormat)reader.ReadUInt32();

            return new RmRawHeader { Magic = magic, Version = version, Width = width, Height = height, PixelFormat = format };
        }

        static uint BytesPerPixel(RmRawPixelFormat format)
        {
            switch (format)
            {
                case RmRawPixelFormat.BGRA8:
                    return 4;
                case RmRawPixelFormat.RGB8:
                    return 3;
                default:
                    throw new ArgumentException();
            }
        }
    }
}
