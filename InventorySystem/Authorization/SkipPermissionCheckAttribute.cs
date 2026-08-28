using System;

namespace InventorySystem.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SkipPermissionCheckAttribute : Attribute
    {
    }
}
