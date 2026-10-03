using System;
using System.IO;
using System.Text;

namespace RothUnity.Formats
{
    public enum RothFileKind
    {
        Unknown,
        DBase100,
        DBase200,
        DBase300,
        DBase400,
        DBase500,
        Das,
        Gdv
    }

    public static class RothFormatDetector
    {
        private static readonly byte[] GdvMagic = { 0x94, 0x19, 0x11, 0x29 };

        public static RothFileKind Detect(string path)
        {
            using (var stream = File.OpenRead(path))
                return Detect(stream);
        }

        public static RothFileKind Detect(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", "stream");

            var origin = stream.CanSeek ? stream.Position : 0;
            var header = new byte[8];
            var read = 0;

            while (read < header.Length)
            {
                var count = stream.Read(header, read, header.Length - read);
                if (count == 0)
                    break;
                read += count;
            }

            if (stream.CanSeek)
                stream.Position = origin;

            if (read >= 8)
            {
                var signature = Encoding.ASCII.GetString(header, 0, 8);
                switch (signature)
                {
                    case "DBASE100": return RothFileKind.DBase100;
                    case "DBASE200": return RothFileKind.DBase200;
                    case "DBASE300": return RothFileKind.DBase300;
                    case "DBASE400": return RothFileKind.DBase400;
                    case "DBASE500": return RothFileKind.DBase500;
                }
            }

            if (read >= 4)
            {
                if (header[0] == (byte)'D' &&
                    header[1] == (byte)'A' &&
                    header[2] == (byte)'S' &&
                    header[3] == (byte)'P')
                    return RothFileKind.Das;

                if (header[0] == GdvMagic[0] &&
                    header[1] == GdvMagic[1] &&
                    header[2] == GdvMagic[2] &&
                    header[3] == GdvMagic[3])
                    return RothFileKind.Gdv;
            }

            return RothFileKind.Unknown;
        }
    }
}
