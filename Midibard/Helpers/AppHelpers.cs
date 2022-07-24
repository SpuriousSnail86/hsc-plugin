using HSC.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace HSC.Helpers
{

    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public static class AppHelpers
    {

        public static string GetAppRelativePath(string path)
        {
            try
            {
                path = path.Substring(Settings.CurrentAppPath.Length + 1);
                return path;
            }
            catch
            {
                return path;
            }
        }
    }
}
