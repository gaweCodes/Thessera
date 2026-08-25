namespace GaWeCodes.Thessera.Domain.Naming;

public static class NameSegment
{
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
