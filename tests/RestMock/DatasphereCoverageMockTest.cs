/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.REST;
using SignalWire.REST.Namespaces;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Full success+error coverage for the datasphere.* canonical routes
/// (9 endpoints). Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/datasphere_coverage_mock_test.go</c>.
///
/// Routes covered:
///   datasphere.list_documents        GET    /api/datasphere/documents
///   datasphere.create_document       POST   /api/datasphere/documents
///   datasphere.search_documents      POST   /api/datasphere/documents/search
///   datasphere.list_document_chunks  GET    /api/datasphere/documents/{id}/chunks
///   datasphere.get_document_chunk    GET    /api/datasphere/documents/{id}/chunks/{cid}
///   datasphere.delete_document_chunk DELETE /api/datasphere/documents/{id}/chunks/{cid}
///   datasphere.get_document          GET    /api/datasphere/documents/{id}
///   datasphere.update_document       PATCH  /api/datasphere/documents/{id}
///   datasphere.delete_document       DELETE /api/datasphere/documents/{id}
/// </summary>
public class DatasphereCoverageMockTest : CoverageBase
{
    public DatasphereCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private DatasphereNs NewDatasphere() => new(NewHttp());

    // ---------- datasphere.list_documents ----------

    [Fact]
    public async Task DatasphereListDocuments_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/datasphere/documents", "datasphere.list_documents");
    }

    [Fact]
    public async Task DatasphereListDocuments_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.list_documents", 500,
            () => c.Documents.ListAsync());
        Assert.Equal(500, status);
    }

    // ---------- datasphere.create_document ----------

    [Fact]
    public async Task DatasphereCreateDocument_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.CreateAsync(new() { ["name"] = "doc-1" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/datasphere/documents", "datasphere.create_document");
        Assert.Equal("doc-1", StringField(j, "name"));
    }

    [Fact]
    public async Task DatasphereCreateDocument_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.create_document", 422,
            () => c.Documents.CreateAsync(new() { ["name"] = "" }));
        Assert.Equal(422, status);
    }

    // ---------- datasphere.search_documents ----------

    [Fact]
    public async Task DatasphereSearchDocuments_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.SearchAsync(new() { ["query"] = "hello" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/datasphere/documents/search", "datasphere.search_documents");
        Assert.Equal("hello", StringField(j, "query"));
    }

    [Fact]
    public async Task DatasphereSearchDocuments_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.search_documents", 422,
            () => c.Documents.SearchAsync(new() { ["query"] = "" }));
        Assert.Equal(422, status);
    }

    // ---------- datasphere.list_document_chunks ----------

    [Fact]
    public async Task DatasphereListChunks_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.ListChunksAsync("doc-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/datasphere/documents/doc-1/chunks", "datasphere.list_document_chunks");
    }

    [Fact]
    public async Task DatasphereListChunks_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.list_document_chunks", 404,
            () => c.Documents.ListChunksAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- datasphere.get_document_chunk ----------

    [Fact]
    public async Task DatasphereGetChunk_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.GetChunkAsync("doc-1", "chunk-9");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/datasphere/documents/doc-1/chunks/chunk-9", "datasphere.get_document_chunk");
    }

    [Fact]
    public async Task DatasphereGetChunk_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.get_document_chunk", 404,
            () => c.Documents.GetChunkAsync("doc-1", "missing"));
        Assert.Equal(404, status);
    }

    // ---------- datasphere.delete_document_chunk ----------

    [Fact]
    public async Task DatasphereDeleteChunk_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.DeleteChunkAsync("doc-1", "chunk-9");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/datasphere/documents/doc-1/chunks/chunk-9", "datasphere.delete_document_chunk");
    }

    [Fact]
    public async Task DatasphereDeleteChunk_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.delete_document_chunk", 404,
            () => c.Documents.DeleteChunkAsync("doc-1", "missing"));
        Assert.Equal(404, status);
    }

    // ---------- datasphere.get_document ----------

    [Fact]
    public async Task DatasphereGetDocument_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.GetAsync("doc-7");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/datasphere/documents/doc-7", "datasphere.get_document");
    }

    [Fact]
    public async Task DatasphereGetDocument_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.get_document", 404,
            () => c.Documents.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- datasphere.update_document (PATCH) ----------

    [Fact]
    public async Task DatasphereUpdateDocument_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.UpdateAsync("doc-7", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/datasphere/documents/doc-7", "datasphere.update_document");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task DatasphereUpdateDocument_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.update_document", 404,
            () => c.Documents.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    // ---------- datasphere.delete_document ----------

    [Fact]
    public async Task DatasphereDeleteDocument_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewDatasphere().Documents.DeleteAsync("doc-7");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/datasphere/documents/doc-7", "datasphere.delete_document");
    }

    [Fact]
    public async Task DatasphereDeleteDocument_Error()
    {
        if (!Fixture.Available) return;
        var c = NewDatasphere();
        var status = await AssertErrorAsync("datasphere.delete_document", 404,
            () => c.Documents.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }
}
