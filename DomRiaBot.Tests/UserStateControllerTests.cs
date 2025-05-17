using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DomRia.Controllers;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DOMRIA.Tests
{
    public class UserStateControllerTests
    {
        private readonly Mock<IUserStateService> _mockService;
        private readonly UserStateController _controller;

        public UserStateControllerTests()
        {
            _mockService = new Mock<IUserStateService>();
            _controller = new UserStateController(_mockService.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsListOfUsers()
        {
            var users = new List<UserSearchState> { new UserSearchState { UserId = 1 } };
            _mockService.Setup(s => s.GetAllAsync()).ReturnsAsync(users);

            var result = await _controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var value = Assert.IsAssignableFrom<List<UserSearchState>>(okResult.Value);
            Assert.Single(value);
        }

        [Fact]
        public async Task GetAll_Returns500_OnException()
        {
            _mockService.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception("DB error"));

            var result = await _controller.GetAll();

            var error = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Помилка при отриманні користувачів", error.Value);
        }

        [Fact]
        public async Task GetUser_ReturnsUser_WhenFound()
        {
            var user = new UserSearchState { UserId = 123 };
            _mockService.Setup(s => s.GetAsync(123)).ReturnsAsync(user);

            var result = await _controller.GetUser(123);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<UserSearchState>(okResult.Value);
            Assert.Equal(123, returnValue.UserId);
        }

        [Fact]
        public async Task GetUser_ReturnsNotFound_WhenUserIsNull()
        {
            _mockService.Setup(s => s.GetAsync(123)).ReturnsAsync((UserSearchState)null!);

            var result = await _controller.GetUser(123);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Користувача не знайдено", notFound.Value);
        }

        [Fact]
        public async Task GetUser_Returns500_OnException()
        {
            _mockService.Setup(s => s.GetAsync(123)).ThrowsAsync(new Exception("DB error"));

            var result = await _controller.GetUser(123);

            var error = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Помилка при отриманні користувача", error.Value);
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenStateIsNull()
        {
            var result = await _controller.CreateUser(null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Модель не може бути null", badRequest.Value);
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenUserIdIsZero()
        {
            var state = new UserSearchState { UserId = 0 };
            var result = await _controller.CreateUser(state);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("UserId обовʼязковий", badRequest.Value);
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            var state = new UserSearchState { UserId = 123 };
            _controller.ModelState.AddModelError("Step", "Step is required");

            var result = await _controller.CreateUser(state);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Невалідна модель", badRequest.Value.ToString());
        }

        [Fact]
        public async Task CreateUser_Returns500_WhenSaveFails()
        {
            var state = new UserSearchState { UserId = 123 };
            _mockService.Setup(s => s.SaveAsync(state)).ReturnsAsync(false);

            var result = await _controller.CreateUser(state);

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Не вдалося зберегти користувача", error.Value);
        }

        [Fact]
        public async Task CreateUser_Returns500_OnException()
        {
            var state = new UserSearchState { UserId = 123 };
            _mockService.Setup(s => s.SaveAsync(state)).ThrowsAsync(new Exception("Error"));

            var result = await _controller.CreateUser(state);

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Помилка при збереженні користувача", error.Value);
        }

        [Fact]
        public async Task CreateUser_ReturnsOk_WhenValid()
        {
            var state = new UserSearchState { UserId = 123 };
            _mockService.Setup(s => s.SaveAsync(state)).ReturnsAsync(true);

            var result = await _controller.CreateUser(state);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UpdateFavorites_ReturnsOk_WhenSuccess()
        {
            var user = new UserSearchState { UserId = 123, FavoriteFlatIds = new List<int>() };
            var favs = new List<int> { 10, 20 };

            _mockService.Setup(s => s.GetAsync(123)).ReturnsAsync(user);
            _mockService.Setup(s => s.SaveAsync(user)).ReturnsAsync(true);

            var result = await _controller.UpdateFavorites(123, favs);

            Assert.IsType<OkResult>(result);
            Assert.Equal(favs, user.FavoriteFlatIds);
        }

        [Fact]
        public async Task UpdateFavorites_ReturnsNotFound_WhenUserIsNull()
        {
            _mockService.Setup(s => s.GetAsync(123)).ReturnsAsync((UserSearchState)null!);

            var result = await _controller.UpdateFavorites(123, new List<int> { 1 });

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Користувача не знайдено", notFound.Value);
        }

        [Fact]
        public async Task UpdateFavorites_Returns500_WhenSaveFails()
        {
            var user = new UserSearchState { UserId = 123 };
            _mockService.Setup(s => s.GetAsync(123)).ReturnsAsync(user);
            _mockService.Setup(s => s.SaveAsync(user)).ReturnsAsync(false);

            var result = await _controller.UpdateFavorites(123, new List<int> { 1 });

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Не вдалося оновити улюблені", error.Value);
        }

        [Fact]
        public async Task UpdateFavorites_Returns500_OnException()
        {
            _mockService.Setup(s => s.GetAsync(123)).ThrowsAsync(new Exception("fail"));

            var result = await _controller.UpdateFavorites(123, new List<int> { 1 });

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            Assert.Equal("Помилка при оновленні обраного", error.Value);
        }
    }
}
