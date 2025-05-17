using System.Collections.Generic;
using DOMRIA.Helpers;
using Xunit;

namespace DomRiaBot.Tests
{
    public class SearchUrlBuilderTests
    {
        [Fact]
        public void BuildSearchUrl_GeneratesCorrectUrl_WithAllParameters()
        {
            var url = SearchUrlBuilder.BuildSearchUrl(
                apiKey: "test_key",
                roomCount: new List<int> { 1, 2 },
                districtId: new List<int> { 101, 102 },
                minPrice: 50000,
                maxPrice: 100000,
                sortBy: "price_up",
                page: 2,
                limit: 10,
                notFirstFloor: true,
                notLastFloor: true,
                onlyYeOselya: true
            );

            Assert.Contains("api_key=test_key", url);
            Assert.Contains("characteristic[209][]=1", url);
            Assert.Contains("characteristic[209][]=2", url);
            Assert.Contains("district_id[]=101", url);
            Assert.Contains("district_id[]=102", url);
            Assert.Contains("characteristic[234][from]=50000", url);
            Assert.Contains("characteristic[234][to]=100000", url);
            Assert.Contains("notFirstFloor=1", url);
            Assert.Contains("notLastFloor=1", url);
            Assert.Contains("characteristic[2001]=2001", url);
            Assert.Contains("sort=p_a", url);
            Assert.Contains("page=2", url);
            Assert.Contains("limit=10", url);
        }
    }
}
