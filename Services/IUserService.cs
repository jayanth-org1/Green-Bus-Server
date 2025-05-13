using System.Collections.Generic;
using System.Threading.Tasks;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(int id);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> CreateUserAsync(User user);
        Task<IEnumerable<User>> CreateUsersAsync(IEnumerable<User> users);
        Task<User?> UpdateUserAsync(int id, User user);
        Task<bool> DeleteUserAsync(int id);
        
    }
} 