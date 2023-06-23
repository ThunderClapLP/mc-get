using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCGet.ModLoaders
{
    public abstract class ModLoader
    {
        public abstract bool Install(String minecraftVersion, String loaderVersion);
    }
}
