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
        public const string RetrievalModeInvalid = "mode yalnızca 'lexical' veya 'hybrid' olabilir.";
        public const string QuestionRequired = "Soru boş olamaz.";
        public const string QuestionTooLong = "Soru en fazla {0} karakter olabilir.";
        public const string NotEnoughInformation = "Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı.";
        public const string LlmUnavailable = "Dil modeli servisine şu anda ulaşılamıyor. Lütfen daha sonra tekrar deneyin.";
        public const string LlmInvalidOutput = "Dil modeli geçerli bir yanıt üretemedi. Lütfen tekrar deneyin.";
    }
}
