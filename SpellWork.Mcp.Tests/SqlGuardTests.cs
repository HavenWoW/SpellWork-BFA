using System;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // No database, by design (spec section 10): the validator is a pure
    // function over a string and is tested independently of MySQL. Every case
    // here is a plain Fact/Theory that runs on any machine, with or without a
    // MySQL server present.
    public class SqlGuardTests
    {
        [Theory]
        [InlineData("SELECT * FROM spell_proc WHERE SpellId = 133")]
        [InlineData("select id, name from spell_dbc limit 10")]
        [InlineData("  SELECT 1  ")]
        [InlineData("SELECT * FROM spell_proc WHERE comment = 'a; DROP TABLE x'")]
        [InlineData("SELECT * FROM spell_bonus_data WHERE entry IN (1,2,3)")]
        [InlineData("WITH ranked AS (SELECT id FROM spell_ranks) SELECT * FROM ranked")]
        [InlineData("SELECT * FROM spell_proc -- trailing comment")]
        [InlineData("SELECT /* inline */ * FROM spell_proc")]
        [InlineData("SELECT * FROM spell_proc WHERE name = 'it''s fine'")]
        // Harmless names that embed a forbidden keyword without a word
        // boundary. These are the other half of the underscore/embedding
        // sweep and must keep working: OFFSET embeds SET and is ordinary
        // paging, CURRENT_USER embeds USE and is how a caller checks which
        // MySQL account it is connected as -- which is the read-only grant
        // spec section 9 requires.
        [InlineData("SELECT id FROM spell_dbc LIMIT 10 OFFSET 5")]
        [InlineData("SELECT CURRENT_USER()")]
        // The two backslash-free escapes the backslash rejection recommends.
        // If either of these were refused, that message would be sending the
        // caller into a second rejection.
        [InlineData("SELECT * FROM spell_script_names WHERE ScriptName LIKE 'spell|_%' ESCAPE '|'")]
        [InlineData("SELECT * FROM spell_proc WHERE Comments REGEXP '[[:digit:]]+'")]
        // Quoted identifiers that are not function calls. Quoting a
        // keyword-named column is the one thing backticks legitimately exist
        // for, so the fix for the quoted-call bypass must not reach these.
        [InlineData("SELECT `sleep` FROM spell_dbc")]
        [InlineData("SELECT `SpellId` FROM `spell_proc` WHERE `SpellId` IN (133, 134)")]
        [InlineData("SELECT COUNT(`SpellId`) FROM `spell_proc`")]
        public void Accepts(string sql)
        {
            Assert.True(SqlGuard.TryValidate(sql, out var error), $"rejected with: {error}");
        }

        [Theory]
        // Not a SELECT at all
        [InlineData("DELETE FROM spell_proc")]
        [InlineData("UPDATE spell_proc SET SpellId = 1")]
        [InlineData("INSERT INTO spell_proc VALUES (1)")]
        [InlineData("DROP TABLE spell_proc")]
        [InlineData("TRUNCATE spell_proc")]
        [InlineData("ALTER TABLE spell_proc ADD COLUMN x INT")]
        [InlineData("CREATE TABLE x (id INT)")]
        [InlineData("GRANT ALL ON *.* TO 'x'@'%'")]
        [InlineData("CALL some_procedure()")]
        [InlineData("SET @x = 1")]
        // Stacked statements
        [InlineData("SELECT 1; DROP TABLE spell_proc")]
        [InlineData("SELECT 1;DELETE FROM spell_proc")]
        [InlineData("SELECT 1 ; ")]
        // DML hidden behind a leading SELECT
        [InlineData("SELECT * FROM spell_proc INTO OUTFILE '/tmp/x'")]
        [InlineData("SELECT * FROM (DELETE FROM spell_proc) t")]
        [InlineData("SELECT * FROM spell_proc FOR UPDATE")]
        // An underscore is a word character, so a \bLOAD\b rule does not
        // reach LOAD_FILE -- which reads a server-side file out of an
        // otherwise ordinary SELECT.
        [InlineData("SELECT LOAD_FILE('/etc/passwd')")]
        // Same shape, and worse: none of these needs any privilege, so the
        // read-only grant does not mitigate them at all. GET_LOCK's second
        // argument is a timeout in seconds -- this one holds the connection
        // for an hour.
        [InlineData("SELECT GET_LOCK('spellwork', 3600)")]
        [InlineData("SELECT RELEASE_LOCK('spellwork')")]
        [InlineData("SELECT RELEASE_ALL_LOCKS()")]
        [InlineData("SELECT MASTER_POS_WAIT('binlog.000001', 4)")]
        [InlineData("SELECT SOURCE_POS_WAIT('binlog.000001', 4)")]
        [InlineData("SELECT WAIT_FOR_EXECUTED_GTID_SET('3E11FA47-71CA-11E1-9E33-C80AA9429562:1')")]
        [InlineData("SELECT WAIT_UNTIL_SQL_THREAD_AFTER_GTIDS('3E11FA47-71CA-11E1-9E33-C80AA9429562:1')")]
        // MariaDB's blocking natives. Both wait forever with no timeout
        // argument, and TrinityCore-family world databases often run MariaDB.
        [InlineData("SELECT MASTER_GTID_WAIT('0-1-100')")]
        [InlineData("SELECT WSREP_SYNC_WAIT_UPTO_GTID('0-1-100')")]
        // A quoted identifier followed by '(' is a native function call that
        // the keyword scan cannot see, because the scan runs over text with
        // literal contents masked. This defeats the whole list, not one entry
        // -- every one of these was ACCEPTED by the guard shipped at ea8999f,
        // measured before the fix.
        [InlineData("SELECT `GET_LOCK`('spellwork', 3600)")]
        [InlineData("SELECT `SLEEP`(3600), id FROM spell_dbc")]
        [InlineData("SELECT `LOAD_FILE`('/etc/passwd')")]
        [InlineData("SELECT \"SLEEP\"(3600)")]
        [InlineData("SELECT `GET_LOCK` ('spellwork', 3600)")]
        [InlineData("SELECT `SLEEP`/**/(3600)")]
        [InlineData("SELECT `DROP`(1)")]
        // The known cost of that rule, pinned so it stays a deliberate
        // decision rather than a surprise: a *quoted* CTE name or
        // derived-table alias carrying a column list is the same character
        // sequence as a quoted function call, and is refused with it. The
        // rejection message names this case and the one-token rewrite; the
        // rewrite itself is asserted by RejectionMessage_NamesTheQuotedFunctionCall.
        [InlineData("WITH `cte` (`a`, `b`) AS (SELECT 1, 2) SELECT * FROM `cte`")]
        [InlineData("SELECT * FROM (SELECT 1 AS c, 2 AS d) AS `t` (`a`, `b`)")]
        // Comment-obscured stacking
        [InlineData("SELECT 1 -- \n; DROP TABLE spell_proc")]
        [InlineData("SELECT 1 /* */; DROP TABLE spell_proc")]
        // Lexer desync: constructs where MySQL's reading of the query differs
        // from a naive guard's. Each of these is accepted by a guard that
        // treats a backslash as an escape inside every quote character, that
        // treats "--" as a comment without requiring the whitespace MySQL
        // requires after it, or that strips /*! ... */ as an ordinary comment.
        [InlineData("SELECT 1 FROM `x\\`; DROP TABLE y`")]
        [InlineData("SELECT * FROM spell_proc WHERE name = 'a\\'; DROP TABLE spell_proc; -- '")]
        [InlineData("SELECT 1 --1; DROP TABLE spell_proc")]
        [InlineData("SELECT 1 /*!00000; DROP TABLE spell_proc */")]
        // Unreadable: a query the guard cannot finish reading is one it
        // cannot vouch for.
        [InlineData("SELECT * FROM spell_proc WHERE name = 'abc")]
        [InlineData("SELECT 1 /* abc")]
        [InlineData("/* nothing but a comment */")]
        // Nothing at all
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Rejects(string sql)
        {
            Assert.False(SqlGuard.TryValidate(sql, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        // The message is part of the contract: a rejection the agent cannot
        // act on produces a retry loop of the same rejected query.
        [Fact]
        public void RejectionMessage_NamesTheReason()
        {
            SqlGuard.TryValidate("DELETE FROM spell_proc", out var error);
            Assert.Contains("SELECT", error);
        }

        // A trailing semicolon and a stacked statement are different mistakes
        // with different fixes. One shared "invalid query" message for both
        // leaves the caller guessing which it made.
        [Fact]
        public void RejectionMessage_NamesTheTrailingSemicolon()
        {
            Assert.False(SqlGuard.TryValidate("SELECT 1 ;", out var trailing));
            Assert.Contains("semicolon", trailing, StringComparison.OrdinalIgnoreCase);

            Assert.False(SqlGuard.TryValidate("SELECT 1; DROP TABLE spell_proc", out var stacked));
            Assert.NotEqual(trailing, stacked);
        }

        // Rejected for the right reason, not by accident: a guard that turned
        // this away as an "unterminated literal" would still be reading the
        // query differently from MySQL everywhere else.
        [Fact]
        public void RejectionMessage_NamesTheBackslash()
        {
            Assert.False(SqlGuard.TryValidate("SELECT 1 FROM `x\\`; DROP TABLE y`", out var error));
            Assert.Contains("backslash", error, StringComparison.OrdinalIgnoreCase);
        }

        // The backslash rejection is correct and stays, but it costs real
        // queries: LIKE 'spell\_%' is the idiomatic way to match a literal
        // underscore against a table this server targets. The message must
        // therefore hand back an escape that works -- and this asserts the
        // recommended rewrite is actually accepted, not merely mentioned.
        [Fact]
        public void RejectionMessage_OffersAnEscapeThatIsAccepted()
        {
            Assert.False(SqlGuard.TryValidate(
                "SELECT * FROM spell_script_names WHERE ScriptName LIKE 'spell\\_%'", out var error));
            Assert.Contains("ESCAPE", error, StringComparison.Ordinal);
            Assert.Contains("REGEXP", error, StringComparison.Ordinal);

            Assert.True(SqlGuard.TryValidate(
                "SELECT * FROM spell_script_names WHERE ScriptName LIKE 'spell|_%' ESCAPE '|'",
                out var rewriteError), $"the recommended rewrite was rejected with: {rewriteError}");
        }

        // Rejected for the right reason: "contains 'GET_LOCK'" would mean the
        // keyword scan was somehow seeing through the mask, which it is not.
        //
        // The same message is what a caller sees for the legitimate construct
        // this rule also refuses -- a quoted CTE name or derived-table alias
        // with a column list -- so it must offer a clause that case can act on.
        // A caller that knows it did not mean a function call and did not mean
        // a column has nothing to do with "drop the quotes or the parentheses"
        // and will resubmit the same query. Both rewrites are asserted here,
        // not merely mentioned, and both keep their column names quoted: the
        // advice is to unquote one token, not to strip every backtick.
        [Fact]
        public void RejectionMessage_NamesTheQuotedFunctionCall()
        {
            Assert.False(SqlGuard.TryValidate("SELECT `GET_LOCK`('spellwork', 3600)", out var error));
            Assert.Contains("quoted identifier", error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CTE", error, StringComparison.Ordinal);
            Assert.Contains("alias", error, StringComparison.OrdinalIgnoreCase);

            Assert.True(SqlGuard.TryValidate(
                "WITH cte (`a`, `b`) AS (SELECT 1, 2) SELECT * FROM `cte`",
                out var cteError), $"the recommended CTE rewrite was rejected with: {cteError}");
            Assert.True(SqlGuard.TryValidate(
                "SELECT * FROM (SELECT 1 AS c, 2 AS d) AS t (`a`, `b`)",
                out var aliasError), $"the recommended alias rewrite was rejected with: {aliasError}");
        }

        [Fact]
        public void RejectionMessage_NamesTheExecutableComment()
        {
            Assert.False(SqlGuard.TryValidate("SELECT 1 /*!00000; DROP TABLE spell_proc */", out var error));
            Assert.Contains("/*!", error, StringComparison.Ordinal);
        }
    }
}
