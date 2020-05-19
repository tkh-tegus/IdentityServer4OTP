using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4OTP.Utils
{
    public class AuthConstants
    {
        public struct TokenRequest
        {
            public const string PhoneNumber = "phone_number";
            public const string Token = "verification_token";
        }

        public struct GrantType
        {
            public const string PhoneNumberToken = "phone_number_token";
        }
    }
}
