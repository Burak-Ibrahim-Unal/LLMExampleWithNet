using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Shared.Application.Common;

namespace Knowledge.Application.BusinessRules;

/// <summary>
/// Knowledge modülünün fail-fast iş kuralları. Her <c>Check*</c> metodu kural sağlanıyorsa <c>null</c>, sağlanmıyorsa
/// uygun HTTP durum kodu ve merkezi Türkçe mesajla (<c>Messages.Knowledge</c>) dolu bir <c>ApiResult&lt;T&gt;</c> döndürür.
/// </summary>
/// <remarks>
/// Handler'lar kuralları sırayla çağırır ve ilk ihlalde hemen döner; böylece geçersiz bir istek pahalı adımlara (arama,
/// embedding, LLM çağrısı, veritabanı yazımı) hiç ulaşmaz. İstisna fırlatmak yerine sonuç döndürmek, beklenen doğrulama
/// hatalarını normal kontrol akışında tutar ve her uçtan aynı <c>ApiResult</c> zarfıyla dönmelerini sağlar. Kurallar tek
/// sınıfta toplandığı için mesajlar ve sınırlar uçlar arasında tutarlı kalır; metotlar generic'tir, çünkü her handler kendi
/// yanıt tipiyle (<c>AnswerDto</c>, <c>SearchResultDto</c>…) döner. İndeks durumu kuralı için <c>IKnowledgeIndex</c>
/// enjekte edilir; sınıf istek başına (scoped) oluşturulur.
/// </remarks>
public sealed class KnowledgeBusinessRules(IKnowledgeIndex index)
{
    /// <summary>
    /// Soru ve arama ifadesi için azami karakter sayısı (500). Bir destek sorusu için fazlasıyla yeterlidir; sınır tek bir
    /// isteğin embedding ve LLM maliyetini öngörülebilir tutar ve çok uzun girdilerin aramayı ve prompt'u şişirmesini önler.
    /// </summary>
    public const int MaxQueryLength = 500;

    /// <summary>
    /// <c>GET /v1/search</c> ucunda istenebilecek azami sonuç sayısı (20). Arama ucu teşhis amaçlıdır; bu kadar sonuç bir
    /// sorunun nasıl sıralandığını görmeye yeter ve yanıt boyutunu sınırlı tutar.
    /// </summary>
    public const int MaxTopK = 20;

    /// <summary>
    /// Bilgi tabanı kaynağında en az bir doküman olmasını şart koşar; yoksa 422 ve <c>KnowledgeBaseEmpty</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Boş bir okuma büyük olasılıkla yanlış yapılandırılmış bir klasördür. Kural ingest'i burada durdurur; aksi hâlde
    /// uzlaştırma adımı veritabanındaki bütün dokümanları "dosyası silinmiş" sayıp kaldırır ve indeksi boşaltırdı.
    /// </remarks>
    public ApiResult<T>? CheckKnowledgeBaseNotEmpty<T>(int documentCount)
    {
        if (documentCount > 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.KnowledgeBaseEmpty, 422);
    }

    /// <summary>
    /// Front matter'daki doküman kimliklerinin (<c>id</c>) benzersiz olmasını şart koşar; aynı kimlik birden çok dosyada
    /// geçiyorsa 422 ve çakışan kimlikleri listeleyen <c>DuplicateDocumentId</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Karşılaştırma büyük/küçük harfe duyarsızdır; ingest de kayıtları kimliğe göre aynı kuralla eşleştirir. Kontrol
    /// olmasaydı ikinci dosya yeni bir kayıt olarak eklenmeye çalışılır ve veritabanındaki tekil kimlik indeksine takılarak
    /// anlaşılması zor bir 500 hatası üretirdi; bunun yerine hangi kimliğin çakıştığı açıkça söylenir.
    /// </remarks>
    public ApiResult<T>? CheckUniqueDocumentIds<T>(IEnumerable<string> sourceIds)
    {
        var duplicates = sourceIds
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count == 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.DuplicateDocumentId, string.Join(", ", duplicates)), 422);
    }

    /// <summary>Arama indeksinin kurulmuş olmasını şart koşar; kurulmadıysa 503 ve <c>IndexNotReady</c> mesajı döner.</summary>
    /// <remarks>
    /// Açılıştaki ingest başarısız olsa da uygulama ayakta kalır; bu durumda soru ve aramalar çökmeden, geçici bir hizmet
    /// durumu olarak reddedilir ("biraz sonra deneyin veya reindex çağırın"). Kontrol olmasaydı kurulmamış indekste arama
    /// bir istisnaya ve 500 yanıtına dönüşürdü.
    /// </remarks>
    public ApiResult<T>? CheckIndexReady<T>()
    {
        if (index.IsReady)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.IndexNotReady, 503);
    }

    /// <summary>İstenen doküman bulunamadıysa 404 ve <c>DocumentNotFound</c> mesajı döner.</summary>
    /// <remarks>
    /// Handler'ın null dokümanla devam edip 500 üretmesini önler; "böyle bir doküman yok" sonucu istemciye doğru durum
    /// koduyla bildirilir.
    /// </remarks>
    public ApiResult<T>? CheckDocumentFound<T>(KnowledgeDocument? document)
    {
        if (document is not null)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.DocumentNotFound, 404);
    }

    /// <summary>
    /// Arama ifadesinin boş ya da yalnızca boşluk olmamasını şart koşar; aksi hâlde 400 ve <c>QueryRequired</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Boş sorgu hiçbir terim üretmez; anlamsız bir arama ve gereksiz bir embedding çağrısı yapmak yerine istek hemen
    /// reddedilir. Soru için ayrı bir kural (<c>CheckQuestionRequired</c>) vardır, çünkü kullanıcı uçtaki kavramla
    /// ("arama ifadesi" / "soru") eşleşen bir mesaj görmelidir.
    /// </remarks>
    public ApiResult<T>? CheckQueryRequired<T>(string query)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.QueryRequired, 400);
    }

    /// <summary>
    /// Arama ifadesinin <c>MaxQueryLength</c> karakteri aşmamasını şart koşar; aşarsa 400 ve sınırı belirten
    /// <c>QueryTooLong</c> mesajı döner.
    /// </summary>
    /// <remarks>Sınır mesaja sabitten yazılır; değer değişirse mesaj kendiliğinden güncel kalır.</remarks>
    public ApiResult<T>? CheckQueryLength<T>(string query)
    {
        if (query.Length <= MaxQueryLength)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.QueryTooLong, MaxQueryLength), 400);
    }

    /// <summary>
    /// Sorunun boş ya da yalnızca boşluk olmamasını şart koşar; aksi hâlde 400 ve <c>QuestionRequired</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Soru akışındaki ilk kuraldır: boş bir soru ne aramaya ne de dil modeline gönderilir, soru loguna da yazılmaz.
    /// </remarks>
    public ApiResult<T>? CheckQuestionRequired<T>(string question)
    {
        if (!string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.QuestionRequired, 400);
    }

    /// <summary>
    /// Sorunun <c>MaxQueryLength</c> (500) karakteri aşmamasını şart koşar; aşarsa 400 ve sınırı belirten
    /// <c>QuestionTooLong</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Kontrol, maliyetli adımlardan (embedding ve LLM çağrısı) önce yapılır; aşırı uzun bir soru bu adımlara hiç ulaşmaz.
    /// </remarks>
    public ApiResult<T>? CheckQuestionLength<T>(string question)
    {
        if (question.Length <= MaxQueryLength)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.QuestionTooLong, MaxQueryLength), 400);
    }

    /// <summary>
    /// Arama modunun verilmemiş (null; varsayılan hibrit), "lexical" ya da "hybrid" olmasını şart koşar (büyük/küçük harf
    /// duyarsız); aksi hâlde 400 ve <c>RetrievalModeInvalid</c> mesajı döner.
    /// </summary>
    /// <remarks>
    /// Bilinmeyen bir değer (ör. "semantic") sessizce hibrit moda düşürülseydi istemci istediği ölçümü aldığını sanırdı;
    /// değerlendirme aracı iki modu ayrı ölçtüğü için bir yazım hatası açık bir hatayla bildirilmelidir.
    /// </remarks>
    public ApiResult<T>? CheckRetrievalMode<T>(string? mode)
    {
        if (mode is null || mode.Equals("lexical", StringComparison.OrdinalIgnoreCase) || mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.RetrievalModeInvalid, 400);
    }

    /// <summary>
    /// <c>topK</c> değerinin 1 ile <c>MaxTopK</c> arasında olmasını şart koşar; aksi hâlde 400 ve <c>TopKOutOfRange</c>
    /// mesajı döner.
    /// </summary>
    /// <remarks>
    /// 0 veya negatif bir değer anlamsız (boş) bir sonuç üretirdi, çok büyük bir değer ise yanıtı gereksiz yere şişirirdi;
    /// ikisi de sessizce düzeltilmek yerine istemciye açıkça bildirilir.
    /// </remarks>
    public ApiResult<T>? CheckTopKInRange<T>(int topK)
    {
        if (topK is >= 1 and <= MaxTopK)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.TopKOutOfRange, MaxTopK), 400);
    }
}
