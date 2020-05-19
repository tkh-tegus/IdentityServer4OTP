using IdentityModel;
using IdentityServer4.Events;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Validation;
using IdentityServer4OTP.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4OTP.Validation
{
    public class PhoneNumberValidator : IExtensionGrantValidator
    {
        private readonly PhoneNumberTokenProvider<User> _phoneNumberTokenProvider;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IEventService _events;
        private readonly ILogger<PhoneNumberValidator> _logger;

        public PhoneNumberValidator(PhoneNumberTokenProvider<User> phoneNumberTokenProvider, UserManager<User> userManager, SignInManager<User> signInManager, IEventService events, ILogger<PhoneNumberValidator> logger)
        {
            _phoneNumberTokenProvider = phoneNumberTokenProvider;
            _userManager = userManager;
            _signInManager = signInManager;
            _events = events;
            _logger = logger;
        }

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            var createUser = false;
            var raw = context.Request.Raw;
            var credential = raw.Get(OidcConstants.TokenRequest.GrantType);
            if (credential == null || credential != Utils.AuthConstants.GrantType.PhoneNumberToken)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid verify_phone_number_token credential");
                return;
            }

            var phoneNumber = raw.Get(Utils.AuthConstants.TokenRequest.PhoneNumber);
            var verificationToken = raw.Get(Utils.AuthConstants.TokenRequest.Token);

            var user = await _userManager.Users.SingleOrDefaultAsync(x =>
               x.PhoneNumber == _userManager.NormalizeKey(phoneNumber));

            
            if (user == null)
            {
                user = new User
                {
                    UserName = phoneNumber,
                    PhoneNumber = phoneNumber,
                    SecurityStamp = new Secret("your_secret_key").Value + phoneNumber.Sha256()
                };
                createUser = true;
            }

            var result =
                await _phoneNumberTokenProvider.ValidateAsync("verify_number", verificationToken, _userManager, user);
            if (!result)
            {
                _logger.LogInformation("Authentication failed for token: {token}, reason: invalid token", verificationToken);
                await _events.RaiseAsync(new UserLoginFailureEvent(verificationToken, "invalid token or verification id", false));
                return;
            }

            if (createUser)
            {
                user.PhoneNumberConfirmed = true;
                var resultCreation = await _userManager.CreateAsync(user);
                if (resultCreation != IdentityResult.Success)
                {
                    _logger.LogInformation("User creation failed: {username}, reason: invalid user", phoneNumber);
                    await _events.RaiseAsync(new UserLoginFailureEvent(phoneNumber,
                        resultCreation.Errors.Select(x => x.Description).Aggregate((a, b) => a + ", " + b), false));
                    return;
                }
            }

            _logger.LogInformation("Credentials validated for username: {phoneNumber}", phoneNumber);
            await _events.RaiseAsync(new UserLoginSuccessEvent(phoneNumber, user.Id, phoneNumber, false));
            await _signInManager.SignInAsync(user, true);
            context.Result = new GrantValidationResult(user.Id, OidcConstants.AuthenticationMethods.ConfirmationBySms);
        }

        public string GrantType => Utils.AuthConstants.GrantType.PhoneNumberToken;
    }
}
