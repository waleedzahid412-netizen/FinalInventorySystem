using InventorySystem.Helpers;
using Xunit;

namespace InventorySystem.Tests
{
    public class ReturnValuationHelperTests
    {
        [Fact]
        public void OddConversion_OneOfThirteen_IsExactEighteen()
        {
            // Old bug: Round(1/13, 3) * 234 = 0.077 * 234 = 18.02
            decimal amount = ReturnValuationHelper.ComputeBaseProportionalAmount(
                thisBaseQty: 1m,
                originalBaseQty: 13m,
                originalAmount: 234m,
                priorBaseQty: 0m);

            Assert.Equal(18.00m, amount);
        }

        [Fact]
        public void OddConversion_ThirteenOnes_SumToLineTotal()
        {
            decimal priorBase = 0m;
            decimal priorAmount = 0m;
            decimal sum = 0m;

            for (int i = 0; i < 13; i++)
            {
                decimal slice = ReturnValuationHelper.ComputeBaseProportionalAmount(
                    thisBaseQty: 1m,
                    originalBaseQty: 13m,
                    originalAmount: 234m,
                    priorBaseQty: priorBase,
                    priorAllocatedAmount: priorAmount);

                Assert.Equal(18.00m, slice);
                sum += slice;
                priorBase += 1m;
                priorAmount += slice;
            }

            Assert.Equal(234.00m, sum);
        }

        [Fact]
        public void LastSlice_PicksUpRemainderCents()
        {
            // 100 / 3 does not divide evenly
            decimal first = ReturnValuationHelper.ComputeBaseProportionalAmount(1m, 3m, 100m, 0m);
            decimal second = ReturnValuationHelper.ComputeBaseProportionalAmount(1m, 3m, 100m, 1m, first);
            decimal third = ReturnValuationHelper.ComputeBaseProportionalAmount(1m, 3m, 100m, 2m, first + second);

            Assert.Equal(33.33m, first);
            Assert.Equal(33.33m, second);
            Assert.Equal(33.34m, third);
            Assert.Equal(100.00m, first + second + third);
        }
    }
}
