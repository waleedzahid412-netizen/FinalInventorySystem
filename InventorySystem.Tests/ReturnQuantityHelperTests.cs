using InventorySystem.Helpers;
using Xunit;

namespace InventorySystem.Tests
{
    public class ReturnQuantityHelperTests
    {
        [Fact]
        public void TryNormalize_PackagingMode_ConvertsToBase()
        {
            var ok = ReturnQuantityHelper.TryNormalize(
                1m,
                ReturnQuantityHelper.PackagingMode,
                24m,
                out decimal packaging,
                out decimal bas,
                out string? error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(1m, packaging);
            Assert.Equal(24m, bas);
        }

        [Fact]
        public void TryNormalize_BaseMode_ConvertsToPackaging()
        {
            var ok = ReturnQuantityHelper.TryNormalize(
                12m,
                ReturnQuantityHelper.BaseMode,
                24m,
                out decimal packaging,
                out decimal bas,
                out string? error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(0.5m, packaging);
            Assert.Equal(12m, bas);
        }

        [Fact]
        public void TryNormalize_PieceAlias_TreatedAsBaseMode()
        {
            var ok = ReturnQuantityHelper.TryNormalize(12m, "Piece", 24m, out decimal packaging, out decimal bas, out _);

            Assert.True(ok);
            Assert.Equal(0.5m, packaging);
            Assert.Equal(12m, bas);
        }

        [Fact]
        public void TryNormalize_FractionalInput_Rejected()
        {
            var ok = ReturnQuantityHelper.TryNormalize(
                0.5m,
                ReturnQuantityHelper.PackagingMode,
                24m,
                out _,
                out _,
                out string? error);

            Assert.False(ok);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TryNormalize_Zero_SucceedsWithZeros()
        {
            var ok = ReturnQuantityHelper.TryNormalize(0m, ReturnQuantityHelper.BaseMode, 24m, out decimal packaging, out decimal bas, out _);

            Assert.True(ok);
            Assert.Equal(0m, packaging);
            Assert.Equal(0m, bas);
        }
    }
}
