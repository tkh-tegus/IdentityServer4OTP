using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4OTP.ViewsModels
{
    public class PhoneLoginViewModel
    {
        [Required]
        [DataType(DataType.PhoneNumber)]
        [JsonProperty("phone")]
        public string PhoneNumber { get; set; }
    }
}
