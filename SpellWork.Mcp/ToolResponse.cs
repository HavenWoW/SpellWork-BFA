using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpellWork.Mcp
{
    /// <summary>
    /// Every tool returns one of these two shapes. A uniform envelope means
    /// the agent can tell "the server answered and the answer is no" apart
    /// from "the call blew up", which a bare exception message cannot.
    /// </summary>
    public static class ToolResponse
    {
        /// <summary>Server-side result cap (spec section 6). A broad query must
        /// not be able to flood the agent's context.</summary>
        public const int MaxRows = 500;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = false,
        };

        public static string Ok(object payload) =>
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = true,
                ["result"] = payload,
            }, Options);

        public static string Error(string code, string message, IDictionary<string, object> details = null)
        {
            var error = new Dictionary<string, object>
            {
                ["code"] = code,
                ["message"] = message,
            };

            if (details != null && details.Count > 0)
                error["details"] = details;

            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = error,
            }, Options);
        }

        /// <summary>
        /// Applies the 500-row cap and reports it -- authoritatively, not as
        /// advice -- for the tools that page: search_spells, query_db2 and
        /// list_enum all funnel through here, so their cap is enforced once
        /// rather than at each call site.
        ///
        /// This method is NOT a universal funnel, and claiming it were one
        /// would only stop the next reader checking. MaxRows is the shared
        /// constant; this is one of three ways it gets applied:
        ///   - here, for the tools returning a paged rows/offset/limit page.
        ///   - Take(MaxRows) plus a "truncated" flag, in get_spell_triggers
        ///     and compare_spells, whose payloads are not pages and do not fit
        ///     this envelope. (list_enum used to be in this group and no
        ///     longer is: it had no offset, so a truncated enum's withheld
        ///     members were unreachable, and its "count" meant total matched
        ///     where every other tool means rows returned.)
        ///   - a row ceiling handed to the query itself, in query_world_db and
        ///     get_spell_db_overrides, so the cap applies at the database.
        /// list_db2 caps nothing at all, deliberately: it is the discovery
        /// entry point and has no offset parameter, so truncating it would
        /// hide stores with no way to reach them.
        /// Callers pass the full match count so the response can say how
        /// much was withheld -- silently truncating reads as "that was
        /// everything".
        ///
        /// Sanitisation rules, made explicit rather than left to infer:
        ///   - a negative <paramref name="offset"/> is treated as 0.
        ///   - a non-positive <paramref name="limit"/> is treated as 0 (no
        ///     rows returned -- never interpreted as "unbounded", which is
        ///     exactly the flood this cap exists to prevent).
        ///   - the effective limit is min(sanitised limit, MaxRows); if
        ///     <paramref name="rows"/> holds more than that (a caller forgot
        ///     to clamp its own query), the excess is dropped here.
        ///   - the envelope's "limit" is that effective, applied ceiling --
        ///     never the raw value the caller asked for.
        ///   - "truncated" is true whenever rows were withheld for *any*
        ///     reason: more matches remain past this page, the cap trimmed
        ///     what was handed in, or both. It is never false while rows are
        ///     missing.
        /// </summary>
        public static Dictionary<string, object> Paged(IList<object> rows, int totalMatches, int offset, int limit)
        {
            var suppliedRows = rows ?? Array.Empty<object>();
            var effectiveOffset = offset < 0 ? 0 : offset;
            var sanitisedLimit = limit < 0 ? 0 : limit;
            var effectiveLimit = Math.Min(sanitisedLimit, MaxRows);

            var cappedByLimit = suppliedRows.Count > effectiveLimit;
            IList<object> cappedRows = cappedByLimit
                ? suppliedRows.Take(effectiveLimit).ToList()
                : suppliedRows;

            var moreMatchesRemain = effectiveOffset + cappedRows.Count < totalMatches;

            return new Dictionary<string, object>
            {
                ["rows"] = cappedRows,
                ["count"] = cappedRows.Count,
                ["totalMatches"] = totalMatches,
                ["offset"] = effectiveOffset,
                ["limit"] = effectiveLimit,
                ["truncated"] = moreMatchesRemain || cappedByLimit,
                ["maxRows"] = MaxRows,
            };
        }
    }
}
