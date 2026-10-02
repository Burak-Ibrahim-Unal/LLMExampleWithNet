using System.Reflection;

namespace Knowledge.Infrastructure;

/// <summary>
/// Knowledge.Infrastructure derlemesini (assembly) tip güvenli biçimde işaret eden işaretçi (marker) sınıf.
/// </summary>
/// <remarks>
/// Bu modülün EF Core <c>IEntityTypeConfiguration</c> sınıfları (<c>KnowledgeDocumentConfiguration</c>,
/// <c>DocumentChunkConfiguration</c>, <c>QuestionLogConfiguration</c>) bu derlemededir. API host'u
/// (<c>AddSharedInfrastructure</c> çağrısı) ve veritabanı kullanan testler bu derlemeyi
/// <c>EntityConfigurationAssemblyRegistry</c>'ye verir; paylaşılan <c>AppDbContext</c> de
/// <c>OnModelCreating</c> sırasında konfigürasyonları oradan uygular. Böylece Shared.Infrastructure modüllere referans
/// vermeden her modülün tablolarını öğrenir ve çağıranlar derlemeyi bulmak için rastgele bir sınıfın adına
/// (<c>typeof(BirSinif).Assembly</c>) bağlanmak zorunda kalmaz.
/// </remarks>
public static class AssemblyReference
{
    /// <summary>Bu modülün derlemesi; EF Core konfigürasyonlarının taranacağı yer.</summary>
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
