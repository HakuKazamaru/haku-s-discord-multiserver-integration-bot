using MultiServerIntegrateBot.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiServerIntegrateBot.Model
{
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DefaultPermissionAttribute : Attribute
    {
        public readonly Permission DefaultPermission;

        public DefaultPermissionAttribute(Permission defaultPermission = Permission.Default)
        {
            this.DefaultPermission = defaultPermission;
        }
    }
}
