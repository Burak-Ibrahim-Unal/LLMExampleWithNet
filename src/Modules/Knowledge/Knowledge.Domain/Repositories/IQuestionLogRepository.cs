using Knowledge.Domain.Entities;
using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Repositories;

/// <summary>
/// <see cref="QuestionLog"/> denetim kayıtları için repository. Yalnızca ekleme ve kaydetme gerektiğinden genel
/// <c>IRepository&lt;T&gt;</c> sözleşmesine ek üye tanımlamaz; ayrı bir arayüz olması, soru handler'ının bağımlılığını
/// açıkça adlandırır ve DI'da modüle özgü bir kayıt sağlar.
/// </summary>
public interface IQuestionLogRepository : IRepository<QuestionLog>;
