using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNUS_kolokvijum_1
{
    public class CustomEventArgs
    {
        public Job Job { get; set; }
        public int Result { get; set; }
        public Status Status { get; set; }

        public CustomEventArgs(Job job, int result, Status status)
        {
            Job = job;
            Result = result;
            Status = status;
        }
    }
}
