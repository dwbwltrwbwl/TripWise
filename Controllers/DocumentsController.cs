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
    public class DocumentsController : Controller
    {
        private readonly TripWiseContext _context;

        public DocumentsController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: Documents
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.Documents.Include(d => d.IdTripNavigation).Include(d => d.UploadedBy);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: Documents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.Documents
                .Include(d => d.IdTripNavigation)
                .Include(d => d.UploadedBy)
                .FirstOrDefaultAsync(m => m.IdDocument == id);
            if (document == null)
            {
                return NotFound();
            }

            return View(document);
        }

        // GET: Documents/Create
        public IActionResult Create()
        {
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            ViewData["UploadedById"] = new SelectList(_context.Users, "IdUser", "IdUser");
            return View();
        }

        // POST: Documents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdDocument,FileName,FileType,FileSize,FilePath,Description,UploadedAt,IdTrip,UploadedById")] Document document)
        {
            if (ModelState.IsValid)
            {
                _context.Add(document);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", document.IdTrip);
            ViewData["UploadedById"] = new SelectList(_context.Users, "IdUser", "IdUser", document.UploadedById);
            return View(document);
        }

        // GET: Documents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", document.IdTrip);
            ViewData["UploadedById"] = new SelectList(_context.Users, "IdUser", "IdUser", document.UploadedById);
            return View(document);
        }

        // POST: Documents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdDocument,FileName,FileType,FileSize,FilePath,Description,UploadedAt,IdTrip,UploadedById")] Document document)
        {
            if (id != document.IdDocument)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(document);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DocumentExists(document.IdDocument))
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
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", document.IdTrip);
            ViewData["UploadedById"] = new SelectList(_context.Users, "IdUser", "IdUser", document.UploadedById);
            return View(document);
        }

        // GET: Documents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.Documents
                .Include(d => d.IdTripNavigation)
                .Include(d => d.UploadedBy)
                .FirstOrDefaultAsync(m => m.IdDocument == id);
            if (document == null)
            {
                return NotFound();
            }

            return View(document);
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document != null)
            {
                _context.Documents.Remove(document);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DocumentExists(int id)
        {
            return _context.Documents.Any(e => e.IdDocument == id);
        }
    }
}
