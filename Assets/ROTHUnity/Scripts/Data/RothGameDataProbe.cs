using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace RothUnity.Data
{
    public sealed class RothFileInfo
    {
        public RothFileInfo(string name, string path, long size)
        {
            Name = name;
            Path = path;
            Size = size;
        }

        public string Name { get; private set; }
        public string Path { get; private set; }
        public long Size { get; private set; }

        public string ComputeSha256()
        {
            using (var stream = File.OpenRead(Path))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }

    public sealed class RothProbeResult
    {
        public RothProbeResult(string root, IList<RothFileInfo> files, IList<string> missing)
        {
            Root = root;
            Files = files;
            Missing = missing;
        }

        public string Root { get; private set; }
        public IList<RothFileInfo> Files { get; private set; }
        public IList<string> Missing { get; private set; }
        public bool IsUsable { get { return Missing.Count == 0; } }
    }

    public static class RothGameDataProbe
    {
        public static readonly string[] CoreFiles =
        {
            "DBASE100.DAT",
            "DBASE200.DAT",
            "DBASE300.DAT",
            "DBASE400.DAT",
            "DBASE500.DAT",
            "ROTH.EXE",
            "ROTH.RES"
        };

        public static RothProbeResult Probe(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("A game-data root is required.", "root");

            var fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
                throw new DirectoryNotFoundException(fullRoot);

            var files = new List<RothFileInfo>();
            var missing = new List<string>();

            foreach (var fileName in CoreFiles)
            {
                var path = FindFile(fullRoot, fileName);
                if (path == null)
                {
                    missing.Add(fileName);
                    continue;
                }

                var info = new FileInfo(path);
                files.Add(new RothFileInfo(fileName, info.FullName, info.Length));
            }

            return new RothProbeResult(fullRoot, files, missing);
        }

        public static string FindFile(string root, string fileName)
        {
            var preferredDirectories = new[]
            {
                root,
                Path.Combine(root, "ROTH"),
                Path.Combine(root, "DATA"),
                Path.Combine(root, "DATA", "DATA"),
                Path.Combine(root, "ROTH", "DATA")
            };

            foreach (var directory in preferredDirectories)
            {
                if (!Directory.Exists(directory))
                    continue;

                var direct = Directory.EnumerateFiles(directory)
                    .FirstOrDefault(p => string.Equals(
                        Path.GetFileName(p),
                        fileName,
                        StringComparison.OrdinalIgnoreCase));

                if (direct != null)
                    return direct;
            }

            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .FirstOrDefault(p => string.Equals(
                    Path.GetFileName(p),
                    fileName,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
