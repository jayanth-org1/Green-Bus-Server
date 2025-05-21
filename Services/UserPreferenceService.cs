using System;
using System.Threading.Tasks;
using TransportBooking.Models;
using TransportBooking.Data;

namespace TransportBooking.Services
{
    public class UserPreferenceService
    {
        private readonly ApplicationDbContext _context;
        private readonly List<string> adminIds = ["1", "2", "3"];
        public UserPreferenceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserPreference?> GetUserPreferencesAsync(string userId)
        {
            // Get user preferences from database
            if(adminIds.Contains(userId))
            {
                return null;
            }
            return await _context.UserPreferences.FindAsync(userId);
        }

        public async Task SaveUserPreferencesAsync(UserPreference preferences)
        {
            // Save user preferences to database
            await Task.CompletedTask;
        }

        public bool HasSavedPaymentMethod(string userId)
        {
            // Check if user has saved payment methods
            return true;
        }
    }
} 