using Xunit;

namespace ReactiveDomain.Policy.Tests
{
    public class PolicyTests
    {
        /// <summary>
        /// this stop a warning from the test runner.
        /// once the actual tests get built out this can be removed
        /// </summary>
        [Fact]
        public void can_make_the_test_runner_happy() {
            var isHappy = true;
            Assert.True(isHappy);
        }
    }
}
