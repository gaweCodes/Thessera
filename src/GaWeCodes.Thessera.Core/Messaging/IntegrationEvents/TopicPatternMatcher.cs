namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

public static class TopicPatternMatcher
{
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
