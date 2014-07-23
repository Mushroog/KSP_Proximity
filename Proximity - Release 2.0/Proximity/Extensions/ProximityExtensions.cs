using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proximity.Extensions
{
    public static class ProximityExtensions
    {
        public static bool IsPrimary(this Part thisPart, List<Part> partsList, int moduleClassID)
        //public static bool IsPrimary(this Part thisPart, Vessel v, int moduleClassID)
        {
            foreach (Part part in partsList)
            {
                if (part.Modules.Contains(moduleClassID))
                {
                    if (part == thisPart)
                    {
                        return true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return false;
        }
    }
}
