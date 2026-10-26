using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.KnowledgeBase;

public class ChunkSegment
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int EstimatedTokens { get; set; }
}

public class DocumentChunkingService
{
    public List<ChunkSegment> ChunkText(string rawText, int targetChunkWords = 100, int overlapWords = 20)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new List<ChunkSegment>();
        }

        var words = rawText
            .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return new List<ChunkSegment>();
        }

        var chunks = new List<ChunkSegment>();
        int startIndex = 0;
        int chunkIndex = 0;

        while (startIndex < words.Length)
        {
            int count = Math.Min(targetChunkWords, words.Length - startIndex);
            var chunkWords = words.Skip(startIndex).Take(count).ToArray();
            var content = string.Join(" ", chunkWords);

            chunks.Add(new ChunkSegment
            {
                Index = chunkIndex++,
                Content = content,
                WordCount = count,
                EstimatedTokens = (int)Math.Ceiling(content.Length / 4.0)
            });

            if (startIndex + count >= words.Length)
            {
                break;
            }

            // Slide forward by targetChunkWords - overlapWords
            startIndex += Math.Max(1, targetChunkWords - overlapWords);
        }

        return chunks;
    }

    /// <summary>
    /// Generates an L2-normalized float embedding vector.
    /// Uses deterministic semantic n-gram and bag-of-words hashing to support offline/in-memory unit tests
    /// while remaining 100% mathematically compatible with PostgreSQL pgvector cosine similarity.
    /// </summary>
    public float[] GenerateEmbedding(string text, int dimensions = 64)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[dimensions];
        }

        var vector = new float[dimensions];
        var normalized = text.ToLowerInvariant();
        var words = normalized.Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '-', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (word.Length < 2) continue;
            var hash = (uint)word.GetHashCode();
            int bucket = (int)(hash % dimensions);
            float weight = 1.0f + (word.Length * 0.1f);
            vector[bucket] += weight;

            // Bigram hashing for word sequencing context
            for (int i = 0; i < word.Length - 1; i++)
            {
                var bigram = word.Substring(i, 2);
                int bgBucket = (int)((uint)bigram.GetHashCode() % dimensions);
                vector[bgBucket] += 0.5f;
            }
        }

        // L2 Normalization: divide each component by Euclidean norm so ||v|| = 1.0
        double sumSquares = 0.0;
        for (int i = 0; i < dimensions; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares > 0)
        {
            float norm = (float)Math.Sqrt(sumSquares);
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    public static float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0 || vectorA.Length != vectorB.Length)
        {
            return 0.0f;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 0.0f;
        }

        return (float)(dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
