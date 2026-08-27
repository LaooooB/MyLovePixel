namespace MyLovePixel.Persistence;

internal static class AtomicFileWriter
{
    public static void Write(string path, Action<Stream> writer, Action<string>? validateBeforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(writer);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Destination path must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var fileName = Path.GetFileName(fullPath);
        var tempPath = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        var committed = false;

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 64 * 1024,
                       FileOptions.WriteThrough))
            {
                writer(stream);
                stream.Flush(flushToDisk: true);
            }

            validateBeforeCommit?.Invoke(tempPath);
            File.Move(tempPath, fullPath, overwrite: true);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // The original exception is more important than temporary-file cleanup.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best-effort cleanup only.
                }
            }
        }
    }
}
