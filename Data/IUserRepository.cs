using TransportBooking.Models;

namespace TransportBooking.Data
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
    }
} 