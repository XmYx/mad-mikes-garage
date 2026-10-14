using System.Collections.Generic;

namespace MadMax.Vehicles
{
    // Vehicle-specific clusters. To add one:
    //   1. write `static DashboardStyle MyCar() { var s = new DashboardStyle { ... }; s.Dial(...); s.Lamp(...); return s; }`
    //      in a partial file DashboardStyles.MyCar.cs (copy a Make* method of DashboardStyles.cs as the start);
    //   2. register it below: table["MyCar"] = MyCar;
    //   3. a style named after the design is picked automatically for it; any other id is chosen with
    //      VehicleDesign.dashboard = "MyCar" (then MadMax/Build Parts + Vehicles).
    public static partial class DashboardStyles
    {
        static partial void RegisterCustoms(Dictionary<string, System.Func<DashboardStyle>> table)
        {
        }
    }
}
