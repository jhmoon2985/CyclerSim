using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyclerSim.Models
{
    public class AlarmData
    {
        public int EquipmentId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    public class AlarmHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "Sent";
    }
}
