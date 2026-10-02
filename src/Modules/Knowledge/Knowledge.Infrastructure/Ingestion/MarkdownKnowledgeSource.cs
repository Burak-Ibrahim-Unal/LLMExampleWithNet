using System.Globalization;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>
/// <see cref="IKnowledgeBaseSource"/> portunun dosya sistemi uygulaması: bilgi tabanı klasöründeki her <c>*.md</c>
/// dosyasını okur ve <see cref="MarkdownDocumentParser"/> ile ayrıştırır. Gerçeğin kaynağı (source of truth) bu
/// dosyalardır; veritabanı ve arama indeksi onlardan türetilir.
/// </summary>
/// <remarks>
/// Kaynağın bir port arkasında durması, ingestion testlerinin dosya sistemi yerine sahte bir kaynak
/// (<c>StubKnowledgeBaseSource</c>) kullanabilmesini sağlar; dokümanlar ileride başka bir yerden gelecekse yalnızca bu
/// adaptör değişir. Durumsuzdur; singleton olarak kaydedilir.
/// </remarks>
/// <param name="options">Klasör yolunu taşıyan <see cref="KnowledgeBaseOptions"/>.</param>
public sealed class MarkdownKnowledgeSource(IOptions<KnowledgeBaseOptions> options) : IKnowledgeBaseSource
{
    /// <summary>
    /// Klasörü çözer, içindeki <c>*.md</c> dosyalarını (alt klasörler hariç) dosya adına göre sıralı okur ve her birini
    /// ayrıştırır. Herhangi bir dosya bozuksa <see cref="KnowledgeBaseFormatException"/> ile yüklemenin tamamı durur.
    /// </summary>
    /// <remarks>
    /// Sıralama ordinal yapılır, çünkü dosya sisteminin listeleme sırası işletim sistemine göre değişebilir; sabit sıra,
    /// dokümanların ve chunk'ların her makinede aynı sırada indekslenmesini ve dolayısıyla eşit puanlı sonuçların ve
    /// değerlendirmenin tekrarlanabilir olmasını sağlar. Ayrıştırıcıya tam yol yerine yalnızca dosya adı verilir: hata
    /// mesajı API yanıtına (422) çıktığından okunaklı kalır ve sunucunun dizin yapısını açığa çıkarmaz. Bozuk bir dosyada
    /// kısmi yükleme yapılmaz; ingestion 422 döner ve mevcut indekse dokunmaz, böylece eksik bir bilgi tabanıyla fark
    /// edilmeden cevap üretilmez.
    /// </remarks>
    public async Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var folder = ResolveFolder(options.Value.Path);
        var documents = new List<SourceDocument>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.md").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            documents.Add(MarkdownDocumentParser.Parse(Path.GetFileName(file), text));
        }

        return documents;
    }

    /// <summary>
    /// Yapılandırılan yolu gerçek bir klasöre çözer. Mutlak yol olduğu gibi kullanılır. Göreli yol önce çalışma
    /// dizininde ve onun üst dizinlerinde, sonra uygulama dizininde (<c>AppContext.BaseDirectory</c>) ve onun üst
    /// dizinlerinde aranır; böylece "knowledge-base" depo kökünden, proje klasöründen ya da <c>bin/</c> altından
    /// çalışırken de bulunur.
    /// </summary>
    /// <remarks>
    /// <c>dotnet run</c>, IDE ve test çalıştırıcıları farklı çalışma dizinleri kullanır; derleme çıktısı ise
    /// <c>bin/Debug/net10.0</c> gibi derin bir klasördedir. Yukarı doğru yürümek, dokümanları çıktı klasörüne kopyalamadan
    /// ya da her ortam için ayrı yol yazmadan aynı varsayılanın her yerde çalışmasını sağlar. Çalışma dizini zinciri önce
    /// arandığından, uygulamayı kendi <c>knowledge-base</c> klasörü olan bir dizinden başlatmak o klasörü seçer. Klasör
    /// hiçbir yerde yoksa açık bir hata fırlatılır.
    /// </remarks>
    private static string ResolveFolder(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Directory.Exists(configuredPath) ? configuredPath : throw Missing(configuredPath);
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, configuredPath);

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw Missing(configuredPath);
    }

    /// <summary>
    /// Klasör bulunamadığında fırlatılacak hatayı oluşturur. <c>DirectoryNotFoundException</c> yerine
    /// <see cref="KnowledgeBaseFormatException"/> kullanılır: ingestion bu tipi açık mesajıyla 422'ye çevirir ve operatör
    /// hangi yolun bulunamadığını görür; <c>IOException</c> türevleri ise ayrıntısı yalnızca loglarda kalan genel
    /// "okunamadı" mesajıyla raporlanırdı.
    /// </summary>
    private static KnowledgeBaseFormatException Missing(string path) =>
        new(string.Format(CultureInfo.InvariantCulture, Messages.Ingestion.FolderNotFound, path));
}
