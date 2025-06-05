using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyclerSim.Models
{
    public class AuxData
    {
        public int EquipmentId { get; set; }
        public string SensorId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public double Value { get; set; }
    }
}
