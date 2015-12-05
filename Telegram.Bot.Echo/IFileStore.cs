namespace Engine.Storage
{
	public interface IFileStore
	{
		bool FileExists (string filePath);
		long GetFileSize (string filePath);
		int FileRead (string filePath, byte[] fileData, long count);
		bool FileWrite (string filePath, byte[] fileData, int count);
		long GetFileTimestamp(string filePath);
    }
}