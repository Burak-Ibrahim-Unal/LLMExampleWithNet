namespace Knowledge.Application.Exceptions;

/// <summary>A knowledge base file is malformed (missing front matter, unknown status, bad date...).</summary>
public sealed class KnowledgeBaseFormatException(string message) : Exception(message);
