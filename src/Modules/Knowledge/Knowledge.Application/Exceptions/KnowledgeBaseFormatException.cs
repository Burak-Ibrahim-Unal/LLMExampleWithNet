namespace Knowledge.Application.Exceptions;

/// <summary>
/// Bir bilgi tabanı dosyası hatalı biçimde (front matter eksik, bilinmeyen status, geçersiz tarih...) ya da bilgi tabanı
/// klasörü bulunamadı.
/// </summary>
/// <remarks>
/// Mesaj Türkçedir ve biçim hatalarında dosya adını içerir; ingest handler'ı onu 422 yanıtında olduğu gibi döndürür,
/// böylece dokümanı düzenleyen kişi hangi dosyada neyin eksik olduğunu doğrudan görür. Okuma hatalarından (<c>IOException</c>,
/// <c>UnauthorizedAccessException</c>) ayrı bir tip olması bilinçlidir: onların ayrıntısı yalnızca sunucu loglarına yazılır.
/// </remarks>
/// <param name="message">API yanıtında gösterilecek Türkçe açıklama (ör. "iade-v1.md: zorunlu front matter alanı eksik: id").</param>
public sealed class KnowledgeBaseFormatException(string message) : Exception(message);
