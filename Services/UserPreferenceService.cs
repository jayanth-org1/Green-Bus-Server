using System;
using System.Threading.Tasks;
using TransportBooking.Models;
using TransportBooking.Data;

namespace TransportBooking.Services
{
    public class UserPreferenceService
    {
        private readonly ApplicationDbContext _context;

        public UserPreferenceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserPreference> GetUserPreferencesAsync(string userId)
        {
            // Get user preferences from database
            return await Task.FromResult(new UserPreference());
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