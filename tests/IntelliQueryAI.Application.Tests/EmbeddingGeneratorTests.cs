using FluentAssertions;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Embeddings;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class EmbeddingGeneratorTests
{
    private readonly AzureOpenAIEmbeddingGenerator _embeddingGenerator;

    public EmbeddingGeneratorTests()
    {
        var loggerMock = new Mock<ILogger<AzureOpenAIEmbeddingGenerator>>();
        var simulationMode = new SimulationMode(true);
        _embeddingGenerator = new AzureOpenAIEmbeddingGenerator(null, simulationMode, loggerMock.Object);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldProduce1536DimensionalNormalizedVector()
    {
        // Arrange
        var text = "Table: customers. Columns: customer_id (integer), email (varchar). Description: User accounts";

        // Act
        var vector = await _embeddingGenerator.GenerateEmbeddingAsync(text);

        // Assert
        vector.Should().NotBeNull();
        var array = vector.ToArray();
        array.Length.Should().Be(1536);

        // Check unit length (L2 norm should be approximately 1.0)
        double sumSquares = 0.0;
        foreach (var val in array)
        {
            sumSquares += val * val;
        }

        double norm = Math.Sqrt(sumSquares);
        norm.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameText_ShouldProduceDeterministicIdenticalVector()
    {
        // Arrange
        var text = "orders total amount by month";

        // Act
        var vector1 = await _embeddingGenerator.GenerateEmbeddingAsync(text);
        var vector2 = await _embeddingGenerator.GenerateEmbeddingAsync(text);

        // Assert
        vector1.ToArray().Should().Equal(vector2.ToArray());
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_Batch_ShouldReturnCorrespondingVectors()
    {
        // Arrange
        var texts = new List<string> { "Table: users", "Table: orders", "Table: invoices" };

        // Act
        var vectors = await _embeddingGenerator.GenerateEmbeddingsAsync(texts);

        // Assert
        vectors.Should().HaveCount(3);
        foreach (var v in vectors)
        {
            v.ToArray().Length.Should().Be(1536);
        }
    }
}
