using System.Collections.Generic;
using System.Threading.Tasks;
using DomRia.Controllers;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DOMRIA.Tests
{
    public class FlatControllerTests
    {
        private readonly Mock<IDomRiaService> _mockService;
        private readonly FlatController _controller;

        public FlatControllerTests()
        {
            _mockService = new Mock<IDomRiaService>();
            _controller = new FlatController(_mockService.Object);
        }

        [Fact]
        public async Task GetDistricts_ReturnsOkResultWithDistricts()
        {
            var districts = new List<DistrictDto>
            {
                new DistrictDto { Id = 1, Name = "Test" },
            };
            _mockService.Setup(s => s.GetDistrictsAsync()).ReturnsAsync(districts);

            var result = await _controller.GetDistricts();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsAssignableFrom<List<DistrictDto>>(okResult.Value);
            Assert.Single(value);
        }

        [Fact]
        public async Task GetDistricts_ReturnsOkResultWhenNull()
        {
            _mockService.Setup(s => s.GetDistrictsAsync()).ReturnsAsync((List<DistrictDto>)null!);

            var result = await _controller.GetDistricts();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(okResult.Value);
        }

        [Fact]
        public async Task GetFlatById_ReturnsOk_WhenFlatExists()
        {
            var flat = new FlatResult { FlatId = 123, Title = "Test Flat" };
            _mockService.Setup(s => s.GetFlatByIdAsync(123)).ReturnsAsync(flat);

            var result = await _controller.GetFlatById(123);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<FlatResult>(okResult.Value);
            Assert.Equal(123, returnValue.FlatId);
        }

        [Fact]
        public async Task GetFlatById_ReturnsNotFound_WhenFlatIsNull()
        {
            _mockService.Setup(s => s.GetFlatByIdAsync(999)).ReturnsAsync((FlatResult)null!);

            var result = await _controller.GetFlatById(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task SearchFlats_ReturnsOkResultWithItems()
        {
            var state = new UserSearchState();
            var response = new FlatSearchResponse
            {
                count = 2,
                items = new List<int> { 1, 2 },
            };

            _mockService.Setup(s => s.SearchFlatsByCriteriaAsync(state)).ReturnsAsync(response);

            var result = await _controller.SearchFlats(state);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<FlatSearchResponse>(okResult.Value);
            Assert.Equal(2, returnValue.count);
        }

        [Fact]
        public async Task SearchFlats_ReturnsEmptyResult_WhenNoItems()
        {
            var state = new UserSearchState();
            var response = new FlatSearchResponse { count = 0, items = new List<int>() };

            _mockService.Setup(s => s.SearchFlatsByCriteriaAsync(state)).ReturnsAsync(response);

            var result = await _controller.SearchFlats(state);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<FlatSearchResponse>(okResult.Value);
            Assert.Empty(returnValue.items);
        }

        [Fact]
        public async Task SearchFlats_ReturnsNullResult_WhenServiceReturnsNull()
        {
            var state = new UserSearchState();

            _mockService
                .Setup(s => s.SearchFlatsByCriteriaAsync(state))
                .ReturnsAsync((FlatSearchResponse)null!);

            var result = await _controller.SearchFlats(state);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(okResult.Value);
        }
    }
}
