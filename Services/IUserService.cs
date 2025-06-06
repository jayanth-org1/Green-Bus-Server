using TransportBooking.Models;

namespace TransportBooking.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(int id);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> CreateUserAsync(User user);
        Task<User?> UpdateUserAsync(int id, User user);
        Task<bool> DeleteUserAsync(int id);
        
    }
} 