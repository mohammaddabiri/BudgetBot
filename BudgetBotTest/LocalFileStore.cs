using System;
using System.IO;
using System.Text;

namespace Engine.Storage
{
	public class LocalFileStore : IFileStore
	{
		public bool Logging;
		
		private string m_rootFolder;
		
		public LocalFileStore (string rootFolder)
		{
			m_rootFolder = rootFolder.Trim();
		}
		
		private string AbsoluteFilePath(string relativeFilePath)
		{
			var filePath = relativeFilePath.Trim ().TrimEnd (s_directorySeparators);
			
			return Path.Combine(m_rootFolder, filePath);
		}
		
		private bool EnsureDirectoryExists(string directoryPath)
		{
			directoryPath = string.Concat(directoryPath.TrimEnd(s_directorySeparators), Path.DirectorySeparatorChar);
			
			if (!Directory.Exists (directoryPath))
				Directory.CreateDirectory(directoryPath);
			
			return Directory.Exists (directoryPath);
		}

        public FileStream OpenForWrite(string filePath)
		{
			filePath = AbsoluteFilePath (filePath);
			EnsureDirectoryExists (Path.GetDirectoryName (filePath));
			
			if(File.Exists(filePath))
				return File.OpenWrite(filePath);
			
			return File.Create(filePath);							
		}
		
		public bool FileExists(string filePath)
		{
			return File.Exists (AbsoluteFilePath(filePath));
		}
		
		public long GetFileSize(string filePath)
		{
			var fileInfo = new FileInfo (AbsoluteFilePath(filePath));
			return fileInfo.Length;
		}

        public int FileRead(string filePath, out string fileData)
        {
            var fileSize = GetFileSize(filePath);
            Byte[] bytes = new Byte[fileSize];
            FileRead(filePath, bytes, fileSize);
            fileData = GetString(bytes);
            return 0;
        }

        public int FileRead(string filePath, byte[] fileData, long count)
        {
            BinaryReader reader = null;
            filePath = AbsoluteFilePath(filePath);
            try
            {
                using (reader = new BinaryReader(File.OpenRead(filePath), Encoding.UTF8))
                {
                    return reader.Read(fileData, 0, (int)count);
                }
            }
            catch
            {
                return 0;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
        }

        public bool FileWrite(string filePath, string contents)
        {
            var bytes = GetBytes(contents);
            return FileWrite(filePath, bytes, bytes.Length);
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        public bool FileWrite(string filePath, byte[] fileData, int count)
		{
			filePath = AbsoluteFilePath (filePath);
			EnsureDirectoryExists (Path.GetDirectoryName (filePath));
			
		    BinaryWriter writer = null;
		    try
            {
                using (writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
                {
                    writer.Write(fileData, 0, count);
                    
                    return true;
                    
                }

		    }
		    catch
		    {
		        return false;
		    }

		    finally
		    {
                if (writer != null)
		        {
                    writer.Close();
		        }
		    }
		}
		
		public long GetFileTimestamp(string filePath)
		{
			filePath = AbsoluteFilePath (filePath);
			if (!File.Exists (filePath))
				return 0;

			return File.GetLastWriteTimeUtc (filePath).ToFileTimeUtc ();
		}

		private static readonly char[] s_directorySeparators = {
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		};
	}
}