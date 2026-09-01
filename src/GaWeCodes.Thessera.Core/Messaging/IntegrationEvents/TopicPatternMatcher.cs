namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

/// <summary>
/// Matches a topic against a subscription pattern, using the same rules a topic exchange does.
/// </summary>
/// <remarks>
/// Kept here rather than delegated to a broker so that a startup check can tell, without connecting
/// to anything, whether a declared subscription would ever deliver to the handlers a host registered.
/// </remarks>
public static class TopicPatternMatcher
{
    /// <summary>
    /// Determines whether a topic is covered by a pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, in dot-separated segments: <c>*</c> matches exactly one segment and <c>#</c>
    /// matches zero or more, so <c>orders.*</c> takes every event of the orders context and
    /// <c>#</c> takes everything.
    /// </param>
    /// <param name="topic">The routing key to test.</param>
    /// <returns><see langword="true"/> when the pattern covers the topic.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pattern"/> or <paramref name="topic"/> is empty or blank.
    /// </exception>
    public static bool Matches(string pattern, string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        return Matches(pattern.Split('.'), 0, topic.Split('.'), 0);
    }

    private static bool Matches(string[] pattern, int patternIndex, string[] topic, int topicIndex)
    {
        while (true)
        {
            if (patternIndex == pattern.Length)
            {
                return topicIndex == topic.Length;
            }

            var segment = pattern[patternIndex];

            if (segment == "#")
            {
                for (var skipped = topicIndex; skipped <= topic.Length; skipped++)
                {
                    if (Matches(pattern, patternIndex + 1, topic, skipped))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (topicIndex == topic.Length)
            {
                return false;
            }

            if (segment != "*" && !string.Equals(segment, topic[topicIndex], StringComparison.Ordinal))
            {
                return false;
            }

            patternIndex++;
            topicIndex++;
        }
    }
}
