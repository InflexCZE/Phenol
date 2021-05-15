using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace NetPrints.Core
{
    [DataContract]
    public class PackageReference : CompilationReference
    {
        [DataMember]
        public string PackageRef { get; set; }

        public string PackagePath
        {
            get => GetRootedPath(this.PackageRef);
        }

        public Project LoadedPackage { get; set; }

        public PackageReference(string packageRef)
        {
            this.PackageRef = packageRef;
        }

        public override string ToString() => $"{Path.GetFileNameWithoutExtension(this.PackageRef)} at {this.PackageRef}";
    }
}
