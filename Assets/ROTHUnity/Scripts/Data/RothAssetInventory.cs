using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RothUnity.Data
{
    public sealed class RothAssetInventory
    {
        private readonly Dictionary<string, List<string>> _byExtension =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static RothAssetInventory Scan(string root)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);

            var inventory = new RothAssetInventory();

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(path);
                if (string.IsNullOrEmpty(extension))
                    extension = "<none>";

                List<string> bucket;
                if (!inventory._byExtension.TryGetValue(extension, out bucket))
                {
                    bucket = new List<string>();
                    inventory._byExtension.Add(extension, bucket);
                }

                bucket.Add(path);
            }

            return inventory;
        }

        public int Count(string extension)
        {
            List<string> bucket;
            return _byExtension.TryGetValue(extension, out bucket) ? bucket.Count : 0;
        }

        public IEnumerable<string> Extensions
        {
            get { return _byExtension.Keys.OrderBy(x => x); }
        }

        public IEnumerable<string> Files(string extension)
        {
            List<string> bucket;
            return _byExtension.TryGetValue(extension, out bucket)
                ? bucket
                : Enumerable.Empty<string>();
        }
    }
}
