using System.Reflection;
using Shared.Application.Common;
using Shouldly;

namespace SupportAssistant.UnitTests.Application;

/// <summary>
/// Mesaj dosyası (<c>Shared.Application/Common/Resources/messages.json</c>) ile <see cref="Messages"/> arasındaki bağın
/// birim testleri.
/// </summary>
public sealed class MessagesTests
{
    /// <summary>
    /// <see cref="Messages"/>'taki her özelliğin dosyada dolu bir metne karşılık geldiğini ve dosyadaki her metnin bir
    /// özellikte kullanıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Metinler koddan ayrıldığında iki yönlü bir kopukluk mümkündür: dosyadan silinen ya da yanlış adlandırılan bir
    /// metin, o mesajın kullanıldığı ilk istekte hataya dönüşür; özelliği silinmiş bir metin ise dosyada sessizce
    /// unutulur. Test her ikisini de adlarıyla listeler.
    /// </remarks>
    [Fact]
    public void Every_message_property_has_a_text_and_every_text_is_used()
    {
        var properties = typeof(Messages).GetNestedTypes()
            .SelectMany(section => section.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Select(property => (Key: $"{section.Name}.{property.Name}", Property: property)))
            .ToList();

        properties.ShouldNotBeEmpty();
        properties.Where(entry => string.IsNullOrWhiteSpace((string?)entry.Property.GetValue(null))).Select(entry => entry.Key).ShouldBeEmpty();
        Messages.Keys.Except(properties.Select(entry => entry.Key)).ShouldBeEmpty();
    }
}
