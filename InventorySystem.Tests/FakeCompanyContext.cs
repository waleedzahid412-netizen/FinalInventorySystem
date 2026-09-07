using System;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Tests
{
    /// <summary>Test stub for ambient company scope (cookie-backed in production).</summary>
    public sealed class FakeCompanyContext : ICompanyContext
    {
        private readonly bool _resolveSpecific;
        private readonly bool _allCompanies;
        private readonly int _companyId;
        private readonly string _companyName;

        public FakeCompanyContext(int companyId, string companyName, bool resolve = true)
        {
            _companyId = companyId;
            _companyName = companyName;
            _resolveSpecific = resolve;
            _allCompanies = false;
            HasCompany = resolve;
            IsAllCompanies = false;
            HasResolvedScope = resolve;
        }

        private FakeCompanyContext(bool allCompanies)
        {
            _companyId = 0;
            _companyName = "All Companies";
            _resolveSpecific = false;
            _allCompanies = allCompanies;
            HasCompany = false;
            IsAllCompanies = allCompanies;
            HasResolvedScope = allCompanies;
        }

        /// <summary>Ambient All Companies scope (cookie sentinel).</summary>
        public static FakeCompanyContext AllCompanies() => new(allCompanies: true);

        /// <summary>No cookie / unscoped.</summary>
        public static FakeCompanyContext Unscoped() => new(companyId: 0, companyName: "", resolve: false);

        public bool HasCompany { get; private set; }
        public bool IsAllCompanies { get; private set; }
        public bool HasResolvedScope { get; private set; }

        public int CompanyID =>
            HasCompany
                ? _companyId
                : throw new InvalidOperationException("No specific company is selected.");

        public string CompanyName =>
            HasCompany
                ? _companyName
                : throw new InvalidOperationException("No specific company is selected.");

        public Task<bool> TryResolveAsync(CancellationToken cancellationToken = default)
        {
            if (_allCompanies)
            {
                HasCompany = false;
                IsAllCompanies = true;
                HasResolvedScope = true;
                return Task.FromResult(true);
            }

            HasCompany = _resolveSpecific;
            IsAllCompanies = false;
            HasResolvedScope = _resolveSpecific;
            return Task.FromResult(_resolveSpecific);
        }

        public void ReapplyFromHttpContext()
        {
        }
    }
}
