using System.Globalization;
using System.Text.Json;

namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly ILogger<FaceRecognitionService> _logger;

        public FaceRecognitionService(ILogger<FaceRecognitionService> logger)
        {
            _logger = logger;
        }

        public float[]? ParseEmbedding(string rawEmbedding)
        {
            if (string.IsNullOrWhiteSpace(rawEmbedding))
                return null;

            try
            {
                var trimmed = rawEmbedding.Trim();

                // 1. Thử parse dạng JSON array [0.1, 0.2, ...]
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var parsed = JsonSerializer.Deserialize<float[]>(trimmed);
                    if (parsed != null && parsed.Length > 0)
                        return parsed;
                }

                // 2. Thử parse dạng phân tách dấu phẩy hoặc khoảng trắng
                var parts = trimmed
                    .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var list = new List<float>();
                foreach (var part in parts)
                {
                    if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    {
                        list.Add(val);
                    }
                }

                if (list.Count > 0)
                    return list.ToArray();

                // 3. Thử parse base64 byte array float32
                var bytes = Convert.FromBase64String(trimmed);
                if (bytes.Length % 4 == 0)
                {
                    var floatCount = bytes.Length / 4;
                    var floats = new float[floatCount];
                    Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                    return floats;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không thể parse FaceEmbedding: {Message}", ex.Message);
            }

            return null;
        }

        public double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0)
                return 0;

            var length = Math.Min(vectorA.Length, vectorB.Length);
            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA <= 0 || normB <= 0)
                return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public double CalculateEuclideanDistance(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null)
                return double.MaxValue;

            var length = Math.Min(vectorA.Length, vectorB.Length);
            double sum = 0.0;

            for (int i = 0; i < length; i++)
            {
                var diff = vectorA[i] - vectorB[i];
                sum += diff * diff;
            }

            return Math.Sqrt(sum);
        }

        public bool IsMatch(float[] vectorA, float[] vectorB, double threshold = 0.75)
        {
            var similarity = CalculateCosineSimilarity(vectorA, vectorB);
            return similarity >= threshold;
        }

        public (int StudentId, double Score)? FindBestMatch(
            float[] queryEmbedding,
            IEnumerable<(int StudentId, string EmbeddingString)> candidates,
            double threshold = 0.75)
        {
            int? bestStudentId = null;
            double maxSimilarity = -1;

            foreach (var (studentId, embeddingStr) in candidates)
            {
                var candidateVec = ParseEmbedding(embeddingStr);
                if (candidateVec == null) continue;

                var similarity = CalculateCosineSimilarity(queryEmbedding, candidateVec);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    bestStudentId = studentId;
                }
            }

            if (bestStudentId.HasValue && maxSimilarity >= threshold)
            {
                return (bestStudentId.Value, maxSimilarity);
            }

            return null;
        }
    }
}
