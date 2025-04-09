using System;
using System.Net.Sockets;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TicketFunction
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        // Azure Function triggered when a new message is added to the "ticket-queue"
        [Function(nameof(Function1))]
        public async Task Run([QueueTrigger("ticket-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            // Log the raw queue message received
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");

            // Extract the message text as JSON
            string json = message.MessageText;

            // Set deserialization options to ignore case differences in property names
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Deserialize the JSON string into a TicketPurchase object
            TicketPurchase? ticket = JsonSerializer.Deserialize<TicketPurchase>(json, options);

            // Null check — in case the message format was invalid or mismatched
            if (ticket == null)
            {
                throw new InvalidOperationException("Failed to deserialize ticket purchase message.");
            }

            // Retrieve the SQL connection string from environment variables (local.settings.json or Azure config)
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("SQL connection string is not set in the environment variables.");
            }

            // Establish a connection to the SQL database using a `using` statement to ensure cleanup
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(); // Open the connection asynchronously

                // SQL INSERT statement to store the ticket information in the database
                var query = @"
                    INSERT INTO ConcertTickets (
                        concertId, email, Name, phone, quantity, creditCard,
                        expiration, securityCode, address, city, province, postalCode, country
                    ) VALUES (
                        @concertId, @Email, @Name, @Phone, @Quantity, @CreditCard,
                        @Expiration, @SecurityCode, @Address, @City, @Province, @PostalCode, @Country
                    );";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Add parameters to the SQL command to safely insert data
                    cmd.Parameters.AddWithValue("@concertId", ticket.ConcertId);
                    cmd.Parameters.AddWithValue("@Email", ticket.Email);
                    cmd.Parameters.AddWithValue("@Name", ticket.Name);
                    cmd.Parameters.AddWithValue("@Phone", ticket.Phone);
                    cmd.Parameters.AddWithValue("@Quantity", ticket.Quantity);

                    // Note: Sensitive data should be handled securely in production
                    cmd.Parameters.AddWithValue("@CreditCard", ticket.CreditCard);      // Consider storing only last 4 digits
                    cmd.Parameters.AddWithValue("@Expiration", ticket.Expiration);      // Format: MM/YY or MM/YYYY
                    cmd.Parameters.AddWithValue("@SecurityCode", ticket.SecurityCode);  // Avoid storing in real-world scenarios

                    cmd.Parameters.AddWithValue("@Address", ticket.Address);
                    cmd.Parameters.AddWithValue("@City", ticket.City);
                    cmd.Parameters.AddWithValue("@Province", ticket.Province);
                    cmd.Parameters.AddWithValue("@PostalCode", ticket.PostalCode);
                    cmd.Parameters.AddWithValue("@Country", ticket.Country);

                    // Execute the SQL command
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
