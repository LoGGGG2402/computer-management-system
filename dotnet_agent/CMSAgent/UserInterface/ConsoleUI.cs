using System;
using System.Threading.Tasks;
using CMSAgent.Models;

namespace CMSAgent.UserInterface
{
    /// <summary>
    /// Provides static methods for interacting with the user via the console.
    /// </summary>
    public static class ConsoleUI
    {
        /// <summary>
        /// Asynchronously prompts the user to enter room configuration details (Room Name, Position X, Position Y).
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="RoomPosition"/> object
        /// with the entered details, or null if the input was invalid or incomplete.
        /// </returns>
        public static Task<RoomPosition?> PromptForRoomPositionAsync()
        {
            Console.WriteLine("Please enter the room configuration:");

            Console.Write("Room Name: ");
            string? roomName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(roomName))
            {
                DisplayError("Room Name cannot be empty.");
                return Task.FromResult<RoomPosition?>(null);
            }

            Console.Write("Position X: ");
            string? posXInput = Console.ReadLine();
            if (!int.TryParse(posXInput, out int posX))
            {
                DisplayError("Invalid input for Position X. It must be an integer.");
                return Task.FromResult<RoomPosition?>(null);
            }

            Console.Write("Position Y: ");
            string? posYInput = Console.ReadLine();
            if (!int.TryParse(posYInput, out int posY))
            {
                DisplayError("Invalid input for Position Y. It must be an integer.");
                return Task.FromResult<RoomPosition?>(null);
            }

            return Task.FromResult<RoomPosition?>(new RoomPosition { RoomName = roomName, PosX = posX.ToString(), PosY = posY.ToString() });
        }

        /// <summary>
        /// Asynchronously prompts the user to enter an MFA (Multi-Factor Authentication) code.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the entered MFA code,
        /// or null if no code was entered.
        /// </returns>
        public static Task<string?> PromptForMfaCodeAsync()
        {
            Console.Write("Enter MFA Code: ");
            string? mfaCode = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(mfaCode))
            {
                DisplayWarning("MFA Code was not provided.");
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(mfaCode);
        }

        /// <summary>
        /// Displays an error message to the console in red text.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public static void DisplayError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Displays a warning message to the console in yellow text.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        public static void DisplayWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"WARNING: {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Displays a success message to the console in green text.
        /// </summary>
        /// <param name="message">The success message to display.</param>
        public static void DisplaySuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SUCCESS: {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Displays an informational message to the console.
        /// </summary>
        /// <param name="message">The informational message to display.</param>
        public static void DisplayInfo(string message)
        {
            Console.WriteLine($"INFO: {message}");
        }

        /// <summary>
        /// Asynchronously prompts the user for a yes/no confirmation.
        /// </summary>
        /// <param name="message">The confirmation message to display.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the user confirms (y/yes),
        /// and false otherwise.
        /// </returns>
        public static Task<bool> ConfirmAsync(string message)
        {
            Console.Write($"{message} (y/n): ");
            string? response = Console.ReadLine()?.ToLowerInvariant();
            return Task.FromResult(response == "y" || response == "yes");
        }
    }
}
