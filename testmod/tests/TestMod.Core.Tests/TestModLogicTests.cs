using TestMod.Core;
using Xunit;

namespace TestMod.Core.Tests
{
    public sealed class TestModLogicTests
    {
        [Fact]
        public void StartupMessageMatchesMelonLogContract()
        {
            TestModLogic logic = new TestModLogic();

            Assert.Equal("Loaded", logic.StartupMessage());
        }

        [Fact]
        public void AddScoreAddsPositiveDelta()
        {
            TestModLogic logic = new TestModLogic();

            Assert.Equal(15, logic.AddScore(10, 5));
        }

        [Fact]
        public void AddScoreIgnoresNegativeDelta()
        {
            TestModLogic logic = new TestModLogic();

            Assert.Equal(10, logic.AddScore(10, -5));
        }
    }
}
