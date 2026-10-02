using System.Net;
using Shouldly;
using SupportAssistant.API.Security;

namespace SupportAssistant.UnitTests.Api;

/// <summary>
/// Hız sınırının istemciyi tanıdığı anahtarın (<see cref="ClientPartitionKey"/>) birim testleri.
/// </summary>
public sealed class ClientPartitionKeyTests
{
    /// <summary>
    /// IPv4 adresinin olduğu gibi, IPv6 içine gömülü IPv4 adresinin (<c>::ffff:a.b.c.d</c>) de aynı IPv4 adresi olarak
    /// anahtar olduğunu ve adres bilinmiyorsa tek bir ortak anahtar kullanıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Çift yığınlı (dual-stack) bir sunucu aynı istemciyi bazen IPv4, bazen gömülü IPv4 biçiminde görebilir; iki ayrı
    /// anahtar, aynı istemciye iki kat kota verirdi.
    /// </remarks>
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void IPv4_clients_are_keyed_by_their_address(string address, string expected)
    {
        ClientPartitionKey.For(IPAddress.Parse(address)).ShouldBe(expected);
        ClientPartitionKey.For(null).ShouldBe("unknown");
    }

    /// <summary>
    /// Aynı /64 ağındaki iki IPv6 adresinin aynı anahtarı, farklı /64 ağlarındaki adreslerin farklı anahtarları aldığını
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Bir IPv6 istemcisine genellikle koca bir /64 ağı verilir ve istemci bu ağda adresini serbestçe değiştirebilir
    /// (gizlilik uzantıları bunu kendiliğinden de yapar). Her adres ayrı sayılsaydı tek bir istemci adres değiştirerek
    /// dakikalık sınırı aşabilirdi (kod incelemesinde bulundu).
    /// </remarks>
    [Fact]
    public void IPv6_clients_are_keyed_by_their_64_bit_prefix()
    {
        var first = ClientPartitionKey.For(IPAddress.Parse("2001:db8:1:2::1"));
        var sameNetwork = ClientPartitionKey.For(IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:9"));
        var otherNetwork = ClientPartitionKey.For(IPAddress.Parse("2001:db8:1:3::1"));

        first.ShouldBe("2001:db8:1:2::/64");
        sameNetwork.ShouldBe(first);
        otherNetwork.ShouldNotBe(first);
    }
}
