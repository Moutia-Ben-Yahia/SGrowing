using System.Text.Json;

namespace SGrowing.Core.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string EmbeddingJson { get; set; } = "[]";
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public float[] GetEmbedding()
    {
        if (string.IsNullOrWhiteSpace(EmbeddingJson) || EmbeddingJson == "[]")
        {
            return Array.Empty<float>();
        }

        try
        {
            return JsonSerializer.Deserialize<float[]>(EmbeddingJson) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    public void SetEmbedding(float[] embedding)
    {
        EmbeddingJson = JsonSerializer.Serialize(embedding);
    }

    public static DocumentChunk Create(
        Guid tenantId,
        Guid documentId,
        string documentTitle,
        int chunkIndex,
        string content,
        float[]? embedding = null,
        string? sourceUrl = null)
    {
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentTitle = documentTitle,
            ChunkIndex = chunkIndex,
            Content = content,
            TokenCount = EstimateTokens(content),
            SourceUrl = sourceUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (embedding != null && embedding.Length > 0)
        {
            chunk.SetEmbedding(embedding);
        }

        return chunk;
    }

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // Approximation: ~4 characters per token in English text
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }
}
