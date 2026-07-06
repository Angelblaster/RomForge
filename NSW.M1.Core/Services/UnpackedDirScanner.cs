using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.M1.Core.Models;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace NSW.M1.Core.Services;

public static class UnpackedDirScanner
{
    public static UnpackResult Scan(string unpackedDir, uint? overrideSdkVersion = null, byte? overrideKeyGeneration = null)
    {
        string nacpPath = Path.Combine(unpackedDir, "control", "control.nacp");
        string controlFile = Directory.GetFiles(unpackedDir, "control*.nca").FirstOrDefault() ?? string.Empty;
        var (krTitle, enTitle, displayVersion, titleId) = LibHacHelper.ReadNacpInfo(nacpPath);
        byte keyGeneration = 1;
        uint sdkVersion = 0;
        uint gameVersion = 0;

        if (File.Exists(controlFile))
        {
            (keyGeneration, sdkVersion) = LibHacHelper.ReadControlNcaInfo(controlFile);

            string fileName = Path.GetFileNameWithoutExtension(controlFile);

            if (fileName.Contains('_'))
                _ = uint.TryParse(fileName.Split('_')[1], out gameVersion);
        }

        var dlcs = new List<DlcUnpackInfo>();
        string dlcBaseDir = Path.Combine(unpackedDir, "DLCs");

        if (Directory.Exists(dlcBaseDir))
        {
            foreach (var dlcDir in Directory.GetDirectories(dlcBaseDir))
            {
                string titleIdStr = Path.GetFileName(dlcDir);
                if (ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong dlcTitleId))
                {
                    dlcs.Add(new DlcUnpackInfo
                    {
                        TitleId = dlcTitleId,
                        Dir = Path.Combine("DLCs", titleIdStr)
                    });
                }
            }
        }

        var exefsDirs = new Dictionary<byte, string>();
        var romfsDirs = new Dictionary<byte, string>();
        var logoDirs = new Dictionary<byte, string>();
        var controlDirs = new Dictionary<byte, string>();
        var htmlDocDirs = new Dictionary<byte, string>();
        var legalDirs = new Dictionary<byte, string>();
        var rawProgramNcaPaths = new Dictionary<byte, string>();

        for (byte i = 0; i < 16; i++)
        {
            string suffix = i == 0 ? string.Empty : i.ToString();
            string exefs = Path.Combine(unpackedDir, $"exefs{suffix}");

            if (Directory.Exists(exefs))
                exefsDirs[i] = exefs;

            string romfs = Path.Combine(unpackedDir, $"romfs{suffix}");

            if (Directory.Exists(romfs))
                romfsDirs[i] = romfs;

            string logo = Path.Combine(unpackedDir, $"logo{suffix}");

            if (Directory.Exists(logo))
                logoDirs[i] = logo;

            string control = Path.Combine(unpackedDir, $"control{suffix}");

            if (Directory.Exists(control))
                controlDirs[i] = control;

            string htmldoc = Path.Combine(unpackedDir, $"htmldoc{suffix}");

            if (Directory.Exists(htmldoc))
                htmlDocDirs[i] = htmldoc;

            string legal = Path.Combine(unpackedDir, $"legal{suffix}");

            if (Directory.Exists(legal))
                legalDirs[i] = legal;

            string rawDir = Path.Combine(unpackedDir, "rawprograms");

            if (Directory.Exists(rawDir))
            {
                var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

                foreach (var ncaFile in Directory.GetFiles(rawDir, "*.nca"))
                {
                    using var fs = new FileStream(ncaFile, FileMode.Open, FileAccess.Read);
                    var nca = new Nca(keySet, new StreamStorage(fs, false));

                    byte offset = (byte)(nca.Header.TitleId - titleId);
                    rawProgramNcaPaths[offset] = ncaFile;
                }
            }

            if (i > 0 && !exefsDirs.ContainsKey(i) && !romfsDirs.ContainsKey(i) && !rawProgramNcaPaths.ContainsKey(i))
                break;
        }

        return new UnpackResult
        {
            TitleId = titleId,
            GameVersion = gameVersion,
            BaseSdkVersion = overrideSdkVersion ?? sdkVersion,
            BaseKeyGeneration = overrideKeyGeneration ?? keyGeneration,
            DisplayVersion = displayVersion,
            KrTitle = krTitle,
            EnTitle = enTitle,
            ExefsDirs = exefsDirs,
            RomfsDirs = romfsDirs,
            LogoDirs = logoDirs,
            ControlDirs = controlDirs,
            HtmlDocDirs = htmlDocDirs,
            LegalDirs = legalDirs,
            Dlcs = dlcs,
            RawProgramNcaPaths = rawProgramNcaPaths,
        };
    }
}