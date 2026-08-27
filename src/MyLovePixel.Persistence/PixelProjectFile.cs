using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Persistence;

public static class PixelProjectFile
{
    private static readonly DateTimeOffset DeterministicZipTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Save(string path, PixelProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);

        var package = BuildPackage(project);
        var expectedSemanticHash = ProjectSemanticHash.Compute(project.Document);

        try
        {
            AtomicFileWriter.Write(
                path,
                stream => WriteArchive(stream, package),
                tempPath =>
                {
                    var reloaded = Load(tempPath);
                    var actualSemanticHash = ProjectSemanticHash.Compute(reloaded.Document);
                    if (!string.Equals(expectedSemanticHash, actualSemanticHash, StringComparison.Ordinal))
                        throw new PixelProjectException(
                            PixelProjectErrorCode.ValidationFailed,
                            "Atomic save verification produced a semantic mismatch before commit.");
                });

            project.PersistenceState = package.PersistenceState;
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.IoFailure, $"Failed to save project '{path}'.", innerException: ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.IoFailure, $"Access was denied while saving project '{path}'.", innerException: ex);
        }
    }

    public static void Save(Stream destination, PixelProject project)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(project);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));

        var package = BuildPackage(project);
        WriteArchive(destination, package);
        project.PersistenceState = package.PersistenceState;
    }

    public static PixelProject Load(
        string path,
        ProjectMigrationRegistry? migrations = null,
        PixelProjectLoadLimits? limits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, FileOptions.SequentialScan);
            return Load(stream, migrations, limits);
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.IoFailure, $"Failed to load project '{path}'.", innerException: ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.IoFailure, $"Access was denied while loading project '{path}'.", innerException: ex);
        }
    }

    public static PixelProject Load(
        Stream source,
        ProjectMigrationRegistry? migrations = null,
        PixelProjectLoadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Source stream must be readable.", nameof(source));

        var effectiveLimits = limits ?? new PixelProjectLoadLimits();
        ValidateLimits(effectiveLimits);
        var entries = ReadEntries(source, effectiveLimits);

        if (!entries.TryGetValue(PixelProjectFormat.ManifestEntry, out var manifestBytes))
            throw new PixelProjectException(PixelProjectErrorCode.MissingEntry, "Project is missing manifest.json.", PixelProjectFormat.ManifestEntry);

        var manifestNode = ProjectJson.ParseObject(manifestBytes, PixelProjectFormat.ManifestEntry);
        var format = GetRequiredString(manifestNode, "format", PixelProjectFormat.ManifestEntry);
        if (!string.Equals(format, PixelProjectFormat.FormatMarker, StringComparison.Ordinal))
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Unsupported project format marker '{format}'.", PixelProjectFormat.ManifestEntry);

        var originalSchemaVersion = ProjectMigrationRegistry.ReadSchemaVersion(manifestNode);
        if (originalSchemaVersion > PixelProjectFormat.CurrentSchemaVersion)
            throw new PixelProjectException(
                PixelProjectErrorCode.UnsupportedSchemaVersion,
                $"Project schema {originalSchemaVersion} is newer than supported schema {PixelProjectFormat.CurrentSchemaVersion}.",
                PixelProjectFormat.ManifestEntry);

        var originalDocumentEntry = GetRequiredString(manifestNode, "documentEntry", PixelProjectFormat.ManifestEntry);
        ProjectEntryName.Validate(originalDocumentEntry);
        if (!entries.TryGetValue(originalDocumentEntry, out var documentBytes))
            throw new PixelProjectException(PixelProjectErrorCode.MissingEntry, $"Project is missing document entry '{originalDocumentEntry}'.", originalDocumentEntry);

        var expectedContentHash = GetRequiredString(manifestNode, "contentHash", PixelProjectFormat.ManifestEntry);
        var hashedEntries = entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal));
        if (!ProjectContentHash.Matches(expectedContentHash, hashedEntries))
            throw new PixelProjectException(
                PixelProjectErrorCode.ContentHashMismatch,
                "Project content hash does not match the logical archive entries.",
                PixelProjectFormat.ManifestEntry);

        var documentNode = ProjectJson.ParseObject(documentBytes, originalDocumentEntry);
        if (originalSchemaVersion < PixelProjectFormat.CurrentSchemaVersion)
        {
            (migrations ?? ProjectMigrationRegistry.CreateDefault()).Migrate(
                manifestNode,
                documentNode,
                PixelProjectFormat.CurrentSchemaVersion);
        }

        var migratedManifestBytes = Encoding.UTF8.GetBytes(manifestNode.ToJsonString(ProjectJson.Options));
        var migratedDocumentBytes = Encoding.UTF8.GetBytes(documentNode.ToJsonString(ProjectJson.Options));
        var manifest = ProjectJson.Deserialize<ManifestDto>(migratedManifestBytes, PixelProjectFormat.ManifestEntry);
        var documentDto = ProjectJson.Deserialize<DocumentDto>(migratedDocumentBytes, originalDocumentEntry);

        if (manifest.SchemaVersion != PixelProjectFormat.CurrentSchemaVersion)
            throw new PixelProjectException(PixelProjectErrorCode.MigrationInvalid, "Migration did not produce the current schema version.", PixelProjectFormat.ManifestEntry);
        if (!string.Equals(manifest.Format, PixelProjectFormat.FormatMarker, StringComparison.Ordinal))
            throw new PixelProjectException(PixelProjectErrorCode.MigrationInvalid, "Migration changed the project format marker.", PixelProjectFormat.ManifestEntry);

        var document = ProjectMapper.FromDto(documentDto, entries);

        var recognized = new HashSet<string>(StringComparer.Ordinal)
        {
            PixelProjectFormat.ManifestEntry,
            originalDocumentEntry,
            manifest.DocumentEntry,
        };
        foreach (var surface in documentDto.Surfaces) recognized.Add(surface.Entry);

        var opaqueEntries = entries
            .Where(pair => !recognized.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        var state = new ProjectPersistenceState(manifest, documentDto, opaqueEntries);
        return new PixelProject(document, state);
    }

    private static PackageBuild BuildPackage(PixelProject project)
    {
        var templateManifest = project.PersistenceState.ManifestTemplate;
        var documentDto = ProjectMapper.ToDto(project.Document, project.PersistenceState.DocumentTemplate);

        var documentEntry = string.IsNullOrWhiteSpace(templateManifest?.DocumentEntry)
            ? PixelProjectFormat.DocumentEntry
            : templateManifest.DocumentEntry;
        ProjectEntryName.Validate(documentEntry);
        if (string.Equals(documentEntry, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal))
            throw new PixelProjectException(PixelProjectErrorCode.DuplicateEntry, "documentEntry cannot be manifest.json.", documentEntry);

        var logicalEntries = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [documentEntry] = ProjectJson.Serialize(documentDto),
        };

        foreach (var surfaceDto in documentDto.Surfaces)
        {
            ProjectEntryName.Validate(surfaceDto.Entry);
            if (string.Equals(surfaceDto.Entry, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal))
                throw new PixelProjectException(PixelProjectErrorCode.DuplicateEntry, "A surface cannot use manifest.json as its entry.", surfaceDto.Entry);

            var resourceId = new ResourceId(Guid.ParseExact(surfaceDto.Id, "N"));
            var encoded = PixelSurfaceBinaryCodec.Encode(project.Document.Resources.GetSurface(resourceId));
            if (!logicalEntries.TryAdd(surfaceDto.Entry, encoded))
                throw new PixelProjectException(PixelProjectErrorCode.DuplicateEntry, $"Duplicate logical project entry '{surfaceDto.Entry}'.", surfaceDto.Entry);
        }

        foreach (var pair in project.PersistenceState.OpaqueEntries)
        {
            ProjectEntryName.Validate(pair.Key);
            if (string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal) || !logicalEntries.TryAdd(pair.Key, pair.Value))
                throw new PixelProjectException(PixelProjectErrorCode.DuplicateEntry, $"Opaque payload collides with reserved entry '{pair.Key}'.", pair.Key);
        }

        var manifest = new ManifestDto
        {
            Format = PixelProjectFormat.FormatMarker,
            SchemaVersion = PixelProjectFormat.CurrentSchemaVersion,
            DocumentEntry = documentEntry,
            ContentHash = ProjectContentHash.Compute(logicalEntries),
            ExtensionData = ExtensionData.Clone(templateManifest?.ExtensionData),
        };

        var state = new ProjectPersistenceState(manifest, documentDto, project.PersistenceState.OpaqueEntries);
        return new PackageBuild(manifest, logicalEntries, state);
    }

    private static void WriteArchive(Stream destination, PackageBuild package)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
        WriteEntry(archive, PixelProjectFormat.ManifestEntry, ProjectJson.Serialize(package.Manifest));
        foreach (var pair in package.LogicalEntries.OrderBy(x => x.Key, StringComparer.Ordinal))
            WriteEntry(archive, pair.Key, pair.Value);
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicZipTime;
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static Dictionary<string, byte[]> ReadEntries(Stream source, PixelProjectLoadLimits limits)
    {
        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
            if (archive.Entries.Count > limits.MaxEntries)
                throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Project contains more than {limits.MaxEntries} entries.");

            var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/', StringComparison.Ordinal) && entry.Length == 0) continue;
                ProjectEntryName.Validate(entry.FullName);
                if (entry.Length < 0 || entry.Length > limits.MaxEntryBytes || entry.Length > int.MaxValue)
                    throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Entry '{entry.FullName}' exceeds the allowed size.", entry.FullName);

                totalBytes = checked(totalBytes + entry.Length);
                if (totalBytes > limits.MaxTotalBytes)
                    throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, "Project exceeds the configured uncompressed size limit.");

                using var input = entry.Open();
                using var buffer = new MemoryStream((int)entry.Length);
                input.CopyTo(buffer);
                if (buffer.Length != entry.Length)
                    throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Entry '{entry.FullName}' length changed while reading.", entry.FullName);

                if (!entries.TryAdd(entry.FullName, buffer.ToArray()))
                    throw new PixelProjectException(PixelProjectErrorCode.DuplicateEntry, $"Project contains duplicate entry '{entry.FullName}'.", entry.FullName);
            }

            return entries;
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, "Project is not a valid ZIP container.", innerException: ex);
        }
        catch (OverflowException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, "Project uncompressed size overflowed configured limits.", innerException: ex);
        }
    }

    private static string GetRequiredString(JsonObject node, string propertyName, string entryName)
    {
        if (node[propertyName] is JsonValue value && value.TryGetValue<string>(out var result) && !string.IsNullOrWhiteSpace(result))
            return result;
        throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, $"Entry '{entryName}' must contain non-empty string property '{propertyName}'.", entryName);
    }

    private static void ValidateLimits(PixelProjectLoadLimits limits)
    {
        if (limits.MaxEntries <= 0) throw new ArgumentOutOfRangeException(nameof(limits), "MaxEntries must be positive.");
        if (limits.MaxEntryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(limits), "MaxEntryBytes must be positive.");
        if (limits.MaxTotalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(limits), "MaxTotalBytes must be positive.");
        if (limits.MaxTotalBytes < limits.MaxEntryBytes)
            throw new ArgumentException("MaxTotalBytes cannot be smaller than MaxEntryBytes.", nameof(limits));
    }

    private sealed record PackageBuild(
        ManifestDto Manifest,
        Dictionary<string, byte[]> LogicalEntries,
        ProjectPersistenceState PersistenceState);
}
