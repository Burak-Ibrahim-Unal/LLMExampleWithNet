using System.Globalization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.API.Errors;

namespace SupportAssistant.UnitTests.Api;

/// <summary>
/// İstek bağlama hatalarını zarflı yanıta çeviren <see cref="BindingErrorResponse"/> için birim testleri.
/// </summary>
public sealed class BindingErrorResponseTests
{
    /// <summary>
    /// Yalnızca genel anahtarlarla gelen hataların (gövde hiç JSON değil) alan adı taşımayan genel mesaja, alanlı hataların
    /// ise sorunlu alanları tekrarsız ve geliş sırasıyla sayan mesaja çevrildiğini doğrular. Her iki durumda yanıt başarısız,
    /// durum kodu çerçevenin verdiği koddur ve veri boştur.
    /// </summary>
    /// <remarks>
    /// <c>serializerErrors</c> ve <c>GeneralErrors</c> çerçevenin iç anahtarlarıdır; istemciye alan adıymış gibi
    /// gösterilmeleri kafa karıştırırdı. Aynı alana ait birden çok ileti tek bir adla anılır.
    /// </remarks>
    [Fact]
    public void Failures_become_an_envelope_naming_only_the_real_fields()
    {
        var general = (ApiResult<object>)BindingErrorResponse.Create(
            [new ValidationFailure("serializerErrors", "'b' is an invalid start of a value.")],
            new DefaultHttpContext(),
            StatusCodes.Status400BadRequest);
        var fields = (ApiResult<object>)BindingErrorResponse.Create(
            [
                new ValidationFailure("topK", "Value [abc] is not valid for a [Int32] property!"),
                new ValidationFailure("GeneralErrors", "genel"),
                new ValidationFailure("q", "bad"),
                new ValidationFailure("topK", "ikinci ileti")
            ],
            new DefaultHttpContext(),
            StatusCodes.Status400BadRequest);

        general.Success.ShouldBeFalse();
        general.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        general.Message.ShouldBe(Messages.Request.Unreadable);
        general.Data.ShouldBeNull();
        fields.Message.ShouldBe(string.Format(CultureInfo.InvariantCulture, Messages.Request.UnreadableFields, "topK, q"));
    }
}
