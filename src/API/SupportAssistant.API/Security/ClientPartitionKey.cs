using System.Net;
using System.Net.Sockets;

namespace SupportAssistant.API.Security;

/// <summary>
/// Hız sınırının istemciyi tanıdığı anahtarı istemci adresinden üretir: IPv4 adresi olduğu gibi, IPv6 adresi ise
/// ilk 64 bitiyle (/64 ağı) anahtar olur.
/// </summary>
/// <remarks>
/// Bir IPv6 istemcisine genellikle bütün bir /64 ağı verilir ve istemci bu ağ içinde adresini serbestçe değiştirebilir
/// (gizlilik uzantıları bunu kendiliğinden de yapar). Her IPv6 adresi ayrı sayılsaydı tek bir istemci adres değiştirerek
/// dakikalık sınırı aşabilirdi; bu, kod incelemesinde bulundu. IPv6 içine gömülü IPv4 adresleri (<c>::ffff:a.b.c.d</c>)
/// IPv4 adresine çevrilir; çift yığınlı bir sunucu aynı istemciyi iki biçimde görebilir.
/// </remarks>
public static class ClientPartitionKey
{
    /// <summary>IPv6 adreslerinde anahtara giren ön ek uzunluğu (bit).</summary>
    private const int IPv6PrefixLength = 64;

    /// <summary>
    /// İstemci adresinin anahtarını döndürür: IPv4 için adresin kendisi, IPv6 için <c>ağ::/64</c> biçiminde ön eki;
    /// adres bilinmiyorsa (ör. bellek içi test sunucusu) tek bir ortak anahtar.
    /// </summary>
    /// <param name="address">Bağlantının uzak adresi; bilinmiyorsa null.</param>
    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        // İlk 64 bit (8 bayt) ağdır; geri kalan arayüz kimliği sıfırlanır.
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, IPv6PrefixLength / 8, bytes.Length - IPv6PrefixLength / 8);

        return $"{new IPAddress(bytes)}/{IPv6PrefixLength}";
    }
}
