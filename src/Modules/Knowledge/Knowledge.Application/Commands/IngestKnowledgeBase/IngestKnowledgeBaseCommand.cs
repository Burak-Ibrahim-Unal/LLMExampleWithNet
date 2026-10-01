using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.IngestKnowledgeBase;

/// <summary>Synchronizes the database and the search index with the knowledge base files.</summary>
public sealed record IngestKnowledgeBaseCommand : IRequest<ApiResult<IngestionSummaryDto>>;
