using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCGet.Platforms
{
    public abstract class Platform
    {
        public abstract bool InstallDependencies();

        public abstract bool DownloadMods();

        public abstract bool InstallMods();

    }
}
