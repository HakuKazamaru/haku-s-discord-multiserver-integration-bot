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
    public class RequireGuildContextAttribute : Attribute
    {
        public readonly bool RequiresGuildContext;

        public RequireGuildContextAttribute(bool requiresGuildContext = true)
        {
            this.RequiresGuildContext = requiresGuildContext;
        }
    }
}
