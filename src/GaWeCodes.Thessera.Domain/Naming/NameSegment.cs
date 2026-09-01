namespace GaWeCodes.Thessera.Domain.Naming;

/// <summary>
/// The grammar every persisted name in the family shares: aggregate names, event names, and both
/// segments of an integration-event topic.
/// </summary>
/// <remarks>
/// The character set is deliberately narrow because these names leave the process. They end up in
/// database columns, in stream keys and in broker routing keys, and each of those tolerates a
/// different set of characters — so the family uses the intersection and nothing else.
/// </remarks>
public static class NameSegment
{
    /// <summary>
    /// Determines whether <paramref name="segment"/> is a valid persisted name.
    /// </summary>
    /// <param name="segment">The candidate name.</param>
    /// <returns>
    /// <see langword="true"/> when the segment is non-empty and consists only of lower-case ASCII
    /// letters, digits and single hyphens, with no hyphen at the start or the end — so
    /// <c>widget-created-v1</c> passes and <c>WidgetCreated</c>, <c>a--b</c> and <c>-a</c> do not.
    /// </returns>
    public static bool IsValid(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment[0] == '-' || segment[^1] == '-')
        {
            return false;
        }

        var previousWasHyphen = false;

        foreach (var character in segment)
        {
            if (character == '-')
            {
                if (previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }

            previousWasHyphen = false;
        }

        return true;
    }
}
