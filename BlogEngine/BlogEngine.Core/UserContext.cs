using System.Linq;
using System.Web;
using System.Web.Security;

namespace BlogEngine.Core
{
    /// <summary>
    /// Context of currently logged in user
    /// </summary>
    public static class UserContext
    {
        /// <summary>
        /// The currently logged in user.
        /// </summary>
        public static string CurrentUser { get; } = HttpContext.Current.User.Identity.Name;

        /// <summary>
        /// Email of the currently logged in user.
        /// </summary>
        public static string CurrentEmail
        {
            get
            {
                var userCollection = Membership.Provider.GetAllUsers(0, 999, out _);
                var members = userCollection.Cast<MembershipUser>().ToList();
                return members.SingleOrDefault(x => x.UserName == CurrentUser)?.Email;
            }
        }
    }
}