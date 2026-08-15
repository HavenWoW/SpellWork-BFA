using System;
using System.Text;
using System.Text.RegularExpressions;

namespace SpellWork.Mcp
{
    /// <summary>
    /// Rejects anything that is not a single read-only SELECT.
    /// <para>
    /// This is one of the two controls spec section 9 requires, not the whole
    /// boundary: the operator must also connect as a read-only MySQL user.
    /// The parser is a guard, the read-only grant is the guarantee. Do not
    /// relax this on the basis that the grant exists, and do not skip the
    /// grant on the basis that this exists.
    /// </para>
    /// <para>
    /// Deliberately conservative. It is not a SQL parser and does not try to
    /// be: anything it cannot confidently classify is rejected, because a
    /// validator that guesses in the caller's favour is not a guard. The
    /// queries this refuses that it arguably should not are a cost worth
    /// paying.
    /// </para>
    /// <para>
    /// The failure mode that matters most here is not a keyword the blacklist
    /// forgot -- it is the guard and MySQL disagreeing about where a literal
    /// or a comment ends, because then the guard vouches for text the server
    /// never sees as text. Three such desyncs are closed by construction:
    /// a backslash inside quoted text is rejected outright rather than
    /// guessed at (its meaning depends on the quote character and on the
    /// server's sql_mode, neither of which the guard can observe); <c>--</c>
    /// only opens a comment when followed by whitespace, exactly as MySQL
    /// requires; and <c>/*! ... */</c> executable comments, whose contents
    /// MySQL runs, are rejected rather than stripped. The literal scan itself
    /// runs once, in <see cref="TryLex"/>, emitting both the comment-stripped
    /// text and its literal-masked twin from a single pass -- so there is no
    /// second lexer that can drift out of step with the first.
    /// </para>
    /// </summary>
    public static class SqlGuard
    {
        // Whole-word matches only, checked outside string literals. INTO
        // catches SELECT ... INTO OUTFILE, which writes a file from a SELECT.
        //
        // An underscore is a word character, so \bKEYWORD\b does not match a
        // built-in whose name merely *contains* that keyword: \bLOAD\b misses
        // LOAD_FILE, \bLOCK\b misses GET_LOCK, \bSET\b misses
        // WAIT_FOR_EXECUTED_GTID_SET. Anything reachable that way has to be
        // named in full here, which is why the list below carries whole
        // function names rather than stems. The whole list was swept for this
        // shape; the escapes that turned out to be harmless (LAST_INSERT_ID,
        // REGEXP_REPLACE, UPDATEXML, CHARSET, OFFSET, CURRENT_USER/USER,
        // IS_FREE_LOCK/IS_USED_LOCK, and information_schema's CREATE_TIME /
        // UPDATE_TIME columns) are non-blocking pure reads and stay allowed
        // on purpose -- OFFSET in particular must keep working, and there are
        // tests pinning that. That is also why this stayed an explicit list
        // rather than becoming a rule that treats '_' as a word separator:
        // such a rule would close the shape generically and reject
        // "SELECT UPDATE_TIME FROM information_schema.TABLES" along with it.
        //
        // The blocking family in the second group is the case where the
        // read-only grant provides NO mitigation at all: GET_LOCK and the
        // *_WAIT/WAIT_* functions need no privilege and can hold the
        // connection for as long as their timeout argument says (or forever,
        // where the argument is optional). For these the guard is not one of
        // two controls, it is the only one -- the same reason SLEEP and
        // BENCHMARK were already listed. WorldDatabase.SelectRows's
        // CommandTimeout is the backstop for whatever this list has not
        // thought of.
        private static readonly string[] ForbiddenKeywords =
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "REPLACE",
            "GRANT", "REVOKE", "RENAME", "LOCK", "UNLOCK", "CALL", "EXEC", "EXECUTE",
            "SET", "LOAD", "LOAD_FILE", "HANDLER", "PREPARE", "DEALLOCATE", "INTO", "OUTFILE",
            "DUMPFILE", "SLEEP", "BENCHMARK", "SHUTDOWN", "FLUSH", "KILL", "USE",

            "GET_LOCK", "RELEASE_LOCK", "RELEASE_ALL_LOCKS",
            "MASTER_POS_WAIT", "SOURCE_POS_WAIT",
            "WAIT_FOR_EXECUTED_GTID_SET", "WAIT_UNTIL_SQL_THREAD_AFTER_GTIDS",

            // MariaDB. The spec says MySQL, but TrinityCore-family world
            // databases very often run on MariaDB, so its blocking natives are
            // not hypothetical here. Both of these wait *indefinitely* when
            // the timeout argument is omitted or negative.
            "MASTER_GTID_WAIT", "WSREP_SYNC_WAIT_UPTO_GTID",
        };

        public static bool TryValidate(string sql, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(sql))
            {
                error = "The query is empty. Provide a single SELECT statement.";
                return false;
            }

            // stripped: comments removed, literals intact.
            // masked:   the same text with every literal's contents blanked.
            // They are index-aligned and produced by one pass, so a keyword or
            // separator is judged "inside a literal" by exactly the same
            // reading that stripped the comments.
            if (!TryLex(sql, out var stripped, out var masked, out error))
                return false;

            var start = 0;
            var end = stripped.Length;
            while (start < end && char.IsWhiteSpace(stripped[start]))
                start++;
            while (end > start && char.IsWhiteSpace(stripped[end - 1]))
                end--;

            if (end == start)
            {
                error = "The query contains nothing but comments. Provide a single SELECT statement.";
                return false;
            }

            // Every pattern check below runs over the masked form, so quoted text
            // can never trigger a rejection and can never hide one either.
            var statement = masked.Substring(start, end - start);

            // No statement separator anywhere outside a literal -- including a
            // trailing one. Stripping a trailing semicolon "just once" is a
            // special case whose only purpose is to let one narrow input
            // through, and every such case is a place the guard's reading and
            // MySQL's can diverge. Rejecting is simpler and strictly more
            // conservative, and the caller can act on it in one edit.
            var separator = statement.IndexOf(';');
            if (separator >= 0)
            {
                error = IsWhiteSpace(statement, separator + 1)
                    ? "A trailing semicolon is not allowed. Remove the ';' and submit the SELECT " +
                      "statement on its own."
                    : "Only a single statement is allowed; the query contains a statement separator (';').";
                return false;
            }

            if (!Regex.IsMatch(statement, @"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase))
            {
                error = "Only SELECT statements are allowed. The query must begin with SELECT (or WITH … SELECT).";
                return false;
            }

            // WITH must actually lead to a SELECT, not a CTE-prefixed DELETE.
            if (Regex.IsMatch(statement, @"^\s*WITH\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(statement, @"\bSELECT\b", RegexOptions.IgnoreCase))
            {
                error = "Only SELECT statements are allowed. A WITH clause must lead to a SELECT.";
                return false;
            }

            // A quoted identifier followed by "(" is a function call wearing a
            // disguise, and it defeats the entire keyword list rather than one
            // entry: the checks below run over the masked text, where a
            // literal's contents are blanked, so `GET_LOCK`('x', 3600) reaches
            // them as `        `('x', 3600) and matches nothing. MySQL is not
            // fooled -- IDENT_sys accepts IDENT or IDENT_QUOTED alike and
            // find_native_function_builder resolves natives from the
            // identifier text, with no quoted/unquoted distinction -- so it
            // runs the native function.
            //
            // Rejecting the construct is the narrow fix. The alternative,
            // scanning keywords over backtick-visible text, would reject
            // SELECT `sleep` FROM t where sleep is an ordinary column, and
            // quoting a keyword-named column is the one thing backticks are
            // legitimately for.
            //
            // Whitespace is tolerated between the quote and the "(" because
            // the guard must not depend on a sql_mode it cannot observe
            // (IGNORE_SPACE makes the spaced form a call too), and because
            // comments are already stripped to a space by this point, which
            // covers `SLEEP`/**/(3600) with the same rule.
            //
            // Matching an *opening* quote here is not possible: every "(" left
            // in the masked text is outside a literal, and the first non-space
            // character after an opening quote is always its closing quote.
            //
            // The cost accepted in exchange: this also refuses a *quoted* CTE
            // name or derived-table alias that carries a column list, as in
            // WITH `cte` (`a`, `b`) AS (…) or AS `t` (`a`, `b`), because that
            // is the same character sequence. Judged acceptable -- the shape
            // is rare, unquoting the alias alone fixes it, and the rejection
            // message below says exactly that.
            //
            // Deliberately not covered, and recorded so the next person
            // weighing "should the keyword list grow?" has the reasoning:
            // installed UDFs are accepted. MariaDB Spider's SPIDER_DIRECT_SQL
            // and SPIDER_BG_DIRECT_SQL run arbitrary SQL on a remote link, and
            // MySQL's SERVICE_GET_WRITE_LOCKS / SERVICE_GET_READ_LOCKS block;
            // calling an already-installed UDF needs no privilege, so the
            // read-only grant does not mitigate them. Installing one needs DBA
            // rights and a stock TrinityCore world database has no Spider, so
            // this is conditional rather than live -- and enumerating every UDF
            // that might exist is not a contest a blacklist can win.
            if (Regex.IsMatch(statement, "[`\"]\\s*\\("))
            {
                error = "A quoted identifier immediately followed by '(' is not allowed: MySQL resolves " +
                        "`GET_LOCK`(…) to the same native function as GET_LOCK(…), so quoting only hides " +
                        "it from this check. If you meant a function call, drop the quotes around the " +
                        "name -- it will then be checked against the read-only rules. If you meant a " +
                        "column, drop the parentheses. If this is a CTE name or a derived-table alias " +
                        "followed by a column list, unquote just that one name: " +
                        "WITH cte (`a`, `b`) AS (…) and AS t (`a`, `b`) are both accepted, and the " +
                        "column names can stay quoted.";
                return false;
            }

            foreach (var keyword in ForbiddenKeywords)
            {
                if (Regex.IsMatch(statement, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                {
                    error = $"Only read-only SELECT statements are allowed; the query contains '{keyword}'.";
                    return false;
                }
            }

            // FOR UPDATE / LOCK IN SHARE MODE take write locks. Neither
            // contains a forbidden keyword on its own.
            if (Regex.IsMatch(statement, @"\bFOR\s+UPDATE\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(statement, @"\bFOR\s+SHARE\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(statement, @"\bIN\s+SHARE\s+MODE\b", RegexOptions.IgnoreCase))
            {
                error = "Locking reads are not allowed; remove FOR UPDATE / FOR SHARE / IN SHARE MODE.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// One pass over the query, producing the comment-stripped text and
        /// its literal-masked twin together. Returns false -- with a reason
        /// the caller can act on -- for anything it cannot read the same way
        /// MySQL will: an unterminated literal or block comment, a backslash
        /// inside quoted text, or a <c>/*!</c> executable comment.
        /// </summary>
        private static bool TryLex(string sql, out string stripped, out string masked, out string error)
        {
            stripped = null;
            masked = null;
            error = null;

            var text = new StringBuilder(sql.Length);
            var blanked = new StringBuilder(sql.Length);
            var index = 0;

            while (index < sql.Length)
            {
                var c = sql[index];

                if (c == '\'' || c == '"' || c == '`')
                {
                    var quote = c;
                    text.Append(quote);
                    blanked.Append(quote);
                    index++;

                    var closed = false;
                    while (index < sql.Length)
                    {
                        var inner = sql[index];

                        // MySQL treats a backslash as an escape inside '' and
                        // "" literals but NOT inside `` identifiers, and not
                        // inside any of them when the server runs with
                        // NO_BACKSLASH_ESCAPES. The guard cannot see sql_mode,
                        // so either reading it picks can be the wrong one --
                        // and being wrong here means masking a real ';' as
                        // literal content, or vice versa. Refusing the
                        // character removes the guess entirely.
                        if (inner == '\\')
                        {
                            // The workarounds are named because the rejection
                            // costs real queries -- LIKE 'spell\_%' is the
                            // idiomatic way to match a literal underscore, and
                            // spell_script_names is one of the tables this
                            // server targets. A rejection the caller cannot
                            // act on just produces the same query again.
                            error = "A backslash inside quoted text is not allowed. Whether it escapes the " +
                                    "closing quote depends on the quote character and on the server's " +
                                    "sql_mode, so this query cannot be read the same way MySQL will read it. " +
                                    "Backslash-free equivalents: '' for a literal quote; " +
                                    "LIKE 'spell|_%' ESCAPE '|' for a literal _ or % (any unused character " +
                                    "works as the escape); POSIX classes such as [[:digit:]], [[:space:]] " +
                                    "or [.] in REGEXP.";
                            return false;
                        }

                        if (inner == quote)
                        {
                            // A doubled quote is an escaped quote in all three
                            // forms, in every sql_mode.
                            if (index + 1 < sql.Length && sql[index + 1] == quote)
                            {
                                text.Append(quote).Append(quote);
                                blanked.Append(' ').Append(' ');
                                index += 2;
                                continue;
                            }

                            text.Append(quote);
                            blanked.Append(quote);
                            index++;
                            closed = true;
                            break;
                        }

                        text.Append(inner);
                        blanked.Append(' ');
                        index++;
                    }

                    if (!closed)
                    {
                        error = "The query has an unterminated string literal or quoted identifier.";
                        return false;
                    }

                    continue;
                }

                // MySQL requires whitespace (or a control character) after the
                // second dash: "-- x" is a comment, "--1" is two unary minuses
                // and stays part of the statement. A guard that swallows
                // "--1; DROP TABLE y" as a comment reads a stacked statement
                // as nothing at all.
                if (c == '-' && index + 1 < sql.Length && sql[index + 1] == '-' &&
                    (index + 2 >= sql.Length || char.IsWhiteSpace(sql[index + 2]) || char.IsControl(sql[index + 2])))
                {
                    while (index < sql.Length && sql[index] != '\n')
                        index++;
                    text.Append(' ');
                    blanked.Append(' ');
                    continue;
                }

                if (c == '#')
                {
                    while (index < sql.Length && sql[index] != '\n')
                        index++;
                    text.Append(' ');
                    blanked.Append(' ');
                    continue;
                }

                if (c == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                {
                    // /*! ... */ is not a comment to MySQL: the server parses
                    // and executes what is inside it. Stripping it would hide
                    // the executed text from every check below.
                    if (index + 2 < sql.Length && sql[index + 2] == '!')
                    {
                        error = "MySQL executable comments (/*! … */) are not allowed: the server executes " +
                                "their contents, so they are not comments. Remove it.";
                        return false;
                    }

                    var close = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        error = "The query has an unterminated block comment.";
                        return false;
                    }

                    index = close + 2;
                    text.Append(' ');
                    blanked.Append(' ');
                    continue;
                }

                text.Append(c);
                blanked.Append(c);
                index++;
            }

            stripped = text.ToString();
            masked = blanked.ToString();
            return true;
        }

        private static bool IsWhiteSpace(string value, int from)
        {
            for (var i = from; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }
    }
}
