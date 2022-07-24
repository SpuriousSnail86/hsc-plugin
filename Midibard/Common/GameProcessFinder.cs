using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSC.Common;

namespace HSC.Common
{    
    
    /// <summary>
     /// author:  SpuriousSnail86
     /// </summary>
    public class ProcessFinder
    {
        public static Process[] Find(string name)
        {
            Process[] processes = Process.GetProcessesByName(name);

            if (processes.IsNullOrEmpty())
                return null;

            return processes;
        }

    }
}
