namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public interface IFaceRecognitionService
    {
        float[]? ParseEmbedding(string rawEmbedding);

        double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);

        double CalculateEuclideanDistance(float[] vectorA, float[] vectorB);

        bool IsMatch(float[] vectorA, float[] vectorB, double threshold = 0.75);

        (int StudentId, double Score)? FindBestMatch(
            float[] queryEmbedding,
            IEnumerable<(int StudentId, string EmbeddingString)> candidates,
            double threshold = 0.75);
    }
}
