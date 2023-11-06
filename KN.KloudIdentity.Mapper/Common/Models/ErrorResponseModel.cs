using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Common.Models
{
    public class ErrorResponseModel
    {
        public int Status { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string? Details { get; set; }
    }
}
