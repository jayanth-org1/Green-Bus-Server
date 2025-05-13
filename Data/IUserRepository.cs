using System.Collections.Generic;
using System.Threading.Tasks;
using TransportBooking.Models;

namespace TransportBooking.Data
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> AddAsync(User user);
        Task<IEnumerable<User>> AddRangeAsync(IEnumerable<User> users);
        Task<User?> GetByIdAsync(int id);
    }
} 