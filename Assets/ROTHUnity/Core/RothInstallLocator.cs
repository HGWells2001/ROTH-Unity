using System;
using System.Collections.Generic;
using System.IO;

namespace ROTHUnity.Core
{
    public sealed class RothInstallPaths
    {
        public string Root;
        public string Study1Raw;
        public string DemoDas;
        public string AdemoDas;
        public string SfxArchive;
        public string DBase100;
        public string DBase300;
        public string DBase400;
        public string DBase500;
        public string GdvDirectory;
        public bool IsComplete { get { return File.Exists(Study1Raw) && File.Exists(DemoDas); } }
    }

    public static class RothInstallLocator
    {
        public const string PreferredGogGalaxyPath = @"C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting";

        public static RothInstallPaths Locate(string preferredRoot = null)
        {
            foreach (string root in CandidateRoots(preferredRoot))
            {
                RothInstallPaths p = ResolveRoot(root);
                if (p != null && p.IsComplete) return p;
            }
            return null;
        }

        public static RothInstallPaths ResolveRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
            string raw = FindMapFile(root, "STUDY1.RAW");
            string demo = FindMapFile(root, "DEMO.DAS");
            string ademo = FindMapFile(root, "ADEMO.DAS");
            string sfx = FindSfx(root);
            string dbase100 = FindData(root, "DBASE100.DAT");
            string dbase300 = FindData(root, "DBASE300.DAT");
            string dbase400 = FindData(root, "DBASE400.DAT");
            string dbase500 = FindData(root, "DBASE500.DAT");
            string gdvDir = FindGdvDirectory(root);
            return new RothInstallPaths { Root = root, Study1Raw = raw, DemoDas = demo, AdemoDas = ademo, SfxArchive = sfx, DBase100 = dbase100, DBase300 = dbase300, DBase400 = dbase400, DBase500 = dbase500, GdvDirectory = gdvDir };
        }

        private static IEnumerable<string> CandidateRoots(string preferredRoot)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] fixedCandidates = {
                preferredRoot,
                PreferredGogGalaxyPath,
                @"C:\GOG Games\Realms of the Haunting",
                @"C:\Program Files (x86)\GOG.com\Realms of the Haunting",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games", "Realms of the Haunting")
            };
            foreach (string c in fixedCandidates)
                if (!string.IsNullOrWhiteSpace(c) && seen.Add(c)) yield return c;

        }


        public static string FindGdvDirectory(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return null;
            string[] dirs = { Path.Combine(root,"DATA","GDV"), Path.Combine(root,"ROTH","GDV"), Path.Combine(root,"GDV") };
            foreach(string d in dirs) if(Directory.Exists(d)) return d;
            return null;
        }

        public static string FindGdvFile(string root, string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return null;
            string dir=FindGdvDirectory(root); if(string.IsNullOrEmpty(dir)) return null;
            string clean=Path.GetFileNameWithoutExtension(name.Trim('\0',' ','\t'));
            string[] exts={".GDV",".gdv",""};
            foreach(string ext in exts){string p=Path.Combine(dir,clean+ext);if(File.Exists(p))return p;}
            return null;
        }

        private static string FindData(string root, string filename)
        {
            string[] relative = { Path.Combine("DATA", filename), Path.Combine("ROTH", filename), filename };
            foreach (string r in relative) { string p = Path.Combine(root, r); if (File.Exists(p)) return p; }
            return null;
        }

        private static string FindSfx(string root)
        {
            string[] relative={Path.Combine("DATA","DATA","FX22.SFX"),Path.Combine("ROTH","DATA","FX22.SFX"),Path.Combine("DATA","DATA","FXSCRIPT.SFX"),Path.Combine("ROTH","DATA","FXSCRIPT.SFX"),Path.Combine("DATA","FXSCRIPT.SFX")};
            foreach(string r in relative){string p=Path.Combine(root,r);if(File.Exists(p))return p;} return null;
        }

        public static string FindMapFile(string root, string filename)
        {
            string[] relative = {
                Path.Combine("DATA", "M", filename),
                Path.Combine("ROTH", "M", filename),
                Path.Combine("M", filename),
                filename
            };
            foreach (string r in relative)
            {
                string p = Path.Combine(root, r);
                if (File.Exists(p)) return p;
            }
            return null;
        }
        public static List<string> ListMapFiles(string root)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(root)) return result;
            string[] dirs = { Path.Combine(root,"DATA","M"), Path.Combine(root,"ROTH","M"), Path.Combine(root,"M") };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (string f in Directory.GetFiles(d,"*.RAW")) if (seen.Add(Path.GetFileName(f))) result.Add(f);
            }
            result.Sort((a,b)=>string.Compare(Path.GetFileName(a),Path.GetFileName(b),StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static List<string> ListPrimaryDasFiles(string root)
        {
            var result = new List<string>();
            for(int i=0;i<=4;i++)
            {
                string name=i==0?"DEMO.DAS":"DEMO"+i+".DAS";
                string p=FindMapFile(root,name);
                if(!string.IsNullOrEmpty(p)) result.Add(p);
            }
            return result;
        }

    }
}
