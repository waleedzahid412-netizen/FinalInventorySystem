namespace InventorySystem.Services.Interfaces
{
    /// <summary>
    /// Ambient company scope for the current HTTP request (cookie <c>wims_company</c>).
    /// CompanyID must never be taken from form posts on hard-scoped screens — always from this context.
    /// </summary>
    /// <remarks>
    /// Cookie values: positive integer = specific company; <c>all</c> = All Companies mode;
    /// missing/invalid = unscoped (hard modules redirect to Select).
    /// The cookie is client-editable. Harmless today because every user is Admin and can switch
    /// to any company via the UI. If per-user company permissions are added, sign the cookie
    /// (<see cref="Microsoft.AspNetCore.DataProtection.IDataProtectionProvider"/>) and validate
    /// against that user's allowed companies.
    /// </remarks>
    public interface ICompanyContext
    {
        /// <summary>True when a specific company is selected (not All Companies, not unscoped).</summary>
        bool HasCompany { get; }

        /// <summary>True when ambient scope is deliberate All Companies (cookie sentinel <c>all</c>).</summary>
        bool IsAllCompanies { get; }

        /// <summary>True when either a specific company or All Companies is resolved (not unscoped).</summary>
        bool HasResolvedScope { get; }

        /// <summary>Throws <see cref="System.InvalidOperationException"/> if no specific company is selected.</summary>
        int CompanyID { get; }

        /// <summary>Throws <see cref="System.InvalidOperationException"/> if no specific company is selected.</summary>
        string CompanyName { get; }

        /// <summary>
        /// Resolves the cookie for this request. Returns true when <see cref="HasResolvedScope"/>
        /// (specific company or All Companies). Returns false when unscoped.
        /// </summary>
        Task<bool> TryResolveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-applies scope from <see cref="HttpContext.Items"/> after it was seeded mid-request.
        /// </summary>
        void ReapplyFromHttpContext();
    }
}
