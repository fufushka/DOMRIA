using System;
using System.Linq;
using System.Threading.Tasks;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;

namespace DomRia.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlatController : ControllerBase
    {
        private readonly IDomRiaService _domRiaService;

        public FlatController(IDomRiaService domRiaService)
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
                var results = await _domRiaService.SearchFlatsByCriteriaAsync(criteria);

                return Ok(results);
            }
            catch (Exception ex)
            {
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
                    return NotFound();

                return Ok(flat);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Не вдалося отримати дані квартири");
            }
        }
    }
}
