using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TripWise.Models;
using TripWise.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlannedActivitiesController : ControllerBase
    {
        private readonly TripWiseContext _context;

        public PlannedActivitiesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: api/PlannedActivities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlannedActivity>>> GetUserActivities()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            return await _context.PlannedActivities
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.Time)
                .ToListAsync();
        }

        // GET: api/PlannedActivities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PlannedActivity>> GetActivity(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .FirstOrDefaultAsync();

            if (activity == null)
            {
                return NotFound();
            }

            return activity;
        }

        // POST: api/PlannedActivities
        [HttpPost]
        public async Task<ActionResult<PlannedActivity>> CreateActivity([FromBody] PlannedActivityDto activityDto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activity = new PlannedActivity
            {
                UserId = userId.Value,
                ActivityId = activityDto.ActivityId,
                Name = activityDto.Name,
                Date = activityDto.Date,
                Time = activityDto.Time,
                Description = activityDto.Description,
                Category = activityDto.Category,
                Tags = activityDto.Tags,
                Latitude = activityDto.Latitude,
                Longitude = activityDto.Longitude,
                Address = activityDto.Address,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlannedActivities.Add(activity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, activity);
        }

        // PUT: api/PlannedActivities/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateActivity(int id, [FromBody] PlannedActivityDto activityDto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var existingActivity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .FirstOrDefaultAsync();

            if (existingActivity == null)
            {
                return NotFound();
            }

            existingActivity.Name = activityDto.Name;
            existingActivity.Date = activityDto.Date;
            existingActivity.Time = activityDto.Time;
            existingActivity.Description = activityDto.Description;
            existingActivity.Category = activityDto.Category;
            existingActivity.Tags = activityDto.Tags;
            existingActivity.Latitude = activityDto.Latitude;
            existingActivity.Longitude = activityDto.Longitude;
            existingActivity.Address = activityDto.Address;

            _context.Entry(existingActivity).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/PlannedActivities/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteActivity(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .FirstOrDefaultAsync();

            if (activity == null)
            {
                return NotFound();
            }

            _context.PlannedActivities.Remove(activity);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/PlannedActivities/clear
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAllActivities()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activities = await _context.PlannedActivities
                .Where(a => a.UserId == userId)
                .ToListAsync();

            _context.PlannedActivities.RemoveRange(activities);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}