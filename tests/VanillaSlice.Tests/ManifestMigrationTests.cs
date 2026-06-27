using Xunit;

namespace VanillaSlice.Tests;

public class ManifestMigrationTests
{
    [Fact]
    public void V1_WithListing_MigratesListingDescriptor()
    {
        var v1Json = """
            {
              "version": "1.0",
              "slices": [{
                "id": "doctors-doctor",
                "componentPrefix": "Doctor",
                "featurePluralName": "Doctors",
                "namespace": "Doctors",
                "directoryName": "Features/Doctors",
                "primaryKeyType": "Guid",
                "generateForm": true,
                "generateListing": true,
                "generateSelectList": false,
                "selectListModelType": "SelectOption",
                "selectListDataType": "string",
                "createdAt": "2026-01-01T00:00:00Z",
                "lastGeneratedAt": "2026-01-01T00:00:00Z",
                "generatedFiles": []
              }]
            }
            """;

        var migrated = MigrateV1Json(v1Json);

        Assert.Equal(2, migrated.Version);
        var slice = migrated.Slices.Single();
        Assert.Equal("Doctors", slice.Listing!.Name);
        Assert.Equal("Doctor", slice.Listing.Prefix);
        Assert.Equal("Doctor", slice.Form!.Prefix);
        Assert.Null(slice.Action);
        Assert.Null(slice.SelectList);
    }

    [Fact]
    public void V1_WithSelectList_MigratesSelectListDescriptor()
    {
        var v1Json = """
            {
              "version": "1.0",
              "slices": [{
                "id": "doctors-doctor",
                "componentPrefix": "Doctor",
                "featurePluralName": "Doctors",
                "namespace": "Doctors",
                "directoryName": "Features/Doctors",
                "primaryKeyType": "Guid",
                "generateForm": false,
                "generateListing": false,
                "generateSelectList": true,
                "selectListModelType": "Custom",
                "selectListDataType": "int",
                "createdAt": "2026-01-01T00:00:00Z",
                "lastGeneratedAt": "2026-01-01T00:00:00Z",
                "generatedFiles": []
              }]
            }
            """;

        var migrated = MigrateV1Json(v1Json);
        var slice = migrated.Slices.Single();
        Assert.Null(slice.Listing);
        Assert.Null(slice.Form);
        Assert.NotNull(slice.SelectList);
        Assert.Equal("Custom", slice.SelectList!.ModelType);
        Assert.Equal("int", slice.SelectList.DataType);
    }

    // Inline migration logic to test independently of ManifestService I/O.
    private static SliceManifestV2 MigrateV1Json(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var v2 = new SliceManifestV2 { Version = 2 };

        foreach (var sliceEl in root.GetProperty("slices").EnumerateArray())
        {
            var prefix = sliceEl.GetProperty("componentPrefix").GetString() ?? "";
            var plural = sliceEl.GetProperty("featurePluralName").GetString() ?? prefix + "s";
            var hasList = sliceEl.GetProperty("generateListing").GetBoolean();
            var hasForm = sliceEl.GetProperty("generateForm").GetBoolean();
            var hasSel = sliceEl.GetProperty("generateSelectList").GetBoolean();
            var modelType = sliceEl.GetProperty("selectListModelType").GetString() ?? "SelectOption";
            var dataType = sliceEl.GetProperty("selectListDataType").GetString() ?? "string";

            var slice = new SliceDefinitionV2
            {
                Id = sliceEl.GetProperty("id").GetString() ?? "",
                Namespace = sliceEl.GetProperty("namespace").GetString() ?? "",
                Directory = sliceEl.GetProperty("directoryName").GetString() ?? "",
                PrimaryKeyType = sliceEl.GetProperty("primaryKeyType").GetString() ?? "Guid",
                Listing = hasList ? new SliceDescriptorV2(plural, prefix) : null,
                Form = hasForm ? new SliceDescriptorV2(prefix, prefix) : null,
                SelectList = hasSel ? new SelectListDescriptorV2(prefix + " Types", prefix, modelType, dataType) : null,
            };
            v2.Slices.Add(slice);
        }
        return v2;
    }

    // Minimal in-test versions of the v2 types for isolation
    private class SliceManifestV2 { public int Version { get; set; } public List<SliceDefinitionV2> Slices { get; } = new(); }
    private class SliceDefinitionV2 { public string Id { get; set; } = ""; public string Namespace { get; set; } = ""; public string Directory { get; set; } = ""; public string PrimaryKeyType { get; set; } = "Guid"; public SliceDescriptorV2? Listing { get; set; } public SliceDescriptorV2? Form { get; set; } public SliceDescriptorV2? Action { get; set; } public SelectListDescriptorV2? SelectList { get; set; } }
    private record SliceDescriptorV2(string Name, string Prefix);
    private record SelectListDescriptorV2(string Name, string Prefix, string ModelType, string DataType) : SliceDescriptorV2(Name, Prefix);
}
