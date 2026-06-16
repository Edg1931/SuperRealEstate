using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperRealEstate.Insights;

namespace SuperRealEstate.Catalog
{
    /// <summary>
    /// Resolves a recognized surface finish to a real catalog product. The
    /// production implementation queries Supabase `materials` (and vendor
    /// catalogs) filtered by brand/category, then ranks with
    /// <see cref="FinishMatcher"/>.
    /// </summary>
    public interface IFinishCatalog
    {
        Task<FinishMatch?> ResolveAsync(SurfaceFinding finding, CancellationToken ct = default);
    }

    /// <summary>
    /// In-memory catalog over a fixed candidate list — usable for tests, the
    /// seeded finish catalog, or an offline cache. Real backends implement
    /// <see cref="IFinishCatalog"/> directly against the database.
    /// </summary>
    public sealed class InMemoryFinishCatalog : IFinishCatalog
    {
        private readonly IReadOnlyList<ProductCandidate> _candidates;

        public InMemoryFinishCatalog(IReadOnlyList<ProductCandidate> candidates)
        {
            _candidates = candidates ?? new List<ProductCandidate>();
        }

        public Task<FinishMatch?> ResolveAsync(SurfaceFinding finding, CancellationToken ct = default)
            => Task.FromResult(FinishMatcher.Best(finding, _candidates));
    }
}
