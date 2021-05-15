using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    [DataContract]
    public class SourceDirectoryReference : CompilationReference
    {
        /// <summary>
        /// All source file paths in the source directory.
        /// </summary>
        public IEnumerable<string> SourceFilePaths
        {
            get
            {
                var directory = GetRootedPath(this.SourceDirectory);
                return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                                .Where(p => !p.Contains("obj" + Path.DirectorySeparatorChar) && !p.Contains("bin" + Path.DirectorySeparatorChar));
            }
        }

        /// <summary>
        /// Whether to include the source files in compilation.
        /// </summary>
        [DataMember]
        public bool IncludeInCompilation { get; set; }

        /// <summary>
        /// Path of source directory.
        /// </summary>
        [DataMember]
        public string SourceDirectory { get; set; }

        public SourceDirectoryReference(string directory, bool includeInCompilation = false)
        {
            this.SourceDirectory = directory;
            this.IncludeInCompilation = includeInCompilation;
        }

        public override string ToString() => $"Source files at {this.SourceDirectory}";
    }
}
