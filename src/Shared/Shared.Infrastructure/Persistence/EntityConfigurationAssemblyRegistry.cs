using System.Reflection;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Paylaşılan <see cref="AppDbContext"/>'e EF yapılandırması (<c>IEntityTypeConfiguration</c>) sağlayan modül
/// derlemelerinin listesi.
/// </summary>
/// <remarks>
/// Bu küçük sınıf bağımlılık yönünü korumak için vardır: <c>Shared.Infrastructure</c> modülleri tanımaz; host (API)
/// hangi derlemelerin taranacağını başlangıçta söyler ve kayıt DI'a singleton olarak eklenir. Yeni bir modülün
/// tablolarını eklemek, listeye bir derleme daha vermekten ibarettir. Birim testleri de context'i bu kayıtla, DI
/// kurmadan oluşturur.
/// </remarks>
public sealed class EntityConfigurationAssemblyRegistry
{
    /// <summary>
    /// Verilen derlemeleri tekrarları ayıklayarak saklar; aynı derleme iki kez verilse bile yapılandırmaları bir kez
    /// uygulanır.
    /// </summary>
    public EntityConfigurationAssemblyRegistry(IEnumerable<Assembly> assemblies)
    {
        Assemblies = assemblies.Distinct().ToArray();
    }

    /// <summary>
    /// <c>AppDbContext.OnModelCreating</c> sırasında taranacak derlemeler. Kopya bir dizi olarak saklandığı için kayıt
    /// oluşturulduktan sonra çağıranın koleksiyonundaki değişikliklerden etkilenmez.
    /// </summary>
    public IReadOnlyCollection<Assembly> Assemblies { get; }
}
