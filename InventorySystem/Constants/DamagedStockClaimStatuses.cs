namespace InventorySystem.Constants
{
    public static class DamagedStockClaimStatuses
    {
        public const string PendingSupplierConfirmation = "PendingSupplierConfirmation";
        public const string PartiallyAccepted = "PartiallyAccepted";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Closed = "Closed";
    }

    public static class DamagedStockSettlementStatuses
    {
        public const string NotRequired = "NotRequired";
        public const string Pending = "Pending";
        public const string PartiallySettled = "PartiallySettled";
        public const string Settled = "Settled";
    }

    public static class DamagedStockRejectedDecisions
    {
        public const string None = "None";
        public const string ReturnToDamaged = "ReturnToDamaged";
        public const string WriteOff = "WriteOff";
        public const string KeepPending = "KeepPending";
    }

    public static class DamagedStockSettlementMethods
    {
        public const string SameProductReplacement = "SameProductReplacement";
        public const string DifferentProductReplacement = "DifferentProductReplacement";
        public const string LedgerAdjustment = "LedgerAdjustment";
        public const string CashRefund = "CashRefund";
    }

    public static class DamagedStockInventoryTxTypes
    {
        public const string ClaimPending = "CLAIM_PENDING";
        public const string ClaimAcceptedClear = "CLAIM_ACCEPTED_CLEAR";
        public const string ClaimReturnDamaged = "CLAIM_RETURN_DAMAGED";
        public const string ClaimWriteOff = "CLAIM_WRITE_OFF";
        public const string ClaimReplacement = "CLAIM_REPLACEMENT";
    }
}
