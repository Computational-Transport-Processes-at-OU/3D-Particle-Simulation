using System.Collections.Generic;

/*
Some miscellaneous helper routines.
*/

public static class MiscUtil
{
    // Easy delimiter-based tokenization of string that may contain text
    // enclosed in literal delimiters. E.g.:
    //    "this is \"an example\" with \"literal delimiters\""
    //    => [ "this", "is", "an example", "with", "literal delimiters" ]
    // Implemented as a simple finite state machine, trailing unterminated
    // strings are not returned.
    
    enum TokStatus { None, InWord, InString };
    enum TokAction { None, Add, Return };
    public static IEnumerable<string> EnumerateTokens(string line, char wordDelim, char stringDelim)
    {
        if (line == null) yield break;

        var sb = new System.Text.StringBuilder();
        var status = TokStatus.None;

        foreach (var c in line)
        {
            var action = TokAction.Add; // default action: use the current character

            switch (status)
            {
                case TokStatus.None:
                    if (c == wordDelim) action = TokAction.None; // ignore current character
                    else if (c == stringDelim)
                    {
                        action = TokAction.None; // ignore current character
                        status = TokStatus.InString;
                    }
                    else status = TokStatus.InWord;
                    break;

                case TokStatus.InString:
                    if (c == stringDelim) action = TokAction.Return; // "Return" action: status => "None"
                    break;

                case TokStatus.InWord:
                    if (c == wordDelim) action = TokAction.Return; // "Return" action: status => "None"
                    break;
            }

            if (action == TokAction.Add) sb.Append(c);
            else if (action == TokAction.Return)
            {
                yield return sb.ToString();
                sb = new System.Text.StringBuilder();
                status = TokStatus.None; // <- automatic  status => None!
            }
        }
        // Trailing token? Return! If status == InString, there's an unterminated string literal. Ignore!
        if ((sb.Length > 0) && (status != TokStatus.InString))
        {
            yield return sb.ToString();
        }
    }

    public static List<string> Tokenize(string line, char wordDelim, char stringDelim = '"')
    {
        var tokens = new List<string>();
        foreach (var t in EnumerateTokens(line, wordDelim, stringDelim)) tokens.Add(t);
        return tokens;
    }
}
