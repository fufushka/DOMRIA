using System;
using System.Linq;
using System.Threading.Tasks;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;

namespace DomRia.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlatController : ControllerBase
    {
        private readonly DomRiaService _domRiaService;

        public FlatController(DomRiaService domRiaService)
        {
            _domRiaService = domRiaService;
        }

        [HttpGet("districts")]
        public async Task<IActionResult> GetDistricts()
        {
            try
            {
                var districts = await _domRiaService.GetDistrictsAsync();
                return Ok(districts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" GetDistricts error: {ex.Message}");
                return StatusCode(500, "Не вдалося отримати список районів");
            }
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchFlats([FromBody] UserSearchState criteria)
        {
            try
            {
                var districtIds = criteria.Districts.Select(d => d.Id).ToList();
                var results = await _domRiaService.SearchFlatsByCriteriaAsync(
                    criteria.RoomCountOptions,
                    districtIds,
                    criteria.MaxPrice ?? 0,
                    criteria.MinPrice ?? 0,
                    criteria.SortBy,
                    0,
                    5,
                    criteria.NotFirstFloor,
                    criteria.NotLastFloor,
                    criteria.OnlyYeOselya
                );

                return Ok(results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" SearchFlats error: {ex.Message}");
                return StatusCode(500, "Не вдалося здійснити пошук квартир");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFlatById(int id)
        {
            try
            {
                var flat = await _domRiaService.GetFlatByIdAsync(id);
                if (flat == null)
                    return NotFound("Квартиру не знайдено");

                return Ok(flat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" GetFlatById error: {ex.Message}");
                return StatusCode(500, "Не вдалося отримати дані квартири");
            }
        }
    }
}
