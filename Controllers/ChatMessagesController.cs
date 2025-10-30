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
    public class ChatMessagesController : Controller
    {
        private readonly TripWiseContext _context;

        public ChatMessagesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: ChatMessages
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.ChatMessages.Include(c => c.IdPointNavigation).Include(c => c.IdTripNavigation).Include(c => c.IdUserNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: ChatMessages/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chatMessage = await _context.ChatMessages
                .Include(c => c.IdPointNavigation)
                .Include(c => c.IdTripNavigation)
                .Include(c => c.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdMessage == id);
            if (chatMessage == null)
            {
                return NotFound();
            }

            return View(chatMessage);
        }

        // GET: ChatMessages/Create
        public IActionResult Create()
        {
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint");
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser");
            return View();
        }

        // POST: ChatMessages/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdMessage,Message,SentAt,IdTrip,IdUser,IdPoint")] ChatMessage chatMessage)
        {
            if (ModelState.IsValid)
            {
                _context.Add(chatMessage);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", chatMessage.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", chatMessage.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", chatMessage.IdUser);
            return View(chatMessage);
        }

        // GET: ChatMessages/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chatMessage = await _context.ChatMessages.FindAsync(id);
            if (chatMessage == null)
            {
                return NotFound();
            }
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", chatMessage.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", chatMessage.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", chatMessage.IdUser);
            return View(chatMessage);
        }

        // POST: ChatMessages/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdMessage,Message,SentAt,IdTrip,IdUser,IdPoint")] ChatMessage chatMessage)
        {
            if (id != chatMessage.IdMessage)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chatMessage);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChatMessageExists(chatMessage.IdMessage))
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
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", chatMessage.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", chatMessage.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", chatMessage.IdUser);
            return View(chatMessage);
        }

        // GET: ChatMessages/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chatMessage = await _context.ChatMessages
                .Include(c => c.IdPointNavigation)
                .Include(c => c.IdTripNavigation)
                .Include(c => c.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdMessage == id);
            if (chatMessage == null)
            {
                return NotFound();
            }

            return View(chatMessage);
        }

        // POST: ChatMessages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var chatMessage = await _context.ChatMessages.FindAsync(id);
            if (chatMessage != null)
            {
                _context.ChatMessages.Remove(chatMessage);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChatMessageExists(int id)
        {
            return _context.ChatMessages.Any(e => e.IdMessage == id);
        }
    }
}
