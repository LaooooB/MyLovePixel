using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class TilemapPersistenceTests
{
    [Fact]
    public void Tilemap_RoundTripPreservesSeedReferencesCellsAndSemanticHash()
    {
        var project = CreateTileProject(out var tilesetId, out var tilemapId, out var tileId, out var surfaceId);
        var expectedHash = ProjectSemanticHash.Compute(project.Document);
        var expectedSeed = project.Document.Seed;

        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, project);
        stream.Position = 0;
        var entries = ReadEntries(stream);
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var documentJson = Parse(entries, documentEntry);
        var tilemapJson = documentJson["tilemaps"]![0]!.AsObject();

        Assert.Equal(PixelProjectFormat.CurrentSchemaVersion, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal(expectedSeed, documentJson["seed"]!.GetValue<ulong>());
        Assert.Null(tilemapJson["chunks"]);
        Assert.Equal(2, tilemapJson["cells"]!.AsArray().Count);

        using var reloadStream = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(reloadStream).Document;
        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded));
        Assert.Equal(expectedSeed, loaded.Seed);

        var tileset = loaded.Resources.GetTileset(tilesetId);
        var tile = tileset.GetTile(tileId);
        var tilemap = loaded.Resources.GetTilemap(tilemapId);
        Assert.Equal(surfaceId, tile.SurfaceId);
        Assert.Equal(tileId, tilemap.GetCell(new IntPoint(-1, -1))!.Value.TileId);
        var transformed = tilemap.GetCell(new IntPoint(3, 2))!.Value;
        Assert.Equal(TileCellFlags.FlipX | TileCellFlags.Rotate90, transformed.Flags);
        Assert.Equal((ushort)7, transformed.Variant);
    }

    [Fact]
    public void Schema3Project_MigratesThroughCurrentSchemaWithDeterministicSeedAndEmptyTileCollections()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var entries = SaveToEntries(new PixelProject(document));
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var documentJson = Parse(entries, documentEntry);

        manifest["schemaVersion"] = 3;
        documentJson.Remove("seed");
        documentJson.Remove("tilesets");
        documentJson.Remove("tilemaps");
        foreach (var cel in documentJson["cels"]!.AsArray()) cel!.AsObject().Remove("effects");
        entries[documentEntry] = Encoding.UTF8.GetBytes(documentJson.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var oldProject = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(oldProject).Document;
        Assert.Equal(DocumentSeed.Derive(document.Id), loaded.Seed);
        Assert.Empty(loaded.Resources.TilesetIds);
        Assert.Empty(loaded.Resources.TilemapIds);
        Assert.All(loaded.Cels, cel => Assert.Empty(cel.Effects.EffectOrder));

        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, new PixelProject(loaded));
        savedAgain.Position = 0;
        var migratedEntries = ReadEntries(savedAgain);
        var migratedManifest = Parse(migratedEntries, PixelProjectFormat.ManifestEntry);
        var migratedDocument = Parse(migratedEntries, migratedManifest["documentEntry"]!.GetValue<string>());
        Assert.Equal(PixelProjectFormat.CurrentSchemaVersion, migratedManifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal(loaded.Seed, migratedDocument["seed"]!.GetValue<ulong>());
        Assert.Empty(migratedDocument["tilesets"]!.AsArray());
        Assert.Empty(migratedDocument["tilemaps"]!.AsArray());
        Assert.All(migratedDocument["cels"]!.AsArray(), cel => Assert.Empty(cel!["effects"]!.AsArray()));
    }

    [Fact]
    public void TilemapUnknownJson_RoundTripsWithoutLoss()
    {
        var entries = SaveToEntries(CreateTileProject(out _, out _, out _, out _));
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = Parse(entries, documentEntry);
        var tileset = document["tilesets"]![0]!.AsObject();
        var tile = tileset["tiles"]![0]!.AsObject();
        var tilemap = document["tilemaps"]![0]!.AsObject();
        var cell = tilemap["cells"]![0]!.AsObject();
        tileset["futureTileset"] = new JsonObject { ["mode"] = "keep-set" };
        tile["futureTile"] = 17;
        tilemap["futureTilemap"] = true;
        cell["futureCell"] = "keep-cell";
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var result = ReadEntries(savedAgain);
        var resultManifest = Parse(result, PixelProjectFormat.ManifestEntry);
        var resultDocument = Parse(result, resultManifest["documentEntry"]!.GetValue<string>());
        var resultTileset = resultDocument["tilesets"]![0]!.AsObject();
        var resultTile = resultTileset["tiles"]![0]!.AsObject();
        var resultTilemap = resultDocument["tilemaps"]![0]!.AsObject();
        var resultCell = resultTilemap["cells"]![0]!.AsObject();

        Assert.Equal("keep-set", resultTileset["futureTileset"]!["mode"]!.GetValue<string>());
        Assert.Equal(17, resultTile["futureTile"]!.GetValue<int>());
        Assert.True(resultTilemap["futureTilemap"]!.GetValue<bool>());
        Assert.Equal("keep-cell", resultCell["futureCell"]!.GetValue<string>());
    }

    [Fact]
    public void DuplicateCellCoordinateAndInvalidFlags_AreRejectedOnLoad()
    {
        var duplicateEntries = SaveToEntries(CreateTileProject(out _, out _, out _, out _));
        var duplicateManifest = Parse(duplicateEntries, PixelProjectFormat.ManifestEntry);
        var duplicateDocumentEntry = duplicateManifest["documentEntry"]!.GetValue<string>();
        var duplicateDocument = Parse(duplicateEntries, duplicateDocumentEntry);
        var cells = duplicateDocument["tilemaps"]![0]!["cells"]!.AsArray();
        cells.Add(cells[0]!.DeepClone());
        duplicateEntries[duplicateDocumentEntry] = Encoding.UTF8.GetBytes(duplicateDocument.ToJsonString(ProjectJson.Options));
        Rehash(duplicateEntries, duplicateManifest);

        using var duplicateStream = WriteEntries(duplicateEntries);
        var duplicateError = Assert.Throws<PixelProjectException>(() => PixelProjectFile.Load(duplicateStream));
        Assert.Equal(PixelProjectErrorCode.InvalidReference, duplicateError.Code);

        var flagEntries = SaveToEntries(CreateTileProject(out _, out _, out _, out _));
        var flagManifest = Parse(flagEntries, PixelProjectFormat.ManifestEntry);
        var flagDocumentEntry = flagManifest["documentEntry"]!.GetValue<string>();
        var flagDocument = Parse(flagEntries, flagDocumentEntry);
        flagDocument["tilemaps"]![0]!["cells"]![0]!["flags"] = 128;
        flagEntries[flagDocumentEntry] = Encoding.UTF8.GetBytes(flagDocument.ToJsonString(ProjectJson.Options));
        Rehash(flagEntries, flagManifest);

        using var flagStream = WriteEntries(flagEntries);
        var flagError = Assert.Throws<PixelProjectException>(() => PixelProjectFile.Load(flagStream));
        Assert.Equal(PixelProjectErrorCode.InvalidJson, flagError.Code);
    }

    private static PixelProject CreateTileProject(
        out TilesetId tilesetId,
        out TilemapId tilemapId,
        out TileId tileId,
        out ResourceId surfaceId)
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surface = new PixelSurface(new IntSize(2, 2));
        surface.SetPixel(0, 0, new Rgba32(200, 30, 10, 255));
        surfaceId = document.Resources.AddSurface(surface);
        tilesetId = TilesetId.New();
        var tileset = new Tileset(tilesetId, "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        tileId = TileId.New();
        document.Resources.AddTile(tilesetId, new TileDefinition(tileId, surfaceId, "Rock"));
        tilemapId = TilemapId.New();
        var tilemap = new MyLovePixel.Core.Tiles.Tilemap(tilemapId, "Ground", tilesetId, "rect");
        document.Resources.AddTilemap(tilemap);
        document.Resources.SetTileCell(tilemapId, new IntPoint(-1, -1), new TileCell(tileId));
        document.Resources.SetTileCell(
            tilemapId,
            new IntPoint(3, 2),
            new TileCell(tileId, TileCellFlags.FlipX | TileCellFlags.Rotate90, 7));
        return new PixelProject(document);
    }

    private static Dictionary<string, byte[]> SaveToEntries(PixelProject project)
    {
        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, project);
        stream.Position = 0;
        return ReadEntries(stream);
    }

    private static Dictionary<string, byte[]> ReadEntries(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var input = entry.Open();
                using var buffer = new MemoryStream();
                input.CopyTo(buffer);
                return buffer.ToArray();
            },
            StringComparer.Ordinal);
    }

    private static MemoryStream WriteEntries(IReadOnlyDictionary<string, byte[]> entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var pair in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                using var output = entry.Open();
                output.Write(pair.Value);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static JsonObject Parse(IReadOnlyDictionary<string, byte[]> entries, string name) =>
        ProjectJson.ParseObject(entries[name], name);

    private static void Rehash(Dictionary<string, byte[]> entries, JsonObject manifest)
    {
        manifest["contentHash"] = ProjectContentHash.Compute(
            entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal)));
        entries[PixelProjectFormat.ManifestEntry] = Encoding.UTF8.GetBytes(manifest.ToJsonString(ProjectJson.Options));
    }
}
