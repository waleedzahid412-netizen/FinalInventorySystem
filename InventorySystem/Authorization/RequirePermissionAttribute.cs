using System;
using InventorySystem.Constants;

namespace InventorySystem.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequirePermissionAttribute : Attribute
    {
        public RequirePermissionAttribute(string pageKey, PermissionAction action)
        {
            PageKey = pageKey;
            Action = action;
        }

        public string PageKey { get; }
        public PermissionAction Action { get; }
    }
}
