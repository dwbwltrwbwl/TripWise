using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Controllers
{
    public class PointsOfInterestsController : Controller
    {
        private readonly TripWiseContext _context;

        public PointsOfInterestsController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: PointsOfInterests
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.PointsOfInterests.Include(p => p.AddedBy).Include(p => p.IdInterestCategoryNavigation).Include(p => p.IdTripNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: PointsOfInterests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointsOfInterest = await _context.PointsOfInterests
                .Include(p => p.AddedBy)
                .Include(p => p.IdInterestCategoryNavigation)
                .Include(p => p.IdTripNavigation)
                .FirstOrDefaultAsync(m => m.IdPoint == id);
            if (pointsOfInterest == null)
            {
                return NotFound();
            }

            return View(pointsOfInterest);
        }

        // GET: PointsOfInterests/Create
        public IActionResult Create()
        {
            ViewData["AddedById"] = new SelectList(_context.Users, "IdUser", "IdUser");
            ViewData["IdInterestCategory"] = new SelectList(_context.InterestCategories, "IdInterestCategory", "IdInterestCategory");
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            return View();
        }

        // POST: PointsOfInterests/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdPoint,Name,Description,Latitude,Longitude,Address,IdInterestCategory,PlannedDate,PlannedTime,Cost,BookingLink,Notes,IdTrip,AddedById")] PointsOfInterest pointsOfInterest)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pointsOfInterest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AddedById"] = new SelectList(_context.Users, "IdUser", "IdUser", pointsOfInterest.AddedById);
            ViewData["IdInterestCategory"] = new SelectList(_context.InterestCategories, "IdInterestCategory", "IdInterestCategory", pointsOfInterest.IdInterestCategory);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", pointsOfInterest.IdTrip);
            return View(pointsOfInterest);
        }

        // GET: PointsOfInterests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointsOfInterest = await _context.PointsOfInterests.FindAsync(id);
            if (pointsOfInterest == null)
            {
                return NotFound();
            }
            ViewData["AddedById"] = new SelectList(_context.Users, "IdUser", "IdUser", pointsOfInterest.AddedById);
            ViewData["IdInterestCategory"] = new SelectList(_context.InterestCategories, "IdInterestCategory", "IdInterestCategory", pointsOfInterest.IdInterestCategory);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", pointsOfInterest.IdTrip);
            return View(pointsOfInterest);
        }

        // POST: PointsOfInterests/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdPoint,Name,Description,Latitude,Longitude,Address,IdInterestCategory,PlannedDate,PlannedTime,Cost,BookingLink,Notes,IdTrip,AddedById")] PointsOfInterest pointsOfInterest)
        {
            if (id != pointsOfInterest.IdPoint)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pointsOfInterest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PointsOfInterestExists(pointsOfInterest.IdPoint))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AddedById"] = new SelectList(_context.Users, "IdUser", "IdUser", pointsOfInterest.AddedById);
            ViewData["IdInterestCategory"] = new SelectList(_context.InterestCategories, "IdInterestCategory", "IdInterestCategory", pointsOfInterest.IdInterestCategory);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", pointsOfInterest.IdTrip);
            return View(pointsOfInterest);
        }

        // GET: PointsOfInterests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointsOfInterest = await _context.PointsOfInterests
                .Include(p => p.AddedBy)
                .Include(p => p.IdInterestCategoryNavigation)
                .Include(p => p.IdTripNavigation)
                .FirstOrDefaultAsync(m => m.IdPoint == id);
            if (pointsOfInterest == null)
            {
                return NotFound();
            }

            return View(pointsOfInterest);
        }

        // POST: PointsOfInterests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var pointsOfInterest = await _context.PointsOfInterests.FindAsync(id);
            if (pointsOfInterest != null)
            {
                _context.PointsOfInterests.Remove(pointsOfInterest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PointsOfInterestExists(int id)
        {
            return _context.PointsOfInterests.Any(e => e.IdPoint == id);
        }
    }
}
