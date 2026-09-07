namespace InventorySystem.Helpers
{
    public static class UserFacingErrorMessages
    {
        public const string Unexpected = "An unexpected error occurred. Please try again or contact support.";

        public const string LoginFailed = "Unable to sign in right now. Please try again.";

        public const string InvalidCredentials = "Invalid username or password.";

        public const string AccountLocked =
            "Account temporarily locked due to too many failed sign-in attempts. Please try again later.";

        public const string LoginRateLimited =
            "Too many sign-in attempts. Please wait a moment and try again.";

        public const string RecordCustomerPaymentFailed = "Unable to record customer payment. Please try again.";
        public const string RecordCompanyPaymentFailed = "Unable to record company payment. Please try again.";

        public const string CategoryCreateFailed = "Unable to create category. Please try again.";
        public const string CategoryUpdateFailed = "Unable to update category. Please try again.";
        public const string CategoryDeleteFailed = "Unable to delete category. Please try again.";

        public const string UnitCreateFailed = "Unable to create unit. Please try again.";
        public const string UnitUpdateFailed = "Unable to update unit. Please try again.";
        public const string UnitDeleteFailed = "Unable to delete unit. Please try again.";

        public const string WarehouseCreateFailed = "Unable to create warehouse. Please try again.";
        public const string WarehouseUpdateFailed = "Unable to update warehouse. Please try again.";
        public const string WarehouseDeleteFailed = "Unable to delete warehouse. Please try again.";

        public const string CompanyCreateFailed = "Unable to create company. Please try again.";
        public const string CompanyUpdateFailed = "Unable to update company. Please try again.";
        public const string CompanyDeleteFailed = "Unable to delete company. Please try again.";

        public const string ProductCreateFailed = "Unable to create product. Please try again.";
        public const string ProductUpdateFailed = "Unable to update product. Please try again.";
        public const string ProductDeleteFailed = "Unable to delete product. Please try again.";

        public const string RoleCreateFailed = "Unable to create role. Please try again.";
        public const string RoleUpdateFailed = "Unable to update role. Please try again.";

        public const string PurchaseFinalizeFailed = "Unable to finalize purchase invoice. Please try again.";
        public const string PurchaseUpdateFailed = "Unable to update purchase invoice. Please try again.";
        public const string PurchaseEditMissingCostLayer = "Cannot edit purchase: no FIFO cost layer found for a purchase line.";
        public const string PurchaseEditInsufficientStock =
            "Insufficient warehouse stock to reverse purchase line for '{0}'. Available: {1:N3}, Required: {2:N3}.";

        public static string PurchaseEditBatchAlreadySold(decimal soldBaseUnits) =>
            $"Cannot edit purchase: {soldBaseUnits:N3} base units from this batch were already sold. Process a sales return or cancel the related sales invoice first.";

        public const string SalesFinalizeFailed = "Unable to finalize sales invoice. Please try again.";
        public const string SalesUpdateFailed = "Unable to update sales invoice. Please try again.";

        public const string SalesReturnProcessFailed = "Unable to process sales return. Please try again.";
        public const string ManualSalesReturnProcessFailed = "Unable to process manual sales return. Please try again.";
        public const string PurchaseReturnProcessFailed = "Unable to process purchase return. Please try again.";

        public const string ClearChequeFailed = "Unable to clear cheque. Please try again.";
        public const string BounceChequeFailed = "Unable to mark cheque as bounced. Please try again.";
        public const string CancelChequeFailed = "Unable to cancel cheque. Please try again.";
        public const string ClearVendorChequeFailed = "Unable to clear vendor cheque. Please try again.";
        public const string BounceVendorChequeFailed = "Unable to mark vendor cheque as bounced. Please try again.";
        public const string CancelVendorChequeFailed = "Unable to cancel vendor cheque. Please try again.";
    }
}
