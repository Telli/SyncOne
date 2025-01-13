using SyncOne.Models;
using System;
using System.Threading.Tasks;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Services
{
    /// <summary>
    /// Defines the contract for an SMS service.
    /// </summary>
    public interface ISmsService
    {
        /// <summary>
        /// Sends an SMS message to the specified phone number.
        /// </summary>
        /// <param name="phoneNumber">The recipient's phone number.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>True if the message was sent successfully; otherwise, false.</returns>
        Task<bool> SendSmsAsync(string phoneNumber, string message);

        /// <summary>
        /// Event triggered when a new SMS message is received.
        /// </summary>
        event EventHandler<SmsMessage> OnSmsReceived;
    }
}