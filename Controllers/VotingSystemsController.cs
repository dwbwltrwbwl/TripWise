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
    public class VotingSystemsController : Controller
    {
        private readonly TripWiseContext _context;

        public VotingSystemsController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: VotingSystems
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.VotingSystems.Include(v => v.CreatedBy).Include(v => v.IdPointNavigation).Include(v => v.IdTripNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: VotingSystems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var votingSystem = await _context.VotingSystems
                .Include(v => v.CreatedBy)
                .Include(v => v.IdPointNavigation)
                .Include(v => v.IdTripNavigation)
                .FirstOrDefaultAsync(m => m.IdVote == id);
            if (votingSystem == null)
            {
                return NotFound();
            }

            return View(votingSystem);
        }

        // GET: VotingSystems/Create
        public IActionResult Create()
        {
            ViewData["CreatedById"] = new SelectList(_context.Users, "IdUser", "IdUser");
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint");
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            return View();
        }

        // POST: VotingSystems/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdVote,Question,CreatedAt,ExpiresAt,IdTrip,CreatedById,IdPoint")] VotingSystem votingSystem)
        {
            if (ModelState.IsValid)
            {
                _context.Add(votingSystem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CreatedById"] = new SelectList(_context.Users, "IdUser", "IdUser", votingSystem.CreatedById);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", votingSystem.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", votingSystem.IdTrip);
            return View(votingSystem);
        }

        // GET: VotingSystems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var votingSystem = await _context.VotingSystems.FindAsync(id);
            if (votingSystem == null)
            {
                return NotFound();
            }
            ViewData["CreatedById"] = new SelectList(_context.Users, "IdUser", "IdUser", votingSystem.CreatedById);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", votingSystem.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", votingSystem.IdTrip);
            return View(votingSystem);
        }

        // POST: VotingSystems/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdVote,Question,CreatedAt,ExpiresAt,IdTrip,CreatedById,IdPoint")] VotingSystem votingSystem)
        {
            if (id != votingSystem.IdVote)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(votingSystem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VotingSystemExists(votingSystem.IdVote))
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
            ViewData["CreatedById"] = new SelectList(_context.Users, "IdUser", "IdUser", votingSystem.CreatedById);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", votingSystem.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", votingSystem.IdTrip);
            return View(votingSystem);
        }

        // GET: VotingSystems/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var votingSystem = await _context.VotingSystems
                .Include(v => v.CreatedBy)
                .Include(v => v.IdPointNavigation)
                .Include(v => v.IdTripNavigation)
                .FirstOrDefaultAsync(m => m.IdVote == id);
            if (votingSystem == null)
            {
                return NotFound();
            }

            return View(votingSystem);
        }

        // POST: VotingSystems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var votingSystem = await _context.VotingSystems.FindAsync(id);
            if (votingSystem != null)
            {
                _context.VotingSystems.Remove(votingSystem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VotingSystemExists(int id)
        {
            return _context.VotingSystems.Any(e => e.IdVote == id);
        }
    }
}
