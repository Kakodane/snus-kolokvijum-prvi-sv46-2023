using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNUS_kolokvijum_1
{
    internal class JobHandle
    {
        public Guid Id { get; set; }
        public Task<int>? Result { get; set; }
    }
}
