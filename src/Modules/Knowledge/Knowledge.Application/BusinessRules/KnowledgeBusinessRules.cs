using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Shared.Application.Common;

namespace Knowledge.Application.BusinessRules;

public sealed class KnowledgeBusinessRules(IKnowledgeIndex index)
{
    public const int MaxQueryLength = 500;

    public const int MaxTopK = 20;

    public ApiResult<T>? CheckKnowledgeBaseNotEmpty<T>(int documentCount)
    {
        if (documentCount > 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.KnowledgeBaseEmpty, 422);
    }

    public ApiResult<T>? CheckUniqueDocumentIds<T>(IEnumerable<string> sourceIds)
    {
        var duplicates = sourceIds
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count == 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.DuplicateDocumentId, string.Join(", ", duplicates)), 422);
    }

    public ApiResult<T>? CheckIndexReady<T>()
    {
        if (index.IsReady)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.IndexNotReady, 503);
    }

    public ApiResult<T>? CheckDocumentFound<T>(KnowledgeDocument? document)
    {
        if (document is not null)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.DocumentNotFound, 404);
    }

    public ApiResult<T>? CheckQueryRequired<T>(string query)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.QueryRequired, 400);
    }

    public ApiResult<T>? CheckQueryLength<T>(string query)
    {
        if (query.Length <= MaxQueryLength)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.QueryTooLong, MaxQueryLength), 400);
    }

    public ApiResult<T>? CheckQuestionRequired<T>(string question)
    {
        if (!string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.QuestionRequired, 400);
    }

    public ApiResult<T>? CheckQuestionLength<T>(string question)
    {
        if (question.Length <= MaxQueryLength)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.QuestionTooLong, MaxQueryLength), 400);
    }

    public ApiResult<T>? CheckRetrievalMode<T>(string? mode)
    {
        if (mode is null || mode.Equals("lexical", StringComparison.OrdinalIgnoreCase) || mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Knowledge.RetrievalModeInvalid, 400);
    }

    public ApiResult<T>? CheckTopKInRange<T>(int topK)
    {
        if (topK is >= 1 and <= MaxTopK)
        {
            return null;
        }

        return ApiResult<T>.Fail(string.Format(Messages.Knowledge.TopKOutOfRange, MaxTopK), 400);
    }
}
