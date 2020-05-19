using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4OTP.Models;
using IdentityServer4OTP.Services;
using IdentityServer4OTP.ViewsModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IdentityServer4OTP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VerifyPhoneNumberController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ISmsService _smsService;
        private readonly DataProtectorTokenProvider<User> _dataProtectorTokenProvider;
        private readonly PhoneNumberTokenProvider<User> _phoneNumberTokenProvider;
        private readonly UserManager<User> _userManager;

        public VerifyPhoneNumberController(
            IConfiguration configuration,
            ISmsService smsService,
            DataProtectorTokenProvider<User> dataProtectorTokenProvider,
            PhoneNumberTokenProvider<User> phoneNumberTokenProvider,
            UserManager<User> userManager)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));

            _dataProtectorTokenProvider = dataProtectorTokenProvider
                                          ?? throw new ArgumentNullException(nameof(dataProtectorTokenProvider));

            _phoneNumberTokenProvider = phoneNumberTokenProvider
                                        ?? throw new ArgumentNullException(nameof(phoneNumberTokenProvider));

            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PhoneLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await GetUser(model);
            var response = await SendSmsRequet(model, user);

            if (!response.Result)
            {
                return BadRequest("Sending sms failed");
            }

            var resendToken = await _dataProtectorTokenProvider.GenerateAsync("resend_token", _userManager, user);
            var body = GetBody(response.VerifyToken, resendToken);

            return Accepted(body);
        }

        [HttpPut]
        public async Task<IActionResult> Put(string resendToken, [FromBody] PhoneLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await GetUser(model);
            if (!await _dataProtectorTokenProvider.ValidateAsync("resend_token", resendToken, _userManager, user))
            {
                return BadRequest("Invalid resend token");
            }

            var response = await SendSmsRequet(model, user);

            if (!response.Result)
            {
                return BadRequest("Sending sms failed");
            }

            var newResendToken = await _dataProtectorTokenProvider.GenerateAsync("resend_token", _userManager, user);
            var body = GetBody(response.VerifyToken, newResendToken);
            return Accepted(body);
        }

        private async Task<User> GetUser(PhoneLoginViewModel loginViewModel)
        {
            var phoneNumber = _userManager.NormalizeKey(loginViewModel.PhoneNumber);
            var user = await _userManager.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber)
                       ?? new User
                       {
                           PhoneNumber = loginViewModel.PhoneNumber,
                           SecurityStamp = new Secret("your_secret_key").Value + loginViewModel.PhoneNumber.Sha256()
                       };
            return user;
        }

        private async Task<(string VerifyToken, bool Result)> SendSmsRequet(PhoneLoginViewModel model, User user)
        {
            var verifyToken = await _phoneNumberTokenProvider.GenerateAsync("verify_number", _userManager, user);
            var result =
                await _smsService.SendAsync(model.PhoneNumber, $"Your login verification code is: {verifyToken}");
            return (verifyToken, result);
        }

        private Dictionary<string, string> GetBody(string verifyToken, string resendToken)
        {
            var body = new Dictionary<string, string> { { "resend_token", resendToken } };
            if (_configuration["ReturnVerifyTokenForTesting"] == bool.TrueString)
            {
                body.Add("verify_token", verifyToken);
            }

            return body;
        }
    }
}