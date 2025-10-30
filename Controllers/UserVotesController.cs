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
    public class UserVotesController : Controller
    {
        private readonly TripWiseContext _context;

        public UserVotesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: UserVotes
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.UserVotes.Include(u => u.IdUserNavigation).Include(u => u.IdVoteOptionNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: UserVotes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userVote = await _context.UserVotes
                .Include(u => u.IdUserNavigation)
                .Include(u => u.IdVoteOptionNavigation)
                .FirstOrDefaultAsync(m => m.IdUserVote == id);
            if (userVote == null)
            {
                return NotFound();
            }

            return View(userVote);
        }

        // GET: UserVotes/Create
        public IActionResult Create()
        {
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser");
            ViewData["IdVoteOption"] = new SelectList(_context.VoteOptions, "IdVoteOption", "IdVoteOption");
            return View();
        }

        // POST: UserVotes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdUserVote,IdVoteOption,IdUser,VotedAt")] UserVote userVote)
        {
            if (ModelState.IsValid)
            {
                _context.Add(userVote);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", userVote.IdUser);
            ViewData["IdVoteOption"] = new SelectList(_context.VoteOptions, "IdVoteOption", "IdVoteOption", userVote.IdVoteOption);
            return View(userVote);
        }

        // GET: UserVotes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userVote = await _context.UserVotes.FindAsync(id);
            if (userVote == null)
            {
                return NotFound();
            }
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", userVote.IdUser);
            ViewData["IdVoteOption"] = new SelectList(_context.VoteOptions, "IdVoteOption", "IdVoteOption", userVote.IdVoteOption);
            return View(userVote);
        }

        // POST: UserVotes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdUserVote,IdVoteOption,IdUser,VotedAt")] UserVote userVote)
        {
            if (id != userVote.IdUserVote)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(userVote);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserVoteExists(userVote.IdUserVote))
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
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", userVote.IdUser);
            ViewData["IdVoteOption"] = new SelectList(_context.VoteOptions, "IdVoteOption", "IdVoteOption", userVote.IdVoteOption);
            return View(userVote);
        }

        // GET: UserVotes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userVote = await _context.UserVotes
                .Include(u => u.IdUserNavigation)
                .Include(u => u.IdVoteOptionNavigation)
                .FirstOrDefaultAsync(m => m.IdUserVote == id);
            if (userVote == null)
            {
                return NotFound();
            }

            return View(userVote);
        }

        // POST: UserVotes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userVote = await _context.UserVotes.FindAsync(id);
            if (userVote != null)
            {
                _context.UserVotes.Remove(userVote);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserVoteExists(int id)
        {
            return _context.UserVotes.Any(e => e.IdUserVote == id);
        }
    }
}
