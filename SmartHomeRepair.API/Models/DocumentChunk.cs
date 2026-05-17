using Microsoft.Extensions.VectorData;

namespace SmartHomeRepair.API.Models
{
    public class DocumentChunk
    {
        [VectorStoreKey]
        public ulong ChunkId { get; set; }

        [VectorStoreData]
        public string Source { get; set; } = string.Empty;

        [VectorStoreData]
        public string Text { get; set; } = string.Empty;

        [VectorStoreVector( Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
