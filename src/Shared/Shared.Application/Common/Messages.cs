namespace Shared.Application.Common;

public static class Messages
{
    public static class Knowledge
    {
        public const string Reindexed = "Bilgi tabanı indekslendi.";
        public const string KnowledgeBaseEmpty = "Bilgi tabanı klasöründe doküman bulunamadı.";
        public const string DuplicateDocumentId = "Aynı doküman kimliği birden fazla dosyada kullanılmış: {0}";
        public const string EmbeddingUnavailable = "Embedding servisine ulaşılamadı; arama yalnızca anahtar kelime (BM25) moduyla çalışıyor.";
        public const string IndexNotReady = "Bilgi tabanı henüz hazır değil. Biraz sonra tekrar deneyin veya POST /v1/documents/reindex çağırın.";
        public const string DocumentNotFound = "Doküman bulunamadı.";
        public const string QueryRequired = "Arama ifadesi boş olamaz.";
        public const string QueryTooLong = "Arama ifadesi en fazla {0} karakter olabilir.";
        public const string TopKOutOfRange = "topK 1 ile {0} arasında olmalıdır.";
    }
}
