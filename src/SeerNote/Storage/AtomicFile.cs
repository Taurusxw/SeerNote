using System;
using System.IO;

namespace SeerNote.Storage
{
    /// <summary>
    /// Keeps the short, failure-sensitive filesystem portion of a save isolated
    /// from serialization and recovery policy.
    /// </summary>
    internal static class AtomicFile
    {
        public static void WriteAndFlush(string path, byte[] contents)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
        }

        public static void Replace(string temporaryPath, string destinationPath, string backupPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, backupPath, true);
                return;
            }

            File.Move(temporaryPath, destinationPath);
        }

        public static byte[] ReadAllBytes(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
    }
}
