using System;
using System.Threading.Tasks;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class PaymentProcessor
    {
        // Process a payment for a booking
        public async Task<bool> ProcessPayment(decimal amount, string paymentMethod, string currency = "USD")
        {
            // In a real application, this would integrate with a payment gateway
            await Task.Delay(500); // Simulate API call to payment processor
            
            Console.WriteLine($"Processing payment of {amount} {currency} via {paymentMethod}");
            
            // Simulate payment success (in real app, would return result from payment gateway)
            return amount > 0 && !string.IsNullOrEmpty(paymentMethod);
        }
        
        // Validate payment details before processing
        public bool ValidatePaymentDetails(string cardNumber, string expiryDate, string cvv)
        {
            // Basic validation logic
            bool isCardNumberValid = !string.IsNullOrEmpty(cardNumber) && cardNumber.Length >= 15;
            bool isExpiryValid = !string.IsNullOrEmpty(expiryDate);
            bool isCvvValid = !string.IsNullOrEmpty(cvv) && cvv.Length >= 3;
            
            return isCardNumberValid && isExpiryValid && isCvvValid;
        }
        
        // Calculate booking fee based on payment method
        public decimal CalculateProcessingFee(decimal amount, string paymentMethod)
        {
            return paymentMethod.ToLower() switch
            {
                "credit" => amount * 0.025m, // 2.5% fee for credit cards
                "debit" => amount * 0.01m,   // 1% fee for debit cards
                "paypal" => amount * 0.035m, // 3.5% fee for PayPal
                _ => 0                       // No fee for other methods
            };
        }
    }
} 