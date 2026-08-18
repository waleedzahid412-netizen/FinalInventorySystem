using System.Collections.Generic;
using System.Linq;
using InventorySystem.Helpers;
using Xunit;

namespace InventorySystem.Tests
{
    public class DiscountAllocationHelperTests
    {
        [Fact]
        public void Percentage5_Allocates_500_400_200_SummingTo1100()
        {
            var lines = CanonicalLines();
            decimal header = DiscountAllocationHelper.ComputeHeaderDiscount("Percentage", 5m, 22_000m);

            Assert.Equal(1_100m, header);

            var allocated = DiscountAllocationHelper.Allocate(lines, header, "Percentage", 5m);

            Assert.Equal(new[] { 500m, 400m, 200m }, allocated.Select(a => a.DiscountAmount).ToArray());
            Assert.All(allocated, a => Assert.Equal(5m, a.DiscountRate));
            Assert.Equal(1_100m, allocated.Sum(a => a.DiscountAmount));
        }

        [Fact]
        public void ManualFixed100_AllocatesProportionally_LastLineGetsRemainder()
        {
            var lines = CanonicalLines();
            decimal header = DiscountAllocationHelper.ComputeHeaderDiscount("FixedAmount", 100m, 22_000m);

            Assert.Equal(100m, header);

            var allocated = DiscountAllocationHelper.Allocate(lines, header, "FixedAmount", 100m);

            Assert.Equal(45.45m, allocated[0].DiscountAmount); // 100 * 10000/22000
            Assert.Equal(36.36m, allocated[1].DiscountAmount); // 100 * 8000/22000
            Assert.Equal(18.19m, allocated[2].DiscountAmount); // remainder so sum == 100
            Assert.All(allocated, a => Assert.Equal(0m, a.DiscountRate));
            Assert.Equal(100m, allocated.Sum(a => a.DiscountAmount));
        }

        [Fact]
        public void LastLineReceivesRemainder_SoSumEqualsHeaderDiscount()
        {
            var lines = new List<DiscountAllocationHelper.LineGross>
            {
                new() { Index = 0, Gross = 10m },
                new() { Index = 1, Gross = 10m },
                new() { Index = 2, Gross = 10m }
            };

            decimal header = DiscountAllocationHelper.ComputeHeaderDiscount("FixedAmount", 1.00m, 30m);
            var allocated = DiscountAllocationHelper.Allocate(lines, header, "FixedAmount", 1.00m);

            Assert.Equal(0.33m, allocated[0].DiscountAmount);
            Assert.Equal(0.33m, allocated[1].DiscountAmount);
            Assert.Equal(0.34m, allocated[2].DiscountAmount);
            Assert.Equal(header, allocated.Sum(a => a.DiscountAmount));
        }

        private static List<DiscountAllocationHelper.LineGross> CanonicalLines() =>
            new List<DiscountAllocationHelper.LineGross>
            {
                new() { Index = 0, Gross = 10_000m },
                new() { Index = 1, Gross = 8_000m },
                new() { Index = 2, Gross = 4_000m }
            };
    }
}
