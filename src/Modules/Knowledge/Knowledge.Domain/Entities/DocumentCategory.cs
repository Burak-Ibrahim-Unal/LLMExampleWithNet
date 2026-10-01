namespace Knowledge.Domain.Entities;

/// <summary>Document type; also the authority order used when sources disagree (policy/procedure &gt; guide &gt; FAQ).</summary>
public enum DocumentCategory
{
    Policy,
    Procedure,
    Guide,
    Faq
}
