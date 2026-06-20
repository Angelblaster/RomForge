using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBP.Core.Services;

public record GameData
{
    public string EName;
    public string LName;
    public string Version;
    public string Serial;

    public GameData(string eName, string lName, string version, string serial) 
    {
        EName = eName;
        LName = lName;
        Version = version;
        Serial = serial;
    }
}

public static class DB
{
    private static readonly Dictionary<string, GameData> dic = new(11000);

    static DB()
    {
        
    }
}