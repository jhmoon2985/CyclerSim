using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyclerSim.Models
{
    public class ChannelData
    {
        public int EquipmentId { get; set; }
        public int ChannelNumber { get; set; }
        public int Status { get; set; }
        public int Mode { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Capacity { get; set; }
        public double Power { get; set; }
        public double Energy { get; set; }
        public string ScheduleName { get; set; } = string.Empty;
    }
}
