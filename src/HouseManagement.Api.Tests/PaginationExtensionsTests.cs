using HouseManagement.Api.Common;

namespace HouseManagement.Api.Tests;

public sealed class PaginationExtensionsTests
{
    [Fact]
    public void ApplyPagination_UsesDefaultPageSizeAndBoundsRequestedPageSize()
    {
        var items = Enumerable.Range(1, 150).AsQueryable();

        var defaultPage = items.ApplyPagination(null, null).ToList();
        var boundedPage = items.ApplyPagination(1, 500).ToList();
        var secondPage = items.ApplyPagination(2, 50).ToList();

        Assert.Equal(PaginationExtensions.DefaultPageSize, defaultPage.Count);
        Assert.Equal(PaginationExtensions.MaximumPageSize, boundedPage.Count);
        Assert.Equal(51, secondPage.First());
    }
}
