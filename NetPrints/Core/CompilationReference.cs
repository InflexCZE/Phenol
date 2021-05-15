using System.IO;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    [DataContract]
    [KnownType(typeof(AssemblyReference))]
    [KnownType(typeof(FrameworkAssemblyReference))]
    [KnownType(typeof(SourceDirectoryReference))]
    public abstract class CompilationReference : ICompilationReference
    {
        public Project Project { get; internal set; }

        public string GetRootedPath(string path)
        {
            if(Path.IsPathRooted(path) == false)
            {
                var projectDir = Path.GetDirectoryName(this.Project.Path);
                if(projectDir is not null)
                {
                    path = Path.Combine(projectDir, path);
                }
            }

            return path;
        }
    }
}
