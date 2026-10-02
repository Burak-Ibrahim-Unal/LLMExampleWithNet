using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.IngestKnowledgeBase;

/// <summary>
/// Veritabanını ve bellek içi arama indeksini bilgi tabanı dosyalarıyla (<c>knowledge-base/*.md</c>) eşitleme isteği.
/// Hem uygulama açılışında hem de <c>POST /v1/documents/reindex</c> ile gönderilir. Parametresi yoktur, çünkü doğruluk
/// kaynağı her zaman dosyalardır: veritabanı bu dosyalardan türetilmiş veridir ve her seferinde onlara göre uzlaştırılır.
/// Sonuç; eklenen, güncellenen, silinen ve değişmeyen doküman sayılarını, embed edilen bölüm sayısını, oluşan arama
/// modunu ve varsa uyarıyı içeren bir özettir (<see cref="IngestionSummaryDto"/>).
/// </summary>
public sealed record IngestKnowledgeBaseCommand : IRequest<ApiResult<IngestionSummaryDto>>;
