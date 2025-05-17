using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/user")]
public class UserStateController : ControllerBase
{
    private readonly IUserStateService _repo;

    public UserStateController(IUserStateService repo)
    {
        _repo = repo;
    }

    [HttpGet("all")]
    public async Task<ActionResult<List<UserSearchState>>> GetAll()
    {
        try
        {
            var users = await _repo.GetAllAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetAll error: {ex.Message}");
            return StatusCode(500, "Помилка при отриманні користувачів");
        }
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<UserSearchState>> GetUser(long userId)
    {
        try
        {
            var user = await _repo.GetAsync(userId);
            return user is null ? NotFound("Користувача не знайдено") : Ok(user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetUser error: {ex.Message}");
            return StatusCode(500, "Помилка при отриманні користувача");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] UserSearchState state)
    {
        if (state == null)
        {
            Console.WriteLine("❌ BadRequest: state is null");
            return BadRequest("Модель не може бути null");
        }

        if (state.UserId == 0)
        {
            Console.WriteLine("❌ BadRequest: UserId == 0");
            return BadRequest("UserId обовʼязковий");
        }

        if (!ModelState.IsValid)
        {
            var errors = string.Join(
                " | ",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            );

            Console.WriteLine($"❌ ModelState invalid: {errors}");
            return BadRequest("Невалідна модель: " + errors);
        }

        try
        {
            var success = await _repo.SaveAsync(state);
            if (!success)
                return StatusCode(500, "Не вдалося зберегти користувача");

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreateUser error: {ex.Message}");
            return StatusCode(500, "Помилка при збереженні користувача");
        }
    }

    [HttpPut("{userId}/favorites")]
    public async Task<IActionResult> UpdateFavorites(long userId, [FromBody] List<int> favoriteIds)
    {
        try
        {
            var user = await _repo.GetAsync(userId);
            if (user is null)
                return NotFound("Користувача не знайдено");

            user.FavoriteFlatIds = favoriteIds ?? new List<int>();

            var success = await _repo.SaveAsync(user);
            if (!success)
                return StatusCode(500, "Не вдалося оновити улюблені");

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpdateFavorites error: {ex.Message}");
            return StatusCode(500, "Помилка при оновленні обраного");
        }
    }

    //[HttpDelete("{userId}")]
    //public async Task<IActionResult> DeleteUser(long userId)
    //{
    //    try
    //    {
    //        var success = await _repo.DeleteAsync(userId);
    //        if (!success)
    //            return StatusCode(500, "Не вдалося видалити користувача");

    //        return NoContent();
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"❌ DeleteUser error: {ex.Message}");
    //        return StatusCode(500, "Помилка при видаленні користувача");
    //    }
    //}
}
