using NetPrints.Core;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace NetPrints.Serialization
{
    public static class SerializationHelper
    {
        private static readonly DataContractSerializer classSerializer = new DataContractSerializer(typeof(ClassGraph), new DataContractSerializerSettings()
        {
            PreserveObjectReferences = true,
            MaxItemsInObjectGraph = int.MaxValue,
        });

        /// <summary>
        /// Saves a class to a path. The class can be loaded again using LoadClass.
        /// </summary>
        /// <param name="cls">Class to save.</param>
        /// <param name="outputPath">Path to save the class at.</param>
        public static void SaveClass(ClassGraph cls, string outputPath)
        {
            using var fileStream = File.Open(outputPath, FileMode.Create);
            using var writer = XmlWriter.Create(fileStream, new XmlWriterSettings { Indent = true });
            classSerializer.WriteObject(writer, cls);
        }

        /// <summary>
        /// Loads a class from a path.
        /// </summary>
        /// <param name="outputPath">Path to load the class from. Throws a FileLoadException if the read object was not a class.</param>
        public static ClassGraph LoadClass(string path)
        {
            using var fileStream = File.OpenRead(path);
            if (classSerializer.ReadObject(fileStream) is ClassGraph cls)
            {
                return cls;
            }

            throw new FileLoadException();
        }
    }
}
