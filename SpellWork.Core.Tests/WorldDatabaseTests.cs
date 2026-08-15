using SpellWork.Database;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class WorldDatabaseTests
    {
        [Fact]
        public void TestConnection_reports_not_configured_without_an_error()
        {
            var database = new WorldDatabase(new SpellWorkConfig { UseDatabase = false });

            Assert.False(database.TestConnection(out var error));
            Assert.Null(error);
            Assert.False(database.Connected);
        }
    }
}
