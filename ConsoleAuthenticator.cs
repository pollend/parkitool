using System;
using System.Threading.Tasks;
using SteamKit2.Authentication;

namespace Parkitool
{
        internal class ConsoleAuthenticator : IAuthenticator
        {
            /// <inheritdoc />
            public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
            {
                if (previousCodeWasIncorrect)
                {
                    Console.Error.WriteLine("The previous 2-factor auth code you have provided is incorrect.");
                }

                string code;

                do
                {
                    Console.Error.Write(
                        "STEAM GUARD! Please enter your 2-factor auth code from your authenticator app: ");
                    code = Console.ReadLine()?.Trim();

                    if (code == null)
                    {
                        break;
                    }
                } while (string.IsNullOrEmpty(code));

                return Task.FromResult(code!);
            }

            /// <inheritdoc />
            public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
            {
                if (previousCodeWasIncorrect)
                {
                    Console.Error.WriteLine("The previous 2-factor auth code you have provided is incorrect.");
                }

                string code;

                do
                {
                    Console.Error.Write($"STEAM GUARD! Please enter the auth code sent to the email at {email}: ");
                    code = Console.ReadLine()?.Trim();

                    if (code == null)
                    {
                        break;
                    }
                } while (string.IsNullOrEmpty(code));

                return Task.FromResult(code!);
            }

            /// <inheritdoc />
            public Task<bool> AcceptDeviceConfirmationAsync()
            {
                // if (ContentDownloader.Config.SkipAppConfirmation)
                // {
                //     return Task.FromResult(false);
                // }

                Console.Error.WriteLine("STEAM GUARD! Use the Steam Mobile App to confirm your sign in...");

                return Task.FromResult(true);
            }
        }
}