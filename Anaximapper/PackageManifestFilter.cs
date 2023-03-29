using System.Collections.Generic;
using Umbraco.Cms.Core.Manifest;

namespace Anaximapper
{
    internal sealed class PackageManifestFilter : IManifestFilter
    {
        public void Filter(List<PackageManifest> manifests) =>
            manifests.Add(new PackageManifest
            {
                PackageName = "Anaximapper",
            });
    }
}
