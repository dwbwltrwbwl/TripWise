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
    public class VoteOptionsController : Controller
    {
        private readonly TripWiseContext _context;

        public VoteOptionsController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: VoteOptions
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.VoteOptions.Include(v => v.IdVoteNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: VoteOptions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voteOption = await _context.VoteOptions
                .Include(v => v.IdVoteNavigation)
                .FirstOrDefaultAsync(m => m.IdVoteOption == id);
            if (voteOption == null)
            {
                return NotFound();
            }

            return View(voteOption);
        }

        // GET: VoteOptions/Create
        public IActionResult Create()
        {
            ViewData["IdVote"] = new SelectList(_context.VotingSystems, "IdVote", "IdVote");
            return View();
        }

        // POST: VoteOptions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdVoteOption,OptionText,IdVote")] VoteOption voteOption)
        {
            if (ModelState.IsValid)
            {
                _context.Add(voteOption);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdVote"] = new SelectList(_context.VotingSystems, "IdVote", "IdVote", voteOption.IdVote);
            return View(voteOption);
        }

        // GET: VoteOptions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voteOption = await _context.VoteOptions.FindAsync(id);
            if (voteOption == null)
            {
                return NotFound();
            }
            ViewData["IdVote"] = new SelectList(_context.VotingSystems, "IdVote", "IdVote", voteOption.IdVote);
            return View(voteOption);
        }

        // POST: VoteOptions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdVoteOption,OptionText,IdVote")] VoteOption voteOption)
        {
            if (id != voteOption.IdVoteOption)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(voteOption);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VoteOptionExists(voteOption.IdVoteOption))
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
            ViewData["IdVote"] = new SelectList(_context.VotingSystems, "IdVote", "IdVote", voteOption.IdVote);
            return View(voteOption);
        }

        // GET: VoteOptions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voteOption = await _context.VoteOptions
                .Include(v => v.IdVoteNavigation)
                .FirstOrDefaultAsync(m => m.IdVoteOption == id);
            if (voteOption == null)
            {
                return NotFound();
            }

            return View(voteOption);
        }

        // POST: VoteOptions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var voteOption = await _context.VoteOptions.FindAsync(id);
            if (voteOption != null)
            {
                _context.VoteOptions.Remove(voteOption);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VoteOptionExists(int id)
        {
            return _context.VoteOptions.Any(e => e.IdVoteOption == id);
        }
    }
}
