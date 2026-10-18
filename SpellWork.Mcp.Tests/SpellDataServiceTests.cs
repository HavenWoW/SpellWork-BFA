using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class SpellDataServiceTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public SpellDataServiceTests(McpClientDataFixture fixture) => _fixture = fixture;

        private static SpellDataService ServiceFor(IDictionary<string, string> environment) =>
            new SpellDataService(McpServerConfig.FromEnvironment(environment), NullLoggerFactoryStub.Logger);

        [Fact]
        public async Task GetAsync_WithNoDbcPath_ReportsTheVariableToFix()
        {
            var data = await ServiceFor(new Dictionary<string, string>()).GetAsync();

            Assert.False(data.Loaded);
            Assert.Equal("dbc_path_missing", data.ErrorCode);
            Assert.Equal("SPELLWORK_DBC_PATH", data.ErrorVariable);
        }

        [Fact]
        public async Task GetAsync_WithNonexistentDbcPath_NamesThePath()
        {
            var data = await ServiceFor(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\definitely\not\here",
            }).GetAsync();

            Assert.False(data.Loaded);
            Assert.Equal("dbc_path_not_found", data.ErrorCode);
            Assert.Contains(@"C:\definitely\not\here", data.ErrorMessage);
        }

        // The whole point of the Lazy: concurrent first callers must produce
        // one load, not N. A second DBC.LoadAsync in the same process throws
        // a duplicate-key ArgumentException from GameTable<T>.Open.
        //
        // A naive `if (_data == null) _data = LoadAsync();` guard only loses
        // this race intermittently (mutation testing against this exact
        // scenario put it around 1.5% of genuinely concurrent first
        // accesses), so the test has to actually force concurrent access
        // rather than just call GetAsync() several times in a row on one
        // thread. A C# array initializer like `new[] { a(), b() }` evaluates
        // strictly sequentially -- against the synchronous missing-path load
        // this method uses, that never races even a broken guard. Each trial
        // below starts several callers on the thread pool and uses a Barrier
        // so they all reach service.GetAsync() at nearly the same instant,
        // then repeats across many fresh service instances so a rare race
        // has a real chance of showing up.
        [Fact]
        public async Task GetAsync_LoadsExactlyOnce_UnderConcurrency()
        {
            const int trials = 3000;
            const int callersPerTrial = 8;

            for (var trial = 0; trial < trials; trial++)
            {
                var service = ServiceFor(new Dictionary<string, string>());
                using var barrier = new Barrier(callersPerTrial);

                var callers = new Task<SpellData>[callersPerTrial];
                for (var i = 0; i < callersPerTrial; i++)
                {
                    callers[i] = Task.Run(async () =>
                    {
                        barrier.SignalAndWait();
                        return await service.GetAsync();
                    });
                }

                var results = await Task.WhenAll(callers);

                for (var i = 1; i < results.Length; i++)
                    Assert.Same(results[0], results[i]);
            }
        }

        // DBC.cs:127 used to run an unguarded "foreach (var spell in
        // SpellName)" -- when the locale directory under DbcPath doesn't
        // exist (wrong SPELLWORK_LOCALE, or DbcPath pointed one level too
        // high), every Storage<T> property is left null by a swallowed
        // DirectoryNotFoundException, and that foreach threw
        // NullReferenceException. This landed in the catch around
        // DBC.LoadAsync and got reported as load_failed with a generic CLR
        // message, so the SpellCount == 0 check just below it -- the one
        // that produces the actionable no_data_loaded error this test
        // asserts on -- was unreachable for exactly the misconfiguration
        // it exists to catch. DBC.cs now guards that foreach (and the
        // GameTable.Open after it) on SpellName != null, so a load with no
        // usable data returns cleanly instead of throwing.
        [SkippableFact]
        public void GetAsync_WithWrongLocale_ReportsNoDataLoaded()
        {
            Skip.IfNot(_fixture.Available, McpClientDataFixture.SkipReason);

            var data = _fixture.NoDataResult;

            Assert.False(data.Loaded);
            Assert.Equal("no_data_loaded", data.ErrorCode);
            Assert.Equal(0, data.SpellCount);
            Assert.Equal("SPELLWORK_DBC_PATH", data.ErrorVariable);
        }

        // Tasks 5-11 all await this service and assume a real load produces
        // Loaded == true, a non-zero SpellCount, and StoreCounts describing
        // what actually loaded -- none of that was exercised anywhere in
        // this suite before real client data was available on this machine.
        [SkippableFact]
        public void GetAsync_WithRealClientData_LoadsSuccessfully()
        {
            Skip.IfNot(_fixture.Available, McpClientDataFixture.SkipReason);

            var data = _fixture.LoadedResult;

            Assert.True(data.Loaded);
            Assert.Null(data.ErrorCode);
            Assert.True(data.SpellCount > 0);
            Assert.NotEmpty(data.StoreCounts);
            Assert.True(data.StoreCounts.TryGetValue("SpellName", out var spellNameCount));
            Assert.Equal(data.SpellCount, spellNameCount);
            Assert.All(data.StoreCounts.Values, count => Assert.True(count >= 0));
        }
    }

    internal static class NullLoggerFactoryStub
    {
        public static Microsoft.Extensions.Logging.ILogger<SpellDataService> Logger { get; } =
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SpellDataService>.Instance;
    }
}
