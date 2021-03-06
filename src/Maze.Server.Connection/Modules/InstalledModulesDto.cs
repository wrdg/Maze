using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace Maze.Server.Connection.Modules
{
    public class InstalledModulesDto
    {
        public List<PackageIdentity> Installed { get; set; }
        public List<PackageIdentity> ToBeInstalled { get; set; }
    }
}