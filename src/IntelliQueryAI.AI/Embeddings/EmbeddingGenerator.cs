using System.Security.Cryptography;
using System.Text;
using Azure.AI.OpenAI;
using IntelliQueryAI.AI.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Pgvector;

namespace IntelliQueryAI.AI.Embeddings;

public interface IEmbeddingGenerator
{
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

public sealed class AzureOpenAIEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly EmbeddingClient? _embeddingClient;
    private readonly SimulationMode _simulationMode;
    private readonly ILogger<AzureOpenAIEmbeddingGenerator> _logger;
    private const int EmbeddingDimensions = 1536; // OpenAI text-embedding-3-small standard

    public AzureOpenAIEmbeddingGenerator(
        EmbeddingClient? embeddingClient,
        SimulationMode simulationMode,
        ILogger<AzureOpenAIEmbeddingGenerator> logger)
    {
        _embeddingClient = embeddingClient;
        _simulationMode = simulationMode;
        _logger = logger;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_simulationMode.IsEnabled || _embeddingClient is null)
        {
            return GenerateSimulatedVector(text);
        }

        try
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            var memory = response.Value.ToFloats();
            return new Vector(memory.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI embedding generation failed. Falling back to deterministic simulation vector.");
            return GenerateSimulatedVector(text);
        }
    }

    public async Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new List<Vector>(texts.Count);
        foreach (var text in texts)
        {
            results.Add(await GenerateEmbeddingAsync(text, cancellationToken));
        }
        return results;
    }

    private static Vector GenerateSimulatedVector(string text)
    {
        // Produce deterministic pseudo-embedding based on SHA-256 hash of text
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        float[] vector = new float[EmbeddingDimensions];
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            byte b = hash[i % hash.Length];
            vector[i] = (float)Math.Sin(b + i);
        }

        // Normalize vector to unit length for cosine similarity
        double sumSquares = 0.0;
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        float norm = (float)Math.Sqrt(sumSquares);
        if (norm > 0)
        {
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return new Vector(vector);
    }
}
