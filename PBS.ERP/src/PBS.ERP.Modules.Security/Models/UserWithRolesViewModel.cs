
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Security.Models
{
    public class UserWithRolesViewModel
    {
        public ApplicationUser User { get; set; }
        public IEnumerable<string> Roles { get; set; }
    }
}
