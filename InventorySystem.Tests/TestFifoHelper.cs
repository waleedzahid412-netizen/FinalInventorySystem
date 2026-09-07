using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Tests
{
    internal static class TestFifoHelper
    {
        public static IFifoCostingService CreateFifo(ApplicationDbContext context) =>
            new FifoCostingService(context, NullLogger<FifoCostingService>.Instance);
    }
}
