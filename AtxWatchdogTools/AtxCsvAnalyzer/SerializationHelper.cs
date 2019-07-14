using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AtxCsvAnalyzer
{
    public static class SerializationHelper
    {
        public static void SerializeTo<T>(string filename, T instance)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                SerializeToStream(fs, instance);
        }

        public static byte[] SerializeToArray<T>(T instance)
        {
            MemoryStream mem = new MemoryStream();
            SerializeToStream(mem, instance);
            mem.Position = 0;

            byte[] buffer = mem.ToArray();
            mem.Dispose();

            return buffer;
        }

        public static void SerializeToStream<T>(Stream stream, T instance)
        {
            XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, NewLineChars = "\r\n", Encoding = new UTF8Encoding(false) });
            DataContractSerializer ser = new DataContractSerializer(typeof(T));

            ser.WriteObject(writer, instance);
            
            writer.Flush();
            writer.Dispose();
        }

        public static T DeserializeFrom<T>(string filename)
        {
            T obj;
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                obj = DeserializeFrom<T>(fs);

            return obj;
        }

        public static T DeserializeFrom<T>(Stream stream)
        {
            DataContractSerializer ser = new DataContractSerializer(typeof(T));
            T obj = (T)ser.ReadObject(stream);

            return obj;
        }
    }
}
